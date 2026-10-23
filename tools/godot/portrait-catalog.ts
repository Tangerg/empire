import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

export interface PortraitAsset {
  resourcePath: string;
  sha256: string;
}

export function objectFields(value: unknown, fields: readonly string[], context: string): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)
    || Object.keys(value).sort().join(',') !== [...fields].sort().join(',')) {
    throw new Error(`Invalid ${context} fields`);
  }
  return value as Record<string, unknown>;
}

export function readNativePortraitCatalog(godotRoot: string): ReadonlyMap<string, PortraitAsset> {
  const document = objectFields(JSON.parse(readFileSync(resolve(godotRoot, 'data/portrait-catalog.json'), 'utf8')),
    ['schema', 'portraits'], 'native portrait catalog');
  if (document.schema !== 1 || !document.portraits || typeof document.portraits !== 'object' || Array.isArray(document.portraits)) {
    throw new Error('Unsupported native portrait catalog');
  }
  const portraits = new Map<string, PortraitAsset>();
  const resources = new Set<string>();
  for (const [id, value] of Object.entries(document.portraits)) {
    const asset = objectFields(value, ['resourcePath', 'sha256'], `portrait ${id}`);
    if (!/^[a-z][a-z0-9-]*$/.test(id) || typeof asset.resourcePath !== 'string'
      || !/^res:\/\/assets\/runtime\/narrative-static\/char-[a-z0-9-]+\.png$/.test(asset.resourcePath)
      || typeof asset.sha256 !== 'string' || !/^[a-f0-9]{64}$/.test(asset.sha256)
      || resources.has(asset.resourcePath)) {
      throw new Error(`Invalid native portrait declaration: ${id}`);
    }
    const source = resolve(godotRoot, asset.resourcePath.slice('res://'.length));
    const digest = createHash('sha256').update(readFileSync(source)).digest('hex');
    if (digest !== asset.sha256) throw new Error(`Native portrait hash mismatch: ${id}`);
    portraits.set(id, { resourcePath: asset.resourcePath, sha256: asset.sha256 });
    resources.add(asset.resourcePath);
  }
  if (portraits.size === 0) throw new Error('Native portrait catalog is empty');
  return portraits;
}

export function requirePortraitId(value: unknown, catalog: ReadonlyMap<string, PortraitAsset>, context: string): void {
  if (typeof value !== 'string' || !catalog.has(value)) throw new Error(`Unknown native portrait ID in ${context}: ${String(value)}`);
}
