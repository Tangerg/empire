import type { ContentCatalog } from './content-pack';
import type { UnitFall } from './domain/unit-fall';
import { emitTransportLossEvents } from './transports';
import type { GameEvent, GameState, Unit } from './types';

/**
 * A unit has left the battlefield.
 *
 * Death has consequences that belong to other subsystems: a commander's aura
 * collapses, a charging strike fizzles, a transport spills its passengers.
 * Each was wired in by hand, so `handleCommanderDefeat` ended up called from
 * eleven places and every new consequence meant finding all eleven again.
 *
 * Departure is now one announcement with an open list of listeners. The module
 * that raises it does not know who reacts — the composition root registers the
 * handlers, which is also how a rule plugin adds a consequence of its own
 * without the core learning about it. Keeping the registry free of subsystem
 * imports is what stops the dependency graph from closing a cycle: combat needs
 * to announce a death, and the reaction to that death may itself need combat.
 */
export interface UnitDeparture {
  readonly state: GameState;
  /** Snapshot of the unit as it left; it is no longer in `state.units`. */
  readonly unit: Unit;
  readonly content: ContentCatalog;
  readonly emit: (event: GameEvent) => void;
}

export interface UnitDepartureHandler {
  readonly id: string;
  handle(departure: UnitDeparture): void;
}

export class UnitDepartureHandlerRegistry {
  private readonly handlers = new Map<string, UnitDepartureHandler>();

  register(handler: UnitDepartureHandler): this {
    if (this.handlers.has(handler.id)) {
      throw new Error(`departure handler already registered for "${handler.id}"`);
    }
    this.handlers.set(handler.id, handler);
    return this;
  }

  replace(handler: UnitDepartureHandler): this {
    this.handlers.set(handler.id, handler);
    return this;
  }

  ids(): string[] {
    return [...this.handlers.keys()];
  }

  /** Announces the departure to every listener, in registration order. */
  announce(departure: UnitDeparture): void {
    for (const handler of this.handlers.values()) handler.handle(departure);
  }

  clone(): UnitDepartureHandlerRegistry {
    const copy = new UnitDepartureHandlerRegistry();
    for (const handler of this.handlers.values()) copy.register(handler);
    return copy;
  }
}

export const UnitDepartureHandlers = new UnitDepartureHandlerRegistry();

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface UnitDepartureRules {
  readonly content: ContentCatalog;
  readonly unitDepartures: UnitDepartureHandlerRegistry;
}

/**
 * Announces that a unit left the field alive — withdrawn, surrendered, recalled.
 * The caller reports the departure in its own vocabulary; this settles the
 * consequences.
 */
export function announceUnitDeparture(
  rules: UnitDepartureRules,
  state: GameState,
  unit: Unit,
  emit: (event: GameEvent) => void,
): void {
  rules.unitDepartures.announce({ state, unit, content: rules.content, emit });
}

/**
 * Announces that a unit fell, with everything that implies.
 *
 * The death event, the corpse marker, the passengers lost with a transport and
 * the subsystem consequences were six lines repeated at every lethal-damage
 * site, and each copy was one edit away from forgetting a step. One call now
 * makes it impossible to report a death without resolving it.
 */
export function announceUnitFall(
  rules: UnitDepartureRules,
  state: GameState,
  fall: UnitFall,
  emit: (event: GameEvent) => void,
): void {
  emit({ type: 'death', unit: fall.unit.id, at: fall.at });
  if (fall.marker) {
    emit({ type: 'markerAdded', marker: fall.marker.id, kind: fall.marker.kind, at: fall.marker.at });
  }
  emitTransportLossEvents(fall.unit.id, fall.at, fall.passengerMarkers, emit);
  announceUnitDeparture(rules, state, fall.unit, emit);
}
