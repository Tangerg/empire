import type { AbilityRules } from '../abilities';
import type { ContentRegistry } from '../registry';
import type { TurnOrderPolicy } from '../turn-order';
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
 */
export interface AiRules extends AbilityRules, VictoryRules, UnitDirectiveRules {
  /** Planning must ask the same policy execution will enforce. */
  readonly turnOrders: ContentRegistry<TurnOrderPolicy>;
}
