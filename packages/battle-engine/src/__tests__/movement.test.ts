import { describe, expect, it } from 'vitest';
import { idx } from '../grid';
import { MovementProfiles } from '../data/movement';
import { Terrains } from '../data/terrain';
import { pathTo } from '../movement';
import { makeLevel, testMoveField, testState, testThreat, u } from './fixtures';

const at = (map: { width: number }, x: number, y: number) => idx(map, x, y);

describe('movement field', () => {
  it('exposes extensible naval and amphibious profiles without changing core unions', () => {
    expect(MovementProfiles.has('naval')).toBe(true);
    expect(MovementProfiles.has('amphibious')).toBe(true);
    expect(Terrains.get('water').cost.naval).toBe(1);
    expect(Terrains.get('water').cost.amphibious).toBe(1);
    expect(Terrains.get('plain').cost.naval).toBeNull();
    expect(Terrains.get('plain').cost.amphibious).toBe(1);
  });

  it('spends the listed terrain cost and stops at the budget', () => {
    // soldier: 3 movement, foot. forest costs 2, plain 1.
    const s = testState(makeLevel(['.....', '.TTT.', '.....'], { units: [u(0, 0, 'soldier', 1)] }));
    const field = testMoveField(s, s.units[0]);

    expect(field.tiles.get(at(s.map, 3, 0))!.cost).toBe(3);
    expect(field.tiles.get(at(s.map, 1, 1))!.cost).toBe(3); // 1 right + 2 into forest
    expect(field.tiles.has(at(s.map, 2, 1))).toBe(false); // would cost 4
    expect(field.tiles.has(at(s.map, 4, 0))).toBe(false);
  });

  it('routes around impassable walls', () => {
    // rogue has 5 movement, so the 4-step detour below the wall is affordable.
    const s = testState(makeLevel(['.#..', '....'], { units: [u(0, 0, 'rogue', 1)] }));
    const field = testMoveField(s, s.units[0]);
    expect(field.tiles.has(at(s.map, 1, 0))).toBe(false); // the wall itself
    const path = pathTo(field, s.map, { x: 2, y: 0 });
    expect(path).not.toBeNull();
    expect(path!.some((c) => c.y === 1)).toBe(true); // detoured below the wall
    expect(field.tiles.get(at(s.map, 2, 0))!.cost).toBe(4);
  });

  it('lets units pass through allies but not stop on them', () => {
    const s = testState(
      makeLevel(['....'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'archer', 1)] }),
    );
    const field = testMoveField(s, s.units[0]);
    expect(field.tiles.has(at(s.map, 1, 0))).toBe(true);
    expect(field.stops.has(at(s.map, 1, 0))).toBe(false);
    expect(field.stops.has(at(s.map, 2, 0))).toBe(true);
  });

  it('is blocked by enemy units', () => {
    const s = testState(
      makeLevel(['....'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    const field = testMoveField(s, s.units[0]);
    expect(field.tiles.has(at(s.map, 1, 0))).toBe(false);
    expect(field.tiles.has(at(s.map, 2, 0))).toBe(false);
  });

  it('flying units ignore terrain cost and water', () => {
    const s = testState(makeLevel(['.~^T.'], { units: [u(0, 0, 'dragon', 1)] }));
    const field = testMoveField(s, s.units[0]);
    expect(field.tiles.get(at(s.map, 4, 0))!.cost).toBe(4);
  });

  it('mounted units cannot enter mountains', () => {
    const s = testState(makeLevel(['.^..'], { units: [u(0, 0, 'knight', 1)] }));
    const field = testMoveField(s, s.units[0]);
    expect(field.tiles.has(at(s.map, 1, 0))).toBe(false);
  });
});

describe('threat range', () => {
  it('covers move-then-attack for a normal unit', () => {
    const s = testState(makeLevel(['.....'], { units: [u(0, 0, 'soldier', 1)] }));
    const threat = testThreat(s, s.units[0]);
    expect(threat.has(at(s.map, 4, 0))).toBe(true); // 3 move + 1 reach
    expect(threat.size).toBeGreaterThan(3);
  });

  it('is only the static firing arc for siege units', () => {
    const s = testState(makeLevel(['......'], { units: [u(0, 0, 'ballista', 1)] }));
    const threat = testThreat(s, s.units[0]);
    expect(threat.has(at(s.map, 2, 0))).toBe(true);
    expect(threat.has(at(s.map, 3, 0))).toBe(true);
    expect(threat.has(at(s.map, 1, 0))).toBe(false); // inside minimum range
    expect(threat.has(at(s.map, 4, 0))).toBe(false); // cannot move and fire
  });
});
