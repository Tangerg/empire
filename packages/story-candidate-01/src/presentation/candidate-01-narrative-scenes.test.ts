import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import hdManifest from '../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json';
import { candidate01Asset } from './candidate-01-assets';
import { CANDIDATE_01_MENU_ART, candidate01ChapterArt, candidate01StoryArt } from './candidate-01-story';

const pack = new URL('../../assets/final-fantasy-v1/', import.meta.url);
const topics = ['C01-CH01-S05', 'C01-CH01-S01', 'C01-ARCH-THREE-BRIDGES', 'C01-ARCH-REDSTONE', 'C01-ARCH-SILVERWOOD', 'C01-CH01-S02'];

describe('narrative scene consumer contracts', () => {
  it.each(topics)('resolves %s as one 256x144 scene through its explicitly registered density', (topic) => {
    const asset = candidate01Asset(topic);
    expect(asset.category).toBe('narrative-static');
    expect(asset.kind).toBe('scene');
    expect([asset.width, asset.height]).toEqual([256, 144]);
    expect(asset.frames).toBeUndefined();
    expect(asset.anchor).toBeUndefined();
    expect(candidate01StoryArt(topic)).toBe(asset.url);
    const hd = hdManifest.assets.find(record => record.topicId === topic);
    expect(hd).toBeDefined(); // All approved scene consumers require authored HD, not an enlarged legacy fallback.
    const selected = hd ?? asset;
    if (hd) {
      expect(hd.pixelDensity).toBe(2);
      expect([hd.width, hd.height]).toEqual([512, 288]);
      expect(hd.png).toMatch(/^runtime-hd\/narrative-static\/[^/]+\.png$/);
    }
    expect(asset.url).toContain(selected.png);
    const png = readFileSync(new URL(selected.png, pack));
    expect(png.subarray(1, 4).toString()).toBe('PNG');
    expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual([selected.width, selected.height]);
    expect(png[25]).toBe(6); // RGBA PNG; exporter pixel tests separately enforce fully opaque alpha.
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
