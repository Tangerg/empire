import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import hdManifest from '../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json';
import sourceOverrides from '../../../../tools/art/source-overrides.json';
import { candidate01Asset } from './candidate-01-assets';
import { CANDIDATE_01_MENU_ART, candidate01ChapterArt, candidate01StoryArt } from './candidate-01-story';

const pack = new URL('../../assets/final-fantasy-v1/', import.meta.url);
const root = new URL('../../../../', import.meta.url);
const scenes = Object.entries(sourceOverrides.assets)
  .filter(([, entry]) => 'kind' in entry && entry.kind === 'opaque-scene-v1');

describe('narrative scene consumer contracts', () => {
  it.each(scenes)('resolves %s as one logical scene at its complete reviewed source density', (topic, entry) => {
    if (!('kind' in entry) || entry.kind !== 'opaque-scene-v1' || !('scene' in entry) || !('sourceCropBox' in entry)) {
      throw new Error('expected scene source');
    }
    const asset = candidate01Asset(topic);
    expect(asset.category).toBe('narrative-static');
    expect(asset.kind).toBe('scene');
    expect([asset.width, asset.height]).toEqual([256, 144]);
    expect(asset.frames).toBeUndefined();
    expect(asset.anchor).toBeUndefined();
    expect(candidate01StoryArt(topic)).toBe(asset.url);
    const hd = hdManifest.assets.find(record => record.topicId === topic);
    if (!hd) throw new Error(`approved scene requires physical source export: ${topic}`);
    const source = readFileSync(new URL(entry.scene.source, root));
    const crop = entry.sourceCropBox ?? [0, 0, source.readUInt32BE(16), source.readUInt32BE(20)];
    expect([hd.width, hd.height]).toEqual([crop[2] - crop[0], crop[3] - crop[1]]);
    expect(hd.pixelDensity).toBe(hd.width / asset.width);
    expect(hd.height).toBe(asset.height * hd.pixelDensity);
    expect(hd.png).toMatch(/^runtime-hd\/narrative-static\/[^/]+\.png$/);
    expect(asset.url).toContain(hd.png);
    const png = readFileSync(new URL(hd.png, pack));
    expect(png.subarray(1, 4).toString()).toBe('PNG');
    expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual([hd.width, hd.height]);
    expect(png[25]).toBe(6); // RGBA PNG; exporter pixel tests separately enforce fully opaque alpha.
  });

  it('keeps approved scene coverage and every unrelated physical asset at 2x', () => {
    expect(scenes.map(([topic]) => topic)).toEqual(expect.arrayContaining([
      'C01-CH01-S05', 'C01-CH01-S01', 'C01-ARCH-THREE-BRIDGES', 'C01-ARCH-REDSTONE',
    ]));
    const sceneTopics = new Set(scenes.map(([topic]) => topic));
    for (const physical of hdManifest.assets) {
      if (!sceneTopics.has(physical.topicId)) expect(physical.pixelDensity).toBe(2);
    }
  });

  it('shares the same resolver for entry screens and explicit chapter backdrops', () => {
    expect(CANDIDATE_01_MENU_ART).toBe(candidate01StoryArt('C01-CH01-S05'));
    expect(candidate01ChapterArt(1)).toBe(candidate01StoryArt('C01-ARCH-THREE-BRIDGES'));
    expect(candidate01ChapterArt(2)).toBe(candidate01StoryArt('C01-ARCH-REDSTONE'));
    expect(candidate01ChapterArt(3)).toBe(candidate01StoryArt('C01-ARCH-SILVERWOOD'));
    expect(candidate01ChapterArt(99)).toBe(candidate01ChapterArt(1));
    expect(() => candidate01StoryArt('unknown-scene')).toThrow('unknown candidate-01 art topic');
  });
});
