import { describe, expect, it } from 'vitest';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { candidate01Level } from '../../packages/story-candidate-01/src/levels';
import { wallTopology, wallGeometry, wallPilotPiece, supportedWall, WALL_SOURCES, type Cell } from './fortification_pilot';

const realLevel = candidate01Level('c01-13');
const actualCells = realLevel.terrain.flatMap((row, y) => [...row].flatMap((tile, x) => tile === '#' ? [[x, y] as Cell] : []));
const actual = wallTopology(actualCells);
const ns = wallTopology(Array.from({ length: 7 }, (_, y) => [1, y] as Cell));
const ew = wallTopology(Array.from({ length: 7 }, (_, x) => [x, 1] as Cell));
const double = wallTopology(Array.from({ length: 7 }, (_, y) => [[1, y] as Cell, [2, y] as Cell]).flat());

function sampleSource(matrix: number[], point: Cell): Cell {
  const [a, b, c, d, e, f] = matrix;
  const determinant = a * d - b * c;
  return [(d * (point[0] - e) - c * (point[1] - f)) / determinant,
    (-b * (point[0] - e) + a * (point[1] - f)) / determinant];
}

describe('unbound four-state fortification diagnostic', () => {
  it('derives real c01-13 topology and limits replacements to 27 of 68 cells', () => {
    expect(actual.cells).toHaveLength(68);
    expect(actual.components).toHaveLength(21);
    expect(actual.connections).toBe(60);
    expect(actual.exposedEdges).toBe(152);
    const counts = Object.fromEntries(Array.from({ length: 16 }, (_, mask) => [mask, actual.cells.filter(c => c.mask === mask).length]));
    expect(counts).toEqual({ 0: 11, 1: 4, 2: 3, 3: 4, 4: 4, 5: 3, 6: 4, 7: 10, 8: 3, 9: 4, 10: 4, 11: 0, 12: 4, 13: 10, 14: 0, 15: 0 });
    expect(actual.cells.filter(supportedWall)).toHaveLength(27);
    expect(realLevel.units).toHaveLength(24);
  });

  it('derives cardinal links from supplied occupancy, without diagonal joining', () => {
    const diagonal = wallTopology([[0, 0], [1, 1]]);
    expect(diagonal.components).toHaveLength(2);
    expect(diagonal.cells.map(c => c.mask)).toEqual([0, 0]);
    for (const cell of [...actual.cells, ...ns.cells, ...ew.cells, ...double.cells]) {
      expect(cell.mask).toBeGreaterThanOrEqual(0);
      expect(cell.mask).toBeLessThan(16);
    }
    expect(() => wallTopology([[0, 0], [0, 0]])).toThrow('duplicate');
    expect(() => wallTopology([[0.5, 0]])).toThrow('integers');
  });

  it('rejects every unimplemented mask rather than inventing doors or a nearest fallback', () => {
    for (let mask = 0; mask < 16; mask++) {
      const cell = { at: [0, 0] as Cell, mask, component: 0, uvOrigin: [0, 0] as Cell, faceJoins: { east: [false, false] as const, south: [false, false] as const } };
      if ([5, 10, 7, 13].includes(mask)) expect(() => wallPilotPiece(cell)).not.toThrow();
      else expect(() => wallPilotPiece(cell)).toThrow(`no mask ${mask}`);
    }
  });

  it('fills every supported cap, including both halves of a two-cell-thick mass', () => {
    for (const cell of [...ns.cells, ...ew.cells, ...double.cells].filter(supportedWall)) {
      expect(wallGeometry(cell).top).toEqual([[-4, -16], [28, -16], [28, 16], [-4, 16]]);
    }
    const at = (x: number, y: number) => double.cells.find(c => c.at[0] === x && c.at[1] === y)!;
    for (let y = 1; y < 5; y++) {
      const west = wallGeometry(at(1, y)), east = wallGeometry(at(2, y));
      expect(west.top[1][0] + 32).toBe(east.top[0][0] + 64);
      expect(west.top[2][1] + y * 32).toBe(wallGeometry(at(1, y + 1)).top[1][1] + (y + 1) * 32);
      expect(west.east).toEqual([]);
      expect(east.south).toEqual([]);
    }
  });

  it('emits only the genuinely exterior camera-facing planes', () => {
    const expected = { 5: ['east', 'top'], 10: ['south', 'top'], 7: ['top'], 13: ['east', 'top'] };
    for (const mask of [5, 10, 7, 13] as const) {
      const cell = { at: [0, 0] as Cell, mask, component: 0, uvOrigin: [0, 0] as Cell, faceJoins: { east: [false, false] as const, south: [false, false] as const } };
      const piece = wallPilotPiece(cell);
      expect([...piece.markup.matchAll(/data-wall-plane="([^"]+)"/g)].map(m => m[1])).toEqual(expected[mask]);
      expect(piece.markup).not.toMatch(/gate|breach|rotate|scale\(-|filter|opacity|fill=/);
      expect(piece.foot).toEqual({ x: 16, y: 32 });
      expect([piece.x, piece.y]).toEqual([0, 0]);
      expect(piece.markup).not.toContain('<path');
    }
  });

  it('keeps UV phase continuous across full top ports with no source repeat', () => {
    for (const topology of [ns, ew, double]) {
      for (const cell of topology.cells.filter(supportedWall)) {
        const [x, y] = cell.at;
        for (const [dx, dy] of [[1, 0], [0, 1]]) {
          const next = topology.cells.find(c => c.at[0] === x + dx && c.at[1] === y + dy);
          if (!next || !supportedWall(next)) continue;
          const a = wallGeometry(cell).topSource, b = wallGeometry(next).topSource;
          expect([b[0] - a[0], b[1] - a[1]]).toEqual([128 * dx, 128 * dy]);
        }
      }
    }
    const oversized = wallTopology(Array.from({ length: 11 }, (_, x) => [x, 0] as Cell));
    expect(() => wallPilotPiece(oversized.cells[9])).toThrow('single-source core');
  });

  it('maps actual emitted affine matrices to the declared stock windows', () => {
    for (const cell of [...ns.cells, ...ew.cells, ...double.cells].filter(supportedWall)) {
      const geometry = wallGeometry(cell);
      const markup = wallPilotPiece(cell).markup;
      const planes = [...markup.matchAll(/data-wall-plane="([^"]+)"[\s\S]*?transform="matrix\(([^)]+)\)"/g)];
      for (const [, name, transform] of planes) {
        const matrix = transform.split(' ').map(Number);
        const polygon = geometry[name as 'top' | 'east' | 'south'];
        const samples = polygon.map(point => sampleSource(matrix, point));
        const bounds = [Math.min(...samples.map(p => p[0])), Math.min(...samples.map(p => p[1])),
          Math.max(...samples.map(p => p[0])), Math.max(...samples.map(p => p[1]))];
        const [u, v, w, h] = geometry[`${name}Source` as 'topSource' | 'eastSource' | 'southSource'];
        expect(bounds).toEqual([u, v, u + w, v + h]);
        expect(bounds[0]).toBeGreaterThanOrEqual(115);
        expect(bounds[2]).toBeLessThanOrEqual(1139);
      }
    }
  });

  it('keeps dense sampling on the same geometry and declares stock-core repetition', () => {
    for (const cell of [...actual.cells, ...double.cells].filter(supportedWall)) {
      const sparse = wallGeometry(cell, 4), dense = wallGeometry(cell, 16);
      expect(dense.top).toEqual(sparse.top);
      expect(dense.paintEast).toEqual(sparse.paintEast);
      expect(dense.paintSouth).toEqual(sparse.paintSouth);
      expect(dense.topSource.slice(2)).toEqual([512, 512]);
      expect(dense.eastSource.slice(1)).toEqual([371, 512, 512]);
      const piece = wallPilotPiece(cell, WALL_SOURCES, false, 16);
      expect(piece.markup).toContain('viewBox="115 115 1024 1024"');
      const matrices = [...piece.markup.matchAll(/data-wall-plane="([^"]+)"[\s\S]*?transform="matrix\(([^)]+)\)"/g)];
      for (const [, name, transform] of matrices) {
        const samples = dense[name as 'top' | 'east' | 'south'].map(point => sampleSource(transform.split(' ').map(Number), point));
        const [u, v, w, h] = dense[`${name}Source` as 'topSource' | 'eastSource' | 'southSource'];
        expect([Math.min(...samples.map(p => p[0])), Math.min(...samples.map(p => p[1])),
          Math.max(...samples.map(p => p[0])), Math.max(...samples.map(p => p[1]))]).toEqual([u, v, u + w, v + h]);
      }
    }
  });

  it('allows seam overlap only into an implemented collinear exterior neighbor', () => {
    for (const topology of [actual, ns, ew, double]) {
      for (const cell of topology.cells.filter(supportedWall)) {
        for (const [side, offsets, bit] of [['east', [[0, -1], [0, 1]], 2], ['south', [[-1, 0], [1, 0]], 4]] as const) {
          offsets.forEach(([dx, dy], index) => {
            const neighbor = topology.cells.find(c => c.at[0] === cell.at[0] + dx && c.at[1] === cell.at[1] + dy);
            expect(cell.faceJoins[side][index]).toBe(Boolean(neighbor && supportedWall(neighbor) && !(neighbor.mask & bit)));
          });
        }
      }
    }
  });

  it('moves UV with component origin rather than resetting it independently per module', () => {
    const shifted = wallTopology(double.cells.map(c => [c.at[0] + 17, c.at[1] + 29] as Cell));
    double.cells.forEach((cell, i) => {
      if (supportedWall(cell)) expect(wallGeometry(shifted.cells[i])).toEqual(wallGeometry(cell));
    });
  });

  it('keeps every opaque polygon within the declared raised projection envelope', () => {
    for (const cell of actual.cells.filter(supportedWall)) {
      const geometry = wallGeometry(cell);
      for (const [x, y] of [...geometry.top, ...geometry.east, ...geometry.south]) {
        expect(x).toBeGreaterThanOrEqual(-4); expect(x).toBeLessThanOrEqual(32);
        expect(y).toBeGreaterThanOrEqual(-16); expect(y).toBeLessThanOrEqual(32);
      }
    }
  });

  it('references only the two unchanged, reviewed native material masters', () => {
    const hashes = {
      top: '7086ba504e68824dc94f761bda9e369229d2033d6664e79c82e1753e7d1e775f',
      face: 'f239c6dacebdfbaccd9341d4ed0edddeea6325226eb1a568758536165325c31c',
    };
    for (const name of ['top', 'face'] as const) {
      const bytes = readFileSync(resolve(import.meta.dirname, '../..', `.${WALL_SOURCES[name]}`));
      expect(createHash('sha256').update(bytes).digest('hex')).toBe(hashes[name]);
    }
  });
});
