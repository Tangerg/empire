import { makeLevel, testDamage, testLineOfSight, testMoveField, testState, u } from './fixtures';
import { describe, expect, it } from 'vitest';

describe('elevation and cliffs', () => {
  it('charges uphill movement, blocks excessive steps and lets flight cross explicit cliffs', () => {
    const level = makeLevel(['...'], { units: [u(0, 0, 'soldier', 1)] });
    level.elevation = [0, 1, 3];
    const climb = testState(level);
    const climbField = testMoveField(climb, climb.units[0]);
    expect(climbField.tiles.get(1)?.cost).toBe(2);
    expect(climbField.tiles.has(2)).toBe(false);

    level.cliffs = [{ from: { x: 0, y: 0 }, to: { x: 1, y: 0 } }];
    const foot = testState(level);
    expect(testMoveField(foot, foot.units[0]).tiles.has(1)).toBe(false);

    const flying = testState({ ...level, units: [u(0, 0, 'dragon', 1)] });
    expect(testMoveField(flying, flying.units[0]).tiles.get(1)?.cost).toBe(1);
    expect(testMoveField(flying, flying.units[0]).tiles.get(2)?.cost).toBe(2);
  });

  it('uses eye and obstruction heights for direct line of sight', () => {
    const lowLevel = makeLevel(['.T.'], { units: [u(0, 0, 'archer', 1), u(2, 0, 'archer', 2)] });
    lowLevel.elevation = [0, 0, 0];
    expect(testLineOfSight(testState(lowLevel), { x: 0, y: 0 }, { x: 2, y: 0 })).toBe(false);

    const highLevel = { ...lowLevel, elevation: [3, 0, 0] };
    expect(testLineOfSight(testState(highLevel), { x: 0, y: 0 }, { x: 2, y: 0 })).toBe(true);
  });

  it('adds an explainable high-ground multiplier', () => {
    const level = makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] });
    level.elevation = [2, 0];
    const state = testState(level);
    const damage = testDamage(state, state.units[0], state.units[1]);
    expect(damage.modifiers).toContainEqual(expect.objectContaining({ id: 'elevation.high-ground', value: 1.1 }));
  });
});

describe('facing, flanking and cover', () => {
  it('distinguishes front, side and back attacks and detects an opposed flanker', () => {
    const rearState = testState(makeLevel(['...'], {
      units: [
        { ...u(0, 0, 'soldier', 1), facing: 'east' },
        { ...u(1, 0, 'soldier', 2), facing: 'east' },
        { ...u(2, 0, 'soldier', 1), facing: 'west' },
      ],
    }));
    const damage = testDamage(rearState, rearState.units[0], rearState.units[1]);
    expect(damage.modifiers.map((entry) => entry.id)).toEqual(expect.arrayContaining([
      'position.back-attack',
      'position.flank',
    ]));
  });

  it('applies directional half/full cover and high ground reduces one cover tier', () => {
    const level = makeLevel(['.....'], {
      units: [u(0, 0, 'archer', 1), u(2, 0, 'archer', 2), u(4, 0, 'archer', 1)],
    });
    level.directionalCover = [{ at: { x: 2, y: 0 }, sides: { west: 'full', east: 'half' } }];
    const state = testState(level);
    const west = testDamage(state, state.units[0], state.units[1]);
    const east = testDamage(state, state.units[2], state.units[1]);
    expect(west.modifiers).toContainEqual(expect.objectContaining({ id: 'cover.full' }));
    expect(east.modifiers).toContainEqual(expect.objectContaining({ id: 'cover.half' }));

    state.map.elevation[0] = 2;
    const high = testDamage(state, state.units[0], state.units[1]);
    expect(high.modifiers).toContainEqual(expect.objectContaining({ id: 'cover.half' }));
  });
});
