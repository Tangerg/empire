import type { TerrainId, UnitTypeId } from '../core/types';
import { tileHash } from '../core/grid';
import {
  runtimeAtlasCellMarkup,
  runtimeUnitMarkup,
  type RuntimeCellAtlas,
  type RuntimeUnitSheet,
} from './runtime-raster';

const unitSheet = (href: string): RuntimeUnitSheet => ({
  href,
  frameWidth: 32,
  frameHeight: 48,
  frameCount: 4,
  anchor: { x: 16, y: 47 },
});

const unitSheets: Readonly<Record<string, RuntimeUnitSheet>> = {
  soldier: unitSheet(
    new URL(
      '../../docs/story-candidates/candidate-01/assets/runtime-v2/units/gray-banner-soldier-walk.png',
      import.meta.url,
    ).href,
  ),
  archer: unitSheet(
    new URL(
      '../../docs/story-candidates/candidate-01/assets/runtime-v2/units/silverwood-archer-walk.png',
      import.meta.url,
    ).href,
  ),
  knight: unitSheet(
    new URL(
      '../../docs/story-candidates/candidate-01/assets/runtime-v2/units/burgundy-knight-walk.png',
      import.meta.url,
    ).href,
  ),
  cleric: unitSheet(
    new URL(
      '../../docs/story-candidates/candidate-01/assets/runtime-v2/units/forge-cleric-walk.png',
      import.meta.url,
    ).href,
  ),
};

const terrainAtlas = (href: string, columns: number): RuntimeCellAtlas => ({
  href,
  cellWidth: 32,
  cellHeight: 32,
  columns,
  rows: 1,
});

const terrainAtlases: Readonly<Record<string, RuntimeCellAtlas>> = {
  plain: terrainAtlas(
    new URL('../../docs/story-candidates/candidate-01/assets/runtime-v2/terrain/plain.png', import.meta.url).href,
    4,
  ),
  road: terrainAtlas(
    new URL('../../docs/story-candidates/candidate-01/assets/runtime-v2/terrain/road.png', import.meta.url).href,
    16,
  ),
  bridge: terrainAtlas(
    new URL('../../docs/story-candidates/candidate-01/assets/runtime-v2/terrain/bridge.png', import.meta.url).href,
    4,
  ),
  forest: terrainAtlas(
    new URL('../../docs/story-candidates/candidate-01/assets/runtime-v2/terrain/forest.png', import.meta.url).href,
    4,
  ),
  hill: terrainAtlas(
    new URL('../../docs/story-candidates/candidate-01/assets/runtime-v2/terrain/hill.png', import.meta.url).href,
    4,
  ),
  mountain: terrainAtlas(
    new URL('../../docs/story-candidates/candidate-01/assets/runtime-v2/terrain/mountain.png', import.meta.url).href,
    4,
  ),
  water: terrainAtlas(
    new URL('../../docs/story-candidates/candidate-01/assets/runtime-v2/terrain/water.png', import.meta.url).href,
    16,
  ),
  wall: terrainAtlas(
    new URL('../../docs/story-candidates/candidate-01/assets/runtime-v2/terrain/wall.png', import.meta.url).href,
    4,
  ),
};

const mapStructureAtlases: Readonly<Record<string, RuntimeCellAtlas>> = {
  village: {
    href: new URL(
      '../../docs/story-candidates/candidate-01/assets/runtime-v2/structures/village-states.png',
      import.meta.url,
    ).href,
    cellWidth: 32,
    cellHeight: 64,
    columns: 1,
    rows: 3,
  },
  barracks: {
    href: new URL(
      '../../docs/story-candidates/candidate-01/assets/runtime-v2/structures/barracks-states.png',
      import.meta.url,
    ).href,
    cellWidth: 32,
    cellHeight: 64,
    columns: 1,
    rows: 3,
  },
  castle: {
    href: new URL(
      '../../docs/story-candidates/candidate-01/assets/runtime-v2/structures/castle-states.png',
      import.meta.url,
    ).href,
    cellWidth: 32,
    cellHeight: 64,
    columns: 1,
    rows: 3,
  },
};

interface RuntimeTerrainContext {
  x: number;
  y: number;
  ownerColor?: string;
  linked: { n: boolean; e: boolean; s: boolean; w: boolean };
}

const connectionMask = (linked: RuntimeTerrainContext['linked']): number =>
  (linked.n ? 1 : 0) | (linked.e ? 2 : 0) | (linked.s ? 4 : 0) | (linked.w ? 8 : 0);

export function candidate01UnitMarkup(type: UnitTypeId, team: string): string | null {
  const sheet = unitSheets[type];
  return sheet ? runtimeUnitMarkup(sheet, team) : null;
}

export function candidate01UnitIcon(type: UnitTypeId, team: string, size: number): string | null {
  const markup = candidate01UnitMarkup(type, team);
  if (!markup) return null;
  return `<svg viewBox="0 -16 32 48" width="${size}" height="${size}" shape-rendering="crispEdges">${markup}</svg>`;
}

export function candidate01TerrainMarkup(id: TerrainId, ctx: RuntimeTerrainContext): string | null {
  const structure = mapStructureAtlases[id];
  if (structure) {
    const captured = ctx.ownerColor !== undefined;
    const cell = captured ? 2 : 0;
    const groundCell = Math.min(3, Math.floor(tileHash(ctx.x, ctx.y, 701) * 4));
    const ground = runtimeAtlasCellMarkup(terrainAtlases.plain, groundCell);
    const ownerMarker = captured
      ? `<rect x="1" y="1" width="30" height="30" rx="2" fill="none" stroke="${ctx.ownerColor}" stroke-width="1.5" opacity="0.95"/>`
      : '';
    return `${ground}<g transform="translate(0,-32)" data-runtime-raster="structure">${runtimeAtlasCellMarkup(structure, cell)}</g>${ownerMarker}`;
  }
  const atlas = terrainAtlases[id];
  if (!atlas) return null;
  const connected = id === 'road' || id === 'water';
  const cell = connected
    ? connectionMask(ctx.linked)
    : Math.min(3, Math.floor(tileHash(ctx.x, ctx.y, 701) * 4));
  return runtimeAtlasCellMarkup(atlas, cell);
}
