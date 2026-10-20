import { mkdtempSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import { afterEach, describe, expect, it } from 'vitest';
import { build } from 'vite';
import { viteSingleFile } from 'vite-plugin-singlefile';
import { lateInlineAssets } from './late-inline-assets';

const roots: string[] = [];
afterEach(() => { for (const root of roots.splice(0)) rmSync(root, { recursive: true, force: true }); });
const hash = (bytes: Uint8Array) => createHash('sha256').update(bytes).digest('hex');

describe('late original-byte resource embedding', () => {
  it('preserves static imports, dynamic import URLs, CSS fonts, public HTML and URL fragments in one file', async () => {
    const root = mkdtempSync(join(tmpdir(), 'empire-inline-'));
    roots.push(root);
    mkdirSync(join(root, 'public'));
    const samples = new Map([
      ['static.png', Buffer.from([137, 80, 78, 71, 0, 1, 2, 255])],
      ['dynamic.webp', Buffer.from('RIFF original dynamic image bytes')],
      ['face.woff2', Buffer.from('wOF2 original font bytes')],
      ['public/public.svg', Buffer.from('<svg xmlns="http://www.w3.org/2000/svg"><g id="shape"/></svg>')],
    ]);
    for (const [name, bytes] of samples) writeFileSync(join(root, name), bytes);
    writeFileSync(join(root, 'index.html'), '<html><head><link rel="stylesheet" href="/style.css"></head><body><img src="/public.svg?v=1#shape"><script type="module" src="/main.js"></script></body></html>');
    writeFileSync(join(root, 'style.css'), '@font-face{font-family:Fixture;src:url("./face.woff2")}body{background-image:url("./static.png")}');
    writeFileSync(join(root, 'main.js'), 'import image from "./static.png?url"; window.fixtureStatic=image; import("./dynamic.js").then(m=>window.fixtureDynamic=m.image);');
    writeFileSync(join(root, 'dynamic.js'), 'export const image = new URL("./dynamic.webp", import.meta.url).href;');
    const resources = lateInlineAssets();
    await build({
      root, configFile: false, base: './', logLevel: 'silent',
      plugins: [resources.plugin, viteSingleFile({ useRecommendedBuildConfig: false })],
      experimental: { renderBuiltUrl: resources.renderBuiltUrl },
      build: { outDir: 'dist', assetsInlineLimit: 0, assetsDir: '', cssCodeSplit: false,
        copyPublicDir: false, rollupOptions: { output: { inlineDynamicImports: true } } },
    });
    expect(readdirSync(join(root, 'dist'))).toEqual(['index.html']);
    const html = readFileSync(join(root, 'dist/index.html'), 'utf8');
    expect(html).not.toContain('__EMPIRE_INLINE_ASSET_');
    const markup = html.replace(/<script\b[^>]*>[\s\S]*?<\/script>/g, '');
    for (const match of markup.matchAll(/(?:src|href)="([^"]+)"/g)) {
      expect(match[1]).toMatch(/^data:/);
    }
    const embedded = [...html.matchAll(/data:[\w/+.-]+;base64,([A-Za-z0-9+/=]+)/g)]
      .map(match => hash(Buffer.from(match[1], 'base64')));
    for (const bytes of samples.values()) expect(embedded).toContain(hash(bytes));
    expect(html).toContain(';base64,' + samples.get('public/public.svg')!.toString('base64') + '#shape');
    expect(html).toContain('font/woff2;base64,');
    expect(html).toContain('fixtureDynamic');
    for (const [name, bytes] of samples) expect(readFileSync(join(root, name))).toEqual(bytes);
  });

  it('fails closed rather than silently losing an unsupported binary dependency', async () => {
    const root = mkdtempSync(join(tmpdir(), 'empire-inline-'));
    roots.push(root);
    writeFileSync(join(root, 'index.html'), '<script type="module" src="/main.js"></script>');
    writeFileSync(join(root, 'main.js'), 'import bytes from "./unknown.bin?url"; window.bytes=bytes;');
    writeFileSync(join(root, 'unknown.bin'), Buffer.from([1, 2, 3]));
    const resources = lateInlineAssets();
    await expect(build({ root, configFile: false, logLevel: 'silent',
      plugins: [resources.plugin, viteSingleFile({ useRecommendedBuildConfig: false })],
      experimental: { renderBuiltUrl: resources.renderBuiltUrl },
      build: { assetsInlineLimit: 0, cssCodeSplit: false, copyPublicDir: false,
        rollupOptions: { output: { inlineDynamicImports: true } } },
    })).rejects.toThrow('Unsupported inline asset type:');
    expect(readFileSync(join(root, 'unknown.bin'))).toEqual(Buffer.from([1, 2, 3]));
  });

  it('does not disguise JavaScript chunk URLs as binary resources', () => {
    const { renderBuiltUrl } = lateInlineAssets();
    expect(renderBuiltUrl('dynamic.js', { hostId: 'index.html', hostType: 'html', type: 'asset', ssr: false })).toBeUndefined();
    const value = renderBuiltUrl('sprite.png', { hostId: 'main.js', hostType: 'js', type: 'asset', ssr: false });
    expect(value).toMatchObject({ runtime: expect.stringContaining('__EMPIRE_INLINE_ASSET_') });
    expect(JSON.stringify(value)).not.toContain('base64');
  });
});
