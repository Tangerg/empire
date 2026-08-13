import { describe, expect, it } from 'vitest';
import { createBattleEngine } from '../engine';
import { cloneContentCatalog } from '../content-pack';
import { DomainInvariantError, IllegalActionError, UnitEntity } from '../domain/index';
import { TEST_CONTENT, makeLevel, u } from './fixtures';
import type { Action, GameState, LevelData } from '../types';

/**
 * Command authority.
 *
 * "May the acting player give this unit this order right now" is one rule:
 * ownership, the spent-action flag, and the ordering policy's entitlement. It
 * used to be re-typed inline in every handler, and six of them left the third
 * part out — invisible under side turns, where owning an un-acted unit happens
 * to mean it is entitled to act, and wide open under a per-unit order, where it
 * let a player retask their whole army during one unit's turn.
 */

const engine = () => createBattleEngine({ content: TEST_CONTENT });

const squad = (rules: Record<string, unknown> = {}): LevelData =>
  makeLevel(['.....', '.....'], {
    units: [
      u(0, 0, 'soldier', 1),
      u(1, 0, 'knight', 1),
      u(4, 0, 'soldier', 2),
      u(4, 1, 'ballista', 2),
    ],
    rules,
  });

/** A unit of the acting player that the ordering policy has not entitled. */
function bystander(state: GameState) {
  const found = state.units.find(
    (unit) => unit.owner === state.currentPlayer && unit.id !== state.turnOrder.activeUnit,
  );
  expect(found, 'the fixture must give the acting player a second unit').toBeTruthy();
  return found!;
}

describe('command authority under a per-unit turn order', () => {
  const passiveOrders = (unitId: number): Action[] => [
    { kind: 'face', unit: unitId, facing: 'south' },
    { kind: 'reaction', unit: unitId, stance: 'guard' },
    { kind: 'changeFormation', unit: unitId, formation: null },
    { kind: 'changeCareer', unit: unitId, career: 'ranger' },
  ];

  it('refuses every order aimed at a unit that is not the active actor', () => {
    const battle = engine();
    const state = battle.createState(squad({ turnOrder: 'initiative' }));
    const idle = bystander(state);

    for (const order of passiveOrders(idle.id)) {
      expect(() => battle.dispatch(state, order), order.kind).toThrow(IllegalActionError);
    }
  });

  it('still lets the entitled actor issue those same orders', () => {
    const battle = engine();
    const state = battle.createState(squad({ turnOrder: 'initiative' }));
    const active = state.units.find((unit) => unit.id === state.turnOrder.activeUnit)!;

    expect(() => battle.dispatch(state, { kind: 'face', unit: active.id, facing: 'south' })).not.toThrow();
    expect(active.facing).toBe('south');
  });

  it('keeps side turns entitling the whole army, as Ancient Empires does', () => {
    const battle = engine();
    const state = battle.createState(squad());
    for (const unit of state.units.filter((candidate) => candidate.owner === state.currentPlayer)) {
      expect(() => battle.dispatch(state, { kind: 'face', unit: unit.id, facing: 'north' })).not.toThrow();
    }
  });

  it('refuses an order aimed at a unit that has already acted', () => {
    const battle = engine();
    const state = battle.createState(squad());
    const unit = state.units.find((candidate) => candidate.owner === 1)!;
    new UnitEntity(unit).finishAction();
    expect(() => battle.dispatch(state, { kind: 'face', unit: unit.id, facing: 'west' }))
      .toThrow(/已行动单位不能调整朝向/);
  });

  it('refuses an order aimed at the enemy, whoever is entitled', () => {
    const battle = engine();
    const state = battle.createState(squad());
    const enemy = state.units.find((candidate) => candidate.owner === 2)!;
    expect(() => battle.dispatch(state, { kind: 'face', unit: enemy.id, facing: 'west' }))
      .toThrow(/不是你的单位/);
  });
});

describe('refusals are typed, not stringly relabelled', () => {
  it('reports an order aimed at nobody as a refusal, not an engine fault', () => {
    const battle = engine();
    const state = battle.createState(squad());
    // Handlers used to reach for the unit with `requireUnit`, which raises a
    // plain Error: a bad *order* surfaced as an engine fault the shell could
    // not tell apart from a defect.
    expect(() => battle.dispatch(state, { kind: 'face', unit: 9999, facing: 'north' }))
      .toThrow(IllegalActionError);
  });

  it('lets a collaborator refuse an order in its own voice', () => {
    const battle = createBattleEngine({ content: cloneContentCatalog(TEST_CONTENT) });
    battle.content.units.override('knight', { transport: { capacity: 2 } });
    // A transport and a passenger two tiles apart: embarking is illegal, and the
    // refusal now comes from the transport rules themselves, instead of a plain
    // Error that the handler caught and relabelled.
    const state = battle.createState(makeLevel(['....'], {
      units: [u(0, 0, 'knight', 1), u(2, 0, 'soldier', 1), u(3, 0, 'soldier', 2)],
    }));
    const [carrier, passenger] = state.units;
    expect(() => battle.dispatch(state, { kind: 'embark', unit: passenger.id, carrier: carrier.id }))
      .toThrow(IllegalActionError);
    expect(() => battle.dispatch(state, { kind: 'embark', unit: passenger.id, carrier: carrier.id }))
      .toThrow(/adjacent/);
  });

  it('keeps an engine fault distinguishable from an illegal order', () => {
    const state = engine().createState(squad());
    const unit = new UnitEntity(state.units[0]);
    // Asking a unit to take negative damage is a defect in the caller, and it
    // must never be presentable to a player as "that move is not allowed".
    expect(() => unit.takeDamage(-1)).toThrow(DomainInvariantError);
    expect(() => unit.takeDamage(-1)).not.toThrow(IllegalActionError);
  });
});

describe('a battle nobody can act in still ends', () => {
  it('announces the outcome instead of freezing in the over phase', () => {
    const battle = engine();
    const state = battle.createState(squad({ turnOrder: 'initiative' }));
    // Every actor is gone: the ordering policy reports exhaustion. This used to
    // set the phase and return, so the battle was over with no winner, no end
    // reason, and no gameOver event for a shell to react to.
    state.units = [];

    const events = battle.dispatch(state, { kind: 'endTurn' });

    expect(state.phase).toBe('over');
    expect(state.endReason).not.toBe('');
    expect(events.some((event) => event.type === 'gameOver')).toBe(true);
  });
});
