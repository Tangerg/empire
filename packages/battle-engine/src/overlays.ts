import { DomainInvariantError } from './domain/errors';
import { Battlefield } from './domain/battlefield';
import { addStatus } from './statuses';
import type { Coord, GameEvent, GameState, TerrainOverlayState, Unit } from './types';
import { type ContentCatalog } from './content-pack';

function overlaysAt(content: ContentCatalog, state: GameState, at: Coord): TerrainOverlayState[] {
  return new Battlefield(state, content).cell(at).overlayStates;
}

export function addTerrainOverlay(
  content: ContentCatalog,
  state: GameState,
  overlay: TerrainOverlayState,
  emit: (event: GameEvent) => void,
): void {
  if (state.scenario.overlays.some((candidate) => candidate.id === overlay.id)) {
    throw new DomainInvariantError(`duplicate terrain overlay id "${overlay.id}"`);
  }
  content.terrainOverlays.get(overlay.type);
  state.scenario.overlays.push({ ...overlay, cells: overlay.cells.map((cell) => ({ ...cell })) });
  emit({
    type: 'overlayAdded',
    overlay: overlay.id,
    overlayType: overlay.type,
    cells: overlay.cells.map((cell) => ({ ...cell })),
  });
}

export function removeTerrainOverlay(
  state: GameState,
  id: string,
  emit: (event: GameEvent) => void,
): boolean {
  const index = state.scenario.overlays.findIndex((overlay) => overlay.id === id);
  if (index < 0) return false;
  state.scenario.overlays.splice(index, 1);
  emit({ type: 'overlayRemoved', overlay: id });
  return true;
}

/** Applies environmental statuses before normal owner-turn status resolution. */
export function applyOverlayTurnStartEffects(
  content: ContentCatalog,
  state: GameState,
  /**
   * Units whose actor turn is starting.
   *
   * Required, and it used to default to "the owner's whole army" — a second way
   * to decide scope that the one caller never took, and one that would have been
   * wrong under unit-by-unit turn order. Removing the default also removed the
   * only reason this function knew whose turn it was: it needs the units, not
   * the side they belong to.
   */
  scope: readonly Unit[],
  emit: (event: GameEvent) => void,
): void {
  for (const unit of scope) {
    for (const instance of overlaysAt(content, state, unit)) {
      const effect = content.terrainOverlays.get(instance.type).turnStartStatus;
      if (effect) addStatus(content, unit, { id: effect.id, remaining: effect.duration }, emit);
    }
  }
}

/** Called exactly when the player order wraps, so durations use full rounds. */
export function advanceTerrainOverlayRound(
  state: GameState,
  emit: (event: GameEvent) => void,
): void {
  for (const overlay of [...state.scenario.overlays]) {
    if (overlay.remainingRounds === null) continue;
    overlay.remainingRounds--;
    if (overlay.remainingRounds <= 0) removeTerrainOverlay(state, overlay.id, emit);
  }
}
