import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import canonical from '../../assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import hdManifest from '../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json';
import sourceOverrides from '../../../../tools/art/source-overrides.json';
import { candidate01Asset } from './candidate-01-assets';
import { candidate01Story } from '../story';
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
    const logical = canonical.assets.find(record => record.topicId === topic);
    if (!logical) throw new Error(`scene missing from canonical manifest: ${topic}`);
    expect([asset.width, asset.height]).toEqual([logical.width, logical.height]);
    expect([[256, 144], [128, 128]]).toContainEqual([logical.width, logical.height]);
    expect(asset.frames).toBeUndefined();
    expect(asset.anchor).toBeUndefined();
    expect(candidate01StoryArt(topic)).toBe(asset.url);
    const hd = hdManifest.assets.find(record => record.topicId === topic);
    if (!hd) throw new Error(`approved scene requires physical source export: ${topic}`);
    const source = readFileSync(new URL(entry.scene.source, root));
    const crop = entry.sourceCropBox ?? [0, 0, source.readUInt32BE(16), source.readUInt32BE(20)];
    expect((crop[2] - crop[0]) * asset.height).toBe((crop[3] - crop[1]) * asset.width);
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

  it('registers the corrected square cemetery with source-native HD and unchanged logical geometry', () => {
    const asset = candidate01Asset('C01-CH03-S11');
    expect(asset).toMatchObject({ assetId: 'C01-FFV1-CH03-S11', topicId: 'C01-CH03-S11',
      label: '墓园姓名木片', category: 'narrative-static', kind: 'scene',
      width: 128, height: 128, png: 'runtime/narrative-static/ch03-s11.png',
      qualityTier: 'approved-chunky-v5-export-verified' });
    for (const field of ['anchor', 'frames', 'variants', 'frameWidth', 'frameHeight']) {
      expect(asset).not.toHaveProperty(field);
    }
    const squareHd = hdManifest.assets.find(record => record.topicId === asset.topicId)!;
    expect(squareHd).toMatchObject({ width: 1254, height: 1254, pixelDensity: 1254 / 128 });
    expect(sourceOverrides.assets).toHaveProperty(asset.topicId);
    expect(asset.url).toContain(squareHd.png);
    expect(candidate01StoryArt(asset.topicId)).toBe(asset.url);
    expect(candidate01Asset('C01-CH01-S00').kind).toBe('scene');
    expect(candidate01Asset('C01-PROP-NAME-TAGS').kind).toBe('key-prop');
    const wideScenes = canonical.assets.filter(record => record.kind === 'scene'
      && record.width === 256 && record.height === 144);
    expect(wideScenes).toHaveLength(41);
  });

  it('binds the corrected square opening scene while preserving the separate prop and prologue', () => {
    const asset = candidate01Asset('C01-CH01-S00');
    expect(asset).toMatchObject({ assetId: 'C01-FFV1-CH01-S00', topicId: 'C01-CH01-S00',
      label: '罗德里克系护腕', category: 'narrative-static', kind: 'scene',
      width: 128, height: 128, png: 'runtime/narrative-static/ch01-s00.png',
      qualityTier: 'approved-chunky-v5-export-verified' });
    for (const field of ['anchor', 'frames', 'variants', 'frameWidth', 'frameHeight']) {
      expect(asset).not.toHaveProperty(field);
    }
    const hd = hdManifest.assets.find(record => record.topicId === asset.topicId)!;
    expect(hd).toMatchObject({ width: 1254, height: 1254, pixelDensity: 1254 / 128 });
    expect(sourceOverrides.assets).toHaveProperty(asset.topicId);
    expect(asset.url).toContain(hd.png);
    expect(candidate01StoryArt(asset.topicId)).toBe(asset.url);
    const prop = candidate01Asset('C01-PROP-LEFT-GLOVE');
    expect(prop.kind).toBe('key-prop');
    expect(prop.png).not.toBe(asset.png);
    expect(candidate01Story('c01/prologue').scene).toBe('C01-CH01-S01');
    expect(canonical.assets.filter(record => record.kind === 'scene')).toHaveLength(43);
    expect(canonical.assets.filter(record => record.kind === 'key-prop')).toHaveLength(15);
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
