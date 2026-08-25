import { ContentRegistry } from './registry';
import type { ReactionStance } from './types';

/**
 * Reaction stances as declared behaviour.
 *
 * A stance used to be one of four hard-coded strings, and its meaning lived in
 * `combat.ts` as `=== 'guard'` comparisons and a literal `0.7`. Adding a fifth
 * — dodge, bulwark, spellbreaker — meant editing damage forecasting, which is
 * the one place a content pack should never have to touch.
 *
 * Every stance is now a value object in a registry, so a stance is *data*: the
 * combat rules ask what a stance does instead of knowing which stances exist.
 * Fields are all required on purpose. Optional hooks would let a stance answer
 * "nothing in particular" to a question, and then the default would once again
 * live in the engine rather than in the stance.
 */
export interface ReactionBehavior {
  readonly id: ReactionStance;
  readonly name: string;
  readonly hint: string;
  /** Steps in front of an adjacent ally and takes the strike instead. */
  readonly intercepts: boolean;
  /** Factor applied to a strike this unit receives; 1 leaves it unchanged. */
  readonly incomingMultiplier: number;
  /** Strikes back when the attacker is within reach of a ready weapon. */
  readonly retaliates: boolean;
  /** Retaliates only with weapons that cost nothing to fire. */
  readonly conservesResources: boolean;
}

export const Reactions = new ContentRegistry<ReactionBehavior>('reaction stance');

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface ReactionRules {
  readonly reactions: ContentRegistry<ReactionBehavior>;
}

export function reactionOf(rules: ReactionRules, stance: ReactionStance): ReactionBehavior {
  return rules.reactions.get(stance);
}

Reactions.defineAll([
  {
    id: 'counter',
    name: '反击',
    hint: '被攻击时用射程覆盖得到的最强武器还击。',
    intercepts: false,
    incomingMultiplier: 1,
    retaliates: true,
    conservesResources: false,
  },
  {
    id: 'guard',
    name: '防御',
    hint: '减少受到的伤害，但放弃还击。',
    intercepts: false,
    incomingMultiplier: 0.7,
    retaliates: false,
    conservesResources: false,
  },
  {
    id: 'support',
    name: '援护',
    hint: '替相邻友军挡下攻击，每轮一次。',
    intercepts: true,
    incomingMultiplier: 1,
    retaliates: true,
    conservesResources: false,
  },
  {
    id: 'conserve',
    name: '节制',
    hint: '只用无消耗的武器还击，保留弹药与冷却。',
    intercepts: false,
    incomingMultiplier: 1,
    retaliates: true,
    conservesResources: true,
  },
]);
Reactions.seal();
