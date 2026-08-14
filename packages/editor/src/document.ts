import { idx } from '@empire/battle-engine/grid';
import { MapLayers, type DirectionalCoverLevel } from '@empire/battle-engine/domain/map-layers';
import { mapFromLevel, terrainRows } from '@empire/battle-engine/level';
import type { ContentCatalog } from '@empire/battle-engine/content-pack';
import type {
  LevelCommander,
  LevelComposite,
  LevelDeployment,
  LevelScenario,
  LevelStructure,
  Coord,
  Direction,
  GameMap,
  LevelData,
  LevelUnit,
  Objective,
  PlayerConfig,
  RuleSet,
  TerrainId,
  UnitTypeId,
} from '@empire/battle-engine/types';

const MIN_MAP_SIZE = 4;
const MAX_MAP_SIZE = 64;

const clampMapSize = (value: number) =>
  Math.max(MIN_MAP_SIZE, Math.min(MAX_MAP_SIZE, Math.round(value)));

/**
 * Level sections the editor has no dedicated tooling for yet.
 *
 * They are carried verbatim rather than dropped: an author who opens a scripted
 * campaign level, paints one tile and saves must not silently lose its
 * triggers, structures, commanders or deployment zones. A round-trip test keeps
 * this list honest whenever LevelData grows a field.
 */
export interface PreservedLevelSections {
  commanders: LevelCommander[];
  structures: LevelStructure[];
  composites: LevelComposite[];
  scenario?: LevelScenario;
  deployment?: LevelDeployment;
  extra?: Record<string, unknown>;
}

export interface EditorDocumentFields {
  id: string;
  name: string;
  author: string;
  description: string;
  map: GameMap;
  units: LevelUnit[];
  players: PlayerConfig[];
  rules: Partial<RuleSet>;
  victory: Objective[];
  preserved: PreservedLevelSections;
}

const clonePreserved = (source: PreservedLevelSections): PreservedLevelSections =>
  structuredClone(source);

const preservedFrom = (level: LevelData): PreservedLevelSections => ({
  commanders: structuredClone(level.commanders ?? []),
  structures: structuredClone(level.structures ?? []),
  composites: structuredClone(level.composites ?? []),
  ...(level.scenario === undefined ? {} : { scenario: structuredClone(level.scenario) }),
  ...(level.deployment === undefined ? {} : { deployment: structuredClone(level.deployment) }),
  ...(level.extra === undefined ? {} : { extra: structuredClone(level.extra) }),
});

/**
 * Rich editor aggregate. It keeps map invariants and serialisation out of the
 * DOM controller, while deliberately exposing form-oriented document fields.
 */
export class EditorDocument {
  id: string;
  name: string;
  author: string;
  description: string;
  map: GameMap;
  units: LevelUnit[];
  players: PlayerConfig[];
  rules: Partial<RuleSet>;
  victory: Objective[];
  /** Sections the editor preserves but does not structurally edit yet. */
  preserved: PreservedLevelSections;
  private layerCache: MapLayers | null = null;

  constructor(
    /** Catalog this document is authored against; never an ambient default. */
    readonly content: ContentCatalog,
    fields: EditorDocumentFields,
  ) {
    this.id = fields.id;
    this.name = fields.name;
    this.author = fields.author;
    this.description = fields.description;
    this.map = fields.map;
    this.units = fields.units;
    this.players = fields.players;
    this.rules = fields.rules;
    this.victory = fields.victory;
    this.preserved = fields.preserved;
  }

  static fromLevel(content: ContentCatalog, level: LevelData): EditorDocument {
    return new EditorDocument(content, {
      id: level.id,
      name: level.name,
      author: level.author ?? '',
      description: level.description ?? '',
      map: mapFromLevel(content, level),
      units: level.units.map((unit) => ({ ...unit })),
      players: level.players.map((player) => ({
        ...player,
        resources: Object.fromEntries(
          Object.entries(player.resources).map(([resource, account]) => [resource, { ...account }]),
        ),
        ai: { ...(player.ai ?? { aggression: 0.5 }) },
      })),
      rules: { ...level.rules },
      victory: level.victory.map((objective) => ({ ...objective })),
      preserved: preservedFrom(level),
    });
  }

  static deserialize(content: ContentCatalog, serialized: string): EditorDocument {
    const value = JSON.parse(serialized) as EditorDocumentFields;
    return new EditorDocument(content, {
      ...value,
      preserved: clonePreserved(value.preserved ?? {
        commanders: [],
        structures: [],
        composites: [],
      }),
    });
  }

  /**
   * Snapshot for the undo stack. Explicitly field-by-field: `JSON.stringify(this)`
   * would drag the whole content catalog into every undo step.
   */
  serialize(): string {
    const fields: EditorDocumentFields = {
      id: this.id,
      name: this.name,
      author: this.author,
      description: this.description,
      map: this.map,
      units: this.units,
      players: this.players,
      rules: this.rules,
      victory: this.victory,
      preserved: this.preserved,
    };
    return JSON.stringify(fields);
  }

  inBounds(at: Coord): boolean {
    return this.layers.contains(at);
  }

  /** The one writer of the map's spatial layers; the editor adds no second one. */
  private get layers(): MapLayers {
    if (!this.layerCache || this.layerCache.map !== this.map) this.layerCache = new MapLayers(this.map);
    return this.layerCache;
  }

  setTerrain(at: Coord, terrain: TerrainId): void {
    if (this.layers.changeTerrain(at, terrain) === null) return;
    if (!this.content.terrains.get(terrain).capturable) this.layers.changeOwner(at, 0);
  }

  floodFill(from: Coord, terrain: TerrainId): void {
    const target = this.layers.terrainAt(from);
    if (target === terrain) return;
    const queue: Coord[] = [from];
    const seen = new Set<number>([idx(this.map, from.x, from.y)]);
    const neighbours = [
      { x: 1, y: 0 },
      { x: -1, y: 0 },
      { x: 0, y: 1 },
      { x: 0, y: -1 },
    ] as const;

    for (let current = queue.pop(); current; current = queue.pop()) {
      this.setTerrain(current, terrain);
      for (const delta of neighbours) {
        const next = { x: current.x + delta.x, y: current.y + delta.y };
        if (!this.inBounds(next)) continue;
        const index = idx(this.map, next.x, next.y);
        if (seen.has(index) || this.layers.terrainAt(next) !== target) continue;
        seen.add(index);
        queue.push(next);
      }
    }
  }

  setElevation(at: Coord, elevation: number): void {
    if (!Number.isFinite(elevation)) throw new Error('elevation must be finite');
    this.layers.changeElevation(at, elevation);
  }

  placeUnit(at: Coord, unit: UnitTypeId, owner: number): void {
    this.requireInBounds(at);
    this.content.units.get(unit);
    this.requireOwner(owner);
    this.removeUnitAt(at);
    this.units.push({ x: at.x, y: at.y, unit, owner });
  }

  /**
   * Forgets a side completely: its roster and everything it held.
   *
   * The controller used to walk the ownership layer itself, which is the one
   * thing the document exists to keep consistent.
   */
  removePlayer(id: number): void {
    this.players = this.players.filter((player) => player.id !== id);
    this.units = this.units.filter((unit) => unit.owner !== id);
    for (let y = 0; y < this.map.height; y++) {
      for (let x = 0; x < this.map.width; x++) {
        if (this.layers.owner({ x, y }) === id) this.layers.changeOwner({ x, y }, 0);
      }
    }
  }

  removeUnitAt(at: Coord): void {
    this.units = this.units.filter((unit) => unit.x !== at.x || unit.y !== at.y);
  }

  setOwner(at: Coord, owner: number): void {
    this.requireOwner(owner);
    if (this.content.terrains.get(this.layers.terrainAt(at)).capturable) {
      this.layers.changeOwner(at, owner);
    }
  }

  /** An author cuts and heals the same edge with the same gesture. */
  toggleCliff(from: Coord, to: Coord): void {
    if (!this.layers.isEdge(from, to)) {
      throw new Error('a cliff must connect orthogonally adjacent cells');
    }
    this.layers.blockEdge(from, to, !this.layers.isBlockedEdge(from, to));
  }

  setDirectionalCover(at: Coord, side: Direction, level: DirectionalCoverLevel | null): void {
    this.layers.changeCoverSide(at, side, level);
  }

  resize(requestedWidth: number, requestedHeight: number): boolean {
    const width = clampMapSize(requestedWidth);
    const height = clampMapSize(requestedHeight);
    const old = this.map;
    if (width === old.width && height === old.height) return false;

    const tiles: TerrainId[] = new Array(width * height).fill('plain');
    const owners: number[] = new Array(width * height).fill(0);
    const elevation: number[] = new Array(width * height).fill(0);
    for (let y = 0; y < Math.min(height, old.height); y++) {
      for (let x = 0; x < Math.min(width, old.width); x++) {
        tiles[y * width + x] = old.tiles[y * old.width + x];
        owners[y * width + x] = old.owners[y * old.width + x];
        elevation[y * width + x] = old.elevation[y * old.width + x];
      }
    }
    this.map = {
      width,
      height,
      tiles,
      owners,
      captureProgress: new Array(width * height).fill(0),
      elevation,
      cliffs: old.cliffs.filter((edge) =>
        edge.from.x < width && edge.from.y < height && edge.to.x < width && edge.to.y < height),
      directionalCover: old.directionalCover.filter((cover) =>
        cover.at.x < width && cover.at.y < height),
    };
    this.units = this.units.filter((unit) => unit.x < width && unit.y < height);
    return true;
  }

  toLevel(): LevelData {
    return {
      schema: 2,
      id: this.id,
      name: this.name,
      author: this.author,
      description: this.description,
      width: this.map.width,
      height: this.map.height,
      terrain: terrainRows(this.content, this.map),
      elevation: this.map.elevation.slice(),
      cliffs: this.map.cliffs.map((edge) => ({ from: { ...edge.from }, to: { ...edge.to } })),
      directionalCover: this.map.directionalCover.map((cover) => ({
        at: { ...cover.at },
        sides: { ...cover.sides },
      })),
      owners: this.owners(),
      units: this.units.map((unit) => ({ ...unit })),
      players: this.players.map((player) => ({
        ...player,
        resources: Object.fromEntries(
          Object.entries(player.resources).map(([resource, account]) => [resource, { ...account }]),
        ),
      })),
      rules: { ...this.rules },
      victory: this.victory.map((objective) => ({ ...objective })),
      commanders: structuredClone(this.preserved.commanders),
      structures: structuredClone(this.preserved.structures),
      composites: structuredClone(this.preserved.composites),
      ...(this.preserved.scenario === undefined
        ? {} : { scenario: structuredClone(this.preserved.scenario) }),
      ...(this.preserved.deployment === undefined
        ? {} : { deployment: structuredClone(this.preserved.deployment) }),
      ...(this.preserved.extra === undefined
        ? {} : { extra: structuredClone(this.preserved.extra) }),
    };
  }

  private owners(): { x: number; y: number; owner: number }[] {
    return this.map.owners.flatMap((owner, index) =>
      this.content.terrains.get(this.map.tiles[index]).capturable
        ? [{ x: index % this.map.width, y: Math.floor(index / this.map.width), owner }]
        : [],
    );
  }

  private requireInBounds(at: Coord): void {
    if (!this.inBounds(at)) throw new RangeError(`cell (${at.x}, ${at.y}) is outside the map`);
  }

  private requireOwner(owner: number): void {
    if (owner !== 0 && !this.players.some((player) => player.id === owner)) {
      throw new Error(`unknown player ${owner}`);
    }
  }
}
