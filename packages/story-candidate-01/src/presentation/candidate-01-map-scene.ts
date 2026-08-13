import { tileHash } from '@empire/battle-engine/grid';
import type { GameMap, TerrainId } from '@empire/battle-engine/types';
import { ANCIENT_EMPIRES_TERRAINS } from '@empire/content-ancient-empires';
import { CANDIDATE_01_TERRAINS } from '../terrain';
import { candidate01Asset } from './candidate-01-assets';
import {
  CANDIDATE_01_ENVIRONMENT,
  candidate01EnvironmentScene,
  type CandidateEnvironmentPlacement,
} from './candidate-01-environment';
import { runtimeAtlasCellMarkup } from '@empire/game-ui';
import type {
  SceneFrameMarkup,
  SceneLayerMarkup,
  SceneViewport,
  SceneViewportProfile,
} from '@empire/game-ui';

const TILE = 32;

const attr = (value: string): string =>
  value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;');

const tileAt = (map: GameMap, x: number, y: number): TerrainId | null =>
  x < 0 || y < 0 || x >= map.width || y >= map.height ? null : map.tiles[y * map.width + x];

const isForest = (map: GameMap, x: number, y: number): boolean => tileAt(map, x, y) === 'forest';

/**
 * Terrain whose art should read as a connected route.
 *
 * Built from this theme's own definitions plus the generic pack it depends on,
 * rather than queried from an ambient registry: a scene module must not be able
 * to observe content that belongs to some other engine instance.
 */
const ROUTE_TERRAIN: ReadonlySet<string> = new Set(
  [...ANCIENT_EMPIRES_TERRAINS, ...CANDIDATE_01_TERRAINS]
    .filter((terrain) =>
      terrain.tags.includes('road') ||
      terrain.tags.includes('building') ||
      terrain.tags.includes('outpost'))
    .map((terrain) => terrain.id),
);

const isRoute = (map: GameMap, x: number, y: number): boolean => {
  const id = tileAt(map, x, y);
  return id !== null && id !== undefined && ROUTE_TERRAIN.has(id);
};

const usesTwinHillsComposition = (levelId: string): boolean =>
  levelId === 'c01-01' || levelId.startsWith('experience-lab');

/** The visual map is wide, while the deterministic board remains a square lattice. */
export function candidate01SceneProfile(levelId: string): SceneViewportProfile {
  return usesTwinHillsComposition(levelId)
    ? { insets: { top: 62, right: 74, bottom: 74, left: 74 } }
    : {};
}

function atlasCellAt(
  atlasId: string,
  cell: number,
  x: number,
  y: number,
  className: string,
): string {
  const atlas = CANDIDATE_01_ENVIRONMENT.atlas(atlasId);
  return `<g class="${className}" transform="translate(${x} ${y})" data-environment-atlas="${atlasId}" data-environment-cell="${cell}">
    ${runtimeAtlasCellMarkup(atlas.raster, cell)}
  </g>`;
}

function environmentCellAt(
  id: string,
  cx: number,
  baseY: number,
  scale = 1,
  flip = false,
  opacity = 1,
  className = '',
): string {
  const record = CANDIDATE_01_ENVIRONMENT.cell(id);
  const anchor = record.cell.anchor ?? record.atlas.anchor ?? [record.atlas.cellWidth / 2, record.atlas.cellHeight];
  const x = cx - anchor[0] * scale;
  const y = baseY - anchor[1] * scale;
  const transform = flip
    ? `translate(${(x + record.atlas.cellWidth * scale).toFixed(1)} ${y.toFixed(1)}) scale(-${scale.toFixed(3)} ${scale.toFixed(3)})`
    : `translate(${x.toFixed(1)} ${y.toFixed(1)}) scale(${scale.toFixed(3)})`;
  return `<g class="candidate-environment-prop ${className}" transform="${transform}" opacity="${opacity}" data-environment-cell-id="${id}">
    ${runtimeAtlasCellMarkup(record.atlas.raster, record.cell.index)}
  </g>`;
}

function topicPlacementMarkup(placement: CandidateEnvironmentPlacement): string {
  if (!placement.topicId) return '';
  const record = candidate01Asset(placement.topicId);
  const frameWidth = record.frameWidth ?? record.width;
  const frameHeight = record.frameHeight ?? record.height;
  const frames = record.frames ?? 1;
  const anchor = record.anchor ?? [frameWidth / 2, frameHeight - 1];
  const scale = placement.scale ?? 1;
  const cx = placement.x * TILE;
  const baseY = (placement.y + 1) * TILE;
  const x = cx - anchor[0] * scale;
  const y = baseY - anchor[1] * scale;
  const transform = placement.flip
    ? `translate(${(x + frameWidth * scale).toFixed(1)} ${y.toFixed(1)}) scale(-${scale} ${scale})`
    : `translate(${x.toFixed(1)} ${y.toFixed(1)}) scale(${scale})`;
  const figure = frames > 1
    ? runtimeAtlasCellMarkup({ href: record.url, cellWidth: frameWidth, cellHeight: frameHeight, columns: frames, rows: 1 }, 0)
    : `<image href="${attr(record.url)}" width="${record.width}" height="${record.height}" preserveAspectRatio="xMidYMid meet"/>`;
  return `<g class="candidate-scenery-topic" transform="${transform}" opacity="${placement.opacity ?? 1}" data-scenery-topic="${placement.topicId}">${figure}</g>`;
}

function placementMarkup(placement: CandidateEnvironmentPlacement): string {
  if (placement.topicId) return topicPlacementMarkup(placement);
  if (!placement.id) return '';
  return environmentCellAt(
    placement.id,
    placement.x * TILE,
    (placement.y + 1) * TILE,
    placement.scale ?? 1,
    placement.flip ?? false,
    placement.opacity ?? 1,
    `is-authored-placement is-${placement.layer ?? 'under-units'}`,
  );
}

function blobMask(map: GameMap, x: number, y: number): number {
  return (isForest(map, x, y - 1) ? 1 : 0)
    | (isForest(map, x + 1, y - 1) ? 2 : 0)
    | (isForest(map, x + 1, y) ? 4 : 0)
    | (isForest(map, x + 1, y + 1) ? 8 : 0)
    | (isForest(map, x, y + 1) ? 16 : 0)
    | (isForest(map, x - 1, y + 1) ? 32 : 0)
    | (isForest(map, x - 1, y) ? 64 : 0)
    | (isForest(map, x - 1, y - 1) ? 128 : 0);
}

function routeMask(map: GameMap, x: number, y: number): number {
  return (isRoute(map, x, y - 1) ? 1 : 0)
    | (isRoute(map, x + 1, y) ? 2 : 0)
    | (isRoute(map, x, y + 1) ? 4 : 0)
    | (isRoute(map, x - 1, y) ? 8 : 0);
}

/**
 * Asset-only terrain composition. It keeps the Ancient Empires virtues—clear
 * connected roads and readable occupied cells—without reverting to flat tiles.
 */
function terrainGroundMarkup(map: GameMap): string {
  const base: string[] = [];
  const transitions: string[] = [];
  const routes: string[] = [];
  const decals: string[] = [];
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      // Broad material patches avoid the noisy checkerboard produced by a
      // per-cell random variant while still breaking up a flat tiled field.
      const patchX = Math.floor(x / 4);
      const patchY = Math.floor(y / 3);
      const variant = Math.floor(tileHash(patchX, patchY, 1101) * 4);
      base.push(atlasCellAt('surface-meadow', variant, x * TILE, y * TILE, 'candidate-ground-surface'));
      if (isForest(map, x, y)) {
        const cell = CANDIDATE_01_ENVIRONMENT.blobIndex('transition-meadow-forest', blobMask(map, x, y));
        transitions.push(atlasCellAt('transition-meadow-forest', cell, x * TILE, y * TILE, 'candidate-ground-transition'));
      }
      if (isRoute(map, x, y)) {
        const mask = routeMask(map, x, y);
        const roadVariant = Math.floor(tileHash(x, y, 1102) * 4);
        const cell = CANDIDATE_01_ENVIRONMENT.connectedIndex('route-dirt-road', mask, roadVariant);
        routes.push(atlasCellAt('route-dirt-road', cell, x * TILE, y * TILE, 'candidate-ground-route'));
        routes.push(atlasCellAt('route-edge-dirt-road', cell, x * TILE, y * TILE, 'candidate-ground-route-edge'));
      } else if (tileAt(map, x, y) === 'plain' && tileHash(x, y, 1103) > 0.9) {
        const detail = tileHash(x, y, 1104) > 0.5 ? 'grass-tuft-a' : 'fallen-leaves';
        decals.push(environmentCellAt(detail, (x + 0.5) * TILE, (y + 1) * TILE, 0.58, tileHash(x, y, 1105) > 0.5, 0.72, 'is-procedural-decal'));
      }
    }
  }
  return `${base.join('')}${transitions.join('')}${routes.join('')}${decals.join('')}`;
}

function authoredPlacements(levelId: string, layer: CandidateEnvironmentPlacement['layer']): string {
  const scene = candidate01EnvironmentScene(levelId);
  return scene?.placements.filter((placement) => placement.layer === layer).map(placementMarkup).join('') ?? '';
}

function forestSceneryMarkup(map: GameMap): string {
  const parts: string[] = [];
  const canopy = ['oak-ancient', 'mixed-forest-autumn', 'mixed-forest-dense', 'oak-grove-dense', 'mixed-forest-edge'] as const;
  const understory = ['sapling-rock-cluster', 'bramble-dark', 'fern-bed', 'bramble-berries', 'stump-low', 'stump-hollow', 'forest-floor-cluster'] as const;
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      if (!isForest(map, x, y)) continue;
      const boundary = x <= 1 || y <= 1 || x >= map.width - 2 || y >= map.height - 2;
      if (!boundary && tileHash(x, y, 1119) < 0.34) continue;
      const choices = boundary ? canopy : understory;
      const id = choices[Math.floor(tileHash(x, y, 1120) * choices.length)];
      const scale = boundary ? 0.72 + tileHash(x, y, 1121) * 0.12 : 0.56 + tileHash(x, y, 1121) * 0.12;
      parts.push(environmentCellAt(
        id,
        (x + 0.5) * TILE + (tileHash(x, y, 1122) - 0.5) * 10,
        (y + 1.18) * TILE,
        scale,
        tileHash(x, y, 1123) > 0.52,
        boundary ? 1 : 0.9,
        boundary ? 'is-boundary-tree' : 'is-interior-forest',
      ));
    }
  }
  return parts.join('');
}

function ambientVillagers(): string {
  return [
    { topicId: 'C01-MISSION-BORDER-FARMER', x: 13.8, y: 9.15, layer: 'under-units', scale: 0.88, opacity: 0.86 },
    { topicId: 'C01-MISSION-BORDER-FARMER', x: 15.4, y: 7.25, layer: 'under-units', scale: 0.82, flip: true, opacity: 0.82 },
  ].map((placement) => topicPlacementMarkup(placement as CandidateEnvironmentPlacement)).join('');
}

function twinHillsGroundLayer(map: GameMap): string {
  return `<g class="candidate-scene-ground" data-depth="ground" pointer-events="none">
    ${terrainGroundMarkup(map)}
    ${authoredPlacements('c01-01', 'foundation')}
    ${authoredPlacements('c01-01', 'ground-decal')}
  </g>`;
}

function twinHillsUnderUnits(map: GameMap): string {
  return `<g class="candidate-map-scenery candidate-map-scenery-under" data-depth="under-units" pointer-events="none">
    ${forestSceneryMarkup(map)}
    ${authoredPlacements('c01-01', 'under-units')}
    ${ambientVillagers()}
    <rect width="${map.width * TILE}" height="${map.height * TILE}" fill="#f3d69a" opacity="0.035" pointer-events="none"/>
  </g>`;
}

function sceneFrameForestMarkup(viewport: SceneViewport): string {
  const parts: string[] = [];
  const trees = ['oak-ancient', 'mixed-forest-autumn', 'mixed-forest-dense', 'oak-grove-dense'] as const;
  const horizontalCount = Math.ceil(viewport.sceneWidth / 54) + 2;
  const verticalCount = Math.ceil(viewport.sceneHeight / 58);
  for (let i = 0; i < horizontalCount; i++) {
    const x = -18 + i * (viewport.sceneWidth + 36) / Math.max(1, horizontalCount - 1);
    for (const [edge, baseY, seed] of [['top', viewport.originY + 20, 1200], ['bottom', viewport.sceneHeight + 20, 1300]] as const) {
      const id = trees[Math.floor(tileHash(i, seed, 1) * trees.length)];
      parts.push(environmentCellAt(id, x + (tileHash(i, seed, 2) - 0.5) * 18, baseY, 0.86 + tileHash(i, seed, 3) * 0.2, tileHash(i, seed, 4) > 0.5, 1, `is-scene-frame is-frame-${edge}`));
    }
  }
  for (let i = 1; i < verticalCount - 1; i++) {
    const y = i * viewport.sceneHeight / Math.max(1, verticalCount - 1);
    for (const [edge, x, seed] of [['left', viewport.originX - 26, 1400], ['right', viewport.originX + viewport.fieldWidth + 28, 1500]] as const) {
      const id = trees[Math.floor(tileHash(i, seed, 1) * trees.length)];
      parts.push(environmentCellAt(id, x, y + 32, 0.86 + tileHash(i, seed, 3) * 0.18, tileHash(i, seed, 4) > 0.5, 1, `is-scene-frame is-frame-${edge}`));
    }
  }
  parts.push(environmentCellAt('boulder-large', viewport.originX - 5, viewport.originY + 82, 1.05, false, 0.96, 'is-scene-frame'));
  parts.push(environmentCellAt('fallen-hollow-log', viewport.originX + 92, viewport.sceneHeight + 4, 0.9, true, 0.96, 'is-scene-frame'));
  return parts.join('');
}

/** Non-playable woodland surrounds, but never changes, the tactical field. */
export function candidate01SceneFrameMarkup(
  levelId: string,
  map: GameMap,
  viewport: SceneViewport,
): SceneFrameMarkup {
  if (!usesTwinHillsComposition(levelId)) return { backdrop: '', foreground: '' };
  const fieldX = viewport.originX - 24;
  const fieldY = viewport.originY - 18;
  return {
    backdrop: `<g class="candidate-scene-backdrop" pointer-events="none" data-scene-viewport="authored-wide">
      <defs>
        <radialGradient id="c01-scene-ground" cx="48%" cy="42%" r="78%">
          <stop offset="0" stop-color="#536c47"/>
          <stop offset="0.7" stop-color="#2d4935"/>
          <stop offset="1" stop-color="#0c2019"/>
        </radialGradient>
        <filter id="c01-field-shadow" x="-20%" y="-20%" width="140%" height="150%">
          <feDropShadow dx="0" dy="8" stdDeviation="9" flood-color="#050c09" flood-opacity="0.66"/>
        </filter>
      </defs>
      <rect width="${viewport.sceneWidth}" height="${viewport.sceneHeight}" rx="24" fill="#091712"/>
      <rect x="${fieldX}" y="${fieldY}" width="${map.width * TILE + 48}" height="${map.height * TILE + 38}" rx="46" fill="url(#c01-scene-ground)" filter="url(#c01-field-shadow)"/>
    </g>`,
    foreground: `<g class="candidate-scene-foreground" pointer-events="none">
      ${sceneFrameForestMarkup(viewport)}
    </g>`,
  };
}

/** Story art is pure presentation and cannot affect deterministic battle rules. */
export function candidate01MapSceneryMarkup(levelId: string, map: GameMap): SceneLayerMarkup {
  if (!usesTwinHillsComposition(levelId)) return { ground: '', underUnits: '', overUnits: '' };
  return {
    ground: twinHillsGroundLayer(map),
    underUnits: twinHillsUnderUnits(map),
    overUnits: `<g class="candidate-map-foreground" data-depth="over-units" pointer-events="none">${authoredPlacements('c01-01', 'over-units')}</g>`,
  };
}
