import type { AbilityRules } from '../abilities';
import type { TurnOrderRules } from '../turn-order';
import type { UnitDirectiveRules } from '../unit-directive';
import type { VictoryRules } from '../victory';

/** How a side is told to play. */
export interface AiOptions {
  /** 0 = turtle on defensible ground, 1 = charge the enemy keep. */
  aggression: number;
}

/**
 * Everything AI planning needs: it must reason with the *same* ruleset that
 * will execute the action, otherwise its predictions are fiction.
 *
 * Nothing of its own — every field arrives from the port of a rule the planner
 * consults. It used to re-declare `turnOrders`, and the turn context then wrote
 * `activeTurnOrder`'s body out again beside it: a port copying a field is a
 * copy of the question that field answers.
 */
export interface AiRules extends AbilityRules, VictoryRules, UnitDirectiveRules, TurnOrderRules {}
