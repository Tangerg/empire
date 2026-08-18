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
  owner: number,
  emit: (event: GameEvent) => void,
  /** Units whose actor turn is starting; defaults to the owner's whole army. */
  scope?: readonly Unit[],
): void {
  for (const unit of scope ?? state.units.filter((candidate) => candidate.owner === owner)) {
    for (const instance of overlaysAt(content, state, unit)) {
      const effect = content.terrainOverlays.get(instance.type).turnStartStatus;
      if (effect) addStatus(content, unit, effect.id, effect.duration, emit);
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
