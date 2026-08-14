import { type ContentCatalog } from '../content-pack';
import { edgeKey, idx, inBounds } from '../grid';
import { directionToward } from '../spatial';
import type { DirectionalCoverSides } from './map-layers';
import type {
  CoverLevel,
  Coord,
  GameState,
  MovementClass,
  StructureState,
  TerrainDef,
  TerrainOverlayDef,
  TerrainOverlayState,
  Unit,
} from '../types';

/**
 * Read-only tactical cell assembled from independent layers:
 * base terrain, dynamic landform/environment overlays, ownership, and structure.
 * Rules query this model instead of knowing where each layer is stored.
 */
export class BattlefieldCell {
  readonly index: number;

  constructor(
    private readonly battlefield: Battlefield,
    readonly at: Coord,
  ) {
    this.index = idx(battlefield.state.map, at.x, at.y);
  }

  get terrain(): TerrainDef {
    return this.battlefield.content.terrains.get(this.battlefield.state.map.tiles[this.index]);
  }

  get elevation(): number {
    return this.battlefield.state.map.elevation[this.index] ?? 0;
  }

  get overlayStates(): TerrainOverlayState[] {
    return this.battlefield.overlayStatesAt(this.index);
  }

  get overlays(): TerrainOverlayDef[] {
    return this.overlayStates.map((overlay) => this.battlefield.content.terrainOverlays.get(overlay.type));
  }

  get structure(): StructureState | undefined {
    return this.battlefield.structureAt(this.index);
  }

  movementCost(movementClass: MovementClass): number | null {
    const base = this.terrain.cost[movementClass];
    if (base == null) return null;
    const profile = this.battlefield.content.movementProfiles.get(movementClass);
    let delta = 0;
    for (const overlay of this.overlays) {
      if (
        overlay.blockedMovementClasses.includes(movementClass) ||
        overlay.blockedMovementClasses.some((entry) => profile.tags.includes(entry))
      ) {
        return null;
      }
      delta += overlay.movementCostDelta;
    }
    return Math.max(1, base + delta);
  }

  /** Highest opaque volume on this cell, measured from global elevation zero. */
  get obstructionTop(): number {
    const structureHeight = this.structure
      ? this.battlefield.content.structures.get(this.structure.type).obstructionHeight
      : 0;
    return this.elevation + Math.max(this.terrain.obstructionHeight, structureHeight);
  }

  get cover(): CoverLevel {
    const structureCover = this.structure ? this.battlefield.content.structures.get(this.structure.type).cover : 'none';
    const score = (level: CoverLevel) => level === 'full' ? 2 : level === 'half' ? 1 : 0;
    return score(structureCover) > score(this.terrain.cover) ? structureCover : this.terrain.cover;
  }

  directionalCoverFrom(attackerAt: Coord): CoverLevel {
    const incoming = directionToward(this.at, attackerAt);
    return this.battlefield.directionalCoverAt(this.index)[incoming] ?? 'none';
  }

  get defense(): number {
    return Math.max(0, this.terrain.defense + this.overlayDefense);
  }

  get vision(): number {
    return this.terrain.vision + this.overlayVision;
  }

  get heal(): number {
    return Math.max(0, this.terrain.heal + this.overlayHeal);
  }

  get overlayDefense(): number {
    return this.overlays.reduce((sum, overlay) => sum + overlay.defenseDelta, 0);
  }

  get overlayVision(): number {
    return this.overlays.reduce((sum, overlay) => sum + overlay.visionDelta, 0);
  }

  get overlayHeal(): number {
    return this.overlays.reduce((sum, overlay) => sum + overlay.healDelta, 0);
  }

  get blocksMovement(): boolean {
    return this.structure ? this.battlefield.content.structures.get(this.structure.type).blocksMovement : false;
  }

  /** The unit standing here, if any. */
  get occupant(): Unit | undefined {
    return this.battlefield.occupantAt(this.at);
  }

  /**
   * May a unit of this movement class stand on this tile?
   *
   * Ground it cannot cross and a structure that fills the tile are two separate
   * layers, and both have to say yes. Every rule that puts a unit somewhere —
   * deployment, disembarking, a shove, a teleport, a scenario spawn, a rescue
   * from a corpse marker — asked the two layers by hand, so the rule existed in
   * six copies, each of them one edit away from remembering only half of it.
   */
  admits(movementClass: MovementClass): boolean {
    return !this.blocksMovement && this.movementCost(movementClass) !== null;
  }

  /** Admits such a unit *and* has nobody standing on it: a placeable tile. */
  canReceive(movementClass: MovementClass): boolean {
    return !this.occupant && this.admits(movementClass);
  }

  get blocksVision(): boolean {
    return this.terrain.opaque || Boolean(
      this.structure && this.battlefield.content.structures.get(this.structure.type).blocksVision,
    );
  }
}

/**
 * Aggregate read model for every rule that projects the battle map.
 *
 * The serialisable state deliberately remains arrays. This short-lived view
 * indexes sparse spatial layers once per rule query, avoiding repeated linear
 * scans without introducing mutation-sensitive caches into GameState.
 */
export class Battlefield {
  private readonly cells: Array<BattlefieldCell | undefined>;
  private overlayStatesByCell: Map<number, TerrainOverlayState[]> | null = null;
  private structuresByCell: Map<number, StructureState> | null = null;
  private directionalCoverByCell: Map<number, DirectionalCoverSides> | null = null;
  private cliffEdges: Set<string> | null = null;
  private overlayLookups = 0;
  private structureLookups = 0;
  private directionalCoverLookups = 0;
  private cliffLookups = 0;

  constructor(
    readonly state: GameState,
    readonly content: ContentCatalog,
  ) {
    this.cells = new Array(state.map.tiles.length);
  }

  contains(at: Coord): boolean {
    return inBounds(this.state.map, at.x, at.y);
  }

  cell(at: Coord): BattlefieldCell {
    if (!this.contains(at)) throw new RangeError(`battlefield cell out of bounds: ${at.x},${at.y}`);
    const index = idx(this.state.map, at.x, at.y);
    const cached = this.cells[index];
    if (cached) return cached;
    const cell = new BattlefieldCell(this, { x: at.x, y: at.y });
    this.cells[index] = cell;
    return cell;
  }

  cellAt(x: number, y: number): BattlefieldCell {
    return this.cell({ x, y });
  }

  isCliff(from: Coord, to: Coord): boolean {
    if (!this.cliffEdges) {
      if (++this.cliffLookups < 3) {
        const key = edgeKey(from, to);
        return this.state.map.cliffs.some((edge) => edgeKey(edge.from, edge.to) === key);
      }
      this.cliffEdges = new Set(this.state.map.cliffs.map((edge) => edgeKey(edge.from, edge.to)));
    }
    return this.cliffEdges.has(edgeKey(from, to));
  }

  overlayStatesAt(index: number): TerrainOverlayState[] {
    if (!this.overlayStatesByCell) {
      if (++this.overlayLookups < 3) {
        const x = index % this.state.map.width;
        const y = Math.floor(index / this.state.map.width);
        return this.state.scenario.overlays.filter((overlay) =>
          overlay.cells.some((cell) => cell.x === x && cell.y === y),
        );
      }
      this.overlayStatesByCell = new Map();
      for (const overlay of this.state.scenario.overlays) {
        for (const cell of overlay.cells) {
          if (!this.contains(cell)) continue;
          const cellIndex = idx(this.state.map, cell.x, cell.y);
          const entries = this.overlayStatesByCell.get(cellIndex);
          if (entries) entries.push(overlay);
          else this.overlayStatesByCell.set(cellIndex, [overlay]);
        }
      }
    }
    return this.overlayStatesByCell.get(index) ?? [];
  }

  /**
   * Deliberately not indexed like the layers above: rules move, spawn and
   * remove units while they hold a battlefield, so a cached answer would be
   * wrong by the next line.
   */
  occupantAt(at: Coord): Unit | undefined {
    return this.state.units.find((unit) => unit.x === at.x && unit.y === at.y);
  }

  structureAt(index: number): StructureState | undefined {
    if (!this.structuresByCell) {
      if (++this.structureLookups < 3) {
        const x = index % this.state.map.width;
        const y = Math.floor(index / this.state.map.width);
        return this.state.structures.find((structure) =>
          structure.hp > 0 && structure.x === x && structure.y === y,
        );
      }
      this.structuresByCell = new Map();
      for (const structure of this.state.structures) {
        if (structure.hp > 0 && this.contains(structure)) {
          this.structuresByCell.set(idx(this.state.map, structure.x, structure.y), structure);
        }
      }
    }
    return this.structuresByCell.get(index);
  }

  directionalCoverAt(index: number): DirectionalCoverSides {
    if (!this.directionalCoverByCell) {
      if (++this.directionalCoverLookups < 3) {
        const x = index % this.state.map.width;
        const y = Math.floor(index / this.state.map.width);
        return this.state.map.directionalCover.find((cover) =>
          cover.at.x === x && cover.at.y === y,
        )?.sides ?? {};
      }
      this.directionalCoverByCell = new Map();
      for (const cover of this.state.map.directionalCover) {
        if (this.contains(cover.at)) {
          this.directionalCoverByCell.set(idx(this.state.map, cover.at.x, cover.at.y), cover.sides);
        }
      }
    }
    return this.directionalCoverByCell.get(index) ?? {};
  }

  /**
   * Terrain, structure, elevation and explicit edge policy for one orthogonal
   * movement step.
   *
   * A tile a structure fills is not a tile with an expensive step — it is a
   * step that does not exist. This used to answer only for the ground, so both
   * callers had to remember the other half by hand, right next to the call.
   */
  traversalCost(from: Coord, to: Coord, movementClass: MovementClass): number | null {
    const profile = this.content.movementProfiles.get(movementClass);
    const destination = this.cell(to);
    const base = destination.movementCost(movementClass);
    if (base === null || destination.blocksMovement) return null;
    if (this.isCliff(from, to) && !profile.ignoresCliffs) return null;
    const delta = destination.elevation - this.cell(from).elevation;
    if (profile.maxClimb !== null && delta > profile.maxClimb) return null;
    if (profile.maxDrop !== null && -delta > profile.maxDrop) return null;
    return base + Math.max(0, delta) * profile.uphillCostPerLevel;
  }
}
