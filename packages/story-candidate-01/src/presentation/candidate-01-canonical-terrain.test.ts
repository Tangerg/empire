import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import manifest from '../../assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import hdManifest from '../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json';
import { candidate01Asset, validateCandidate01HdAsset } from './candidate-01-assets';

const pack = new URL('../../assets/final-fantasy-v1/', import.meta.url);
const topics = [
  ['C01-TERRAIN-BORDER-1', 'variants', 4],
  ['C01-TERRAIN-BORDER-2', 'nesw-16', 16],
  ['C01-TERRAIN-BORDER-3', 'variants', 4],
  ['C01-TERRAIN-BORDER-4', 'nesw-16', 16],
  ['C01-TERRAIN-CAPITAL-1', 'nesw-16', 16],
  ['C01-TERRAIN-CAPITAL-2', 'nesw-16', 16],
  ['C01-TERRAIN-CAPITAL-3', 'variants', 4],
  ['C01-TERRAIN-CAPITAL-4', 'variants', 4],
  ['C01-TERRAIN-FORGE-1', 'variants', 4],
  ['C01-TERRAIN-FORGE-2', 'nesw-16', 16],
  ['C01-TERRAIN-FORGE-3', 'variants', 4],
  ['C01-TERRAIN-FORGE-4', 'variants', 4],
  ['C01-TERRAIN-GRAVEYARD-1', 'variants', 4],
  ['C01-TERRAIN-GRAVEYARD-2', 'variants', 4],
  ['C01-TERRAIN-GRAVEYARD-3', 'variants', 4],
  ['C01-TERRAIN-GRAVEYARD-4', 'variants', 4],
] as const;

describe('canonical reviewed family terrain catalog contract', () => {
  it.each(topics)('resolves %s through HD without changing logical geometry', (topic, mode, count) => {
    const matches = manifest.assets.filter(asset => asset.topicId === topic);
    const high = hdManifest.assets.filter(asset => asset.topicId === topic);
    expect(matches).toHaveLength(1);
    expect(high).toHaveLength(1);
    const record = candidate01Asset(topic);
    const hd = high[0]!;
    expect(record.category).toBe('terrain');
    expect(record.tileMode).toBe(mode);
    expect([record.width, record.height, record.tileWidth, record.tileHeight]).toEqual([count * 32, 32, 32, 32]);
    expect([hd.width, hd.height, hd.pixelDensity]).toEqual([count * 64, 64, 2]);
    expect(record.url).toContain(hd.png);
    expect(record.anchor).toBeUndefined();
    expect(record.footprint).toBeUndefined();
    expect(record.frames).toBeUndefined();
    if (mode === 'variants') {
      expect(matches[0]).toMatchObject({ variants: 4, variantOrder: ['a', 'b', 'c', 'd'] });
      expect(record.connectionMasks).toBeUndefined();
    } else {
      expect(matches[0]).toMatchObject({ connectionMasks: 16, maskBits: ['N', 'E', 'S', 'W'] });
      expect(record.variants).toBeUndefined();
    }
    for (const descriptor of [record, hd]) {
      const bytes = readFileSync(new URL(descriptor.png, pack));
      expect(bytes.subarray(1, 4).toString()).toBe('PNG');
      expect([bytes.readUInt32BE(16), bytes.readUInt32BE(20)]).toEqual([descriptor.width, descriptor.height]);
      expect(bytes[25]).toBe(6);
    }
    expect(() => validateCandidate01HdAsset(record, { ...hd, pixelDensity: 1 })).toThrow();
    expect(() => validateCandidate01HdAsset(record, { ...hd, width: record.width })).toThrow();
  });

  it('counts sixteen source identities independently from their124 derived cells', () => {
    expect(new Set(topics.map(([topic]) => topic)).size).toBe(16);
    expect(topics.reduce((count, [, , cells]) => count + cells, 0)).toBe(124);
  });
});

describe('registered canonical terrain provenance', () => {
  it('ties all registered sources and native exports to reviewed hashes', async () => {
    const { createHash } = await import('node:crypto');
    const root = new URL('../../../../', import.meta.url);
    const catalog = JSON.parse(readFileSync(new URL('tools/art/canonical-terrain-overrides.json', root), 'utf8'));
    const report = ['border', 'capital', 'forge', 'graveyard'].flatMap(family => JSON.parse(readFileSync(new URL(`docs/art-rebuild-20261019/approved-v5/canonical-terrain/${family}/formal-export.json`, root), 'utf8')));
    const hash = (path: string) => createHash('sha256').update(readFileSync(new URL(path, root))).digest('hex');
    expect(Object.keys(catalog.assets).sort()).toEqual(topics.map(([topic]) => topic).sort());
    for (const [topic, entry] of Object.entries(catalog.assets) as [string, { source: string; sha256: string }][]) {
      expect(hash(entry.source)).toBe(entry.sha256);
      expect(candidate01Asset(topic).qualityTier).toBe('approved-chunky-v5-export-verified');
      const exports = report.filter((item: { topicId: string }) => item.topicId === topic);
      expect(exports.map((item: { density: number }) => item.density)).toEqual([1, 2]);
      for (const item of exports) expect(hash(item.output)).toBe(item.fileSha256);
    }
  });
});
