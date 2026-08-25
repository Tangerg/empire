import type { ResourceAccounts, Unit } from './types';

/**
 * A copied set of accounts: the entries are new objects, not the same ones.
 *
 * Exported because a career change rebuilds a unit's weapon accounts and had its
 * own copy of this line. An account is mutated in place by the resource system,
 * so two spellings of "copy the accounts" is one edit away from a snapshot that
 * shares a balance with the unit it was taken from.
 */
export const cloneAccounts = (accounts: ResourceAccounts): ResourceAccounts =>
  Object.fromEntries(Object.entries(accounts).map(([id, account]) => [id, { ...account }]));

/** Deep battle-local snapshot used by simulation, undo, corpses and revival. */
export function cloneUnitState(unit: Unit): Unit {
  return {
    ...unit,
    resources: cloneAccounts(unit.resources),
    statuses: unit.statuses.map((status) => ({ ...status })),
    weaponState: Object.fromEntries(
      Object.entries(unit.weaponState).map(([weaponId, state]) => [
        weaponId,
        { ...state, resources: cloneAccounts(state.resources) },
      ]),
    ),
    career: {
      current: unit.career.current,
      unlocked: unit.career.unlocked.slice(),
      mastery: { ...unit.career.mastery },
    },
    morale: { ...unit.morale },
    directive: {
      ...unit.directive,
      waypoints: unit.directive.waypoints.map((point) => ({ ...point })),
    },
    learnedAbilities: unit.learnedAbilities.slice(),
  };
}
