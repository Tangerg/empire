import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '../index';
import { candidate01Level } from '../levels';
import { CANDIDATE_01_WATER_MATERIAL as material, candidate01IsAreaWater, candidate01WaterMaterialPieces } from './candidate-01-water-material';
import { candidateMaterial } from './candidate-01-terrain-materials';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { CANDIDATE_01_ENVIRONMENT } from './candidate-01-environment';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';
import { candidate01PlayableShape } from './candidate-01-playable-shape';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const neighbours = [[0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1]];

describe('approved reusable area water', () => {
  it('resolves the immutable source, native 2x period and all 256 reusable alpha cells', () => {
    const pack = resolve(import.meta.dirname, '../../assets/final-fantasy-v1');
    expect(createHash('sha256').update(readFileSync(resolve(pack, material.source))).digest('hex')).toBe(material.sourceSha256);
    expect(material.logicalSize).toEqual([512, 512]);
    expect(material.rasterPixelsPerWorldUnit).toEqual([2, 2]);
    expect(material.worldOrigin).toEqual([0, 0]);
    expect(material.fit).toEqual({ inset: 0, bleed: 0 });
    for (const [path, size] of [[material.png, 512], [material.png2x, 1024], [material.occupancy.png, 512], [material.occupancy.png2x, 1024]] as const) {
      const png = readFileSync(resolve(pack, 'world-materials-v1', path));
      expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual([size, size]);
    }
    expect(CANDIDATE_01_ENVIRONMENT.atlas('water-slow-river').raster).toMatchObject({ cellWidth: 32, cellHeight: 32, columns: 16, rows: 4 });
  });

  it('maps every actual eight-neighbour configuration to the corresponding alpha cell', () => {
    for (let mask = 0; mask < 256; mask++) {
      const cells = new Set(['1,1', ...neighbours.flatMap(([dx, dy], bit) => mask & (1 << bit) ? [`${1 + dx},${1 + dy}`] : [])]);
      const [piece] = candidate01WaterMaterialPieces(3, 3, (x, y) => cells.has(`${x},${y}`));
      const centre = /<g transform="translate\(32 32\)">([\s\S]*?)<\/g>/.exec(piece.markup)?.[1];
      expect(centre, `mask ${mask}`).toContain(`x="${-(mask % 16) * 32}" y="${-Math.floor(mask / 16) * 32}"`);
    }
  });

  it('keeps UV phase through tactical cells and period edges, without embedding a texture per cell', () => {
    const pieces = candidate01WaterMaterialPieces(2, 50, () => true);
    expect(pieces.map(({ x, y }) => [x, y])).toEqual([[0, 0], [0, 512], [0, 1024], [0, 1536]]);
    expect(pieces[1].markup).toBe(pieces[2].markup);
    for (const piece of pieces) {
      expect(piece.markup.match(/href="[^\"]*\/water-area@2x.png[^\"]*"/g)).toHaveLength(1);
      expect(piece.markup).toContain('width="512" height="512" preserveAspectRatio="none"');
      const id = /<mask id="([^\"]+)"/.exec(piece.markup)?.[1];
      expect(piece.markup).toContain(`mask="url(#${id})"`);
    }
    expect(pieces[3].markup).toContain('width="64" height="64" viewBox="0 0 64 64"');
    expect(candidate01WaterMaterialPieces(2, 50, () => false)).toEqual([]);
  });

  it('retains bridge underlay, source terrain semantics and actual c01-02 occupied cells', () => {
    const map = mapFromLevel(content, candidate01Level('c01-02'));
    const before = JSON.stringify(map);
    const occupied = (x: number, y: number) => candidate01IsAreaWater(candidateMaterial(content, map.tiles[y * map.width + x]));
    const pieces = candidate01WaterMaterialPieces(map.width, map.height, occupied);
    const layers = candidate01MapSceneryLayers({ content, levelId: 'c01-02', map, viewport: candidateReviewViewport(map), editing: true });
    const shape = candidate01PlayableShape(content, map);
    if (!shape) throw new Error('Three Bridges must have a terrain-owned irregular boundary');
    const actual = layers.ground.filter((piece) => piece.markup.includes('data-runtime-raster="area-water"'));
    const unwrapped = actual.map(piece => {
      const clip = /^<defs><clipPath id="([^"]+)"><path d="([^"]*)" transform="translate\(([^)]+)\)"\/><\/clipPath><\/defs><g clip-path="url\(#([^"]+)\)">([\s\S]*)<\/g>$/.exec(piece.markup);
      if (!clip) throw new Error('water piece lacks its self-contained playable clip');
      expect(clip[2]).toBe(shape.path);
      expect(clip[3]).toBe(`${-piece.x} ${-piece.y}`);
      expect(clip[4]).toBe(clip[1]);
      return { ...piece, markup: clip[5] };
    });
    expect(unwrapped).toEqual(pieces);
    expect(pieces.map(({ x, y }) => [x, y])).toEqual([[512, 0], [512, 512]]);
    const occupiedCells = map.tiles.filter(id => candidate01IsAreaWater(candidateMaterial(content, id)));
    expect(occupiedCells).toHaveLength(48);
    expect(pieces.reduce((count, piece) => count + (piece.markup.match(/water-masks@2x.png/g)?.length ?? 0), 0)).toBe(occupiedCells.length);
    expect(candidate01IsAreaWater(candidateMaterial(content, 'bridge'))).toBe(true);
    expect(candidate01IsAreaWater(candidateMaterial(content, 'road'))).toBe(false);
    expect(layers.ground.every((piece) => !piece.markup.includes('/water-slow-river'))).toBe(true);
    expect(layers.underUnits.filter((piece) => piece.markup.includes('stone-crossing-horizontal@2x.png'))).toHaveLength(3);
    expect(JSON.stringify(map)).toBe(before);
  });

  it('never queries occupancy outside the map or accepts invalid extents', () => {
    candidate01WaterMaterialPieces(1, 1, (x, y) => {
      expect([x, y]).toEqual([0, 0]);
      return true;
    });
    for (const [width, height] of [[0, 1], [1, -1], [1.5, 2], [1, Infinity]]) {
      expect(() => candidate01WaterMaterialPieces(width, height, () => true)).toThrow('positive integers');
    }
  });
});
