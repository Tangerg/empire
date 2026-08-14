import { player } from '../state';
import type { Action, GameState } from '../types';
import { AiTurnContext, type AiIntentRegistry, type AiPlanningDependencies } from './turn-context';
import type { AiOptions } from './rules';

/**
 * One-ply utility AI.
 *
 * It scores every (destination, ability, target) triple a unit can legally
 * produce this turn and commits to the single best action in the whole army,
 * one action per call — which lets the UI animate each step and keeps the search
 * shallow enough to stay instant.
 *
 * The planner is split by the question each part answers: what a unit or tile is
 * worth (`measures`, `tile-appraisal`), what the enemy could do to it (`threat`),
 * what this side is playing for (`agenda`), what an ability is worth doing
 * (`ability-evaluators`), what analysis a decision may share (`turn-context`),
 * and what the AI considers doing at all (`default-intents`).
 *
 * Fog of war is intentionally ignored by the AI (documented, not accidental).
 */

export * from './rules';
export * from './measures';
export * from './threat';
export * from './agenda';
export * from './tile-appraisal';
export * from './ability-evaluators';
export * from './turn-context';
export * from './default-intents';

/**
 * What choosing an action needs: everything a turn context is built from, plus
 * the chain that chooses among the things it makes possible.
 *
 * Declared here rather than on `AiPlanningDependencies` because a context must
 * not carry the registry that is iterating intents over it — and because a port
 * that names a service its own module never reads is a port for somebody else.
 */
export interface AiDriverDependencies extends AiPlanningDependencies {
  readonly intents: AiIntentRegistry;
}

/**
 * The next single action for the current (AI) player. Returns `endTurn` when
 * no intent has anything to propose, so callers can simply loop.
 */
export function chooseAction(
  dependencies: AiDriverDependencies,
  state: GameState,
  overrides?: Partial<AiOptions>,
): Action {
  if (state.phase === 'deployment') return { kind: 'finishDeployment' };
  const side = state.currentPlayer;
  const options: AiOptions = {
    aggression: overrides?.aggression ?? player(state, side).ai.aggression,
  };
  return dependencies.intents.choose(new AiTurnContext(dependencies, state, side, options))
    ?? { kind: 'endTurn' };
}

