import { edgeKey, idx, inBounds, sharesEdge } from '../grid';
import type {
  Coord,
  CoverLevel,
  Direction,
  GameMap,
  LevelCliffEdge,
  LevelDirectionalCover,
  TerrainId,
} from '../types';

/** One height change, in the two terms an announcement needs. */
export interface ElevationChange {
  readonly from: number;
  readonly to: number;
}

/** A cover strength worth recording; `none` is the absence of an entry. */
export type DirectionalCoverLevel = Exclude<CoverLevel, 'none'>;
/** Named once here, so the read model and the writer describe the same shape. */
export type DirectionalCoverSides = LevelDirectionalCover['sides'];

/**
 * Write access to the spatial layers of one map: ground, height, blocked edges
 * and directional cover.
 *
 * `Battlefield` is the read model — it indexes these layers and caches, so it
 * must not also mutate them. Writing had no owner at all: a scenario effect and
 * the editor document each reached into `tiles`, `elevation`, `cliffs` and
 * `directionalCover` with their own index arithmetic, their own edge matching
 * (one by `edgeKey`, one by comparing both orientations by hand) and their own
 * idea of when a cover entry should exist. Every method here reports what
 * actually changed, so a runtime caller can announce it and an editor can leave
 * it alone; deciding *whether* something changed is not a judgement two callers
 * should make separately.
 */
export class MapLayers {
  constructor(readonly map: GameMap) {}

  contains(at: Coord): boolean {
    return inBounds(this.map, at.x, at.y);
  }

  /** The ground that was there, or null when it was already this terrain. */
  changeTerrain(at: Coord, terrain: TerrainId): TerrainId | null {
    const index = this.indexOf(at);
    const from = this.map.tiles[index];
    if (from === terrain) return null;
    this.map.tiles[index] = terrain;
    return from;
  }

  terrainAt(at: Coord): TerrainId {
    return this.map.tiles[this.indexOf(at)];
  }

  elevationAt(at: Coord): number {
    return this.map.elevation[this.indexOf(at)] ?? 0;
  }

  /** The step taken, or null when the cell already stood that high. */
  changeElevation(at: Coord, value: number): ElevationChange | null {
    const index = this.indexOf(at);
    const from = this.map.elevation[index] ?? 0;
    const to = Math.round(value);
    if (from === to) return null;
    this.map.elevation[index] = to;
    return { from, to };
  }

  raiseElevation(at: Coord, amount: number): ElevationChange | null {
    return this.changeElevation(at, this.elevationAt(at) + Math.round(amount));
  }

  owner(at: Coord): number {
    return this.map.owners[this.indexOf(at)];
  }

  changeOwner(at: Coord, owner: number): void {
    this.map.owners[this.indexOf(at)] = owner;
  }

  captureProgressAt(at: Coord): number {
    return this.map.captureProgress[this.indexOf(at)] ?? 0;
  }

  changeCaptureProgress(at: Coord, value: number): void {
    this.map.captureProgress[this.indexOf(at)] = value;
  }

  /* -------------------------------------------------------------- cliff edges */

  /** Only orthogonal neighbours share an edge, so only they can be cut. */
  isEdge(from: Coord, to: Coord): boolean {
    return this.contains(from) && this.contains(to) && sharesEdge(from, to);
  }

  isBlockedEdge(from: Coord, to: Coord): boolean {
    return this.cliffIndex(from, to) >= 0;
  }

  /** Blocks or clears one edge; false when it was already in that state. */
  blockEdge(from: Coord, to: Coord, blocked: boolean): boolean {
    this.requireEdge(from, to);
    const index = this.cliffIndex(from, to);
    if (blocked === (index >= 0)) return false;
    if (blocked) this.map.cliffs.push({ from: { ...from }, to: { ...to } });
    else this.map.cliffs.splice(index, 1);
    return true;
  }

  /* ---------------------------------------------------------------- cover */

  coverAt(at: Coord): DirectionalCoverSides {
    return this.coverEntry(at)?.sides ?? {};
  }

  /** Replaces every side of one cell at once. */
  changeCover(at: Coord, sides: DirectionalCoverSides): void {
    this.indexOf(at);
    const entry = this.coverEntry(at);
    if (entry) entry.sides = { ...sides };
    else this.map.directionalCover.push({ at: { ...at }, sides: { ...sides } });
    this.pruneCover();
  }

  /** Raises or clears one side of one cell. */
  changeCoverSide(at: Coord, side: Direction, level: DirectionalCoverLevel | null): void {
    const sides = { ...this.coverAt(at) };
    if (level) sides[side] = level;
    else delete sides[side];
    this.changeCover(at, sides);
  }

  private coverEntry(at: Coord): { at: Coord; sides: DirectionalCoverSides } | undefined {
    return this.map.directionalCover.find((cover) => cover.at.x === at.x && cover.at.y === at.y);
  }

  /**
   * A cell with no raised side has no cover entry.
   *
   * The two writers disagreed about this: the editor dropped an emptied entry,
   * the scenario effect left it behind. Reads treat the two the same, so the
   * difference only ever showed up in a serialised level as dead data.
   */
  private pruneCover(): void {
    this.map.directionalCover = this.map.directionalCover.filter(
      (cover) => Object.keys(cover.sides).length > 0,
    );
  }

  private cliffIndex(from: Coord, to: Coord): number {
    const key = edgeKey(from, to);
    return this.map.cliffs.findIndex((edge: LevelCliffEdge) => edgeKey(edge.from, edge.to) === key);
  }

  private requireEdge(from: Coord, to: Coord): void {
    if (!this.isEdge(from, to)) {
      throw new RangeError(
        `not an orthogonal map edge: ${from.x},${from.y} -> ${to.x},${to.y}`,
      );
    }
  }

  private indexOf(at: Coord): number {
    if (!this.contains(at)) throw new RangeError(`cell (${at.x}, ${at.y}) is outside the map`);
    return idx(this.map, at.x, at.y);
  }
}
