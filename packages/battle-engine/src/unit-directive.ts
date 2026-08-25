import { sameCoord } from './grid';
import type { Board } from './domain/board';
import { boardOf, } from './domain/board';
import { ContentRegistry } from './registry';
import type { Coord, GameState, Unit, UnitDirectiveMode } from './types';
import type { GridRules } from './tactical-grid';

/**
 * A standing order, and what a unit under it wants.
 *
 * The four orders were a closed union read by four scattered branches: a
 * four-arm ladder that decided what ground to want, a factor on how close to
 * stand to the enemy, a penalty on stopping to fight, and a cursor bump when a
 * patrol reached its waypoint. Adding a fifth order — raid, forage, escort a
 * convoy — meant finding all four, and the ladder had already grown an accident
 * nobody wrote on purpose: a patrol with a zone but no waypoints fell through
 * into the retreat arm and inherited the extraction pull.
 *
 * Each order states its own answers now, and the registry is open, so a content
 * pack can add one the way it adds a blast shape.
 */
export interface DirectiveSurvey {
  readonly state: GameState;
  readonly unit: Unit;
  /** Tile being considered, which is not always where the unit is standing. */
  readonly at: Coord;
  /** Cells of the order's zone; empty when it names none, or names an empty one. */
  readonly zone: readonly Coord[];
  /** The board under its tiling: how far the order's ground is, is a board question. */
  readonly board: Board;
}

export interface UnitDirectiveBehavior {
  readonly id: UnitDirectiveMode;
  /** What standing on this tile is worth to a unit under this order. */
  pull(survey: DirectiveSurvey): number;
  /** How much of a fight the order still wants, as a factor on positioning. */
  readonly engagement: number;
  /** What a plan pays for stopping to trade blows against this order. */
  readonly fightPenalty: number;
  /** The unit reached a tile — a patrol aims at its next waypoint here. */
  arriveAt?(unit: Unit, at: Coord): void;
}

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface UnitDirectiveRules extends GridRules {
  readonly directives: ContentRegistry<UnitDirectiveBehavior>;
}

/** The order this unit is under, resolved through the ruleset. */
export function directiveOf(
  rules: UnitDirectiveRules,
  unit: Unit,
): UnitDirectiveBehavior {
  return rules.directives.get(unit.directive.mode);
}

/** How badly a unit under orders wants to be standing on `at`. */
export function directivePull(
  rules: UnitDirectiveRules,
  state: GameState,
  unit: Unit,
  at: Coord,
): number {
  const zone = unit.directive.zone ? state.scenario.zones[unit.directive.zone] ?? [] : [];
  return directiveOf(rules, unit).pull({ state, unit, at, zone, board: boardOf(rules, state) });
}

/** Stand on the zone, or close on it hard. Extraction and patrol-by-area share this. */
const holdZone = ({ at, zone, board }: DirectiveSurvey): number => {
  const distance = board.nearestDistance(at, zone);
  return distance === 0 ? 1_200 : -distance * 180;
};

export const UnitDirectives = new ContentRegistry<UnitDirectiveBehavior>('unit directive');

UnitDirectives.defineAll([
  {
    id: 'assault',
    // No ground of its own to want: the rest of the appraisal decides.
    pull: () => 0,
    engagement: 1,
    fightPenalty: 0,
  },
  {
    id: 'guard',
    pull: (survey) => {
      if (survey.zone.length === 0) return 0;
      const distance = survey.board.nearestDistance(survey.at, survey.zone);
      return distance === 0 ? 260 : -distance * 55;
    },
    engagement: 1,
    fightPenalty: 0,
  },
  {
    id: 'patrol',
    pull: (survey) => {
      const { waypoints, cursor } = survey.unit.directive;
      if (waypoints.length > 0) {
        return 320 / (1 + survey.board.distance(survey.at, waypoints[cursor % waypoints.length]));
      }
      // A patrol with a zone and no waypoints paces the zone. Inherited from
      // the ladder's fall-through and kept deliberately: it is the only
      // sensible reading of "patrol this area, route unspecified".
      return survey.zone.length === 0 ? 0 : holdZone(survey);
    },
    arriveAt: (unit, at) => {
      const directive = unit.directive;
      if (directive.waypoints.length === 0) return;
      const waypoint = directive.waypoints[directive.cursor % directive.waypoints.length];
      if (sameCoord(waypoint, at)) {
        directive.cursor = (directive.cursor + 1) % directive.waypoints.length;
      }
    },
    engagement: 1,
    fightPenalty: 0,
  },
  {
    id: 'retreat',
    // Nowhere to run to is still an order to stop being here.
    pull: (survey) => (survey.zone.length === 0 ? -100 : holdZone(survey)),
    /** A withdrawing unit barely cares where the enemy is. */
    engagement: 0.15,
    /** And will not stop to trade blows at all if it can help it. */
    fightPenalty: -5_000,
  },
]);
UnitDirectives.seal();
