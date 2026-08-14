import { describe, expect, it } from 'vitest';
import { createBattleEngine } from '../plugins/default';
import { cloneContentCatalog } from '../content-pack';
import { controlRadius, disengagedControllers, hostileControlZone } from '../zone-of-control';
import { UnitEntity } from '../domain/unit-entity';
import { idx } from '../grid';
import { forceMoveUnit } from '../forced-movement';
import { TEST_CONTENT, TEST_RULES, makeLevel, testState, u } from './fixtures';
import type { GameEvent, GameState, LevelUnit, RuleSet } from '../types';

/**
 * Zone of control: holding ground, and what it costs to walk away from it.
 */

const battlefield = (
  units: LevelUnit[],
  rules: Partial<RuleSet> = { zoneOfControl: true },
): GameState => testState(makeLevel(['........', '........'], { units, rules }));

const stops = (state: GameState) =>
  [...TEST_RULES.space.moveField(state, state.units[0]).stops]
    .map((index) => ({ x: index % state.map.width, y: Math.floor(index / state.map.width) }))
    .sort((left, right) => left.x - right.x || left.y - right.y);

describe('holding ground', () => {
  it('is off unless the battle asks for it', () => {
    const state = battlefield([u(0, 0, 'knight', 1), u(3, 0, 'soldier', 2)], {});
    expect(controlRadius(TEST_CONTENT, state, state.units[1])).toBe(0);
    expect(hostileControlZone(TEST_RULES, state, state.units[0]).size).toBe(0);
    // Six movement and an open flank: the enemy is an obstacle to walk around.
    expect(stops(state).some((cell) => cell.x === 5)).toBe(true);
  });

  it('stops a move that walks into it, without forbidding the tile', () => {
    const state = battlefield([u(0, 0, 'knight', 1), u(3, 0, 'soldier', 2)]);
    const reachable = stops(state);
    // (2,0) is the swordsman's ground: reachable, and the end of the road.
    expect(reachable).toContainEqual({ x: 2, y: 0 });
    expect(reachable.some((cell) => cell.x >= 4)).toBe(false);
  });

  it('lets a unit that starts held leave the tile it stands on', () => {
    const state = battlefield([u(2, 0, 'knight', 1), u(3, 0, 'soldier', 2)]);
    // Otherwise a unit adjacent to an enemy would be frozen for the whole battle.
    expect(stops(state).map((cell) => cell.x)).toContain(1);
  });

  it('asks the content how far each type reaches', () => {
    const content = cloneContentCatalog(TEST_CONTENT);
    content.units.override('archer', { zoneOfControl: 0 });
    content.units.override('soldier', { zoneOfControl: 2 });
    const state = battlefield([u(0, 0, 'knight', 1), u(4, 0, 'archer', 2)]);

    expect(controlRadius(content, state, state.units[1])).toBe(0);
    expect(hostileControlZone({ ...TEST_RULES, content }, state, state.units[0]).size).toBe(0);

    const held = hostileControlZone({ ...TEST_RULES, content }, battlefield([u(0, 0, 'knight', 1), u(4, 0, 'soldier', 2)]), state.units[0]);
    expect(held.has(idx(state.map, 2, 0))).toBe(true);
    expect(held.has(idx(state.map, 1, 0))).toBe(false);
  });
});

describe('parting shots', () => {
  const disengage = (state: GameState, to: { x: number; y: number }) => {
    const engine = createBattleEngine({ content: TEST_CONTENT });
    return engine.dispatch(state, {
      kind: 'command',
      unit: state.units[0].id,
      path: [{ x: state.units[0].x, y: state.units[0].y }, to],
      command: { ability: 'wait' },
    });
  };

  it('fires when a unit leaves the ground an enemy holds', () => {
    const state = battlefield([u(1, 0, 'knight', 1), u(0, 0, 'soldier', 2)]);
    const events = disengage(state, { x: 3, y: 0 });
    const shot = events.find((event) => event.type === 'partingShot');

    expect(shot).toMatchObject({ attacker: state.units[1].id, defender: state.units[0].id });
    // Aimed at the tile being vacated, so the log and the animation agree.
    expect(shot).toMatchObject({ at: { x: 1, y: 0 } });
    expect(state.units[0].hp).toBeLessThan(TEST_CONTENT.units.get('knight').maxHp);
  });

  it('does not fire for sliding along the line', () => {
    const state = battlefield([u(1, 0, 'knight', 1), u(1, 1, 'soldier', 2)]);
    // Still adjacent afterwards: the swordsman never lost contact.
    const events = disengage(state, { x: 0, y: 1 });
    expect(events.some((event) => event.type === 'partingShot')).toBe(false);
  });

  it('costs the controller its one reaction for the round', () => {
    const state = battlefield([u(1, 0, 'knight', 1), u(0, 0, 'soldier', 2)]);
    const controller = state.units[1];
    expect(new UnitEntity(controller).canReact(state.turn)).toBe(true);
    disengage(state, { x: 3, y: 0 });
    expect(new UnitEntity(controller).canReact(state.turn)).toBe(false);
    expect(disengagedControllers(TEST_RULES, state, state.units[0], { x: 1, y: 0 }, { x: 3, y: 0 })).toEqual([]);
  });

  it('is given up by a stance that gives up its riposte', () => {
    const state = battlefield([u(1, 0, 'knight', 1), u(0, 0, 'soldier', 2)]);
    state.units[1].reaction = 'guard';
    expect(disengage(state, { x: 3, y: 0 }).some((event) => event.type === 'partingShot')).toBe(false);
  });

  it('abandons the order when it kills the unit that was leaving', () => {
    const state = battlefield([u(1, 0, 'mage', 1, 4), u(0, 0, 'knight', 2), u(5, 0, 'soldier', 2)]);
    const mage = state.units[0];
    const events = createBattleEngine({ content: TEST_CONTENT }).dispatch(state, {
      kind: 'command',
      unit: mage.id,
      path: [{ x: 1, y: 0 }, { x: 3, y: 0 }],
      command: { ability: 'attack', target: { x: 5, y: 0 } },
    });

    expect(events.some((event) => event.type === 'partingShot' && event.killed)).toBe(true);
    expect(state.units.some((unit) => unit.id === mage.id)).toBe(false);
    // The order died with its owner: no attack was ever resolved.
    expect(events.some((event) => event.type === 'attack')).toBe(false);
  });

  it('is not provoked by being thrown out of a zone', () => {
    const state = battlefield([u(1, 0, 'knight', 1), u(0, 0, 'soldier', 2)]);
    const knight = state.units[0];
    // The same displacement, chosen, would provoke — this one was not chosen.
    expect(disengagedControllers(TEST_RULES, state, knight, { x: 1, y: 0 }, { x: 4, y: 0 }))
      .toHaveLength(1);

    const events: GameEvent[] = [];
    forceMoveUnit(TEST_RULES, state, {
      unit: knight.id,
      source: { x: 0, y: 0 },
      mode: 'push',
      distance: 3,
    }, (event) => events.push(event));

    expect(knight.x).toBe(4);
    expect(events.some((event) => event.type === 'partingShot')).toBe(false);
  });
});
