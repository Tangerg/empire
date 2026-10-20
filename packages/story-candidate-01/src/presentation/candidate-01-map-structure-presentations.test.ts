import { describe, expect, it } from 'vitest';
import { runtimeAtlasCellMarkup, CELL_FOOT, groundShadow } from '@empire/game-ui';
import manifest from '../../assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import document from '../../assets/final-fantasy-v1/manifest-map-structure-presentations.json';
import { candidate01Asset } from './candidate-01-assets';
import { CANDIDATE_01_MAP_STRUCTURE_ART } from './candidate-01-bindings';
import { candidate01TerrainPicture, candidate01TerrainMarkup } from './candidate-01-runtime';
import { CANDIDATE_01_MAP_STRUCTURE_PRESENTATIONS, readCandidate01MapStructurePresentations,
  resolveCandidate01MapStructureArt, type Candidate01MapStructurePresentationDocument } from './candidate-01-map-structure-presentations';

const root = '../../assets/final-fantasy-v1/';
const variant = { id: 'field-post-compact', topicId: 'C01-STRUCT-REFUGEE-SHELTER',
  png: 'runtime-approved-v5/map-structure-presentation/field-post-compact.png',
  hdPng: 'runtime-approved-v5/map-structure-presentation/field-post-compact@2x.png',
  pixelDensity: 2, hdWidth: 384, hdHeight: 128, frameWidth: 64, frameHeight: 64, frames: 3,
  stateOrder: ['normal', 'damaged', 'captured'], anchor: [32, 60], footprint: [1, 1], presentationScale: .5 };
const fixture: Candidate01MapStructurePresentationDocument = { schemaVersion: 1, variants: [variant] };
const files = Object.fromEntries([variant.png, variant.hdPng].map(path => [root + path, `/${path}`]));
const read = (delta: Partial<typeof variant>) => readCandidate01MapStructurePresentations({ ...fixture, variants: [{ ...variant, ...delta }] }, files);

describe('map structure presentation contract', () => {
  it('compiles a role variant into the existing asset shape while preserving canonical identity and bytes', () => {
    const before = JSON.stringify(candidate01Asset(variant.topicId));
    const variants = read({});
    const actual = resolveCandidate01MapStructureArt(variants, { topicId: variant.topicId, presentationId: variant.id });
    expect(actual).toMatchObject({ assetId: candidate01Asset(variant.topicId).assetId, topicId: variant.topicId,
      category: 'interactive-structure', frameWidth: 64, frameHeight: 64, width: 192, height: 64,
      anchor: [32, 60], footprint: [1, 1], presentationScale: .5, url: `/${variant.hdPng}` });
    expect(actual.stateOrder).toEqual(['normal', 'damaged', 'captured']);
    const atlas = { href: actual.url, cellWidth: actual.frameWidth!, cellHeight: actual.frameHeight!, columns: actual.frames!, rows: 1 };
    actual.stateOrder!.forEach((state, frame) => {
      expect(actual.stateOrder!.indexOf(state)).toBe(frame);
      expect(runtimeAtlasCellMarkup(atlas, frame)).toContain(`x="${-frame * 64}" y="0" width="192" height="64"`);
    });
    expect(resolveCandidate01MapStructureArt(variants, { topicId: variant.topicId })).toBe(candidate01Asset(variant.topicId));
    expect(JSON.stringify(candidate01Asset(variant.topicId))).toBe(before);
    expect(candidate01Asset(variant.topicId).frameWidth).toBe(160);
    expect(candidate01Asset(variant.topicId).footprint).toEqual([2, 2]);
    expect(manifest.assetCount).toBe(404);
    expect(manifest.assets.filter(asset => asset.topicId === variant.topicId)).toHaveLength(1);
  });

  it('rejects unknown and mismatched explicit bindings without a silent fallback', () => {
    const variants = read({});
    expect(() => resolveCandidate01MapStructureArt(variants, { topicId: variant.topicId, presentationId: 'unknown' })).toThrow('unknown bound presentation');
    expect(() => resolveCandidate01MapStructureArt(variants, { topicId: variant.topicId, presentationId: '' })).toThrow('unknown bound presentation');
    expect(() => resolveCandidate01MapStructureArt(variants, { topicId: 'C01-STRUCT-LORNE-KEEP', presentationId: variant.id })).toThrow('topic mismatch');
  });

  it('rejects malformed schema, identities, ownership fields and reused paths', () => {
    expect(() => read({ topicId: 'unknown' })).toThrow('unknown candidate-01 art topic');
    expect(() => read({ topicId: 'C01-UNIT-SWORDSMAN' })).toThrow('three-state interactive structure');
    expect(() => read({ id: '../field-post' })).toThrow('invalid or duplicate presentation id');
    expect(() => readCandidate01MapStructurePresentations({ ...fixture, variants: [variant, variant] }, files)).toThrow('duplicate presentation id');
    expect(() => readCandidate01MapStructurePresentations({ ...fixture, variants: [variant, { ...variant, id: 'second' }] }, files)).toThrow('duplicate runtime path');
    expect(() => readCandidate01MapStructurePresentations({ ...fixture, schemaVersion: 2 }, files)).toThrow('unsupported presentation schema');
    const unexpected = { ...variant, terrainId: 'c01.field-post' };
    expect(() => readCandidate01MapStructurePresentations({ ...fixture, variants: [unexpected] }, files)).toThrow('unsupported or missing fields');
  });

  it.each([{ frames: 2 }, { frameWidth: 0 }, { frameHeight: -1 }, { frameWidth: 64.5 },
    { frameHeight: Number.NaN }, { pixelDensity: 1 }, { hdWidth: 383 }, { hdHeight: 127 },
    { stateOrder: ['normal', 'captured', 'damaged'] }, { stateOrder: ['normal', 'damaged', 'damaged'] },
  ])('rejects inconsistent frame/HD geometry or state order: %j', delta => {
    expect(() => read(delta)).toThrow('inconsistent frame/HD geometry or state order');
  });

  it.each([[32], [32, Number.NaN], [-1, 60], [64, 60], [32, 64]])('rejects invalid pivot %j', (...anchor) => {
    expect(() => read({ anchor })).toThrow();
  });
  it.each([[1], [0, 1], [1, -1], [1.5, 1], [1, Number.NaN]])('rejects invalid visual footprint %j', (...footprint) => {
    expect(() => read({ footprint })).toThrow();
  });
  it.each([0, -1, Number.NaN, Number.POSITIVE_INFINITY])('rejects invalid scale %s', presentationScale => {
    expect(() => read({ presentationScale })).toThrow('invalid presentation scale');
  });

  it('requires two dedicated paired runtime files', () => {
    expect(() => readCandidate01MapStructurePresentations(fixture, {})).toThrow('missing from Vite graph');
    expect(() => readCandidate01MapStructurePresentations(fixture, { [root + variant.hdPng]: '/hd.png' })).toThrow('missing from Vite graph');
    expect(() => read({ png: candidate01Asset(variant.topicId).png })).toThrow('paired presentation runtime paths');
    expect(() => read({ png: 'runtime-approved-v5/map-structure-presentation/../field-post.png' })).toThrow('paired presentation runtime paths');
    expect(() => read({ hdPng: variant.png })).toThrow('paired presentation runtime paths');
  });
});

describe('reviewed compact field-post presentation catalog', () => {
  it('uses the compact trio only for field-post while keeping other map identities exact', () => {
    expect(document.variants.map(x => x.id)).toEqual(['field-post-compact']);
    expect(CANDIDATE_01_MAP_STRUCTURE_PRESENTATIONS.size).toBe(1);
    expect(CELL_FOOT).toEqual({ x: 16, y: 31 });
    for (const [terrain, binding] of Object.entries(CANDIDATE_01_MAP_STRUCTURE_ART)) {
      const selected = resolveCandidate01MapStructureArt(CANDIDATE_01_MAP_STRUCTURE_PRESENTATIONS, binding!);
      if (terrain === 'c01.field-post') {
        expect(binding!.presentationId).toBe('field-post-compact');
        expect(selected.url).toContain('field-post-compact@2x.png');
        expect(selected.presentationScale).toBe(0.5);
        if (!selected.anchor) throw new Error('registered compact presentation must have a pivot');
        expect(selected.anchor[0]).toBeCloseTo(32, 10);
        expect(selected.anchor[1]).toBeCloseTo(53, 10);
      } else {
        expect(binding!.presentationId).toBeUndefined();
        expect(selected).toBe(candidate01Asset(binding!.topicId));
      }
      for (const ownerColor of [undefined, '#4d86b8', '#a8493f']) {
        const ctx = { x: 0, y: 0, linked: { n: false, e: false, s: false, w: false }, ...(ownerColor ? { ownerColor } : {}) };
        const picture = candidate01TerrainPicture(terrain, ctx);
        expect(picture.foot).toBe(CELL_FOOT);
        expect(picture.markup).toContain(selected.url);
        if (!selected.frameWidth || !selected.frameHeight) throw new Error('structure frame dimensions must be explicit');
        const scale = selected.presentationScale ?? 1;
        const contact = groundShadow({ cx: CELL_FOOT.x, cy: CELL_FOOT.y },
          { rx: selected.frameWidth * scale * 0.32, ry: selected.frameHeight * scale * 0.055 });
        expect(picture.markup).toContain(`<g class="candidate-structure-contact">${contact}</g>`);
        expect(picture.markup.indexOf('candidate-structure-contact')).toBeLessThan(picture.markup.indexOf('data-candidate-art="structure"'));

        expect(picture.markup.includes('candidate-owner-banner')).toBe(ownerColor !== undefined);
        expect(candidate01TerrainMarkup(terrain, ctx)).toBe(picture.markup);
      }
    }
  });
});
