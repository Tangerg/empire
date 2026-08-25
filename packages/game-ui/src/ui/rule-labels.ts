import type { BattleResourceSystem, ResourceAmount, UnitDef } from '@empire/battle-engine';

/**
 * What the rules are called to a player.
 *
 * One question, one answer, asked of the engine that was handed over — never of a
 * table this package keeps. "What is a resource called" had three answers before
 * this module: the HUD asked the resource adapter, `event-presentation.ts` kept a
 * three-entry map of its own, and the demo app wrote the adapter lookup out a
 * third time. The shipped ruleset has four resources, so the map's answer for the
 * fourth was `weapon_uses` — the id, to the person playing.
 */

/** A resource's player-facing name, or its id when nothing named it. */
export const resourceLabel = (resources: BattleResourceSystem, id: string): string =>
  resources.adapters.tryGet(id)?.name ?? id;

/**
 * A list of resource amounts in words, and what to say when there are none.
 *
 * The empty case is the caller's word because it is the caller's meaning: an
 * empty modifier list is 无, and a unit with no recruit cost is 不可招募. The
 * formatting of the amounts themselves is not — it was written three times, once
 * of them printing raw ids into the editor's palette.
 */
export const amountsLabel = (
  resources: BattleResourceSystem,
  amounts: readonly ResourceAmount[],
  empty: string,
): string => (amounts.length === 0
  ? empty
  : amounts.map((amount) => `${resourceLabel(resources, amount.resource)} ${amount.amount}`).join(' · '));

/**
 * What a unit costs to recruit, or that it cannot be.
 *
 * Both the codex and the editor's palette answer this, and both used to write
 * `不可招募` themselves — the one word `amountsLabel` deliberately leaves to the
 * caller, which is right when the callers mean different things and wrong when they
 * mean the same one.
 */
export const recruitCostLabel = (resources: BattleResourceSystem, unit: UnitDef): string =>
  amountsLabel(resources, unit.recruitCosts, '不可招募');
