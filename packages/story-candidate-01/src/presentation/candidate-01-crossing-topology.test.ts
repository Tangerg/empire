import { describe, expect, it } from 'vitest';
import { inBounds, idx, mapFromLevel } from '@empire/battle-engine';
import { GROUND_TAGS, taggedGround } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '../index';
import { candidate01Level } from '../levels';
import { candidate01CrossingJunction, candidate01CrossingTopology, type CandidateCrossingComponent } from './candidate-01-crossing-topology';
import { candidate01StoneJunctionMarkup, readCandidate01StoneJunction, CANDIDATE_01_HORIZONTAL_CROSSING, CANDIDATE_01_VERTICAL_CROSSING } from './candidate-01-crossing';
import junctionDocument from '../../assets/final-fantasy-v1/registered-crossings-v1/manifest-junction-v1.json';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const map = mapFromLevel(content, candidate01Level('c01-12'));
const topology = () => candidate01CrossingTopology(map.width, map.height,
  (x, y) => map.tiles[idx(map, x, y)] === 'bridge',
  (x, y) => inBounds(map, x, y)
    && taggedGround(content.terrains.get(map.tiles[idx(map, x, y)]), GROUND_TAGS.way, GROUND_TAGS.settlement));
const stonePluses = () => topology().filter(component => component.cells.length === 5);

describe('crossing component ownership', () => {
  it('derives both real southern pluses and all four ports from unchanged bridge cells', () => {
    const before = JSON.stringify(map);
    const components = topology();
    expect(components).toHaveLength(4);
    expect(components.flatMap(component => component.runs)).toHaveLength(8);
    for (const [index, component] of stonePluses().entries()) {
      const x = index === 0 ? 6 : 17;
      expect(component.cells.map(({ cell }) => cell)).toEqual([[x, 9], [x - 1, 10], [x, 10], [x + 1, 10], [x, 11]]);
      expect(component.runs.map(({ axis, length }) => [axis, length])).toEqual([['vertical', 3], ['horizontal', 3]]);
      expect(candidate01CrossingJunction(component)).toEqual({
        center: [x, 10], pivot: [x * 32 + 16, 336],
        ports: [[x * 32 + 16, 288], [x * 32 + 64, 336], [x * 32 + 16, 384], [x * 32 - 32, 336]],
      });
    }
    expect(JSON.stringify(map)).toBe(before);
  });

  it('rejects missing-port, wrong-center-mask, wrong-length and wrong-pivot mutations', () => {
    const component = stonePluses()[0];
    const mutations: CandidateCrossingComponent[] = [
      { ...component, cells: component.cells.slice(1) },
      { ...component, cells: component.cells.map(cell => cell.mask === 15 ? { ...cell, mask: 7 } : cell) },
      { ...component, runs: component.runs.map(run => run.axis === 'vertical' ? { ...run, length: 2 } : run) },
      { ...component, runs: component.runs.map(run => ({ ...run, pivot: [run.pivot[0] + 1, run.pivot[1]] })) },
    ];
    for (const mutation of mutations) {
      expect(candidate01CrossingJunction(mutation)).toBeNull();
      expect(() => candidate01StoneJunctionMarkup(mutation)).toThrow('complete five-cell plus');
    }
    for (const { cell: [px, py] } of component.cells.filter(cell => cell.mask !== 15)) {
      const changed = candidate01CrossingTopology(map.width, map.height,
        (x, y) => !(x === px && y === py) && map.tiles[idx(map, x, y)] === 'bridge',
        () => false);
      expect(changed.filter(c => c.cells.some(({ cell }) => cell[0] === 6 && cell[1] === 10))
        .some(c => candidate01CrossingJunction(c) !== null)).toBe(false);
    }
  });

  it('emits one self-contained lying composition per stone plus and retains the two wood runs', () => {
    const layers = candidate01MapSceneryLayers({ content, map, levelId: 'c01-12', viewport: candidateReviewViewport(map) });
    const junctions = layers.underUnits.filter(piece => piece.markup.includes('data-crossing-junction'));
    expect(junctions.map(({ x, y }) => [x, y])).toEqual([[208, 336], [560, 336]]);
    for (const piece of junctions) {
      expect(piece).not.toHaveProperty('foot');
      expect(piece.markup.match(/<image /g)).toHaveLength(2);
      expect(piece.markup.match(/<clipPath /g)).toHaveLength(2);
      expect(piece.markup).toContain(CANDIDATE_01_HORIZONTAL_CROSSING.href);
      expect(piece.markup).toContain(CANDIDATE_01_VERTICAL_CROSSING.href);
      expect(piece.markup).not.toMatch(/rotate|filter|opacity=/);
    }
    const legacy = layers.underUnits.filter(piece => piece.markup.includes('crossings-fortifications@2x.png'));
    expect(legacy.map(({ x, y }) => [x, y])).toEqual([[208, 144], [560, 144]]);
    for (const piece of legacy) {
      expect(piece).not.toHaveProperty('foot');
      expect(piece.markup).toMatch(/translate\(-48(?:\.0+)? -48(?:\.0+)?\)/);
    }
    const horizontal = layers.underUnits.filter(piece => piece.markup.includes(CANDIDATE_01_HORIZONTAL_CROSSING.href));
    expect(horizontal).toHaveLength(4);
  });

  it('rejects out-of-canvas, missing-window, center-erasing and port-erasing manifest mutations', () => {
    expect(() => readCandidate01StoneJunction(junctionDocument)).not.toThrow();
    const mutations: ((copy: typeof junctionDocument) => void)[] = [
      copy => { copy.horizontal.cutouts.pop(); },
      copy => { copy.vertical.cutouts.push([-10, -10, 1, 1]); },
      copy => { copy.horizontal.cutouts[0][0] = -100; },
      copy => { copy.vertical.cutouts[0][1] = -100; },
      copy => { copy.horizontal.cutouts[1][3] = 100; },
      copy => { copy.vertical.cutouts[1][2] = 100; },
      copy => { copy.horizontal.cutouts[0] = [-17, -16, 34, 17]; },
      copy => { copy.vertical.cutouts[1] = [15, -6, 34, 12]; },
      copy => { copy.vertical.cutouts[0][1] = -5; },
      copy => { copy.horizontal.cutouts[1][0] = -16; },
      copy => { copy.horizontal.cutouts.reverse(); },
      copy => { copy.vertical.cutouts[0] = [-40, -6, 25, 12]; },
      copy => { copy.horizontal.cutouts[0] = [-65, -16, 130, 10]; },
    ];
    for (const mutate of mutations) {
      const copy = structuredClone(junctionDocument);
      mutate(copy);
      expect(() => readCandidate01StoneJunction(copy)).toThrow();
    }
  });
});
