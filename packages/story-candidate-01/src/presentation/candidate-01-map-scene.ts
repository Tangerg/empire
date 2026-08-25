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
  runtimeTileMarkup,
  wholeField,
  type RuntimeTileFit,
  type BoardPiece,
  type BattleSceneContext,
} from '@empire/game-ui';
import { CANDIDATE_01_BOARD_STYLE } from './candidate-01-board-style';
import {
  CANDIDATE_FIELD_BASE,
  CANDIDATE_FOREST_FLOOR,
  CANDIDATE_FOUNDATIONS,
  CANDIDATE_CROSSINGS,
  CANDIDATE_FRAME_TREES,
  CANDIDATE_RIVER_STONES,
  CANDIDATE_SETTLEMENT_LIFE,
  CANDIDATE_SETTLEMENT_TAGS,
  CANDIDATE_SHORE,
  CANDIDATE_SHORE_DECALS,
  CANDIDATE_SURFACE_FIT,
  candidateMaterial,
  type CandidateConnected,
  type CandidateMaterial,
} from './candidate-01-terrain-materials';
import type {
  SceneFrameMarkup,
  SceneLayers,
  SceneViewport,
  SceneViewportProfile,
} from '@empire/game-ui';

const TILE = 32;

/** The four cells a connection mask counts, in the order the kit numbers them. */
const ORTHOGONAL = [[0, -1], [1, 0], [0, 1], [-1, 0]] as const;

/** Where a cell's art goes: the cell's own origin. */
const cellOrigin = (x: number, y: number): { x: number; y: number } => ({ x: x * TILE, y: y * TILE });

const tileAt = (map: GameMap, x: number, y: number): TerrainId | null =>
  x < 0 || y < 0 || x >= map.width || y >= map.height ? null : map.tiles[y * map.width + x];

const isRoute = (content: ContentCatalog, map: GameMap, x: number, y: number): boolean => {
  const id = tileAt(map, x, y);
  if (id === null) return false;
  const tags = content.terrains.get(id).tags;
  return tags.includes('road') || tags.includes('building') || tags.includes('outpost');
};

/**
 * The visual map is wide, while the deterministic board remains a square lattice.
 *
 * Every level, not one. These insets are the band the woodland frame stands in, and
 * the frame is what makes a battlefield a place rather than a rectangle of tiles.
 * It used to be granted to chapter one and the experience lab by level id.
 */
export function candidate01SceneProfile(): SceneViewportProfile {
  return { insets: { top: 62, right: 74, bottom: 74, left: 74 } };
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
function atlasCellMarkup(
  atlasId: string,
  cell: number,
  { className, fit }: { className?: string; fit?: RuntimeTileFit } = {},
): string {
  const atlas = CANDIDATE_01_ENVIRONMENT.atlas(atlasId);
  // A tile, not a cell: the ground is continuous, so each one spills past its own
  // box and the seam between two of them is covered twice.
  const figure = runtimeTileMarkup(atlas.raster, cell, fit ?? { bleed: 0.5 });
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
  /**
   * This module lies across the ground rather than standing on it.
   *
   * A tree, a rock and a granary have a foot, and the kit declares where it is —
   * place them by it and they stand where you put them. A bridge deck has no foot:
   * it spans a channel, and its own middle is what has to land on the middle of the
   * crossing. Placed by its declared anchor instead, a 96-unit deck hangs three
   * rows up the map from the road it carries.
   */
  readonly lying?: boolean;
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
  const { scale = 1, flip = false, opacity = 1, className = '', lying = false } = placing;
  const record = CANDIDATE_01_ENVIRONMENT.cell(id);
  const anchor = lying
    ? [record.atlas.cellWidth / 2, record.atlas.cellHeight / 2]
    : record.cell.anchor ?? record.atlas.anchor ?? [record.atlas.cellWidth / 2, record.atlas.cellHeight];
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

/**
 * The ground of one cell: one tone of one material.
 *
 * *One* tone, chosen by the material. A surface sheet's four cells are not four
 * shades of one ground — the meadow's are flowering grass, bare dirt, grass over
 * stones and a blue-flowered patch. Choosing between them per cell reads as a
 * checkerboard; choosing per 4×3 block, which is what this did, reads as a
 * lattice, because the block boundaries continue across the whole field.
 * Staggering the blocks only bends the lines. They are materials, so the material
 * table picks one, and the variation on the ground comes from the decals — an
 * outline with no straight edge cannot line up with its neighbour into anything.
 *
 * One orientation, too. Mirroring the tile per cell was an attempt to break the
 * repeat for free, and it costs the same thing the four tones cost: the cell is
 * not uniform, so its bright corner lands somewhere different in each of the four
 * orientations and the field reads as a checker again. A plain repeat of soft
 * ground is quieter than any arrangement of variety at cell resolution.
 */
const surfaceMarkup = (atlasId: string, tone: number): string =>
  atlasCellMarkup(atlasId, tone, { fit: CANDIDATE_SURFACE_FIT });

/** Does this cell hold water? A crossing does: the river runs under its deck. */
const isWater = (content: ContentCatalog, map: GameMap, x: number, y: number): boolean => {
  const id = tileAt(map, x, y);
  return id !== null && candidateMaterial(content, id).connected?.mask === 'same';
};

/**
 * Is this dry ground that touches water?
 *
 * The band of mud a river leaves. Not a property of any terrain — 平原 next to a
 * river is the same 平原 — so it is the scene's rule, like a wood's edge and the
 * ground beside a settlement.
 */
const isShore = (content: ContentCatalog, map: GameMap, x: number, y: number): boolean =>
  !isWater(content, map, x, y)
  && ORTHOGONAL.some(([dx, dy]) => isWater(content, map, x + dx, y + dy));

/** The eight-neighbour mask the shore band is indexed by. */
function shoreMask(content: ContentCatalog, map: GameMap, x: number, y: number): number {
  const bank = (dx: number, dy: number): boolean => isShore(content, map, x + dx, y + dy);
  return (bank(0, -1) ? 1 : 0)
    | (bank(1, -1) ? 2 : 0)
    | (bank(1, 0) ? 4 : 0)
    | (bank(1, 1) ? 8 : 0)
    | (bank(0, 1) ? 16 : 0)
    | (bank(-1, 1) ? 32 : 0)
    | (bank(-1, 0) ? 64 : 0)
    | (bank(-1, -1) ? 128 : 0);
}

/** One field the whole map is read through: what each cell is made of. */
type MaterialField = (x: number, y: number) => CandidateMaterial | null;

const materialField = (content: ContentCatalog, map: GameMap): MaterialField => (x, y) => {
  const id = tileAt(map, x, y);
  return id === null ? null : candidateMaterial(content, id);
};

/** The eight-neighbour mask a blob transition sheet is indexed by. */
function blendMask(field: MaterialField, sheet: string, x: number, y: number): number {
  const same = (dx: number, dy: number): boolean => field(x + dx, y + dy)?.blend === sheet;
  return (same(0, -1) ? 1 : 0)
    | (same(1, -1) ? 2 : 0)
    | (same(1, 0) ? 4 : 0)
    | (same(1, 1) ? 8 : 0)
    | (same(0, 1) ? 16 : 0)
    | (same(-1, 1) ? 32 : 0)
    | (same(-1, 0) ? 64 : 0)
    | (same(-1, -1) ? 128 : 0);
}

/** The four-neighbour mask a connected sheet is indexed by. */
function connectedMask(
  content: ContentCatalog,
  field: MaterialField,
  map: GameMap,
  connected: CandidateConnected,
  x: number,
  y: number,
): number {
  const linked = connected.mask === 'route'
    ? (dx: number, dy: number): boolean => isRoute(content, map, x + dx, y + dy)
    : (dx: number, dy: number): boolean => field(x + dx, y + dy)?.connected?.atlas === connected.atlas;
  return (linked(0, -1) ? 1 : 0) | (linked(1, 0) ? 2 : 0) | (linked(0, 1) ? 4 : 0) | (linked(-1, 0) ? 8 : 0);
}

/**
 * The ground of every cell, composed from the kit the campaign ships.
 *
 * Four passes concatenated at the end, because within one layer the order of the
 * pieces *is* the depth: the surface, then the blob transitions that soften its
 * seams, then the connected roads and waters, then the loose detail on top.
 *
 * It used to run on chapter one alone. Everything below reads the material table,
 * so a level this pack has never seen — a built-in map, a level somebody drew in
 * the editor — is painted by the same four passes.
 */
function groundPieces(content: ContentCatalog, map: GameMap): BoardPiece[] {
  const field = materialField(content, map);
  const surfaces: BoardPiece[] = [];
  const blends: BoardPiece[] = [];
  const connections: BoardPiece[] = [];
  const detail: BoardPiece[] = [];
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      const at = cellOrigin(x, y);
      const id = tileAt(map, x, y);
      if (id === null) continue;
      const material = candidateMaterial(content, id);

      surfaces.push({
        markup: surfaceMarkup(material.surface ?? CANDIDATE_FIELD_BASE, material.tone ?? 0),
        ...at,
      });

      if (material.blend) {
        const cell = CANDIDATE_01_ENVIRONMENT.blobIndex(material.blend, blendMask(field, material.blend, x, y));
        blends.push({ markup: atlasCellMarkup(material.blend, cell), ...at });
      }

      // The mud a river leaves, before the water is drawn over it.
      if (material.blend === undefined && isShore(content, map, x, y)) {
        const cell = CANDIDATE_01_ENVIRONMENT.blobIndex(
          CANDIDATE_SHORE,
          shoreMask(content, map, x, y),
        );
        blends.push({ markup: atlasCellMarkup(CANDIDATE_SHORE, cell), ...at });
      }

      if (material.connected) {
        const mask = connectedMask(content, field, map, material.connected, x, y);
        const variant = Math.floor(tileHash(x, y, 1102) * 4);
        const cell = CANDIDATE_01_ENVIRONMENT.connectedIndex(material.connected.atlas, mask, variant);
        // These two classes are the only ones in this layer a stylesheet reads.
        connections.push({
          markup: atlasCellMarkup(material.connected.atlas, cell, { className: 'candidate-ground-route' }),
          ...at,
        });
        if (material.connected.edge) {
          connections.push({
            markup: atlasCellMarkup(material.connected.edge, cell, { className: 'candidate-ground-route-edge' }),
            ...at,
          });
        }
      }

      const foundation = CANDIDATE_FOUNDATIONS[id];
      if (foundation) {
        detail.push({
          markup: environmentCellMarkup(foundation, { scale: 0.3 }),
          x: (x + 0.5) * TILE,
          y: (y + 1) * TILE,
        });
      }

      /*
       * A bank is dressed as a bank, and a channel has rocks in it.
       *
       * The material's own decals lose to these on the cells where they apply: a
       * river's edge is mud and roots whatever the terrain beside it is, and a
       * river with nothing in it is a blue ribbon.
       */
      const decals = isWater(content, map, x, y)
        ? CANDIDATE_RIVER_STONES
        : isShore(content, map, x, y) ? CANDIDATE_SHORE_DECALS : material.decals;
      if (decals && tileHash(x, y, 1103) < decals.chance) {
        const pick = decals.ids[Math.floor(tileHash(x, y, 1104) * decals.ids.length)];
        detail.push({
          markup: environmentCellMarkup(pick, {
            scale: decals.scale,
            flip: tileHash(x, y, 1105) > 0.5,
            opacity: 0.72,
          }),
          x: (x + 0.5) * TILE,
          y: (y + 1) * TILE,
        });
      }
    }
  }
  return [...surfaces, ...blends, ...connections, ...detail];
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

/**
 * The deck spanning one crossing, drawn once at the run's near end.
 *
 * A crossing is as many cells as the channel is wide, and the kit's deck is one
 * 96-unit module for the whole span. So the module is placed by the *run*: the
 * cell that starts it draws it, centred across every cell it covers, and the rest
 * of the run draws nothing.
 */
function crossing(
  content: ContentCatalog,
  map: GameMap,
  parts: BoardPiece[],
  x: number,
  y: number,
): void {
  const deck = (dx: number, dy: number): boolean => tileAt(map, x + dx, y + dy) === 'bridge';
  // Which way it carries: what a traveller could reach from it, then how the run lies.
  const alongX = isRoute(content, map, x - 1, y) || isRoute(content, map, x + 1, y)
    ? true
    : !(isRoute(content, map, x, y - 1) || isRoute(content, map, x, y + 1)) && (deck(-1, 0) || deck(1, 0));
  // Only the near end of the run draws, so a two-cell crossing is one bridge.
  if (alongX ? deck(-1, 0) : deck(0, -1)) return;
  let length = 1;
  while (alongX ? deck(length, 0) : deck(0, length)) length += 1;

  const decks = alongX ? CANDIDATE_CROSSINGS.alongX : CANDIDATE_CROSSINGS.alongY;
  const pick = decks[Math.floor(tileHash(x, y, 1131) * decks.length)];
  const middle = (span: number) => (span + length / 2) * TILE;
  // The middle of the run, both ways: a deck lies across the channel.
  parts.push({
    markup: environmentCellMarkup(pick, { className: 'is-standing', lying: true }),
    x: alongX ? middle(x) : (x + 0.5) * TILE,
    y: alongX ? (y + 0.5) * TILE : middle(y),
  });
}

/** Does open ground here sit next to somewhere people live? */
function neighboursSettlement(content: ContentCatalog, map: GameMap, x: number, y: number): boolean {
  return ORTHOGONAL.some(([dx, dy]) => {
    const id = tileAt(map, x + dx, y + dy);
    if (id === null) return false;
    const tags = content.terrains.get(id).tags;
    return CANDIDATE_SETTLEMENT_TAGS.some((tag) => tags.includes(tag));
  });
}

/**
 * What stands on a cell: a tree, a rock, a wall, a haystack, a bridge deck.
 *
 * Two rules earn their place here beyond the material table.
 *
 * A wood shows its shape at its edge. A canopy on every forest cell hides the
 * units standing in the wood, which is the one thing a tactical map may not do —
 * so the trees go where the wood ends and the inside gets ferns and stumps. The
 * old rule asked whether the cell was near the *map's* border, which is a
 * different question with the same answer only on chapter one's map.
 *
 * A crossing is drawn along the way it carries. `wood-bridge-horizontal` and
 * `-vertical` are two pictures, and which one a cell wants is decided by the
 * neighbours a traveller could reach from it.
 */
function sceneryPieces(content: ContentCatalog, map: GameMap): BoardPiece[] {
  const field = materialField(content, map);
  const parts: BoardPiece[] = [];
  const stand = (id: string, x: number, y: number, placing: EnvironmentCellPlacing, jitter = 0): void => {
    parts.push({
      markup: environmentCellMarkup(id, placing),
      x: (x + 0.5) * TILE + (tileHash(x, y, 1122) - 0.5) * jitter,
      y: (y + 1.18) * TILE,
    });
  };

  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      const id = tileAt(map, x, y);
      if (id === null) continue;
      const material = candidateMaterial(content, id);

      if (id === 'bridge') {
        crossing(content, map, parts, x, y);
        continue;
      }

      // Inside a wood, understory; at its edge, canopy. Same material on all four
      // sides is what "inside" means.
      const enclosed = material.blend !== undefined
        && ORTHOGONAL.every(([dx, dy]) => field(x + dx, y + dy)?.blend === material.blend);
      const scenery = enclosed
        ? CANDIDATE_FOREST_FLOOR
        // Open ground next to a settlement is where its life is kept, and where
        // its life can be drawn: the scenery layer is over the terrain layer, so a
        // prop on the building's own cell would stand in front of the building.
        : material.scenery ?? (neighboursSettlement(content, map, x, y) ? CANDIDATE_SETTLEMENT_LIFE : undefined);
      if (!scenery) continue;
      if (scenery.chance !== undefined && tileHash(x, y, 1119) > scenery.chance) continue;

      const pick = scenery.ids[Math.floor(tileHash(x, y, 1120) * scenery.ids.length)];
      stand(pick, x, y, {
        scale: scenery.scale + tileHash(x, y, 1121) * 0.12,
        flip: tileHash(x, y, 1123) > 0.52,
        // Undergrowth sits back; anything that stands tall wears the heavier shadow.
        ...(enclosed ? { opacity: 0.9 } : { className: 'is-standing' }),
      }, scenery.jitter ?? 0);
    }
  }
  return parts;
}

/**
 * Two farmers working chapter one's fields.
 *
 * Placed by coordinate, so they belong to the map those coordinates are on — the
 * same thing an authored placement is, written in a different file.
 */
function ambientVillagerPieces(levelId: string): BoardPiece[] {
  if (levelId !== 'c01-01') return [];
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
/**
 * Chapter one's hand-placed dressing, on top of what the material table paints.
 *
 * Authored placements stay level-specific because that is what they are: somebody
 * decided that a hill cap goes *there*. Everything procedural applies everywhere,
 * so a level with no authored scene is dressed rather than bare — which is what
 * fifteen chapters and every built-in level used to be.
 */
const authoredGroundPieces = (levelId: string): BoardPiece[] => [
  ...authoredPlacementPieces(levelId, 'foundation'),
  ...authoredPlacementPieces(levelId, 'ground-decal'),
];

function sceneFrameForestMarkup(viewport: SceneViewport): string {
  const parts: string[] = [];
  const trees = CANDIDATE_FRAME_TREES;
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
  { map, viewport }: BattleSceneContext,
): SceneFrameMarkup {
  // Every level of this campaign carries the pack's board style: an atlas tile and
  // a unit figure wear its shadows. It is declared as the scene's `style` rather
  // than pasted into `backdrop` — a `<style>` in the DOM tree is obeyed by the DOM
  // backend and invisible to every texture the GPU backend bakes, so half of this
  // pack's look existed on one backend only.
  //
  // Every level gets the woodland too — it used to be chapter one's, so fifteen
  // chapters were a rectangle of tiles on a flat page.
  const fieldX = viewport.originX - 24;
  const fieldY = viewport.originY - 18;
  return {
    style: CANDIDATE_01_BOARD_STYLE,
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
export function candidate01MapSceneryLayers(
  { content, levelId, map }: BattleSceneContext,
): SceneLayers {
  return {
    ground: [...groundPieces(content, map), ...authoredGroundPieces(levelId)],
    underUnits: [
      ...sceneryPieces(content, map),
      ...authoredPlacementPieces(levelId, 'under-units'),
      ...ambientVillagerPieces(levelId),
      // A warm wash over the whole field, which is art with no place of its own.
      ...wholeField(`<rect width="${map.width * TILE}" height="${map.height * TILE}" fill="#f3d69a" opacity="0.035"/>`),
    ],
    overUnits: authoredPlacementPieces(levelId, 'over-units'),
  };
}
