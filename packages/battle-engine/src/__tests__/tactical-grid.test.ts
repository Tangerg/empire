import { describe, expect, it } from 'vitest';
import { Board } from '../domain/board';
import { TacticalGrids, type TacticalGrid } from '../tactical-grid';
import { createBattleEngine } from '../plugins/default';
import { hashState } from '../replay';
import { TEST_CONTENT, makeLevel, testState, u } from './fixtures';
import type { Coord, LevelData } from '../types';

const grids = () => TacticalGrids.all();
const cells = (_grid: TacticalGrid, width = 7, height = 7): Coord[] =>
  Array.from({ length: width * height }, (_unused, index) =>
    ({ x: index % width, y: Math.floor(index / width) }));

/**
 * Laws every tiling owes its callers.
 *
 * A tiling is only useful if the rules can trust these: pathfinding walks
 * `adjacent`, ranges come from `within`, line of sight walks `line`, and facing
 * reads `toward`. A tiling that disagreed with its own `distance` would make a
 * weapon whose range is 2 hit a cell it cannot see.
 */
describe.each(grids().map((grid) => [grid.id, grid] as const))('%s', (_id, grid) => {
  const middle = { x: 3, y: 3 };

  it('measures distance as a symmetric metric', () => {
    expect(grid.distance(middle, middle)).toBe(0);
    for (const cell of cells(grid)) {
      expect(grid.distance(middle, cell)).toBe(grid.distance(cell, middle));
      expect(grid.distance(middle, cell)).toBeGreaterThanOrEqual(0);
    }
  });

  it('agrees with itself about what is one step away', () => {
    const neighbours = grid.adjacent(middle);
    expect(neighbours).toHaveLength(grid.directions.length);
    for (const neighbour of neighbours) expect(grid.distance(middle, neighbour)).toBe(1);
    // And nothing else is: the ring of distance 1 is exactly the neighbour set.
    expect(new Set(grid.within(middle, 1, 1).map((cell) => `${cell.x},${cell.y}`)))
      .toEqual(new Set(neighbours.map((cell) => `${cell.x},${cell.y}`)));
  });

  it('fills a ring with exactly the cells inside it', () => {
    for (const cell of grid.within(middle, 2, 3)) {
      expect(grid.distance(middle, cell)).toBeGreaterThanOrEqual(2);
      expect(grid.distance(middle, cell)).toBeLessThanOrEqual(3);
    }
    const inRange = cells(grid, 9, 9).filter((cell) => {
      const steps = grid.distance(middle, cell);
      return steps >= 2 && steps <= 3;
    });
    const found = new Set(grid.within(middle, 2, 3).map((cell) => `${cell.x},${cell.y}`));
    for (const cell of inRange) expect(found.has(`${cell.x},${cell.y}`)).toBe(true);
  });

  it('traces a ray that never jumps a cell, endpoints included', () => {
    // A sight ray, deliberately not a walk: on a four-way board it cuts corners,
    // which is why a wall on the diagonal does not block a diagonal shot. What
    // it may never do is skip storage cells, or the blockers between would go
    // unexamined.
    for (const target of cells(grid)) {
      const trace = grid.line(middle, target);
      expect(trace[0]).toEqual(middle);
      expect(trace[trace.length - 1]).toEqual(target);
      for (let step = 1; step < trace.length; step++) {
        const previous = trace[step - 1];
        const cell = trace[step];
        expect(Math.max(Math.abs(cell.x - previous.x), Math.abs(cell.y - previous.y)), `${_id} ${step}`)
          .toBeLessThanOrEqual(1);
      }
    }
  });

  it('turns a step into a direction and back', () => {
    for (const direction of grid.directions) {
      expect(grid.opposite(grid.opposite(direction.id))).toBe(direction.id);
      const neighbour = grid.step(middle, direction.id);
      // The direction toward a neighbour is the one that steps onto it.
      expect(grid.step(middle, grid.toward(middle, neighbour))).toEqual(neighbour);
      expect(direction.name.length).toBeGreaterThan(0);
    }
    expect(() => grid.step(middle, 'nonesuch')).toThrow(RangeError);
  });

  it('places a cell where it can be picked up again', () => {
    for (const cell of cells(grid)) {
      expect(grid.cellAt(grid.center(cell)), `${_id} ${cell.x},${cell.y}`).toEqual(cell);
    }
    const extent = grid.extent({ width: 7, height: 7 });
    expect(extent.x).toBeGreaterThanOrEqual(7);
    expect(grid.outline().length).toBeGreaterThanOrEqual(4);
  });
});

describe('what each tiling means', () => {
  it('keeps the four-way board exactly as it was', () => {
    const square = TacticalGrids.get('square4');
    expect(square.distance({ x: 0, y: 0 }, { x: 2, y: 3 })).toBe(5);
    expect(square.adjacent({ x: 1, y: 1 })).toEqual([
      { x: 1, y: 0 }, { x: 2, y: 1 }, { x: 1, y: 2 }, { x: 0, y: 1 },
    ]);
    // A vertical tie still goes to the vertical, which flanking depends on.
    expect(square.toward({ x: 0, y: 0 }, { x: 2, y: 2 })).toBe('south');
    expect(square.within({ x: 3, y: 3 }, 1, 1).length).toBe(4);
  });

  it('lets a diagonal count as one step on the eight-way board', () => {
    const octile = TacticalGrids.get('square8');
    expect(octile.distance({ x: 0, y: 0 }, { x: 3, y: 3 })).toBe(3);
    expect(octile.adjacent({ x: 1, y: 1 })).toHaveLength(8);
    expect(octile.toward({ x: 0, y: 0 }, { x: 2, y: 2 })).toBe('southeast');
    // Range 2 is a 5×5 square rather than a diamond: the reason to want it.
    expect(octile.within({ x: 5, y: 5 }, 1, 2)).toHaveLength(24);
  });

  it('gives a hex six neighbours whichever row it is on', () => {
    const hex = TacticalGrids.get('hex');
    expect(hex.adjacent({ x: 2, y: 2 })).toHaveLength(6);
    // Odd rows are shifted, so the same direction lands on a different column.
    expect(hex.step({ x: 2, y: 2 }, 'hexNortheast')).toEqual({ x: 2, y: 1 });
    expect(hex.step({ x: 2, y: 3 }, 'hexNortheast')).toEqual({ x: 3, y: 2 });
    // Two rows straight down is two steps, not three: the point of a hex board.
    expect(hex.distance({ x: 2, y: 2 }, { x: 2, y: 4 })).toBe(2);
    expect(hex.within({ x: 3, y: 3 }, 1, 2)).toHaveLength(18);
  });
});

describe('a board is a map read under a tiling', () => {
  const level = (): LevelData =>
    makeLevel(['....', '....', '....'], { units: [u(0, 0, 'soldier', 1), u(3, 2, 'soldier', 2)] });

  it('clips every question to the map', () => {
    const state = testState(level());
    const board = new Board(state.map, TacticalGrids.get('square8'));
    expect(board.neighbours({ x: 0, y: 0 })).toHaveLength(3);
    expect(board.ring({ x: 0, y: 0 }, 0, 1)).toHaveLength(4);
    expect(board.contains({ x: 4, y: 0 })).toBe(false);
    expect(board.step({ x: 0, y: 0 }, 'west')).toBeNull();
    expect(board.coordOf(board.indexOf({ x: 2, y: 1 }))).toEqual({ x: 2, y: 1 });
  });
});

/**
 * The tilings are rules, so a level names one and everything downstream follows:
 * reach, adjacency, blast shapes, the AI's own measurements.
 */
describe('a battle fought on another tiling', () => {
  const duel = (grid: string): LevelData => ({
    ...makeLevel(['.....', '.....', '.....', '.....', '.....'], {
      units: [u(1, 1, 'soldier', 1), u(2, 2, 'soldier', 2)],
    }),
    rules: { grid },
  });

  it('refuses a tiling nobody registered', () => {
    const battle = createBattleEngine({ content: TEST_CONTENT });
    expect(() => battle.createState(duel('nonesuch'))).toThrow(/未注册的棋盘几何「nonesuch」/);
  });

  it('lets a melee unit strike diagonally only where diagonals are steps', () => {
    const reach = (grid: string): boolean => {
      const battle = createBattleEngine({ content: TEST_CONTENT });
      const state = battle.createState(duel(grid));
      const [attacker, defender] = state.units;
      return battle.space
        .attackTargets(state, attacker, attacker, TEST_CONTENT.weapons.get('soldier_sword'))
        .some((cell) => cell.x === defender.x && cell.y === defender.y);
    };

    expect(reach('square4')).toBe(false);
    expect(reach('square8')).toBe(true);
  });

  it('plays out to a finish on every registered tiling', () => {
    for (const grid of TacticalGrids.all()) {
      const battle = createBattleEngine({ content: TEST_CONTENT });
      const state = battle.createState(duel(grid.id), { seed: 11 });
      for (const player of state.players) player.controller = 'ai';
      let actions = 0;
      while (state.phase === 'playing' && actions < 400) {
        battle.dispatch(state, battle.chooseAiAction(state));
        actions++;
      }
      expect({ grid: grid.id, phase: state.phase }).toEqual({ grid: grid.id, phase: 'over' });
      // And the battle is reproducible on that tiling, like any other.
      expect(hashState(battle.loadBattle(JSON.parse(JSON.stringify(battle.saveBattle(state))))))
        .toBe(hashState(state));
    }
  });
});
