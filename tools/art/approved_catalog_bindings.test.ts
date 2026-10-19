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
type SourceRef = { source: string; sha256: string };
type CardOverride = { kind: 'character-card-composite-v1'; character: SourceRef; background: SourceRef; placement: { renderWidth: number; origin: number[] } };
const entries = Object.entries(overrides.assets) as Array<[string, SourceRef | CardOverride]>;
const approved = entries.filter(([, entry]) => ('kind' in entry ? entry.character.source : entry.source).includes('/masters/approved-v5/'));

describe('approved static catalogs preserve campaign ownership', () => {
  it.each(approved)('validates %s in its owning manifest without inventing a playable consumer', (topic, entry) => {
    const matches = catalogs.flatMap(catalog => catalog.assets.filter(asset => asset.topicId === topic).map(asset => ({ ...catalog, asset })));
    expect(matches).toHaveLength(1);
    const { directory, asset } = matches[0]!;
    const sources = 'kind' in entry ? [entry.character, entry.background] : [entry];
    for (const source of sources) {
      expect(source.source.startsWith(`${directory}masters/approved-v5/`)).toBe(true);
      expect(createHash('sha256').update(readFileSync(new URL(source.source, root))).digest('hex')).toBe(source.sha256);
    }
    if ('kind' in entry) {
      expect(entry.character.source).not.toBe(entry.background.source);
      const character = readFileSync(new URL(entry.character.source, root));
      expect(character[25]).toBe(6);
      expect([asset.width, asset.height]).toEqual([192, 224]);
    }
    expect(asset.qualityTier).toBe('approved-chunky-v5-export-verified');
    const png = readFileSync(new URL(`${directory}${asset.png}`, root));
    expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual([asset.width, asset.height]);
    expect(png[25]).toBe(6);
  });
});
