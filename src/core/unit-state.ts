import type { ResourceAccounts, Unit } from './types';

const cloneAccounts = (accounts: ResourceAccounts): ResourceAccounts =>
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
    meta: { ...unit.meta },
  };
}
