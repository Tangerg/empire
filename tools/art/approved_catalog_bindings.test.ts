import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import overrides from './source-overrides.json';

const root = new URL('../../', import.meta.url);
const packs = [
  ['story-candidate-01', 'final-fantasy-v1', 'fantasy'],
  ['story-candidate-02', 'final-scifi-v1', 'scifi'],
  ['story-candidate-03', 'final-ancient-china-v1', 'ancient-china'],
] as const;
const catalogs = packs.map(([campaign, folder, name]) => {
  const directory = `packages/${campaign}/assets/${folder}/`;
  const document = JSON.parse(readFileSync(new URL(`${directory}manifest-final-${name}-v1.json`, root), 'utf8'));
  return { directory, assets: document.assets as Array<{ topicId: string; png: string; width: number; height: number; qualityTier: string }> };
});
const approved = Object.entries(overrides.assets).filter(([, entry]) => entry.source.includes('/masters/approved-v5/'));

describe('approved static catalogs preserve campaign ownership', () => {
  it.each(approved)('validates %s in its owning manifest without inventing a playable consumer', (topic, entry) => {
    const matches = catalogs.flatMap(catalog => catalog.assets.filter(asset => asset.topicId === topic).map(asset => ({ ...catalog, asset })));
    expect(matches).toHaveLength(1);
    const { directory, asset } = matches[0]!;
    expect(entry.source.startsWith(`${directory}masters/approved-v5/`)).toBe(true);
    expect(createHash('sha256').update(readFileSync(new URL(entry.source, root))).digest('hex')).toBe(entry.sha256);
    expect(asset.qualityTier).toBe('approved-chunky-v5-export-verified');
    const png = readFileSync(new URL(`${directory}${asset.png}`, root));
    expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual([asset.width, asset.height]);
    expect(png[25]).toBe(6);
  });
});
