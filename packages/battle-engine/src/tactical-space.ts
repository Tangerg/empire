import {
  attackTargetCoords,
  computeMoveField,
  healTargetsFrom,
  pathTo,
  threatTiles,
  type MoveField,
} from './movement';
import { isUnitVisible, visibleTiles, visibleUnits } from './vision';
import { hostileControlZone } from './zone-of-control';
import type { Coord, GameState, PlayerId, Unit, WeaponDef } from './types';
import { type ContentCatalog } from './content-pack';
import { boardOf, type Board } from './domain/board';
import type { GridRegistry } from './tactical-grid';

/**
 * Cohesive port for rules that project units onto the battlefield.
 *
 * Movement, targeting and visibility deliberately travel together: a variant
 * such as zones of control, teleporting, stealth or a different line-of-sight
 * policy must not make the menu, AI and authoritative action validation
 * disagree. Geometry primitives and map storage remain ordinary domain code;
 * this port owns only their gameplay interpretation.
 */
export interface TacticalSpace {
  moveField(state: GameState, unit: Unit): MoveField;
  pathTo(field: MoveField, state: GameState, destination: Coord): Coord[] | null;
  threatOf(state: GameState, unit: Unit, field?: MoveField): Set<number>;
  /** Tiles held by this unit's enemies; entering one ends its move. */
  controlZoneAgainst(state: GameState, unit: Unit): Set<number>;
  attackTargets(state: GameState, unit: Unit, from: Coord, weapon: WeaponDef): Coord[];
  healTargets(state: GameState, unit: Unit, from: Coord): Unit[];
  /** The board this state is played on, so a caller need not resolve the tiling. */
  board(state: GameState): Board;
  visibleTiles(state: GameState, viewer: PlayerId): Set<number>;
  isUnitVisible(state: GameState, viewer: PlayerId, unit: Unit, seen?: Set<number>): boolean;
  visibleUnits(state: GameState, viewer: PlayerId): Unit[];
}

export class DefaultTacticalSpace implements TacticalSpace {
  /**
   * The tilings come in beside the catalog: which board this ruleset plays on is
   * a rule, and every query below is measured under it.
   */
  constructor(
    readonly content: ContentCatalog,
    readonly grids: GridRegistry,
  ) {}

  board(state: GameState): Board {
    return boardOf(this, state);
  }

  moveField(state: GameState, unit: Unit): MoveField {
    return computeMoveField(this, state, unit);
  }

  pathTo(field: MoveField, state: GameState, destination: Coord): Coord[] | null {
    return pathTo(field, state.map, destination);
  }

  threatOf(state: GameState, unit: Unit, field?: MoveField): Set<number> {
    return threatTiles(this, state, unit, field);
  }

  controlZoneAgainst(state: GameState, unit: Unit): Set<number> {
    return hostileControlZone(this, state, unit);
  }

  attackTargets(state: GameState, unit: Unit, from: Coord, weapon: WeaponDef): Coord[] {
    return attackTargetCoords(this, state, unit, from, weapon);
  }

  healTargets(state: GameState, unit: Unit, from: Coord): Unit[] {
    return healTargetsFrom(this, state, unit, from);
  }

  visibleTiles(state: GameState, viewer: PlayerId): Set<number> {
    return visibleTiles(this, state, viewer);
  }

  isUnitVisible(state: GameState, viewer: PlayerId, unit: Unit, seen?: Set<number>): boolean {
    return isUnitVisible(this, state, viewer, unit, seen);
  }

  visibleUnits(state: GameState, viewer: PlayerId): Unit[] {
    return visibleUnits(this, state, viewer);
  }
}

