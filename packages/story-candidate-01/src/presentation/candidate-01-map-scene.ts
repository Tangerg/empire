import { tileHash, type ContentCatalog, type GameMap, type TerrainId } from '@empire/battle-engine';
import { candidate01Asset } from './candidate-01-assets';
import {
  CANDIDATE_01_ENVIRONMENT,
  candidate01EnvironmentScene,
  type CandidateEnvironmentPlacement,
} from './candidate-01-environment';
import {
  boardPiecesMarkup,
  escapeAttr as attr,
  runtimeAtlasCellMarkup,
  wholeField,
  type BoardPiece,
  type BattleSceneContext,
} from '@empire/game-ui';
import { CANDIDATE_01_BOARD_STYLE } from './candidate-01-board-style';
import type {
  SceneFrameMarkup,
  SceneLayers,
  SceneViewport,
  SceneViewportProfile,
} from '@empire/game-ui';

const TILE = 32;

/** Where a cell's art goes: the cell's own origin. */
const cellOrigin = (x: number, y: number): { x: number; y: number } => ({ x: x * TILE, y: y * TILE });

const tileAt = (map: GameMap, x: number, y: number): TerrainId | null =>
  x < 0 || y < 0 || x >= map.width || y >= map.height ? null : map.tiles[y * map.width + x];

const isForest = (map: GameMap, x: number, y: number): boolean => tileAt(map, x, y) === 'forest';

const isRoute = (content: ContentCatalog, map: GameMap, x: number, y: number): boolean => {
  const id = tileAt(map, x, y);
  if (id === null) return false;
  const tags = content.terrains.get(id).tags;
  return tags.includes('road') || tags.includes('building') || tags.includes('outpost');
};

const usesTwinHillsComposition = (levelId: string): boolean =>
  levelId === 'c01-01' || levelId.startsWith('experience-lab');

/** The visual map is wide, while the deterministic board remains a square lattice. */
export function candidate01SceneProfile(levelId: string): SceneViewportProfile {
  return usesTwinHillsComposition(levelId)
    ? { insets: { top: 62, right: 74, bottom: 74, left: 74 } }
    : {};
}

/**
 * One atlas cell, drawn about its own origin.
 *
 * The class is optional because most of these had one that nothing styled. Four
 * labels rode on every cell of the biggest layer on the board —
 * `candidate-ground-surface`, `candidate-ground-transition`,
 * `data-environment-atlas`, `data-environment-cell` — and no stylesheet, test or
 * module in the repository read any of them. Two classes here are real, and both
 * carry a filter: the road and its edge.
 */
function atlasCellMarkup(atlasId: string, cell: number, className?: string): string {
  const atlas = CANDIDATE_01_ENVIRONMENT.atlas(atlasId);
  const figure = runtimeAtlasCellMarkup(atlas.raster, cell);
  return className ? `<g class="${className}">${figure}</g>` : figure;
}

/**
 * How one environment cell is placed, beyond where it stands.
 *
 * Named because the call sites could not be read: four positional trailers meant
 * `environmentCellAt(detail, x, y, 0.58, tileHash(x, y, 1105) > 0.5, 0.72, '…')`,
 * where the two numbers are a scale and an opacity and the boolean is a mirror.
 */
interface EnvironmentCellPlacing {
  readonly scale?: number;
  readonly flip?: boolean;
  readonly opacity?: number;
  readonly className?: string;
}

/**
 * One environment prop, drawn about the point it stands on.
 *
 * Its anchor is inside the picture and its place is outside it, which is what makes
 * two ferns of the same kind and scale one picture instead of two strings.
 *
 * The offset is rounded to a thousandth rather than a tenth. A tenth was right when
 * this was `standingPoint - anchor * scale`: a value with a cell's coordinates in
 * it, of arbitrary precision, and rounding it kept the markup short. The offset
 * alone depends only on the prop and its scale, so rounding buys no shorter strings
 * and no extra sharing — it only moves the picture. Positions still differ from the
 * old ones, because the old ones were the rounded sum.
 */
function environmentCellMarkup(id: string, placing: EnvironmentCellPlacing = {}): string {
  const { scale = 1, flip = false, opacity = 1, className = '' } = placing;
  const record = CANDIDATE_01_ENVIRONMENT.cell(id);
  const anchor = record.cell.anchor ?? record.atlas.anchor ?? [record.atlas.cellWidth / 2, record.atlas.cellHeight];
  const x = -anchor[0] * scale;
  const y = -anchor[1] * scale;
  const transform = flip
    ? `translate(${(x + record.atlas.cellWidth * scale).toFixed(3)} ${y.toFixed(3)}) scale(-${scale.toFixed(3)} ${scale.toFixed(3)})`
    : `translate(${x.toFixed(3)} ${y.toFixed(3)}) scale(${scale.toFixed(3)})`;
  return `<g class="candidate-environment-prop ${className}" transform="${transform}" opacity="${opacity}">
    ${runtimeAtlasCellMarkup(record.atlas.raster, record.cell.index)}
  </g>`;
}

/**
 * The same prop, at a place, as the one string the scene frame needs.
 *
 * The frame is art outside the tactical field and reaches a renderer as one
 * picture, so it spells a placement the way every other stringly consumer does —
 * through the port's own `boardPiecesMarkup`.
 */
const environmentCellAt = (
  id: string,
  cx: number,
  baseY: number,
  placing: EnvironmentCellPlacing = {},
): string => boardPiecesMarkup([{ markup: environmentCellMarkup(id, placing), x: cx, y: baseY }]);

function topicPlacementPiece(placement: CandidateEnvironmentPlacement): BoardPiece | null {
  if (!placement.topicId) return null;
  const record = candidate01Asset(placement.topicId);
  const frameWidth = record.frameWidth ?? record.width;
  const frameHeight = record.frameHeight ?? record.height;
  const frames = record.frames ?? 1;
  const anchor = record.anchor ?? [frameWidth / 2, frameHeight - 1];
  const scale = placement.scale ?? 1;
  const x = -anchor[0] * scale;
  const y = -anchor[1] * scale;
  const transform = placement.flip
    ? `translate(${(x + frameWidth * scale).toFixed(3)} ${y.toFixed(3)}) scale(-${scale} ${scale})`
    : `translate(${x.toFixed(3)} ${y.toFixed(3)}) scale(${scale})`;
  const figure = frames > 1
    ? runtimeAtlasCellMarkup({ href: record.url, cellWidth: frameWidth, cellHeight: frameHeight, columns: frames, rows: 1 }, 0)
    : `<image href="${attr(record.url)}" width="${record.width}" height="${record.height}" preserveAspectRatio="xMidYMid meet"/>`;
  return {
    // Both kinds of authored placement name the depth they were declared at, which
    // is what lets a test hold this module to the depth contract rather than to a
    // string somewhere in the markup.
    markup: `<g class="candidate-scenery-topic is-${placement.layer ?? 'under-units'}"`
      + ` transform="${transform}" opacity="${placement.opacity ?? 1}">${figure}</g>`,
    x: placement.x * TILE,
    y: (placement.y + 1) * TILE,
  };
}

function placementPiece(placement: CandidateEnvironmentPlacement): BoardPiece | null {
  if (placement.topicId) return topicPlacementPiece(placement);
  if (!placement.id) return null;
  return {
    markup: environmentCellMarkup(placement.id, {
      scale: placement.scale ?? 1,
      flip: placement.flip ?? false,
      opacity: placement.opacity ?? 1,
      className: `is-${placement.layer ?? 'under-units'}`,
    }),
    x: placement.x * TILE,
    y: (placement.y + 1) * TILE,
  };
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

function routeMask(content: ContentCatalog, map: GameMap, x: number, y: number): number {
  return (isRoute(content, map, x, y - 1) ? 1 : 0)
    | (isRoute(content, map, x + 1, y) ? 2 : 0)
    | (isRoute(content, map, x, y + 1) ? 4 : 0)
    | (isRoute(content, map, x - 1, y) ? 8 : 0);
}

/**
 * Asset-only terrain composition. It keeps the Ancient Empires virtues—clear
 * connected roads and readable occupied cells—without reverting to flat tiles.
 */
function terrainGroundPieces(content: ContentCatalog, map: GameMap): BoardPiece[] {
  // Kept in four passes and concatenated at the end: the depth order within the
  // layer is surface, then forest transitions, then roads, then loose detail.
  const base: BoardPiece[] = [];
  const transitions: BoardPiece[] = [];
  const routes: BoardPiece[] = [];
  const decals: BoardPiece[] = [];
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      const at = cellOrigin(x, y);
      // Broad material patches avoid the noisy checkerboard produced by a
      // per-cell random variant while still breaking up a flat tiled field.
      const patchX = Math.floor(x / 4);
      const patchY = Math.floor(y / 3);
      const variant = Math.floor(tileHash(patchX, patchY, 1101) * 4);
      base.push({ markup: atlasCellMarkup('surface-meadow', variant), ...at });
      if (isForest(map, x, y)) {
        const cell = CANDIDATE_01_ENVIRONMENT.blobIndex('transition-meadow-forest', blobMask(map, x, y));
        transitions.push({ markup: atlasCellMarkup('transition-meadow-forest', cell), ...at });
      }
      if (isRoute(content, map, x, y)) {
        const mask = routeMask(content, map, x, y);
        const roadVariant = Math.floor(tileHash(x, y, 1102) * 4);
        const cell = CANDIDATE_01_ENVIRONMENT.connectedIndex('route-dirt-road', mask, roadVariant);
        // These two classes are the only ones in this layer a stylesheet reads.
        routes.push({ markup: atlasCellMarkup('route-dirt-road', cell, 'candidate-ground-route'), ...at });
        routes.push({ markup: atlasCellMarkup('route-edge-dirt-road', cell, 'candidate-ground-route-edge'), ...at });
      } else if (tileAt(map, x, y) === 'plain' && tileHash(x, y, 1103) > 0.9) {
        const detail = tileHash(x, y, 1104) > 0.5 ? 'grass-tuft-a' : 'fallen-leaves';
        decals.push({
          markup: environmentCellMarkup(detail, {
            scale: 0.58,
            flip: tileHash(x, y, 1105) > 0.5,
            opacity: 0.72,
          }),
          x: (x + 0.5) * TILE,
          y: (y + 1) * TILE,
        });
      }
    }
  }
  return [...base, ...transitions, ...routes, ...decals];
}

function authoredPlacementPieces(
  levelId: string,
  layer: CandidateEnvironmentPlacement['layer'],
): BoardPiece[] {
  const scene = candidate01EnvironmentScene(levelId);
  return (scene?.placements ?? [])
    .filter((placement) => placement.layer === layer)
    .map(placementPiece)
    .filter((piece): piece is BoardPiece => piece !== null);
}

function forestSceneryPieces(map: GameMap): BoardPiece[] {
  const parts: BoardPiece[] = [];
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
      parts.push({
        markup: environmentCellMarkup(id, {
          scale,
          flip: tileHash(x, y, 1123) > 0.52,
          opacity: boundary ? 1 : 0.9,
          className: boundary ? 'is-boundary-tree' : 'is-interior-forest',
        }),
        x: (x + 0.5) * TILE + (tileHash(x, y, 1122) - 0.5) * 10,
        y: (y + 1.18) * TILE,
      });
    }
  }
  return parts;
}

function ambientVillagerPieces(): BoardPiece[] {
  return [
    { topicId: 'C01-MISSION-BORDER-FARMER', x: 13.8, y: 9.15, layer: 'under-units', scale: 0.88, opacity: 0.86 },
    { topicId: 'C01-MISSION-BORDER-FARMER', x: 15.4, y: 7.25, layer: 'under-units', scale: 0.82, flip: true, opacity: 0.82 },
  ]
    .map((placement) => topicPlacementPiece(placement as CandidateEnvironmentPlacement))
    .filter((piece): piece is BoardPiece => piece !== null);
}

/**
 * Three wrapper groups used to hold these layers together, and none of them held
 * anything.
 *
 * `candidate-scene-ground`, `candidate-map-scenery-under` and
 * `candidate-map-foreground` carried `data-depth` — read by three assertions in
 * this pack's own test and nothing else — and `pointer-events="none"`, which the
 * general layer states for every layer that is art. The scenery wrapper also
 * carried `isolation: isolate`, to confine a `mix-blend-mode: screen` declared on
 * `.candidate-map-ambient`: a class no module in the repository emits.
 *
 * Eight more labels rode on the pieces themselves. Five were read by nothing at
 * all. `data-environment-cell-id` was read by four assertions in this pack's test,
 * which matched authored prop ids inside it to check that the scene had been
 * composed — a debug attribute standing in for the depth contract those props
 * actually have. They assert the contract now.
 *
 * A layer is its pieces. The renderer's own layer group is the group.
 */
function twinHillsGroundPieces(content: ContentCatalog, map: GameMap): BoardPiece[] {
  return [
    ...terrainGroundPieces(content, map),
    ...authoredPlacementPieces('c01-01', 'foundation'),
    ...authoredPlacementPieces('c01-01', 'ground-decal'),
  ];
}

function twinHillsUnderUnitPieces(map: GameMap): BoardPiece[] {
  return [
    ...forestSceneryPieces(map),
    ...authoredPlacementPieces('c01-01', 'under-units'),
    ...ambientVillagerPieces(),
    // A warm wash over the whole field, which is art with no place of its own.
    ...wholeField(`<rect width="${map.width * TILE}" height="${map.height * TILE}" fill="#f3d69a" opacity="0.035"/>`),
  ];
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
      parts.push(environmentCellAt(id, x + (tileHash(i, seed, 2) - 0.5) * 18, baseY, {
        scale: 0.86 + tileHash(i, seed, 3) * 0.2,
        flip: tileHash(i, seed, 4) > 0.5,
        className: `is-scene-frame is-frame-${edge}`,
      }));
    }
  }
  for (let i = 1; i < verticalCount - 1; i++) {
    const y = i * viewport.sceneHeight / Math.max(1, verticalCount - 1);
    for (const [edge, x, seed] of [['left', viewport.originX - 26, 1400], ['right', viewport.originX + viewport.fieldWidth + 28, 1500]] as const) {
      const id = trees[Math.floor(tileHash(i, seed, 1) * trees.length)];
      parts.push(environmentCellAt(id, x, y + 32, {
        scale: 0.86 + tileHash(i, seed, 3) * 0.18,
        flip: tileHash(i, seed, 4) > 0.5,
        className: `is-scene-frame is-frame-${edge}`,
      }));
    }
  }
  parts.push(environmentCellAt('boulder-large', viewport.originX - 5, viewport.originY + 82, {
    scale: 1.05, opacity: 0.96, className: 'is-scene-frame',
  }));
  parts.push(environmentCellAt('fallen-hollow-log', viewport.originX + 92, viewport.sceneHeight + 4, {
    scale: 0.9, flip: true, opacity: 0.96, className: 'is-scene-frame',
  }));
  return parts.join('');
}

/** Non-playable woodland surrounds, but never changes, the tactical field. */
export function candidate01SceneFrameMarkup(
  { levelId, map, viewport }: BattleSceneContext,
): SceneFrameMarkup {
  // Every level of this campaign carries the pack's board style, painted scene or
  // not: an atlas tile and a unit figure wear its shadows even where no scenery was
  // authored, and a stylesheet is not in the room when markup becomes a texture.
  if (!usesTwinHillsComposition(levelId)) {
    return { backdrop: CANDIDATE_01_BOARD_STYLE, foreground: '' };
  }
  const fieldX = viewport.originX - 24;
  const fieldY = viewport.originY - 18;
  return {
    backdrop: `${CANDIDATE_01_BOARD_STYLE}<g class="candidate-scene-backdrop" pointer-events="none" data-scene-viewport="authored-wide">
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
export function candidate01MapSceneryLayers(
  { content, levelId, map }: BattleSceneContext,
): SceneLayers {
  if (!usesTwinHillsComposition(levelId)) return { ground: [], underUnits: [], overUnits: [] };
  return {
    ground: twinHillsGroundPieces(content, map),
    underUnits: twinHillsUnderUnitPieces(map),
    overUnits: authoredPlacementPieces('c01-01', 'over-units'),
  };
}
