import { describe, expect, it } from 'vitest';
import { Battlefield, createBattleEngine, computeMoveField, validateLevel, type Coord, type MovementClass } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from './index';
import contract from './__fixtures__/river-contract.json';
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const engine = createBattleEngine({ content });
const level = candidate01Level('c01-02');
const state = engine.createState(level);
const battlefield = new Battlefield(state, content);
const grid = engine.rules.grids.get('square4');
const cellKey = ({ x, y }: Coord) => `${x},${y}`;
function connected(start: Coord, movement: MovementClass = 'foot', bridgeRow?: number) {
  const cells = new Set([cellKey(start)]), queue = [start];
  for (const from of queue) for (const next of grid.adjacent(from)) {
    if (!battlefield.contains(next) || battlefield.traversalCost(from, next, movement) === null
      || bridgeRow !== undefined && battlefield.cell(next).terrain.id === 'bridge' && next.y !== bridgeRow
      || cells.has(cellKey(next))) continue;
    cells.add(cellKey(next)); queue.push(next);
  }
  return cells;
}
describe('Three Bridges authored river layout', () => {
  it('preserves every actor identity, rule building, victory, truce and escort contract', () => {
    expect(validateLevel(engine.rules, level)).toEqual([]);
    expect(level.id).toBe(contract.id);
    expect([level.width, level.height]).toEqual([36, 24]);
    expect(level.victory).toEqual(contract.victory);
    expect(level.owners).toEqual([{ x: 4, y: 8, owner: 1 }, { x: 31, y: 3, owner: 2 }, { x: 12, y: 20, owner: 0 }, { x: 30, y: 21, owner: 0 }]);
    expect(level.scenario!.engagementRules).toEqual(contract.scenario.engagementRules);
    // Travel distance grows intentionally; the same truce expires at turn six.
    expect(level.scenario!.triggers).toEqual(contract.scenario.triggers.map(trigger => ({ ...trigger,
      condition: { type: 'turnAtLeast', turn: 6 },
    })));
    expect(level.scenario!.zones!.map(zone => zone.id)).toEqual(contract.scenario.zones.map(zone => zone.id));
    expect(level.units.map(({ key, unit, owner }) => ({ key, unit, owner }))).toEqual(contract.roster);
    const symbols = level.terrain.flatMap(row => [...row].filter(symbol => 'qv'.includes(symbol))).sort();
    expect(symbols).toEqual(contract.buildings.map(building => building.symbol).sort());
    expect(level.terrain[8][4]).toBe('q');
    expect(level.terrain[3][31]).toBe('q');
    expect(level.terrain[20][12]).toBe('q');
    expect(level.terrain[21][30]).toBe('q');
  });
  it('keeps unique legal starts, opening moves and all opposing targets and safety cells reachable', () => {
    expect(new Set(state.units.map(cellKey)).size).toBe(20);
    const buildings = level.terrain.flatMap((row, y) => [...row].flatMap((symbol, x) => 'qv'.includes(symbol) ? [{ x, y }] : []));
    const goals = [...state.units, ...buildings, ...level.scenario!.zones!.find(z => z.id === 'east-safety')!.cells];
    for (const actor of state.units) {
      expect(battlefield.cell(actor).admits(content.units.get(actor.type).movementClass), actor.key).toBe(true);
      expect(computeMoveField(engine.rules, state, actor).stops.size, actor.key).toBeGreaterThan(1);
      const found = connected(actor, content.units.get(actor.type).movementClass);
      for (const goal of goals) expect(found.has(cellKey(goal)), `${actor.key} to ${cellKey(goal)}`).toBe(true);
    }
  });
  it('retains three independent bank-to-bank crossings, including the relocated truce bridge', () => {
    const bridges = level.terrain.flatMap((row, y) => [...row].flatMap((cell, x) => cell === '=' ? [{ x, y }] : []));
    expect(bridges).toHaveLength(6);
    const rows = [...new Set(bridges.map(p => p.y))];
    expect(rows).toHaveLength(3);
    for (const row of rows) {
      expect(bridges.filter(p => p.y === row)).toHaveLength(2);
      const destination = level.scenario!.zones!.find(zone => zone.id === 'east-safety')!.cells[0];
      expect(connected(state.units[0], 'foot', row).has(cellKey(destination))).toBe(true);
    }
    expect(bridges.filter(p => p.y === 11)).toEqual([{ x: 16, y: 11 }, { x: 17, y: 11 }]);
    expect(level.scenario!.zones!.find(zone => zone.id === 'middle-bridge')!.cells).toEqual(bridges.filter(p => p.y === 11));
  });
});
