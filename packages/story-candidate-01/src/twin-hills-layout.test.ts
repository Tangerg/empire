import { describe, expect, it } from 'vitest';
import {
  Battlefield, createBattleEngine, computeMoveField, validateLevel,
  type Coord, type MovementClass,
} from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from './index';
import { candidate01Level, TWIN_HILLS_RUSH_POSITION } from './levels';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const engine = createBattleEngine({ content });
const grid = engine.rules.grids.get('square4');
const level = candidate01Level('c01-01');

function reachable(start: Coord, movement: MovementClass, allows: (at: Coord) => boolean = () => true): Set<string> {
  const state = engine.createState(level);
  const battlefield = new Battlefield(state, content);
  const found = new Set([`${start.x},${start.y}`]);
  const queue = [start];
  for (const from of queue) {
    for (const to of grid.adjacent(from)) {
      if (!battlefield.contains(to) || !allows(to) || battlefield.traversalCost(from, to, movement) === null) continue;
      const key = `${to.x},${to.y}`;
      if (found.has(key)) continue;
      found.add(key);
      queue.push(to);
    }
  }
  return found;
}

describe('Twin Hills authored tactical layout', () => {
  it('preserves the complete actor roster, four rule buildings and mission contract', () => {
    expect([level.width, level.height]).toEqual([27, 17]);
    expect(validateLevel(engine.rules, level)).toEqual([]);
    expect(level.units.map(({ key, unit, owner }) => [key, unit, owner])).toEqual([
      ['campaign-laiya', 'c01.laiya', 1], ['campaign-torren', 'c01.swordsman', 1],
      ['campaign-elin', 'c01.archer', 1], ['field-cleric', 'c01.gravekeeper', 1],
      ['roderick-reserve', 'c01.roderick', 1], ['ridge-scout', 'c01.archer', 1],
      ['ridge-guard', 'c01.banner-guard', 1], ['west-spear', 'c01.banner-guard', 1],
      ['village-runner', 'c01.archer', 1], ['camp-guard', 'c01.legion-shield', 1],
      ['field-knight', 'c01.knight', 1], ['vanguard-1', 'c01.legion-shield', 2],
      ['vanguard-2', 'c01.swordsman', 2], ['vanguard-archer', 'c01.archer', 2],
      ['vanguard-cain', 'c01.cain', 2], ['north-shield', 'c01.legion-shield', 2],
      ['south-sword', 'c01.swordsman', 2], ['south-bow', 'c01.archer', 2],
      ['camp-bow', 'c01.archer', 2],
    ]);
    expect(level.owners).toEqual([
      { x: 2, y: 8, owner: 1 }, { x: 12, y: 7, owner: 0 },
      { x: 15, y: 9, owner: 0 }, { x: 24, y: 6, owner: 2 },
    ]);
    expect(level.owners?.map(({ x, y }) => level.terrain[y][x])).toEqual(['q', 'v', 'v', 'q']);
    expect(level.terrain.join('').match(/[qv]/g)).toHaveLength(4);
    expect(level.victory).toEqual([{
      id: 'primary', type: 'failOn', label: '莱娅存活并击退维尔萨前锋',
      condition: { type: 'unitCount', selector: { keys: ['campaign-laiya'] }, op: 'lte', value: 0 },
      objective: { id: 'route', type: 'routEnemies', label: '击退维尔萨前锋' },
    }]);
    expect(level.scenario?.triggers).toEqual([{
      id: 'matching-orders', timing: 'afterAction',
      condition: { type: 'eventCount', event: 'death', op: 'gte', value: 1 },
      effects: [{ type: 'emitSignal', signal: 'found_matching_orders' }],
    }]);
  });

  it('deploys every actor on a unique legal cell with an available opening move', () => {
    const state = engine.createState(level);
    const battlefield = new Battlefield(state, content);
    expect(new Set(state.units.map(unit => `${unit.x},${unit.y}`)).size).toBe(19);
    for (const unit of state.units) {
      expect(battlefield.cell(unit).admits(content.units.get(unit.type).movementClass), unit.key).toBe(true);
      expect(computeMoveField(engine.rules, state, unit).stops.size, unit.key).toBeGreaterThan(1);
    }
  });

  it('connects all objectives and opposing positions through real movement rules', () => {
    for (const unit of level.units) {
      const movement = content.units.get(unit.unit).movementClass;
      const found = reachable(unit, movement);
      for (const building of level.owners ?? []) expect(found.has(`${building.x},${building.y}`), `${unit.key} to building`).toBe(true);
      if (movement === 'foot') for (const enemy of level.units.filter(other => other.owner !== unit.owner)) {
        expect(found.has(`${enemy.x},${enemy.y}`), `${unit.key} to ${enemy.key}`).toBe(true);
      }
    }
    const rush = TWIN_HILLS_RUSH_POSITION;
    expect(level.elevation?.[rush.y * level.width + rush.x]).toBe(2);
    expect(level.units.some(unit => unit.x === rush.x && unit.y === rush.y)).toBe(false);
    expect(reachable(rush, 'foot').has('12,7')).toBe(true);
  });

  it('keeps two-cell maneuver corridors around the hills and south copse', () => {
    const state = engine.createState(level);
    const battlefield = new Battlefield(state, content);
    const corridors = [
      [[6, 10], [7, 10], [6, 11], [7, 11], [6, 12], [7, 12], [6, 13], [7, 13]],
      [[11, 7], [12, 7], [11, 8], [12, 8], [11, 9], [12, 9]],
      [[22, 10], [23, 10], [22, 11], [23, 11], [22, 12], [23, 12], [22, 13], [23, 13]],
    ];
    for (const corridor of corridors) for (const [x, y] of corridor) {
      expect(battlefield.cellAt(x, y).admits('mounted'), `${x},${y}`).toBe(true);
      expect(battlefield.cellAt(x, y).terrain.id, `${x},${y}`).not.toBe('forest');
    }
    for (const [x, low, high] of [[0, 7, 11], [26, 5, 8]]) {
      for (let y = low; y <= high; y++) expect(battlefield.cellAt(x, y).terrain.id).not.toBe('forest');
    }
    // The village road bends north instead of continuing as an edge-to-edge stripe.
    expect(level.terrain[8].slice(11, 13)).toBe('..');
  });

  it('reaches both villages by the upper road and by the independent southern bypass', () => {
    const road = ({ x, y }: Coord) => '-qv'.includes(level.terrain[y][x]);
    const upper = reachable({ x: 2, y: 8 }, 'mounted', at => road(at) && at.y <= 9);
    const southern = reachable({ x: 2, y: 8 }, 'mounted', at => road(at) && !(at.x >= 8 && at.x <= 11 && at.y < 12));
    for (const village of ['12,7', '15,9']) {
      expect(upper.has(village), `upper road to ${village}`).toBe(true);
      expect(southern.has(village), `southern bypass to ${village}`).toBe(true);
    }
    expect(upper.has('5,13')).toBe(false);
    expect(southern.has('5,13')).toBe(true);
  });
});
