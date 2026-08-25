import { describe, expect, it } from 'vitest';
import { Abilities, defineAbility } from '../abilities';
import { unitWeapons } from '../combat';
import { ActionHandlerRegistry, type ActionHandler } from '../action-system';
import { CoreActionHandlers } from '../actions';
import { BattleEngineConfigurationError, BattleLevelError } from '../engine';
import { DomainInvariantError, StoredDocumentError } from '../domain/errors';
import { createBattleEngine } from '../plugins/default';

import { GameSession } from '../session';
import { DefaultTacticalSpace } from '../tactical-space';
import type { GameEventKindMap } from '../types';
import { TEST_CONTENT, TEST_RULES, makeLevel, u } from './fixtures';
import { createTestCatalog } from '@empire/test-content';

declare module '../types' {
  interface GameEventKindMap {
    testPulse: { type: 'testPulse'; unit: number; strength: number };
  }
}

const pulse = defineAbility({
  id: 'test-pulse',
  name: '测试脉冲',
  hint: '由隔离能力目录提供',
  priority: 15,
  tags: ['extension'],
  execute: (_rules, { state, unit }, _target, emit) => {
    state.scenario.variables.pulses = Number(state.scenario.variables.pulses ?? 0) + 1;
    emit({ type: 'testPulse', unit: unit.id, strength: 3 });
  },
});

/**
 * The second weapon-using ability — the volley, the channelled beam that
 * `AbilityDef.weaponFor` was written for. It offers one order per weapon and
 * says so, instead of being expanded by a menu that recognises `attack`.
 */
const charge = defineAbility({
  id: 'test-charge',
  name: '蓄能',
  hint: '把一件武器充能到下一击。',
  priority: 12,
  weaponFor: (rules, q) => (q.weaponId ? rules.content.weapons.get(q.weaponId) : null),
  weaponChoices: (rules, q) => unitWeapons(rules.content, q.unit),
  execute: (_rules, { state, unit, weaponId }, _target, emit) => {
    state.scenario.variables.charged = weaponId ?? '';
    emit({ type: 'testPulse', unit: unit.id, strength: 1 });
  },
});

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

  it('offers one order per weapon for any ability that fires one', () => {
    const abilities = Abilities.clone();
    abilities.define(charge);
    const session = new GameSession(
      makeLevel(['..'], { units: [u(0, 0, 'mage', 1), u(1, 0, 'soldier', 2)] }),
      createBattleEngine({ content: TEST_CONTENT, abilities }),
    );
    const mage = session.state.units[0];
    mage.learnedAbilities.push(charge.id);

    // Three weapons, three orders — the expansion the menu used to perform only
    // for the ability whose id is `attack`.
    const offered = session.commandsAt(mage, mage).filter((option) => option.ability === charge.id);
    expect(offered.map((option) => option.key)).toEqual([
      'test-charge:mage_bolt',
      'test-charge:mage_meteor',
      'test-charge:mage_overcharge',
    ]);
    expect(offered.map((option) => option.name)).toEqual([
      '蓄能 · 魔法弹', '蓄能 · 陨石术', '蓄能 · 奥术过载',
    ]);
  });

  it('carries the chosen weapon all the way to the ability that fires it', () => {
    const abilities = Abilities.clone();
    abilities.define(charge);
    const session = new GameSession(
      makeLevel(['..'], { units: [u(0, 0, 'mage', 1), u(1, 0, 'soldier', 2)] }),
      createBattleEngine({ content: TEST_CONTENT, abilities }),
    );
    const mage = session.state.units[0];
    mage.learnedAbilities.push(charge.id);

    session.dispatch({
      kind: 'command',
      unit: mage.id,
      path: [{ x: 0, y: 0 }],
      command: { ability: charge.id, weapon: 'mage_meteor' },
    });

    // The dispatcher used to keep the weapon only for `attack` and drop it
    // everywhere else, so this fired the first weapon in the rack in silence.
    expect(session.state.scenario.variables.charged).toBe('mage_meteor');
  });

  it('refuses an order naming a weapon its ability never offered', () => {
    const session = new GameSession(
      makeLevel(['..'], { units: [u(0, 0, 'mage', 1), u(1, 0, 'soldier', 2)] }),
      createBattleEngine({ content: TEST_CONTENT }),
    );
    const mage = session.state.units[0];

    expect(() => session.dispatch({
      kind: 'command',
      unit: mage.id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'wait', weapon: 'mage_meteor' },
    })).toThrow(/「待机」无法使用「mage_meteor」/);
  });

  it('uses one spatial policy for menus and authoritative validation', () => {
    const session = new GameSession(duel(), createBattleEngine({ content: TEST_CONTENT, space: new PacifistSpace(TEST_CONTENT, TEST_RULES.grids) }));
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
    const abilities = Abilities.clone();
    abilities.define(pulse);
    const actionHandlers = CoreActionHandlers.clone();
    actionHandlers.replace(new FailingEndTurn());
    const first = new GameSession(duel(), createBattleEngine({
      content: TEST_CONTENT,
      abilities,
      actionHandlers,
    }));
    const second = new GameSession(duel(), createBattleEngine({ content: TEST_CONTENT }));

    expect(first.engine.rules.abilities.has(pulse.id)).toBe(true);
    expect(second.engine.rules.abilities.has(pulse.id)).toBe(false);
    expect(() => first.engine.rules.abilities.replace(pulse)).toThrow('sealed after composition');
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
    expect(() => createBattleEngine({ content })).toThrow(DomainInvariantError);

    const engine = createBattleEngine({ content: TEST_CONTENT });
    const invalid = duel();
    invalid.units[1].x = invalid.units[0].x;
    expect(() => engine.createState(invalid)).toThrow(BattleLevelError);
    expect(() => engine.createState(invalid)).toThrow(StoredDocumentError);
  });
});
