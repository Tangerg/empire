import { idx, inBounds } from '../grid';
import { activeGrid, type GridRules, type TacticalGrid } from '../tactical-grid';
import type { Coord, Direction, GameMap, GameState } from '../types';

/**
 * One battlefield read under one tiling.
 *
 * The pair travels together everywhere: a distance means nothing without the
 * tiling, and a tiling clips against nothing without the board's extent. Rules
 * used to import free functions that silently assumed both — `dist` was
 * Manhattan and `ring` was a diamond, in fifteen modules that never said so.
 *
 * Cheap and disposable: it holds no caches and copies nothing, so a rule builds
 * one per query the way it already built a `Battlefield`.
 */
export class Board {
  constructor(
    readonly map: GameMap,
    readonly grid: TacticalGrid,
  ) {}

  get width(): number {
    return this.map.width;
  }

  get height(): number {
    return this.map.height;
  }

  contains(at: Coord): boolean {
    return inBounds(this.map, at.x, at.y);
  }

  indexOf(at: Coord): number {
    return idx(this.map, at.x, at.y);
  }

  coordOf(index: number): Coord {
    return { x: index % this.map.width, y: Math.floor(index / this.map.width) };
  }

  distance(a: Coord, b: Coord): number {
    return this.grid.distance(a, b);
  }

  /** Distance to the closest of several places — not the place itself. */
  nearestDistance(from: Coord, places: readonly Coord[]): number {
    let best = Infinity;
    for (const place of places) best = Math.min(best, this.distance(from, place));
    return best;
  }

  /** One step in a direction, or null when that step leaves the board. */
  step(at: Coord, direction: Direction): Coord | null {
    const next = this.grid.step(at, direction);
    return this.contains(next) ? next : null;
  }

  /** Cells one step away, on the board, in the tiling's own order. */
  neighbours(at: Coord): Coord[] {
    return this.grid.adjacent(at).filter((cell) => this.contains(cell));
  }

  /** Cells within `[min, max]` steps, on the board. */
  ring(at: Coord, min: number, max: number): Coord[] {
    return this.grid.within(at, min, max).filter((cell) => this.contains(cell));
  }

  /** Cells a sight ray crosses, endpoints included. */
  line(from: Coord, to: Coord): Coord[] {
    return this.grid.line(from, to);
  }

  /** Tile indices of a ring, for the callers that work in flat indices. */
  ringIndices(at: Coord, min: number, max: number): number[] {
    return this.ring(at, min, max).map((cell) => this.indexOf(cell));
  }
}

/** The board this battle is being fought on, under the tiling its level named. */
export const boardOf = (rules: GridRules, state: GameState): Board =>
  new Board(state.map, activeGrid(rules, state));
