import type { TerrainId, UnitTypeId } from '../core/types';
import { tileHash } from '../core/grid';
import {
  runtimeAtlasCellMarkup,
  runtimeUnitMarkup,
  type RuntimeCellAtlas,
  type RuntimeUnitSheet,
} from './runtime-raster';

const unitSheet = (href: string, frameWidth = 32, frameHeight = 48): RuntimeUnitSheet => ({
  href,
  frameWidth,
  frameHeight,
  frameCount: 4,
  anchor: { x: frameWidth / 2, y: frameHeight - 1 },
});

const runtimeAssets = import.meta.glob<string>([
  '../../docs/story-candidates/candidate-01/assets/runtime-v2/batch-02/units/*.png',
  '../../docs/story-candidates/candidate-01/assets/runtime-v2/units/*.png',
  '../../docs/story-candidates/candidate-01/assets/runtime-v2/batch-02/mission-units/*.png',
  '../../docs/story-candidates/candidate-01/assets/runtime-v2/batch-03/units/*.png',
  '../../docs/story-candidates/candidate-01/assets/runtime-v2/batch-02/terrain/*.png',
], { eager: true, import: 'default', query: '?url' });

const runtimeAsset = (path: string): string => {
  const key = `../../docs/story-candidates/candidate-01/assets/runtime-v2/${path}`;
  const href = runtimeAssets[key];
  if (!href) throw new Error(`missing candidate-01 runtime asset: ${path}`);
  return href;
};

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
  'c01.laiya': unitSheet(runtimeAsset('batch-02/units/banner-guard.png')),
  'c01.roderick': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-heavy-knight.png'), 64, 64),
  'c01.cain': unitSheet(runtimeAsset('batch-02/units/legion-shield.png')),
  'c01.bran': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-ranger.png')),
  'c01.mirelle': unitSheet(runtimeAsset('batch-02/units/gravekeeper.png')),
  'c01.tasha': unitSheet(runtimeAsset('batch-02/units/engineer.png')),
  'c01.ivra': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-ivra-growth.png')),
  'c01.swordsman': unitSheet(runtimeAsset('batch-02/units/swordsman.png')),
  'c01.banner-guard': unitSheet(runtimeAsset('batch-02/units/banner-guard.png')),
  'c01.archer': unitSheet(runtimeAsset('units/silverwood-archer-walk.png')),
  'c01.knight': unitSheet(runtimeAsset('units/burgundy-knight-walk.png')),
  'c01.legion-shield': unitSheet(runtimeAsset('batch-02/units/legion-shield.png')),
  'c01.gravekeeper': unitSheet(runtimeAsset('batch-02/units/gravekeeper.png')),
  'c01.engineer': unitSheet(runtimeAsset('batch-02/units/engineer.png')),
  'c01.wolf-rider': unitSheet(runtimeAsset('batch-02/units/wolf-rider.png'), 64, 64),
  'c01.skeleton-guard': unitSheet(runtimeAsset('batch-02/units/skeleton-guard.png')),
  'c01.ghost': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-ghost.png')),
  'c01.inquisitor': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-inquisitor.png')),
  'c01.templar': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-templar.png')),
  'c01.ballista': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-ballista.png'), 96, 64),
  'c01.battle-mage': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-battle-mage.png')),
  'c01.rune-shield': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-rune-shield.png')),
  'c01.rune-artificer': unitSheet(runtimeAsset('batch-02/units/rune-artificer.png')),
  'c01.stone-golem': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-stone-golem.png'), 64, 64),
  'c01.silver-longbow': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-silver-longbow.png')),
  'c01.woodland-walker': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-woodland-walker.png')),
  'c01.druid': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-druid.png')),
  'c01.cemetery-colossus': unitSheet(runtimeAsset('batch-03/units/c01-v2-b03-unit-cemetery-colossus.png'), 96, 64),
  'c01.refugee': unitSheet(runtimeAsset('batch-02/mission-units/refugee-adult.png')),
  'c01.laborer': unitSheet(runtimeAsset('batch-02/mission-units/bridge-laborer.png')),
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
  'c01.scorched': terrainAtlas(runtimeAsset('batch-02/terrain/scorched-farmland.png'), 4),
  'c01.riverbank': terrainAtlas(runtimeAsset('batch-02/terrain/riverbank.png'), 4),
  'c01.street': terrainAtlas(runtimeAsset('batch-02/terrain/capital-street.png'), 4),
  'c01.oathway': terrainAtlas(runtimeAsset('batch-02/terrain/controlled-oath.png'), 16),
  'c01.forge': terrainAtlas(runtimeAsset('batch-02/terrain/forge-stone.png'), 4),
  'c01.graveyard': terrainAtlas(runtimeAsset('batch-02/terrain/graveyard.png'), 4),
  'c01.molten': terrainAtlas(runtimeAsset('batch-02/terrain/molten-channel.png'), 16),
  'c01.mother-root': terrainAtlas(runtimeAsset('batch-02/terrain/mother-root.png'), 4),
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
  'c01.outpost': {
    href: new URL(
      '../../docs/story-candidates/candidate-01/assets/runtime-v2/structures/castle-states.png',
      import.meta.url,
    ).href,
    cellWidth: 32,
    cellHeight: 64,
    columns: 1,
    rows: 3,
  },
  'c01.field-post': {
    href: new URL(
      '../../docs/story-candidates/candidate-01/assets/runtime-v2/structures/barracks-states.png',
      import.meta.url,
    ).href,
    cellWidth: 32,
    cellHeight: 64,
    columns: 1,
    rows: 3,
  },
  'c01.keep': {
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
  const connected = id === 'road' || id === 'water' || id === 'c01.oathway' || id === 'c01.molten';
  const cell = connected
    ? connectionMask(ctx.linked)
    : Math.min(3, Math.floor(tileHash(ctx.x, ctx.y, 701) * 4));
  return runtimeAtlasCellMarkup(atlas, cell);
}
