import { createHash } from 'node:crypto';
import { readFileSync, realpathSync } from 'node:fs';
import { extname, isAbsolute, relative, resolve } from 'node:path';
import type { OutputBundle } from 'rollup';
import type { Plugin, RenderBuiltAssetUrl } from 'vite';

const MIME: Record<string, string> = {
  '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg',
  '.webp': 'image/webp', '.gif': 'image/gif', '.svg': 'image/svg+xml',
  '.avif': 'image/avif', '.ico': 'image/x-icon',
  '.woff': 'font/woff', '.woff2': 'font/woff2', '.ttf': 'font/ttf', '.otf': 'font/otf',
  '.mp3': 'audio/mpeg', '.ogg': 'audio/ogg', '.wav': 'audio/wav',
  '.mp4': 'video/mp4', '.webm': 'video/webm', '.wasm': 'application/wasm',
  '.json': 'application/json',
};
const TOKEN = /__EMPIRE_INLINE_ASSET_[a-f0-9]{64}__/g;
interface Reference { filename: string; suffix: string; type: 'asset' | 'public' }

/** Keep image bytes out of Rollup's module AST; embed only after code generation. */
export function lateInlineAssets(): { plugin: Plugin; renderBuiltUrl: RenderBuiltAssetUrl } {
  const references = new Map<string, Reference>();
  let publicDir: string | false = false;
  const renderBuiltUrl: RenderBuiltAssetUrl = (filename, context) => {
    // JS chunks belong to the single-file script plugin, not the binary asset pass.
    if (/\.[cm]?js$/.test(filename) || filename.endsWith('.css')) return undefined;
    const key = `${context.type}:${filename}`;
    const token = `__EMPIRE_INLINE_ASSET_${createHash('sha256').update(key).digest('hex')}__`;
    const boundary = filename.search(/[?#]/);
    references.set(token, { filename: boundary < 0 ? filename : filename.slice(0, boundary),
      // A query only selected/cached the source file; data URLs cannot carry it.
      // SVG fragments still select a view/symbol and must remain after the payload.
      suffix: filename.includes('#') ? filename.slice(filename.indexOf('#')) : '', type: context.type });
    return context.hostType === 'js' ? { runtime: JSON.stringify(token) } : token;
  };

  function bytes(reference: Reference, bundle: OutputBundle): Uint8Array {
    if (reference.type === 'asset') {
      const asset = bundle[reference.filename];
      if (!asset || asset.type !== 'asset') throw new Error(`Missing inline asset: ${reference.filename}`);
      return typeof asset.source === 'string' ? Buffer.from(asset.source) : asset.source;
    }
    if (!publicDir) throw new Error(`Public directory unavailable for ${reference.filename}`);
    const root = realpathSync(publicDir);
    const file = realpathSync(resolve(root, reference.filename));
    const path = relative(root, file);
    if (isAbsolute(path) || path === '..' || path.startsWith('../')) {
      throw new Error(`Public asset escapes its directory: ${reference.filename}`);
    }
    return readFileSync(file);
  }

  return {
    renderBuiltUrl,
    plugin: {
      name: 'empire:late-inline-assets',
      apply: 'build',
      enforce: 'post',
      configResolved(config) { publicDir = config.publicDir; },
      buildStart() { references.clear(); },
      generateBundle(_options, bundle) {
        const encoded = new Map<string, string>();
        const used = new Set<string>();
        const audit: { filename: string; sha256: string; byteLength: number }[] = [];
        for (const [token, reference] of references) {
          const mime = MIME[extname(reference.filename).toLowerCase()];
          if (!mime) throw new Error(`Unsupported inline asset type: ${reference.filename}`);
          const original = Buffer.from(bytes(reference, bundle));
          audit.push({ filename: reference.filename, sha256: createHash('sha256').update(original).digest('hex'), byteLength: original.length });
          encoded.set(token, `data:${mime};base64,${original.toString('base64')}${reference.suffix}`);
        }
        const replace = (text: string) => text.replace(TOKEN, token => {
          const value = encoded.get(token);
          if (value === undefined) throw new Error(`Unknown inline asset token: ${token}`);
          used.add(token);
          return value;
        });
        for (const item of Object.values(bundle)) {
          if (item.type === 'chunk') item.code = replace(item.code);
          else if (/\.(css|html?)$/.test(item.fileName)) {
            item.source = replace(typeof item.source === 'string' ? item.source : Buffer.from(item.source).toString('utf8'));
          }
        }
        for (const [token, reference] of references) {
          if (!used.has(token)) throw new Error(`Inline asset reference was lost: ${reference.filename}`);
          if (reference.type === 'asset') delete bundle[reference.filename];
        }
        const leftovers = Object.values(bundle).filter(item => item.type === 'asset' && !/\.(css|html?)$/.test(item.fileName));
        if (leftovers.length) throw new Error(`Unembedded assets: ${leftovers.map(item => item.fileName).join(', ')}`);
        this.info(`ORIGINAL_RESOURCE_AUDIT ${JSON.stringify(audit)}`);
        this.info(`Embedded ${references.size} original binary resources after module generation`);
      },
    },
  };
}
