import { describe, expect, it } from 'vitest';
import { consumeWeapon } from '../combat';
import {
  BattleAggregate,
  DomainInvariantError,
  PlayerEntity,
  StructureEntity,
  UnitEntity,
} from '../domain/index';
import { structureDef } from '../data/structures';
import { TEST_CONTENT, TEST_RULES, makeLevel, testState, u } from './fixtures';
import {
  COMMAND_POINTS_RESOURCE,
  DefaultBattleResources,
  WEAPON_USES_RESOURCE,
  playerResource,
} from '../resources';

describe('rich domain model', () => {
  it('keeps HP, reaction, cooldown and progression invariants inside UnitEntity', () => {
    const state = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1, 50), u(1, 0, 'soldier', 2)] }),
    );
    const unit = new UnitEntity(state.units[0]);
    expect(unit.heal(999, 100)).toBe(50);
    expect(unit.takeDamage(35)).toMatchObject({ amount: 35, hpAfter: 65, killed: false });
    unit.changeReaction('guard');
    unit.consumeReaction(3);
    consumeWeapon(TEST_RULES, state, unit.state, 'soldier_javelin');
    expect(unit.state).toMatchObject({ reaction: 'guard', reactionUsedRound: 3, rankProgress: 0 });
    expect(unit.state.weaponState.soldier_javelin.resources[WEAPON_USES_RESOURCE].current).toBe(1);
    expect(unit.addRankProgress(4.6)).toBe(5);
    expect(() => unit.takeDamage(-1)).toThrow(DomainInvariantError);
  });

  it('keeps accounts on the player while resource policy is handled by the domain service', () => {
    const state = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    const subject = playerResource(state.players[0]);
    expect(DefaultBattleResources.credit(COMMAND_POINTS_RESOURCE, subject, 999)).toBe(5);
    DefaultBattleResources.spend(COMMAND_POINTS_RESOURCE, subject, 2);
    expect(state.players[0].resources[COMMAND_POINTS_RESOURCE].current).toBe(3);
    expect(() => DefaultBattleResources.spend(COMMAND_POINTS_RESOURCE, subject, 4)).toThrow(
      DomainInvariantError,
    );
  });

  it('keeps objective lifecycle transitions inside the player aggregate boundary', () => {
    const state = testState(
      makeLevel(['..'], {
        units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
        victory: [{ id: 'hold', type: 'surviveTurns', turns: 3, hidden: true }],
      }),
    );
    const objective = new PlayerEntity(state.players[0]).objective('hold');
    objective.changeVisibility(false);
    expect(objective.resolve('pending')).toBe(false);
    expect(objective.resolve('success')).toBe(true);
    expect(objective.resolve('failure')).toBe(false);
    expect(objective.state).toMatchObject({ status: 'completed', hidden: false });
  });

  it('lets the aggregate own unit death and board cleanup atomically', () => {
    const state = testState(
      makeLevel(['v.'], {
        units: [u(0, 0, 'soldier', 1, 5), u(1, 0, 'soldier', 2)],
        owners: [{ x: 0, y: 0, owner: 2 }],
      }),
    );
    state.map.captureProgress[0] = 40;
    const battle = new BattleAggregate(state, TEST_CONTENT);
    expect(battle.damageUnit(state.units[0].id, 99)).toMatchObject({ killed: true, hpAfter: 0 });
    expect(state.units.some((unit) => unit.id === 1)).toBe(false);
    expect(state.map.captureProgress[0]).toBe(0);
  });

  it('keeps structure defense and repair rules in StructureEntity', () => {
    const state = testState(
      makeLevel(['..'], {
        units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
        structures: [{ id: 'gate', type: 'gate', x: 1, y: 0, hp: 100 }],
      }),
    );
    const structure = state.structures[0];
    const entity = new StructureEntity(structure, structureDef('gate'));
    expect(entity.takeRawDamage(50)).toMatchObject({ amount: 40, hpAfter: 60, destroyed: false });
    expect(entity.repair(20)).toBe(20);
    expect(structure.hp).toBe(80);
  });
});
