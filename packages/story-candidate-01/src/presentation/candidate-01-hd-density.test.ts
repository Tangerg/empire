import { afterEach, describe, expect, it, vi } from 'vitest';

// Exercise the real catalog/resolver with mixed metadata. PNG bytes are independently
// checked by the exporter tests and the integrated narrative scene contract suite.
vi.mock('../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json', async importOriginal => {
  const actual = await importOriginal<{ default: typeof import('../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json') }>();
  const canonical = await import('../../assets/final-fantasy-v1/manifest-final-fantasy-v1.json');
  const wideSceneTopics = new Set(canonical.default.assets
    .filter(asset => asset.kind === 'scene' && asset.width === 256 && asset.height === 144)
    .map(asset => asset.topicId));
  const data = actual.default;
  return { default: {
    schemaVersion: 'candidate-01-runtime-hd-v2', assetCount: data.assetCount,
    assets: data.assets.map(asset => wideSceneTopics.has(asset.topicId)
      ? { ...asset, width: 1664, height: 936, pixelDensity: 6.5 } : asset),
  } };
});

import { candidate01Asset, readCandidate01HdManifest, validateCandidate01HdAsset } from './candidate-01-assets';
import { CANDIDATE_01_MENU_ART, candidate01ChapterArt, candidate01StoryArt } from './candidate-01-story';

const scene = candidate01Asset('C01-CH01-S05');
const physical = { topicId: scene.topicId, png: 'runtime-hd/narrative-static/ch01-s05.png',
  width: 1664, height: 936, pixelDensity: 6.5 };
const manifest = { schemaVersion: 'candidate-01-runtime-hd-v2', assetCount: 1, assets: [physical] };

afterEach(() => { vi.doUnmock('../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json'); });

describe('per-asset physical density', () => {
  it('accepts exact source-native fractional density without changing logical geometry', () => {
    expect(readCandidate01HdManifest(manifest).get(scene.topicId)).toEqual(physical);
    expect(() => validateCandidate01HdAsset(scene, physical)).not.toThrow();
    expect([scene.width, scene.height]).toEqual([256, 144]);
    expect(scene.anchor).toBeUndefined();
    expect(scene.frames).toBeUndefined();
  });

  it('uses the same real scene resolver for menu, story and chapter consumers at 6.5x', () => {
    expect(CANDIDATE_01_MENU_ART).toBe(scene.url);
    expect(candidate01StoryArt(scene.topicId)).toBe(scene.url);
    expect(scene.url).toContain(physical.png);
    expect(candidate01ChapterArt(1)).toBe(candidate01Asset('C01-ARCH-THREE-BRIDGES').url);
    expect(candidate01ChapterArt(1)).toContain('runtime-hd/narrative-static/arch-three-bridges.png');
  });

  it('accepts explicit square-scene source density without changing the canonical record or URL', () => {
    const square = candidate01Asset('C01-CH03-S11');
    const before = JSON.stringify(square);
    const hd = { topicId: square.topicId, png: 'runtime-hd/narrative-static/ch03-s11.png',
      width: 617, height: 617, pixelDensity: 617 / 128 };
    expect(square).toMatchObject({ kind: 'scene', width: 128, height: 128 });
    expect(readCandidate01HdManifest({ ...manifest, assets: [hd] }).get(square.topicId)).toEqual(hd);
    expect(() => validateCandidate01HdAsset(square, hd)).not.toThrow();
    expect(square.url).toContain(square.png);
    expect(candidate01StoryArt(square.topicId)).toBe(square.url);
    for (const patch of [{ width: 616 }, { height: 616 }, { pixelDensity: 4 },
      { width: 512, height: 288, pixelDensity: 4 }]) {
      expect(() => validateCandidate01HdAsset(square, { ...hd, ...patch })).toThrow('disagree');
    }
    expect(JSON.stringify(square)).toBe(before);
  });

  it.each<Record<string, unknown>>([
    { pixelDensity: Number.NaN }, { pixelDensity: Number.POSITIVE_INFINITY },
    { pixelDensity: Number.NEGATIVE_INFINITY }, { pixelDensity: 0 }, { pixelDensity: -2 },
    { pixelDensity: 1 }, { pixelDensity: 1.5 }, { pixelDensity: true }, { pixelDensity: '6.5' },
    { width: 0 }, { height: -936 }, { width: 1664.5 }, { width: Number.NaN },
    { height: Number.POSITIVE_INFINITY }, { width: true },
  ])('rejects invalid physical geometry or density %j', patch => {
    const invalid = { ...physical, ...patch };
    expect(() => readCandidate01HdManifest({ ...manifest, assets: [invalid] })).toThrow('invalid physical');
    expect(() => validateCandidate01HdAsset(scene, invalid)).toThrow('invalid physical');
  });

  it.each([{ width: 1663 }, { height: 935 }, { pixelDensity: 6 }, { topicId: 'wrong-topic' }])(
    'rejects a physical/logical mismatch %j', patch => {
      expect(() => validateCandidate01HdAsset(scene, { ...physical, ...patch })).toThrow('disagree');
    },
  );

  it.each([{ width: 256, height: 145 }, { width: 320, height: 180 }, { width: 256, height: 256 },
    { width: Number.NaN }, { height: Number.POSITIVE_INFINITY }, { frames: 1 }, { variants: 0 },
    { category: 'battle-prop' as const }, { frameWidth: 256 }, { frameHeight: 144 }, { anchor: [128, 72] as const }])(
    'rejects altered scene aspect or framed geometry %j', patch => {
      for (const record of [scene, candidate01Asset('C01-CH03-S11')]) {
        expect(() => validateCandidate01HdAsset({ ...record, ...patch }, physical))
          .toThrow('unframed 256x144 or 128x128 scene');
      }
    },
  );

  it('keeps tactical sprite density exactly 2 and leaves its geometry unchanged', () => {
    const sprite = candidate01Asset('C01-UNIT-SWORDSMAN');
    const before = JSON.stringify(sprite);
    const hd = { topicId: sprite.topicId, png: 'test.png', width: sprite.width * 2, height: sprite.height * 2, pixelDensity: 2 };
    expect(() => validateCandidate01HdAsset(sprite, hd)).not.toThrow();
    expect(() => validateCandidate01HdAsset(sprite, { ...hd, width: sprite.width * 6.5,
      height: sprite.height * 6.5, pixelDensity: 6.5 })).toThrow('must remain 2');
    expect(JSON.stringify(sprite)).toBe(before);
  });

  it('rejects the obsolete global owner, old schemas, duplicate ids and stale counts', () => {
    expect(() => readCandidate01HdManifest({ ...manifest, ...{ pixelDensity: 2 } })).toThrow('per-asset density only');
    expect(() => readCandidate01HdManifest({ ...manifest, schemaVersion: 'candidate-01-tactical-runtime-hd-v1' })).toThrow('requires v2');
    expect(() => readCandidate01HdManifest({ ...manifest, assetCount: 0 })).toThrow('incomplete');
    expect(() => readCandidate01HdManifest({ ...manifest, assetCount: 2, assets: [physical, physical] })).toThrow('duplicate');
  });

  it('fails loudly if the registered physical file is missing instead of using canonical fallback', async () => {
    vi.resetModules();
    vi.doMock('../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json', () => ({
      default: { ...manifest, assets: [{ ...physical, png: 'runtime-hd/narrative-static/missing-source-native.png' }] },
    }));
    await expect(import('./candidate-01-assets')).rejects.toThrow('runtime HD asset is missing from the Vite graph');
  });
});
