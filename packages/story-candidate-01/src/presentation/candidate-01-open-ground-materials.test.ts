import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { mapFromLevel, type GameMap } from '@empire/battle-engine';
import type { BoardPiece } from '@empire/game-ui';
import { candidate01PlayableShape } from './candidate-01-playable-shape';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '../index';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';
import catalog from '../../../../tools/art/surface-overrides.json';
import { CANDIDATE_01_ENVIRONMENT, candidate01EnvironmentScene } from './candidate-01-environment';
import { CANDIDATE_01_FIELD_MATERIAL } from './candidate-01-field-material';
import { CANDIDATE_01_OPEN_GROUND_MATERIALS } from './candidate-01-open-ground-materials';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { candidateMaterial } from './candidate-01-terrain-materials';
import { candidateWorldMaterialTileMarkup } from './candidate-01-world-material';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const pack = resolve(import.meta.dirname, '../../assets/final-fantasy-v1');
const roles = [
  { id: 'meadow-stony', atlas: 'surface-meadow', tone: 2, cell: 'meadow-3',
    sha256: '5ebcf899dfadb9a81b5cafe77fd33a270c262730968ef039492c95d9688785ea' },
  { id: 'wasteland-scorched', atlas: 'surface-wasteland', tone: 1, cell: 'wasteland-2',
    sha256: '2f5b3576b4387ce993f2dc2b646d0b63969a8775b930a1b4e17b9576cbdc4972' },
  { id: 'wasteland-scree', atlas: 'surface-wasteland', tone: 2, cell: 'wasteland-3',
    sha256: '79ebc9aa69129cd424892264571f55bfaff599a20d12360bcc383988ab92be74' },
] as const;

function unwrapPlayableClip(pieces: readonly BoardPiece[], map: GameMap): BoardPiece[] {
  const shape = candidate01PlayableShape(content, map);
  if (!shape) return [...pieces];
  return pieces.map(piece => {
    const clip = /^<defs><clipPath id="([^"]+)"><path d="([^"]*)" transform="translate\(([^)]+)\)"\/><\/clipPath><\/defs><g clip-path="url\(#([^"]+)\)">([\s\S]*)<\/g>$/.exec(piece.markup);
    if (!clip) throw new Error('open-ground piece lacks its self-contained playable clip');
    expect(clip[2]).toBe(shape.path);
    expect(clip[3]).toBe(`${-piece.x} ${-piece.y}`);
    expect(clip[4]).toBe(clip[1]);
    return { ...piece, markup: clip[5] };
  });
}

describe('native meadow and wasteland keep their original semantic tones', () => {
  it('binds exactly three active full sources with independent world and raster scales', () => {
    expect(CANDIDATE_01_OPEN_GROUND_MATERIALS.map(material => material.id)).toEqual(roles.map(role => role.id));
    for (const role of roles) {
      const material = CANDIDATE_01_OPEN_GROUND_MATERIALS.find(value => value.id === role.id);
      if (!material) throw new Error(`missing world surface ${role.id}`);
      expect(material.replaces).toEqual({ atlas: role.atlas, tone: role.tone });
      expect(material.source).toBe(`masters/approved-v5/environment/${role.atlas}/${role.cell}/master.png`);
      expect(material.sourceSha256).toBe(role.sha256);
      expect(material.logicalSize).toEqual([128, 128]);
      expect(material.rasterSize).toEqual([512, 512]);
      expect(material.rasterPixelsPerWorldUnit).toEqual([8, 8]);
      expect(material.worldOrigin).toEqual([0, 0]);
      expect(material.fit).toEqual({ inset: 0, bleed: 0 });
      const source = readFileSync(resolve(pack, material.source));
      expect(createHash('sha256').update(source).digest('hex')).toBe(role.sha256);
      expect([source.readUInt32BE(16), source.readUInt32BE(20), source[25]]).toEqual([1254, 1254, 2]);
      for (const [density, file] of [[1, material.png], [2, material.png2x]] as const) {
        const png = readFileSync(resolve(pack, 'world-materials-v1', file));
        expect([png.readUInt32BE(16), png.readUInt32BE(20), png[25]]).toEqual([512 * density, 512 * density, 2]);
      }
      const markup = candidateWorldMaterialTileMarkup(material, 96, 32);
      expect(markup).toContain('x="-96" y="-32" width="128" height="128"');
      expect(candidateWorldMaterialTileMarkup(material, 224, 160)).toBe(markup);
      expect(candidateWorldMaterialTileMarkup(material, -32, 32)).toBe(markup);
    }
  });

  it('replaces only the 71 hill, 56 scorched, and 30 non-authored scree cells across all campaign levels', () => {
    const expectedCounts = new Map([
      ['c01-02', [10, 0, 0]], ['c01-03', [7, 0, 1]], ['c01-04', [6, 0, 0]],
      ['c01-05', [6, 0, 0]], ['c01-06', [6, 0, 0]], ['c01-09', [5, 0, 1]],
      ['c01-10', [13, 0, 28]], ['c01-12', [6, 0, 0]], ['c01-14', [0, 56, 0]], ['c01-15', [12, 0, 0]],
    ]);
    const totals = [0, 0, 0];
    for (const level of CANDIDATE_01_LEVELS) {
      const map = mapFromLevel(content, level);
      const before = JSON.stringify({ level, map });
      const context = { content, levelId: level.id, map, viewport: candidateReviewViewport(map) };
      const scene = candidate01MapSceneryLayers(context);
      const counts = [];
      for (const [index, world] of CANDIDATE_01_OPEN_GROUND_MATERIALS.entries()) {
        const expected = [];
        for (let y = 0; y < map.height; y++) for (let x = 0; x < map.width; x++) {
          const id = map.tiles[y * map.width + x];
          const material = candidateMaterial(content, id);
          const atlas = material.surface ?? 'surface-meadow';
          if (atlas !== world.replaces.atlas || (material.tone ?? 0) !== world.replaces.tone) continue;
          const authored = candidate01EnvironmentScene(level.id)?.zones.some(zone => {
            const [left, top, right, bottom] = zone.bounds;
            return zone.authoredTerrainSurface?.includes(id) && x >= left && x <= right && y >= top && y <= bottom;
          });
          if (!authored) expected.push({ x: x * 32, y: y * 32, markup: candidateWorldMaterialTileMarkup(world, x * 32, y * 32) });
        }
        const actual = scene.ground.filter(piece => piece.markup.includes(world.href));
        expect(unwrapPlayableClip(actual, map), `${level.id}:${world.id}`).toEqual(expected);
        expect(candidate01MapSceneryLayers({ ...context, editing: true }).ground
          .filter(piece => piece.markup.includes(world.href)), `${level.id}:${world.id}`).toEqual(actual);
        counts.push(actual.length);
        totals[index] += actual.length;
        for (const layer of [scene.underUnits, scene.overUnits]) expect(layer.some(piece => piece.markup.includes(world.href))).toBe(false);
      }
      expect(counts, level.id).toEqual(expectedCounts.get(level.id) ?? [0, 0, 0]);
      for (const atlas of ['surface-meadow', 'surface-wasteland']) {
        expect(scene.ground.map(piece => piece.markup).join('')).not.toContain(CANDIDATE_01_ENVIRONMENT.atlas(atlas).raster.href);
      }
      expect(candidate01MapSceneryLayers({ ...context, viewport: { ...context.viewport, sceneWidth: context.viewport.sceneWidth + 97 } }), level.id).toEqual(scene);
      expect(JSON.stringify({ level, map })).toBe(before);
    }
    expect(totals).toEqual([71, 56, 30]);
  });

  it('registers the complete original families while keeping unused tones and snow catalog-only', () => {
    for (const [atlasId, prefix] of [['surface-meadow', 'meadow'], ['surface-wasteland', 'wasteland'], ['surface-snow', 'snow']] as const) {
      const entries = catalog.atlases[atlasId].cells;
      expect(Object.keys(entries)).toEqual([1, 2, 3, 4].map(number => `${prefix}-${number}`));
      const atlas = CANDIDATE_01_ENVIRONMENT.atlas(atlasId);
      expect(atlas.componentCount).toBe(4);
      for (const [slot, entry] of Object.entries(entries)) {
        if (slot === 'meadow-1') expect(entry.sha256).toBe(CANDIDATE_01_FIELD_MATERIAL.sourceSha256);
        else expect(entry.source).toContain(`/masters/approved-v5/environment/${atlasId}/${slot}/master.png`);
        const source = readFileSync(resolve(import.meta.dirname, '../../../..', entry.source));
        expect(createHash('sha256').update(source).digest('hex')).toBe(entry.sha256);
        expect([source.readUInt32BE(16), source.readUInt32BE(20), source[25]]).toEqual([1254, 1254, 2]);
      }
      for (const density of [1, 2]) {
        const png = readFileSync(resolve(pack, `environment-builder-v1/runtime/atlas/${atlasId}${density === 2 ? '@2x' : ''}.png`));
        expect([png.readUInt32BE(16), png.readUInt32BE(20), png[25]]).toEqual([128 * density, 32 * density, 2]);
      }
    }
    for (const level of CANDIDATE_01_LEVELS) for (const id of mapFromLevel(content, level).tiles) {
      const material = candidateMaterial(content, id);
      expect(material.surface).not.toBe('surface-snow');
      expect(material.blend).not.toBe('transition-meadow-snow');
      if (material.surface === 'surface-wasteland') expect([1, 2]).toContain(material.tone);
      if (!material.surface && material.tone !== undefined) expect(material.tone).toBe(2);
    }
  });
});
