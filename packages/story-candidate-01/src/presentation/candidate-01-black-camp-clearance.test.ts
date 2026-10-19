import { describe, expect, it } from 'vitest';
import { createBattleEngine, mapFromLevel, tileHash, type GameState } from '@empire/battle-engine';
import type { BoardPiece } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';
import { reviewFingerprint as hash } from '../../../../tools/art/review_fingerprint';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS, candidate01Level } from '../index';
import { candidate01EnvironmentScene, CANDIDATE_01_ENVIRONMENT } from './candidate-01-environment';
import { candidate01MapSceneryLayers, candidate01SceneFrameMarkup } from './candidate-01-map-scene';
import contract from './fixtures/c01-04-presentation-contract.json';

const catalog = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const engine = createBattleEngine({ content: catalog });
const level = candidate01Level('c01-04');
const map = mapFromLevel(catalog, level);
const context = { content: catalog, levelId: level.id, map, viewport: candidateReviewViewport(map) };
const baseline = { ...context, levelId: 'unregistered-black-camp-projection' };
const scene = candidate01EnvironmentScene(level.id)!;
const specs = [
  { cell: [4, 9], terrain: 'plain', original: 'stone-well', replacement: 'stone-well', foot: [145.34498, 312] },
  { cell: [2, 10], terrain: 'forest', original: 'pine-mature-b', replacement: 'fern-bed', foot: [76.49765, 357.76] },
  { cell: [4, 12], terrain: 'forest', original: 'pine-mature-b', replacement: 'fern-bed', foot: [146.64546, 421.76] },
  { cell: [14, 10], terrain: 'forest', original: 'oak-ancient', replacement: 'fern-bed', foot: [464.29394, 357.76] },
] as const;
const contact = (piece: BoardPiece, x: number, y: number) => piece.foot !== undefined
  && Math.abs(piece.x - x) < 1e-6 && Math.abs(piece.y - y) < 1e-6;
const prior = (piece: BoardPiece) => specs.some(({cell:[x,y]}) => piece.foot !== undefined
  && Math.floor(piece.x / 32) === x && Math.abs(piece.y - (y + 1.18) * 32) < 1e-6);
const authored = (piece: BoardPiece) => specs.some(spec => contact(piece, spec.foot[0], spec.foot[1]));
const crop = (id: string) => {
  const { cell, atlas } = CANDIDATE_01_ENVIRONMENT.cell(id);
  return `x="${-(cell.index % atlas.columns) * atlas.cellWidth}" y="${-Math.floor(cell.index / atlas.columns) * atlas.cellHeight}"`;
};

describe('Black Camp four-piece decorative clearance', () => {
  it.each(specs)('keeps the exact $terrain cell and proven original $original identity at $cell', spec => {
    const [x, y] = spec.cell;
    const before = candidate01MapSceneryLayers(baseline).underUnits;
    const after = candidate01MapSceneryLayers(context).underUnits;
    const original = before.find(piece => piece.foot !== undefined && Math.floor(piece.x / 32) === x
      && Math.abs(piece.y - (y + 1.18) * 32) < 1e-6)!;
    const replacement = after.find(piece => contact(piece, spec.foot[0], spec.foot[1]))!;
    expect(original).toBeDefined();
    expect(original.markup).toContain(CANDIDATE_01_ENVIRONMENT.cell(spec.original).atlas.raster.href);
    expect(original.markup).toContain(crop(spec.original));
    expect(replacement).toBeDefined();
    expect(replacement.markup).toContain(CANDIDATE_01_ENVIRONMENT.cell(spec.replacement).atlas.raster.href);
    expect(replacement.markup).toContain(crop(spec.replacement));
    expect(replacement.foot).toEqual(original.foot);
    expect(replacement.x).toBe(original.x);
    const placement = scene.placements[specs.indexOf(spec)];
    expect(placement).toMatchObject({ id: spec.replacement, layer: 'under-units', flip: tileHash(x, y, 1123) > .52 });
    expect(map.tiles[y * map.width + x]).toBe(spec.terrain);
    if (spec.terrain === 'plain') {
      expect(placement.scale).toBe(.3 + tileHash(x, y, 1121) * .12);
      expect(placement.standingShadow).toBe(true);
      expect(replacement.y - original.y).toBeCloseTo(-13.76, 10);
      expect(replacement.markup).toBe(original.markup.replace('candidate-environment-prop is-standing', 'candidate-environment-prop is-under-units is-standing'));
      expect(replacement.y).toBeGreaterThan(y * 32);
      expect(replacement.y).toBeLessThan(y * 32 + 31);
    } else {
      expect(placement.scale).toBe(.48);
      expect(replacement.y).toBe(original.y);
      expect(catalog.terrains.get(spec.terrain)).toMatchObject({ opaque: true, cover: 'half', obstructionHeight: 2, defense: .2 });
    }
    expect(catalog.terrains.get(spec.terrain).capturable).toBe(false);
    expect(level.scenario!.zones!.flatMap(zone => zone.cells)).not.toContainEqual({ x, y });
    expect(map.owners[y * map.width + x]).toBe(0);
  });

  it('replaces exactly four decorations while preserving every other piece, ground, frame and editor projection', () => {
    expect(scene.mapSize).toEqual([20, 14]);
    expect(scene.selection.allowAtlases).toEqual(['rural-life', 'forest-temperate']);
    expect(scene.zones.map(zone => zone.bounds)).toEqual(specs.map(({ cell: [x, y] }) => [x, y, x, y]));
    expect(scene.zones.map(zone => zone.authoredTerrainScenery)).toEqual([['plain'], ['forest'], ['forest'], ['forest']]);
    expect(scene.placements).toHaveLength(4);
    expect(scene.roadCorridor).toBeUndefined();
    expect(scene.framePlacements).toBeUndefined();
    expect(scene.groundOutline).toBeUndefined();
    expect(scene.zones.every(zone => zone.authoredTerrainSurface === undefined)).toBe(true);
    const before = candidate01MapSceneryLayers(baseline), after = candidate01MapSceneryLayers(context);
    expect(before.underUnits.filter(prior)).toHaveLength(4);
    expect(after.underUnits.filter(authored)).toHaveLength(4);
    expect(after.underUnits.filter(piece => !authored(piece))).toEqual(before.underUnits.filter(piece => !prior(piece)));
    expect(after.underUnits).toHaveLength(before.underUnits.length);
    expect(after.ground).toEqual(before.ground);
    expect(after.overUnits).toEqual(before.overUnits);
    expect(candidate01SceneFrameMarkup(context)).toEqual(candidate01SceneFrameMarkup(baseline));
    expect(candidate01MapSceneryLayers({ ...context, editing: true })).toEqual(after);
  });

  it('preserves all 19 actors, 25 buildings/owners, complete level/state and visibility contracts', () => {
    const state = engine.createState(level);
    const visible = () => state.players.map(player => [player.id, engine.space.visibleUnits(state, player.id).map(unit => unit.key)]);
    expect(hash(level)).toBe(contract.levelSha256);
    expect(hash(state)).toBe(contract.stateSha256);
    expect(state.units.map(unit => [unit.key, unit.type, unit.x, unit.y, unit.owner])).toEqual(contract.actors);
    expect(state.units).toHaveLength(19);
    expect(state.map.tiles.flatMap((id, i) => catalog.terrains.get(id).capturable
      ? [[id, i % map.width, Math.floor(i / map.width), state.map.owners[i]]] : [])).toEqual(contract.buildings);
    expect(contract.buildings).toHaveLength(25);
    expect(state.structures).toEqual([]);
    expect(visible()).toEqual(contract.visibility);
    candidate01MapSceneryLayers({ ...context, map: state.map });
    candidate01SceneFrameMarkup(context);
    expect(hash(state)).toBe(contract.stateSha256);
    expect(visible()).toEqual(contract.visibility);
    // The well shares Mirelle's cell; it owns no rule or mission identity.
    expect(state.units.find(unit => unit.x === 4 && unit.y === 9)?.key).toBe('campaign-mirelle');
    expect(level.scenario!.zones).toEqual([
      { id: 'tent-a', cells: [{ x: 7, y: 4 }] }, { id: 'tent-b', cells: [{ x: 9, y: 6 }] },
      { id: 'tent-c', cells: [{ x: 11, y: 8 }] },
      { id: 'west-exit', cells: [{ x: 0, y: 11 }, { x: 0, y: 12 }, { x: 0, y: 13 }] },
    ]);
  });

  it('distinguishes the same-cell patrol decoration from the scout’s rule-owned granary', () => {
    const state = engine.createState(level);
    expect(state.units.find(unit => unit.key === 'south-patrol')).toMatchObject({ x: 14, y: 10, owner: 2 });
    expect(map.tiles[10 * map.width + 14]).toBe('forest');
    expect(state.units.find(unit => unit.key === 'scout-1')).toMatchObject({ x: 5, y: 11, owner: 1 });
    const granary = map.tiles[11 * map.width + 6];
    expect(granary).toBe('village');
    expect(catalog.terrains.get(granary)).toMatchObject({ capturable: true });
    expect(scene.zones.some(zone => zone.bounds[0] === 6 && zone.bounds[1] === 11)).toBe(false);
    expect(scene.placements.slice(0, 3)).toEqual([
      { id: 'stone-well', x: 4.542030625, y: 8.75, layer: 'under-units', scale: 0.3720204, flip: false, standingShadow: true },
      { id: 'fern-bed', x: 2.3905515625, y: 10.18, layer: 'under-units', scale: 0.48, flip: false },
      { id: 'fern-bed', x: 4.582670625, y: 12.18, layer: 'under-units', scale: 0.48, flip: false },
    ]);
  });

  it('inventories all 34 field-post consumers across six levels without changing their rule cells or owners', () => {
    const inventory = CANDIDATE_01_LEVELS.flatMap(level => {
      const map = mapFromLevel(catalog, level);
      const cells = map.tiles.flatMap((id, i) => id === 'c01.field-post' ? [[i % map.width, Math.floor(i / map.width), map.owners[i]]] : []);
      return cells.length ? [{ id: level.id, cells }] : [];
    });
    expect(inventory).toEqual(contract.fieldPosts);
    expect(inventory.map(entry => [entry.id, entry.cells.length])).toEqual([
      ['c01-03', 1], ['c01-04', 19], ['c01-07', 4], ['c01-08', 4], ['c01-09', 2], ['c01-13', 4],
    ]);
    expect(inventory.reduce((sum, entry) => sum + entry.cells.length, 0)).toBe(34);
  });
});

// Controlled scenario fixtures place a player at each site, then execute real
// engine actions. They test the original rules, not a played route or turn count.
function actionAt(state: GameState, key: string, x: number, y: number, ability = 'wait') {
  const actor = state.units.find(unit => unit.key === key)!;
  actor.x = x; actor.y = y; actor.done = false;
  return engine.dispatch(state, { kind: 'command', unit: actor.id, path: [{ x, y }], command: { ability } });
}

describe('Black Camp rule regression with presentation-only changes', () => {
  it('keeps field-post capture, healing, defense and movement semantics', () => {
    expect(catalog.terrains.get('c01.field-post')).toMatchObject({ capturable: true, heal: 15, defense: .15,
      tags: ['building', 'outpost'], cost: { foot: 1, mounted: 1, heavy: 1, flying: 1 } });
    const state = engine.createState(level);
    const actor = state.units.find(unit => unit.key === 'campaign-laiya')!;
    actionAt(state, actor.key!, 7, 3, 'capture');
    expect(state.map.owners[3 * map.width + 7]).toBe(1);
    actor.hp = 40;
    engine.dispatch(state, { kind: 'endTurn' });
    engine.dispatch(state, { kind: 'endTurn' });
    expect(actor.hp).toBe(55);
  });

  it('fires each evidence visit once, issues retreat after three and requires evidence before exit victory', () => {
    const state = engine.createState(level);
    actionAt(state, 'campaign-laiya', 0, 12);
    expect(state.phase).toBe('playing');
    expect(state.scenario.variables.evidence).toBe(0);
    const sites = [[7, 4, 'found_prisoners'], [9, 6, 'found_oath_template'], [11, 8, 'saved_witnesses']] as const;
    for (const [index, [x, y, signal]] of sites.entries()) {
      const events = actionAt(state, 'campaign-laiya', x, y);
      expect(events.filter(event => event.type === 'scenarioSignal')).toEqual([{ type: 'scenarioSignal', signal }]);
      expect(state.scenario.variables.evidence).toBe(index + 1);
      const repeated = actionAt(state, 'campaign-laiya', x, y);
      expect(repeated.filter(event => event.type === 'scenarioSignal')).toEqual([]);
      expect(state.scenario.variables.evidence).toBe(index + 1);
    }
    expect(state.scenario.firedTriggerIds).toEqual(['inspect-a', 'inspect-b', 'inspect-c', 'withdraw-with-evidence']);
    for (const key of ['campaign-laiya', 'campaign-mirelle']) {
      expect(state.units.find(unit => unit.key === key)?.directive).toEqual({ mode: 'retreat', waypoints: [{ x: 0, y: 12 }], cursor: 0, zone: undefined });
    }
    expect(state.phase).toBe('playing');
    actionAt(state, 'campaign-laiya', 0, 12);
    expect(state.phase).toBe('over');
    expect(state.winnerTeam).toBe(1);
  });

  it('still fails on Laiya loss even with all evidence collected', () => {
    const state = engine.createState(level);
    state.scenario.variables.evidence = 3;
    state.units = state.units.filter(unit => unit.key !== 'campaign-laiya');
    actionAt(state, 'campaign-mirelle', 0, 12);
    expect(state.phase).toBe('over');
    expect(state.winnerTeam).not.toBe(1);
  });
});
