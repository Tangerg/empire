import {
  attackTargetCoords,
  computeMoveField,
  healTargetsFrom,
  pathTo,
  threatTiles,
  type MoveField,
} from './movement';
import { isUnitVisible, visibleTiles, visibleUnits } from './vision';
import type { Coord, GameState, PlayerId, Unit, WeaponDef } from './types';
import { type ContentCatalog } from './content-pack';

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
  attackTargets(state: GameState, unit: Unit, from: Coord, weapon: WeaponDef): Coord[];
  healTargets(state: GameState, unit: Unit, from: Coord): Unit[];
  visibleTiles(state: GameState, viewer: PlayerId): Set<number>;
  isUnitVisible(state: GameState, viewer: PlayerId, unit: Unit, seen?: Set<number>): boolean;
  visibleUnits(state: GameState, viewer: PlayerId): Unit[];
}

export class DefaultTacticalSpace implements TacticalSpace {
  constructor(readonly content: ContentCatalog) {}

  moveField(state: GameState, unit: Unit): MoveField {
    return computeMoveField(state, unit, this.content);
  }

  pathTo(field: MoveField, state: GameState, destination: Coord): Coord[] | null {
    return pathTo(field, state.map, destination);
  }

  threatOf(state: GameState, unit: Unit, field?: MoveField): Set<number> {
    return threatTiles(state, unit, this.content, field);
  }

  attackTargets(state: GameState, unit: Unit, from: Coord, weapon: WeaponDef): Coord[] {
    return attackTargetCoords(state, unit, from, weapon, this.content);
  }

  healTargets(state: GameState, unit: Unit, from: Coord): Unit[] {
    return healTargetsFrom(state, unit, from, this.content);
  }

  visibleTiles(state: GameState, viewer: PlayerId): Set<number> {
    return visibleTiles(state, viewer, this.content);
  }

  isUnitVisible(state: GameState, viewer: PlayerId, unit: Unit, seen?: Set<number>): boolean {
    return isUnitVisible(state, viewer, unit, seen, this.content);
  }

  visibleUnits(state: GameState, viewer: PlayerId): Unit[] {
    return visibleUnits(state, viewer, this.content);
  }
}

