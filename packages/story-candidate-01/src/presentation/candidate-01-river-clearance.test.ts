import { describe, expect, it } from 'vitest';
import { createBattleEngine, mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import type { BoardPiece } from '@empire/game-ui';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '../index';
import { candidate01EnvironmentScene } from './candidate-01-environment';
import { candidate01MapSceneryLayers, candidate01SceneFrameMarkup } from './candidate-01-map-scene';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';

const cells = [[19, 5], [2, 9], [19, 9], [2, 13]] as const;
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const level = candidate01Level('c01-02');
const map = mapFromLevel(content, level);
const context = { content, map, levelId: level.id, viewport: candidateReviewViewport(map) };
const baseline = { ...context, levelId: 'unregistered-river-clearance-projection' };
const scene = candidate01EnvironmentScene(level.id)!;
const local = (piece: BoardPiece) => piece.foot !== undefined && cells.some(([x, y]) =>
  Math.floor(piece.x / 32) === x && Math.abs(piece.y - (y + 1.18) * 32) < .000001);

describe('river battle clearings', () => {
  it('replaces only four measured forest decorations with grounded low vegetation', () => {
    expect(scene.zones.map(zone => zone.bounds)).toEqual(cells.map(([x, y]) => [x, y, x, y]));
    expect(scene.zones.every(zone => zone.authoredTerrainScenery?.join() === 'forest'
      && zone.authoredTerrainSurface === undefined)).toBe(true);
    for (const [x, y] of cells) expect(map.tiles[y * map.width + x]).toBe('forest');
    expect(scene.placements.map(p => p.id)).toEqual(['fern-bed', 'stump-low', 'fern-bed', 'stump-low']);
    expect(scene.placements.every(p => p.layer === 'under-units' && p.opacity === undefined)).toBe(true);
    const before = candidate01MapSceneryLayers(baseline);
    const after = candidate01MapSceneryLayers(context);
    expect(before.underUnits.filter(local)).toHaveLength(4);
    expect(after.underUnits.filter(local)).toHaveLength(4);
    expect(after.underUnits.filter(p => !local(p))).toEqual(before.underUnits.filter(p => !local(p)));
    expect(after.underUnits).toHaveLength(before.underUnits.length);
    expect(after.overUnits).toEqual(before.overUnits);
    expect(scene.framePlacements).toHaveLength(24);
    expect(candidate01SceneFrameMarkup(context).foreground.match(/is-scene-frame/g)).toHaveLength(24);
  });

  it('keeps all gameplay terrain, actors, buildings, truce and escort contracts unchanged', () => {
    const engine = createBattleEngine({ content });
    const state = engine.createState(level);
    const before = JSON.stringify({ level, state, map });
    candidate01MapSceneryLayers({ ...context, map: state.map });
    candidate01SceneFrameMarkup({ ...context, map: state.map });
    expect(JSON.stringify({ level, state, map })).toBe(before);
    expect(state.units).toHaveLength(20);
    expect(candidate01MapSceneryLayers(context)).toEqual(candidate01MapSceneryLayers(context));
  });
});
