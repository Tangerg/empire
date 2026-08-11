import { Terrains } from '../core/data/terrain';
import { UnitTypes } from '../core/data/units';
import { idx } from '../core/grid';
import { mapFromLevel, terrainRows } from '../core/mapio';
import type {
  Coord,
  Direction,
  GameMap,
  LevelData,
  LevelUnit,
  Objective,
  PlayerConfig,
  RuleSet,
  TerrainId,
  CoverLevel,
  UnitTypeId,
} from '../core/types';

const MIN_MAP_SIZE = 4;
const MAX_MAP_SIZE = 64;

const clampMapSize = (value: number) =>
  Math.max(MIN_MAP_SIZE, Math.min(MAX_MAP_SIZE, Math.round(value)));

const sameCoord = (left: Coord, right: Coord) => left.x === right.x && left.y === right.y;

interface SerializedEditorDocument {
  id: string;
  name: string;
  author: string;
  description: string;
  map: GameMap;
  units: LevelUnit[];
  players: PlayerConfig[];
  rules: Partial<RuleSet>;
  victory: Objective[];
}

/**
 * Rich editor aggregate. It keeps map invariants and serialisation out of the
 * DOM controller, while deliberately exposing form-oriented document fields.
 */
export class EditorDocument {
  constructor(
    public id: string,
    public name: string,
    public author: string,
    public description: string,
    public map: GameMap,
    public units: LevelUnit[],
    public players: PlayerConfig[],
    public rules: Partial<RuleSet>,
    public victory: Objective[],
  ) {}

  static fromLevel(level: LevelData): EditorDocument {
    return new EditorDocument(
      level.id,
      level.name,
      level.author ?? '',
      level.description ?? '',
      mapFromLevel(level),
      level.units.map((unit) => ({ ...unit })),
      level.players.map((player) => ({
        ...player,
        resources: Object.fromEntries(
          Object.entries(player.resources).map(([resource, account]) => [resource, { ...account }]),
        ),
        ai: { ...(player.ai ?? { aggression: 0.5 }) },
      })),
      { ...level.rules },
      level.victory.map((objective) => ({ ...objective })),
    );
  }

  static deserialize(serialized: string): EditorDocument {
    const value = JSON.parse(serialized) as SerializedEditorDocument;
    return new EditorDocument(
      value.id,
      value.name,
      value.author,
      value.description,
      value.map,
      value.units,
      value.players,
      value.rules,
      value.victory,
    );
  }

  serialize(): string {
    return JSON.stringify(this);
  }

  inBounds(at: Coord): boolean {
    return at.x >= 0 && at.y >= 0 && at.x < this.map.width && at.y < this.map.height;
  }

  setTerrain(at: Coord, terrain: TerrainId): void {
    const index = this.indexAt(at);
    if (this.map.tiles[index] === terrain) return;
    this.map.tiles[index] = terrain;
    if (!Terrains.get(terrain).capturable) this.map.owners[index] = 0;
  }

  floodFill(from: Coord, terrain: TerrainId): void {
    const target = this.map.tiles[this.indexAt(from)];
    if (target === terrain) return;
    const queue: Coord[] = [from];
    const seen = new Set<number>([idx(this.map, from.x, from.y)]);
    const neighbours = [
      { x: 1, y: 0 },
      { x: -1, y: 0 },
      { x: 0, y: 1 },
      { x: 0, y: -1 },
    ] as const;

    while (queue.length > 0) {
      const current = queue.pop()!;
      this.setTerrain(current, terrain);
      for (const delta of neighbours) {
        const next = { x: current.x + delta.x, y: current.y + delta.y };
        if (!this.inBounds(next)) continue;
        const index = idx(this.map, next.x, next.y);
        if (seen.has(index) || this.map.tiles[index] !== target) continue;
        seen.add(index);
        queue.push(next);
      }
    }
  }

  setElevation(at: Coord, elevation: number): void {
    if (!Number.isFinite(elevation)) throw new Error('elevation must be finite');
    this.map.elevation[this.indexAt(at)] = Math.round(elevation);
  }

  placeUnit(at: Coord, unit: UnitTypeId, owner: number): void {
    this.indexAt(at);
    UnitTypes.get(unit);
    this.requireOwner(owner);
    this.removeUnitAt(at);
    this.units.push({ x: at.x, y: at.y, unit, owner });
  }

  removeUnitAt(at: Coord): void {
    this.units = this.units.filter((unit) => unit.x !== at.x || unit.y !== at.y);
  }

  setOwner(at: Coord, owner: number): void {
    const index = this.indexAt(at);
    this.requireOwner(owner);
    if (Terrains.get(this.map.tiles[index]).capturable) this.map.owners[index] = owner;
  }

  toggleCliff(from: Coord, to: Coord): void {
    this.indexAt(from);
    this.indexAt(to);
    if (Math.abs(from.x - to.x) + Math.abs(from.y - to.y) !== 1) {
      throw new Error('a cliff must connect orthogonally adjacent cells');
    }
    const index = this.map.cliffs.findIndex((edge) =>
      (sameCoord(edge.from, from) && sameCoord(edge.to, to)) ||
      (sameCoord(edge.from, to) && sameCoord(edge.to, from)),
    );
    if (index >= 0) this.map.cliffs.splice(index, 1);
    else this.map.cliffs.push({ from: { ...from }, to: { ...to } });
  }

  setDirectionalCover(
    at: Coord,
    side: Direction,
    level: Exclude<CoverLevel, 'none'> | null,
  ): void {
    this.indexAt(at);
    let entry = this.map.directionalCover.find((cover) => sameCoord(cover.at, at));
    if (!entry && level) {
      entry = { at: { ...at }, sides: {} };
      this.map.directionalCover.push(entry);
    }
    if (!entry) return;
    if (level) entry.sides[side] = level;
    else delete entry.sides[side];
    if (Object.keys(entry.sides).length === 0) {
      this.map.directionalCover = this.map.directionalCover.filter((cover) => cover !== entry);
    }
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
      terrain: terrainRows(this.map),
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
    };
  }

  private owners(): { x: number; y: number; owner: number }[] {
    return this.map.owners.flatMap((owner, index) =>
      Terrains.get(this.map.tiles[index]).capturable
        ? [{ x: index % this.map.width, y: Math.floor(index / this.map.width), owner }]
        : [],
    );
  }

  private indexAt(at: Coord): number {
    if (!this.inBounds(at)) throw new RangeError(`cell (${at.x}, ${at.y}) is outside the map`);
    return idx(this.map, at.x, at.y);
  }

  private requireOwner(owner: number): void {
    if (owner !== 0 && !this.players.some((player) => player.id === owner)) {
      throw new Error(`unknown player ${owner}`);
    }
  }
}
