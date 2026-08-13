import { idx } from '../grid';
import type { GameMap, LevelData, TerrainId } from '../types';
import { type ContentCatalog } from '../content-pack';
import { LevelFormatError } from './schema';

/**
 * Level files store terrain as rows of single characters so a map diff is
 * readable in git and hand-editable in a pinch. The editor reads and writes
 * exactly this format.
 */
export const terrainCharacter = (
  content: ContentCatalog,
  terrain: TerrainId,
): string | undefined => content.terrainEncoding.character(terrain);

export const terrainForCharacter = (
  content: ContentCatalog,
  character: string,
): TerrainId | undefined => content.terrainEncoding.terrain(character);

function serializedTerrainCharacter(content: ContentCatalog, terrain: TerrainId): string {
  const exact = terrainCharacter(content, terrain);
  if (exact !== undefined) return exact;
  const fallback = terrainCharacter(content, content.terrainEncoding.defaultTerrain);
  if (fallback !== undefined) return fallback;
  throw new LevelFormatError(`terrain "${terrain}" has no serialized character`);
}

/* --------------------------------------------------------------- deserialize */

export function mapFromLevel(level: LevelData, content: ContentCatalog): GameMap {
  const { width, height } = level;
  if (!Number.isInteger(width) || !Number.isInteger(height) || width < 1 || height < 1) {
    throw new LevelFormatError(`bad map size ${width}x${height}`);
  }
  if (level.terrain.length !== height) {
    throw new LevelFormatError(`terrain has ${level.terrain.length} rows, expected ${height}`);
  }

  const tiles: TerrainId[] = new Array(width * height).fill(content.terrainEncoding.defaultTerrain);
  level.terrain.forEach((row, y) => {
    if (row.length !== width) {
      throw new LevelFormatError(`terrain row ${y} has ${row.length} chars, expected ${width}`);
    }
    for (let x = 0; x < width; x++) {
      const character = row[x];
      const terrain = content.terrainEncoding.terrain(character);
      if (!terrain) throw new LevelFormatError(`unknown terrain char "${character}" at ${x},${y}`);
      tiles[y * width + x] = terrain;
    }
  });

  const map: GameMap = {
    width,
    height,
    tiles,
    owners: new Array(width * height).fill(0),
    captureProgress: new Array(width * height).fill(0),
    elevation: level.elevation?.slice() ?? new Array(width * height).fill(0),
    cliffs: (level.cliffs ?? []).map((edge) => ({ from: { ...edge.from }, to: { ...edge.to } })),
    directionalCover: (level.directionalCover ?? []).map((cover) => ({
      at: { ...cover.at },
      sides: { ...cover.sides },
    })),
  };

  if (map.elevation.length !== width * height) {
    throw new LevelFormatError(`elevation has ${map.elevation.length} cells, expected ${width * height}`);
  }
  if (map.elevation.some((value) => !Number.isInteger(value))) {
    throw new LevelFormatError('elevation values must be integers');
  }
  for (const cliff of map.cliffs) {
    const valid = [cliff.from, cliff.to].every((cell) =>
      Number.isInteger(cell.x) && Number.isInteger(cell.y) &&
      cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height);
    if (!valid || Math.abs(cliff.from.x - cliff.to.x) + Math.abs(cliff.from.y - cliff.to.y) !== 1) {
      throw new LevelFormatError(`invalid cliff edge ${cliff.from.x},${cliff.from.y} -> ${cliff.to.x},${cliff.to.y}`);
    }
  }
  for (const cover of map.directionalCover) {
    if (cover.at.x < 0 || cover.at.y < 0 || cover.at.x >= width || cover.at.y >= height) {
      throw new LevelFormatError(`directional cover out of bounds at ${cover.at.x},${cover.at.y}`);
    }
    for (const [side, strength] of Object.entries(cover.sides)) {
      if (!['north', 'east', 'south', 'west'].includes(side) || !['half', 'full'].includes(strength!)) {
        throw new LevelFormatError(`invalid directional cover ${side}:${String(strength)}`);
      }
    }
  }

  for (const owned of level.owners ?? []) {
    if (owned.x < 0 || owned.y < 0 || owned.x >= width || owned.y >= height) {
      throw new LevelFormatError(`owner entry out of bounds at ${owned.x},${owned.y}`);
    }
    const tile = idx(map, owned.x, owned.y);
    if (!content.terrains.get(map.tiles[tile]).capturable) {
      throw new LevelFormatError(`tile ${owned.x},${owned.y} (${map.tiles[tile]}) cannot be owned`);
    }
    map.owners[tile] = owned.owner;
  }

  return map;
}

/* ----------------------------------------------------------------- serialize */

export function terrainRows(map: GameMap, content: ContentCatalog): string[] {
  const rows: string[] = [];
  for (let y = 0; y < map.height; y++) {
    let row = '';
    for (let x = 0; x < map.width; x++) {
      row += serializedTerrainCharacter(content, map.tiles[y * map.width + x]);
    }
    rows.push(row);
  }
  return rows;
}
