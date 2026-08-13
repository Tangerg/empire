import { describe, expect, it } from 'vitest';
import { Abilities, type AbilityDef } from '../abilities';
import { ActionHandlerRegistry, type ActionHandler } from '../action-system';
import { CoreActionHandlers } from '../actions';
import {
  BattleEngineConfigurationError,
  BattleLevelError,
  createBattleEngine,
} from '../engine';

import { GameSession } from '../session';
import { DefaultTacticalSpace } from '../tactical-space';
import type { GameEventKindMap } from '../types';
import { TEST_CONTENT, makeLevel, u } from './fixtures';
import { createTestCatalog } from '@empire/test-content';

declare module '../types' {
  interface GameEventKindMap {
    testPulse: { type: 'testPulse'; unit: number; strength: number };
  }
}

const pulse: AbilityDef = {
  id: 'test-pulse',
  name: '测试脉冲',
  hint: '由隔离能力目录提供',
  selfTargeted: true,
  priority: 15,
  tags: ['extension'],
  targets: () => [],
  usable: () => true,
  execute: (_rules, { state, unit }, _target, emit) => {
    state.scenario.variables.pulses = Number(state.scenario.variables.pulses ?? 0) + 1;
    emit({ type: 'testPulse', unit: unit.id, strength: 3 });
  },
};

class PacifistSpace extends DefaultTacticalSpace {
  override attackTargets(): [] {
    return [];
  }
}

class FailingEndTurn implements ActionHandler<'endTurn'> {
  readonly kind = 'endTurn' as const;

  execute(context: Parameters<ActionHandler<'endTurn'>['execute']>[0]): void {
    context.state.turn = 999;
    context.state.currentPlayer = 2;
    throw new Error('turn extension failed');
  }
}

function duel() {
  return makeLevel(['..'], {
    units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
  });
}

describe('balanced engine extension seams', () => {
  it('installs a custom ability and semantic event in one isolated engine', () => {
    const abilities = Abilities.clone();
    abilities.define(pulse);
    const session = new GameSession(duel(), createBattleEngine({ content: TEST_CONTENT, abilities }));
    const actor = session.state.units[0];
    actor.learnedAbilities.push(pulse.id);

    expect(session.commandsAt(actor, actor).map((option) => option.ability)).toContain(pulse.id);
    expect(Abilities.has(pulse.id)).toBe(false);

    const events = session.dispatch({
      kind: 'command',
      unit: actor.id,
      path: [{ x: actor.x, y: actor.y }],
      command: { ability: pulse.id },
    });

    const emitted: GameEventKindMap['testPulse'] | undefined = events.find(
      (event): event is GameEventKindMap['testPulse'] => event.type === 'testPulse',
    );
    expect(emitted).toEqual({ type: 'testPulse', unit: actor.id, strength: 3 });
    expect(session.state.scenario.variables.pulses).toBe(1);
  });

  it('uses one spatial policy for menus and authoritative validation', () => {
    const session = new GameSession(duel(), createBattleEngine({ content: TEST_CONTENT, space: new PacifistSpace(TEST_CONTENT) }));
    const actor = session.state.units[0];

    expect(session.commandsAt(actor, actor).some((option) => option.ability === 'attack')).toBe(false);
    expect(session.tryDispatch({
      kind: 'command',
      unit: actor.id,
      path: [{ x: actor.x, y: actor.y }],
      command: { ability: 'attack', target: { x: 1, y: 0 } },
    })).toBeNull();
    expect(session.state.units[1].hp).toBe(100);
  });

  it('rolls back failed end-turn extensions without making turn changes undoable', () => {
    const handlers = CoreActionHandlers.clone().replace(new FailingEndTurn());
    const session = new GameSession(duel(), createBattleEngine({
      content: TEST_CONTENT,
      actionHandlers: handlers as ActionHandlerRegistry,
    }));
    const before = structuredClone(session.state);

    expect(() => session.dispatch({ kind: 'endTurn' })).toThrow('turn extension failed');
    expect(session.state).toEqual(before);
    expect(session.log).toEqual([]);
    expect(session.canUndo).toBe(false);
  });

  it('rolls the semantic event log back with an undone action', () => {
    const abilities = Abilities.clone();
    abilities.define(pulse);
    const session = new GameSession(duel(), createBattleEngine({ content: TEST_CONTENT, abilities }));
    const actor = session.state.units[0];
    actor.learnedAbilities.push(pulse.id);
    session.dispatch({
      kind: 'command',
      unit: actor.id,
      path: [{ x: actor.x, y: actor.y }],
      command: { ability: pulse.id },
    });
    expect(session.log.length).toBeGreaterThan(0);

    expect(session.undo()).toBe(true);
    expect(session.log).toEqual([]);
    expect(session.state.scenario.variables.pulses).toBeUndefined();
  });

  it('keeps direct BattleEngine dispatch transactional without a session shell', () => {
    const handlers = CoreActionHandlers.clone().replace(new FailingEndTurn());
    const engine = createBattleEngine({ content: TEST_CONTENT, actionHandlers: handlers as ActionHandlerRegistry });
    const state = engine.createState(duel());
    const before = structuredClone(state);

    expect(() => engine.dispatch(state, { kind: 'endTurn' })).toThrow('turn extension failed');
    expect(state).toEqual(before);
  });

  it('builds isolated default strategy graphs for every session', () => {
    const first = new GameSession(duel(), createBattleEngine({ content: TEST_CONTENT }));
    const second = new GameSession(duel(), createBattleEngine({ content: TEST_CONTENT }));
    first.engine.rules.abilities.define(pulse);
    first.engine.actionHandlers.replace(new FailingEndTurn());

    expect(second.engine.rules.abilities.has(pulse.id)).toBe(false);
    expect(() => first.dispatch({ kind: 'endTurn' })).toThrow('turn extension failed');
    expect(() => second.dispatch({ kind: 'endTurn' })).not.toThrow();
    expect(second.state.currentPlayer).toBe(2);
  });

  it('fails fast for incompatible engine capabilities and malformed levels', () => {
    const content = createTestCatalog();
    const soldier = content.units.get('soldier');
    content.units.override(soldier.id, {
      abilities: [...soldier.abilities, 'missing-engine-ability'],
    });
    expect(() => createBattleEngine({ content })).toThrow(BattleEngineConfigurationError);

    const engine = createBattleEngine({ content: TEST_CONTENT });
    const invalid = duel();
    invalid.units[1].x = invalid.units[0].x;
    expect(() => engine.createState(invalid)).toThrow(BattleLevelError);
  });
});
