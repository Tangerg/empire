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
    for (const key of ['id', 'width', 'height', 'owners', 'victory', 'scenario'] as const) expect(level[key]).toEqual(contract[key]);
    expect(level.units.map(({ key, unit, owner }) => ({ key, unit, owner }))).toEqual(contract.roster);
    expect(level.terrain.flatMap((row, y) => [...row].flatMap((symbol, x) => 'qv'.includes(symbol) ? [{ x, y, symbol }] : []))).toEqual(contract.buildings);
  });
  it('keeps unique legal starts, opening moves and all opposing targets and safety cells reachable', () => {
    expect(new Set(state.units.map(cellKey)).size).toBe(20);
    const goals = [...state.units, ...contract.buildings, ...level.scenario!.zones!.find(z => z.id === 'east-safety')!.cells];
    for (const actor of state.units) {
      expect(battlefield.cell(actor).admits(content.units.get(actor.type).movementClass), actor.key).toBe(true);
      expect(computeMoveField(engine.rules, state, actor).stops.size, actor.key).toBeGreaterThan(1);
      const found = connected(actor, content.units.get(actor.type).movementClass);
      for (const goal of goals) expect(found.has(cellKey(goal)), `${actor.key} to ${cellKey(goal)}`).toBe(true);
    }
  });
  it('retains three independent bank-to-bank crossings, including the unchanged truce bridge', () => {
    const bridges = level.terrain.flatMap((row, y) => [...row].flatMap((cell, x) => cell === '=' ? [{ x, y }] : []));
    expect(bridges).toHaveLength(6);
    const rows = [...new Set(bridges.map(p => p.y))];
    expect(rows).toHaveLength(3);
    for (const row of rows) {
      expect(bridges.filter(p => p.y === row)).toHaveLength(2);
      expect(connected({ x: 1, y: 9 }, 'foot', row).has('20,1')).toBe(true);
    }
    expect(bridges.filter(p => p.y === 6)).toEqual([{ x: 10, y: 6 }, { x: 11, y: 6 }]);
  });
});
