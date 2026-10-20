import { describe, expect, it } from 'vitest';
import { createBattleEngine, mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '../index';
import { candidate01EnvironmentScene } from './candidate-01-environment';
import { candidate01MapSceneryLayers, candidate01SceneFrameMarkup } from './candidate-01-map-scene';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const level = candidate01Level('c01-02');
const map = mapFromLevel(content, level);
const context = { content, map, levelId: level.id, viewport: candidateReviewViewport(map) };
const baseline = { ...context, levelId: 'unregistered-river-clearance-projection' };
const scene = candidate01EnvironmentScene(level.id)!;
describe('river battle clearings', () => {
  it('opens genuine deployment clearings without hiding or replacing forest terrain art', () => {
    expect(scene.zones).toEqual([]);
    expect(scene.placements).toEqual([]);
    const before = candidate01MapSceneryLayers(baseline);
    const after = candidate01MapSceneryLayers(context);
    expect(after.underUnits).toEqual(before.underUnits);
    expect(after.overUnits).toEqual(before.overUnits);
    const forest = map.tiles.flatMap((id, index) => id === 'forest'
      ? [{ x: index % map.width, y: Math.floor(index / map.width) }] : []);
    expect(forest.length).toBeGreaterThan(15);
    for (const unit of level.units) {
      expect(Math.min(...forest.map(tree => Math.abs(tree.x - unit.x) + Math.abs(tree.y - unit.y))), unit.key)
        .toBeGreaterThanOrEqual(2);
    }
    expect(scene.framePlacements).toHaveLength(21);
    expect(candidate01SceneFrameMarkup(context).foreground.match(/is-scene-frame/g)).toHaveLength(21);
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
