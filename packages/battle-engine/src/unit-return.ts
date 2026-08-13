import { UnitEntity } from './domain/unit-entity';
import { unitAtCoord } from './state';
import { cloneUnitState } from './unit-state';
import type { BattlefieldMarker, Coord, GameEvent, GameState, PlayerId, Unit } from './types';

/**
 * A unit comes back onto the battlefield.
 *
 * Mirror of `unit-departure.ts`, and it exists for the same reason. Bringing a
 * unit back was written twice — once for reviving a corpse, once for recalling
 * a withdrawal — and the two copies had drifted apart in exactly the way two
 * copies do. One floored morale on the way back and the other did not, so a
 * unit revived from a rout returned at zero morale and broke again on the next
 * shock: a scenario that resurrected a broken soldier handed the player
 * something that evaporated. One cleared statuses and the other carried a
 * poison whose duration had stopped ticking while its owner was off the board.
 *
 * Neither difference was a design decision. They are settled here, once.
 */
export interface UnitReturn {
  readonly at: Coord;
  /** Whose side it rejoins; its own, if not given. */
  readonly owner?: PlayerId;
  /** Hit points on return; the snapshot's own, if not given. */
  readonly hp?: number;
}

/**
 * Puts the marker's fallen unit back on the field and consumes the marker.
 *
 * Returns null when it cannot come back — the tile is taken, the marker holds
 * no unit, or that unit is somehow already here — so a caller can simply move
 * on to the next marker.
 */
export function returnUnitToField(
  state: GameState,
  marker: BattlefieldMarker,
  request: UnitReturn,
  emit: (event: GameEvent) => void,
): Unit | null {
  const fallen = marker.fallenUnit;
  if (!fallen) return null;
  if (unitAtCoord(state, request.at)) return null;
  if (state.units.some((unit) => unit.id === fallen.id || (fallen.key && unit.key === fallen.key))) return null;

  const unit = cloneUnitState(fallen);
  unit.owner = request.owner ?? unit.owner;
  unit.x = request.at.x;
  unit.y = request.at.y;
  if (request.hp !== undefined) unit.hp = request.hp;
  // Arriving is not acting: whatever brought it back, it waits a turn.
  unit.done = true;
  unit.capture = 0;
  // Leaving the field ends what was clinging to you, and a status that stopped
  // ticking while you were gone would come back with a stale duration.
  unit.statuses = [];
  // Back on the field means back in the fight, for the same reason the
  // statuses go: leaving ended the panic as well as the poison. Flooring at 1
  // — which is what the withdrawal path did — is arithmetically non-zero and
  // practically still doomed: the next shock of any size breaks it again.
  unit.morale.current = unit.morale.maximum;
  new UnitEntity(unit).restoreReaction();

  state.units.push(unit);
  state.nextUnitId = Math.max(state.nextUnitId, unit.id + 1);
  const commander = state.commanders.find((candidate) => candidate.unitId === unit.id);
  if (commander) commander.owner = unit.owner;

  const index = state.markers.indexOf(marker);
  if (index >= 0) state.markers.splice(index, 1);
  emit({ type: 'markerRemoved', marker: marker.id, kind: marker.kind, at: marker.at });
  emit({ type: 'unitRevived', unit: unit.id, marker: marker.id, at: { ...request.at }, hp: unit.hp });
  return unit;
}
