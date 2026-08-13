import type { BattlefieldMarker, Coord, Unit } from '../types';

/**
 * The record of a unit leaving the field under lethal damage.
 *
 * It carries the unit itself rather than an id: by the time anything reacts,
 * the unit is no longer in `state.units`, and every consequence — the aura it
 * anchored, the strike it was charging, the passengers it carried — needs to
 * know who it was.
 */
export interface UnitFall {
  readonly unit: Unit;
  readonly at: Coord;
  /** Corpse left behind, when the rules leave one. */
  readonly marker: BattlefieldMarker | null;
  /** Markers for passengers lost with a transport. */
  readonly passengerMarkers: readonly BattlefieldMarker[];
}
