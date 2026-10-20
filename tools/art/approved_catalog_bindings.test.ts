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
  return { directory, assets: document.assets as Array<{ topicId: string; png: string; width: number; height: number; qualityTier: string; category: string; kind?: string }> };
});
type SourceRef = { source: string; sha256: string };
type CardOverride = { kind: 'character-card-composite-v1'; character: SourceRef; background: SourceRef; placement: { renderWidth: number; origin: number[] } };
type SceneOverride = { kind: 'opaque-scene-v1'; scene: SourceRef; sourceCropBox: number[] | null };
type PropOverride = { kind: 'transparent-prop-v1'; prop: SourceRef };
type Override = SourceRef | CardOverride | SceneOverride | PropOverride;
function sourcesOf(entry: Override): SourceRef[] {
  if (!('kind' in entry)) return [entry];
  switch (entry.kind) {
    case 'opaque-scene-v1': return [entry.scene];
    case 'transparent-prop-v1': return [entry.prop];
    case 'character-card-composite-v1': return [entry.character, entry.background];
  }
}
const entries = Object.entries(overrides.assets) as Array<[string, Override]>;
const approved = entries.filter(([, entry]) => sourcesOf(entry).some(source => source.source.includes('/masters/approved-v5/')));

describe('approved static catalogs preserve campaign ownership', () => {
  it('reads opaque scene provenance independently from reusable transparent character/card sources', () => {
    const scene = { source: 'scene.png', sha256: 'scene hash' };
    const character = { source: 'character.png', sha256: 'character hash' };
    const background = { source: 'background.png', sha256: 'background hash' };
    expect(sourcesOf({ kind: 'opaque-scene-v1', scene, sourceCropBox: null })).toEqual([scene]);
    expect(sourcesOf({ kind: 'character-card-composite-v1', character, background,
      placement: { renderWidth: 144, origin: [24, 4] } })).toEqual([character, background]);
    expect(sourcesOf({ kind: 'transparent-prop-v1', prop: character })).toEqual([character]);
    expect(sourcesOf(character)).toEqual([character]);
  });
  it.each(approved)('validates %s in its owning manifest without inventing a playable consumer', (topic, entry) => {
    const matches = catalogs.flatMap(catalog => catalog.assets.filter(asset => asset.topicId === topic).map(asset => ({ ...catalog, asset })));
    expect(matches).toHaveLength(1);
    const { directory, asset } = matches[0]!;
    for (const source of sourcesOf(entry)) {
      expect(source.source.startsWith(`${directory}masters/approved-v5/`)).toBe(true);
      expect(createHash('sha256').update(readFileSync(new URL(source.source, root))).digest('hex')).toBe(source.sha256);
    }
    if ('kind' in entry && entry.kind === 'character-card-composite-v1') {
      expect(entry.character.source).not.toBe(entry.background.source);
      const character = readFileSync(new URL(entry.character.source, root));
      expect(character[25]).toBe(6);
      expect([asset.width, asset.height]).toEqual([192, 224]);
    }
    if ('kind' in entry && entry.kind === 'opaque-scene-v1') {
      const scene = readFileSync(new URL(entry.scene.source, root));
      expect([2, 6]).toContain(scene[25]); // Native RGB or RGBA; exporter checks opaque pixels.
      expect([asset.width, asset.height]).toEqual([256, 144]);
    }
    if ('kind' in entry && entry.kind === 'transparent-prop-v1') {
      const prop = readFileSync(new URL(entry.prop.source, root));
      expect(prop[25]).toBe(6); // Exporter tests verify source and output alpha pixels.
      expect(asset.category).toBe('narrative-static');
      expect(asset.kind).toBe('key-prop');
      expect([asset.width, asset.height]).toEqual([128, 128]);
      for (const field of ['anchor', 'frames', 'variants', 'frameWidth', 'frameHeight']) {
        expect(asset).not.toHaveProperty(field);
      }
    }
    expect(asset.qualityTier).toBe('approved-chunky-v5-export-verified');
    const png = readFileSync(new URL(`${directory}${asset.png}`, root));
    expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual([asset.width, asset.height]);
    expect(png[25]).toBe(6);
  });
});
