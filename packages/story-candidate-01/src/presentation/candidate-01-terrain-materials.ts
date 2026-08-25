import type { ContentCatalog, TerrainId } from '@empire/battle-engine';

/**
 * What the ground of one terrain is made of, in the environment kit's vocabulary.
 *
 * There used to be two answers to this question and a level-id allowlist choosing
 * between them. `CANDIDATE_01_TERRAIN_ART` mapped each terrain to one 4-variant
 * high-density tile — no transitions, no connections, so a field of it came out as
 * a visible grid of stamps — and `terrainGroundPieces` composed the environment
 * builder's surfaces, blob transitions and connected routes properly. The second
 * one ran on chapter one and on nothing else, so fifteen chapters and every
 * built-in level were drawn by the first.
 *
 * That is the whole difference between the campaign's opening map and the rest of
 * it. The kit was always there: eight surfaces, four blob transitions, four routes
 * with edge sheets, four waters, cliff modules, forests, crossings, camps and two
 * hundred props. The pack reached for ten of the thirty-six atlases, because the
 * catalog was filtered by the asset allowlist inside chapter one's authored scene
 * document — one map's dressing list standing in for the pack's own catalog.
 *
 * So there is one owner now: this table says what a terrain is made of, and the
 * scene paints every level from it.
 */

/** The surface a field is painted on before anything is drawn over it. */
export const CANDIDATE_FIELD_BASE = 'surface-meadow';

/**
 * How this kit's surface cells are fitted into the cells they are drawn in.
 *
 * The sheets declare `edgeLocked: true`, which reads as a promise that their four
 * cells tile seamlessly. They do not: every cell carries a darkened border, and
 * tiling one draws that border once per boundary — visible at exactly 1:1 with
 * filtering turned off, so it is in the art and no renderer setting reaches it.
 *
 * Three units off each edge of a 32-unit cell, magnifying what is left by 1.23 to
 * fill the box. Ground this soft can afford the magnification; a road cannot,
 * which is why only the surface pass is fitted this way. The bleed on top of it is
 * for the rasteriser rather than the art — see `RuntimeTileFit`.
 */
export const CANDIDATE_SURFACE_FIT = { inset: 3, bleed: 0.5 } as const;

/** A connected sheet: a road or a watercourse, knitted to its own kind. */
export interface CandidateConnected {
  readonly atlas: string;
  /** The companion sheet that draws this one's edge, when the kit ships one. */
  readonly edge?: string;
  /**
   * What counts as a neighbour.
   *
   * `route` means anything a traveller walks on — a road, a bridge, a building's
   * threshold — so a road runs into a village square instead of stopping a cell
   * short. `same` means this exact material, which is what makes a river a river.
   */
  readonly mask: 'route' | 'same';
}

/** Loose ground detail: flat, drawn in the ground layer, no standing shadow. */
export interface CandidateDecals {
  readonly ids: readonly string[];
  /** Fraction of cells that get one, so a field is dressed and not carpeted. */
  readonly chance: number;
  readonly scale: number;
}

/** Standing scenery: drawn under the units, casting the kit's prop shadow. */
export interface CandidateScenery {
  readonly ids: readonly string[];
  readonly scale: number;
  /** Fraction of cells that get one. Absent means every cell of this material. */
  readonly chance?: number;
  /** Wobble, in scene units, so a row of rocks is not a row. */
  readonly jitter?: number;
}

export interface CandidateMaterial {
  /** A surface tile drawn instead of the field base, for a hard-edged material. */
  readonly surface?: string;
  /**
   * Which cell of that surface sheet, defaulting to the first.
   *
   * A sheet's four cells are four materials, not four shades of one: the meadow's
   * are flowering grass, bare dirt, grass over stones and a blue-flowered patch.
   * So the choice belongs to the terrain, not to a per-cell hash — hashing it
   * turns a field into a patchwork of squares whichever block size you use.
   */
  readonly tone?: number;
  /**
   * A blob sheet that paints this material over the field base with soft edges.
   *
   * The kit's transitions carry their own interior, so a material with a blend
   * needs no surface of its own: base, then blend, and the seam is drawn for you.
   */
  readonly blend?: string;
  readonly connected?: CandidateConnected;
  readonly decals?: CandidateDecals;
  readonly scenery?: CandidateScenery;
}

const FOREST_CANOPY = [
  'oak-ancient',
  'mixed-forest-autumn',
  'mixed-forest-dense',
  'oak-grove-dense',
  'mixed-forest-edge',
  'pine-mature-a',
  'pine-mature-b',
  'pine-cluster',
] as const;

const FOREST_FLOOR = [
  'sapling-rock-cluster',
  'bramble-dark',
  'fern-bed',
  'bramble-berries',
  'stump-low',
  'stump-hollow',
  'forest-floor-cluster',
  'pine-underbrush-a',
  'pine-underbrush-c',
] as const;

const DIRT_ROAD: CandidateConnected = {
  atlas: 'route-dirt-road',
  edge: 'route-edge-dirt-road',
  mask: 'route',
};

const RIVER: CandidateConnected = { atlas: 'water-slow-river', mask: 'same' };

/**
 * The materials this campaign's terrains are made of.
 *
 * Keyed by terrain id, because a region's ground is a story decision: 焦土农田 is
 * bare earth and 王都石街 is old stone, and no tag on either says so. What tags
 * *can* say is handled below, so a content pack this art was not written for still
 * gets painted rather than left blank.
 */
const MATERIALS: Readonly<Partial<Record<TerrainId, CandidateMaterial>>> = {
  plain: {
    // The ground's variation is these, not the surface sheet: an outline with no
    // straight edge cannot line up with its neighbour into a lattice.
    decals: {
      ids: [
        'grass-tuft-a', 'grass-tuft-b', 'grass-tuft-c', 'fallen-leaves',
        'stone-cluster-a', 'stone-cluster-b', 'mud-patch', 'animal-tracks',
        'exposed-roots', 'mossy-boulder',
      ],
      chance: 0.3,
      scale: 0.58,
    },
  },
  road: { connected: DIRT_ROAD, decals: { ids: ['cart-ruts', 'bootprints'], chance: 0.16, scale: 0.55 } },
  forest: {
    blend: 'transition-meadow-forest',
    scenery: { ids: FOREST_CANOPY, scale: 0.74, jitter: 9 },
  },
  hill: {
    // The meadow sheet's third cell is grass over broken stone: a slope, exactly.
    tone: 2,
    decals: { ids: ['stone-cluster-a', 'grass-tuft-c'], chance: 0.3, scale: 0.6 },
    scenery: { ids: ['temperate-hill-cap', 'temperate-low-ledge', 'temperate-talus-foot'], scale: 0.46, jitter: 5 },
  },
  mountain: {
    // Bare scree, not the cobbles of `surface-old-stone` — that sheet is paving,
    // and a mountain paved with it read as a courtyard dropped in a meadow.
    surface: 'surface-wasteland',
    tone: 2,
    scenery: {
      ids: ['temperate-pillar-wide', 'temperate-plateau-cap', 'temperate-mountain-pass', 'temperate-cliff-convex-corner'],
      scale: 0.56,
      jitter: 6,
    },
  },
  water: { connected: RIVER },
  // A crossing keeps the water running under it; the deck is a standing prop.
  bridge: { connected: RIVER },
  wall: {
    surface: 'surface-old-stone',
    scenery: { ids: ['stone-wall-straight', 'stone-wall-corner', 'stone-gatehouse'], scale: 0.38 },
  },
  village: {
    connected: DIRT_ROAD,
    scenery: {
      ids: ['wheat-sheaves', 'stone-well', 'farm-fence-straight', 'grain-sacks-baskets', 'farm-handcart-tools'],
      scale: 0.3,
      chance: 0.5,
      jitter: 7,
    },
  },
  barracks: { connected: DIRT_ROAD },
  castle: { connected: DIRT_ROAD },
  'c01.scorched': {
    blend: 'transition-meadow-earth',
    decals: { ids: ['ash-scorch', 'burned-twigs', 'dry-crack'], chance: 0.34, scale: 0.6 },
  },
  'c01.riverbank': {
    blend: 'transition-meadow-earth',
    decals: { ids: ['mud-patch', 'exposed-roots'], chance: 0.3, scale: 0.6 },
  },
  'c01.street': {
    surface: 'surface-old-stone',
    connected: { atlas: 'route-stone-road', edge: 'route-edge-stone-road', mask: 'route' },
  },
  'c01.oathway': {
    surface: 'surface-old-stone',
    decals: { ids: ['stone-cluster-b', 'stone-cluster-c'], chance: 0.22, scale: 0.55 },
  },
  'c01.forge': {
    surface: 'surface-forge-stone',
    scenery: { ids: ['forge-coal-pile', 'forge-ember-pile', 'forge-charcoal-kiln', 'forge-coal-sacks'], scale: 0.4, chance: 0.4 },
  },
  'c01.graveyard': {
    surface: 'surface-graveyard',
    scenery: { ids: ['grave-cross-cluster', 'grave-signboards', 'grave-dead-tree', 'grave-marker-lantern'], scale: 0.42, chance: 0.55, jitter: 6 },
  },
  'c01.molten': {
    surface: 'surface-wasteland',
    // The one reddish cell of the four: burnt ground, not desert sand.
    tone: 1,
    scenery: { ids: ['forge-ember-pile', 'wasteland-rock-spire'], scale: 0.42, chance: 0.6 },
  },
  'c01.mother-root': {
    blend: 'transition-meadow-forest',
    scenery: { ids: ['oak-ancient', 'oak-grove-dense', 'boulder-ferns'], scale: 0.8, jitter: 8 },
  },
  'c01.outpost': { connected: DIRT_ROAD },
  'c01.field-post': { connected: DIRT_ROAD },
  'c01.keep': { connected: DIRT_ROAD },
};

/**
 * What a terrain this art was not written for is made of.
 *
 * Not a guess: each entry reads one thing the rules already say out loud. A cell
 * a boat crosses is water, a cell a cart runs along is a road, high broken ground
 * is rock. Presentation has to draw *something* — a painter that refuses leaves a
 * hole in the field — so the refusal it owes the caller is to draw plain ground
 * rather than to invent a region.
 */
const BY_TAG: readonly (readonly [string, CandidateMaterial])[] = [
  ['water', { connected: RIVER }],
  ['road', { connected: DIRT_ROAD }],
  ['building', { connected: DIRT_ROAD }],
  ['blocking', { surface: 'surface-old-stone' }],
  ['high', { surface: 'surface-wasteland', tone: 2 }],
  ['urban', { surface: 'surface-old-stone' }],
  ['graveyard', { surface: 'surface-graveyard' }],
  ['forge', { surface: 'surface-forge-stone' }],
  ['molten', { surface: 'surface-wasteland' }],
  ['burned', { blend: 'transition-meadow-earth' }],
  ['wet', { blend: 'transition-meadow-earth' }],
  ['silverwood', { blend: 'transition-meadow-forest' }],
];

/** Plain meadow with nothing on it: what an unrecognised terrain is drawn as. */
const BARE: CandidateMaterial = {};

/** What this cell's ground is made of. Every terrain gets an answer. */
export function candidateMaterial(content: ContentCatalog, id: TerrainId): CandidateMaterial {
  const declared = MATERIALS[id];
  if (declared) return declared;
  const tags = content.terrains.get(id).tags;
  for (const [tag, material] of BY_TAG) if (tags.includes(tag)) return material;
  return BARE;
}

/**
 * Understory rather than canopy, for a forest cell with woodland on every side.
 *
 * A canopy on every forest cell hides the units standing in the wood, which is the
 * one thing a tactical map may not do. The trees go on the edges of a wood, where
 * they read as its shape, and the inside gets ferns and stumps.
 */
export const CANDIDATE_FOREST_FLOOR: CandidateScenery = {
  ids: FOREST_FLOOR,
  scale: 0.58,
  chance: 0.66,
  jitter: 8,
};

/**
 * The decks a crossing is spanned by, and how wide one module is.
 *
 * These are 96-unit modules — three cells — drawn to span a channel bank to bank.
 * Drawing one per bridge *cell* at a third of its size, which is what this pack
 * did, squashes a bridge into an unreadable sliver on the water: the crossings on
 * the shipped valley map were invisible. One module per run of bridge cells,
 * centred on the run, at its own size.
 *
 * Two materials, chosen by a hash of where the crossing is. A crossing's material
 * is not something the rules say, and a valley with three identical planks across
 * it looks built by one carpenter on one afternoon.
 */
export const CANDIDATE_CROSSING_SPAN = 96;

export const CANDIDATE_CROSSINGS = {
  alongX: ['wood-bridge-horizontal', 'stone-bridge-horizontal'],
  alongY: ['wood-bridge-vertical', 'stone-bridge-vertical'],
} as const;

/**
 * The ground where the land meets the water.
 *
 * A river drawn only from the water sheet stops dead at a cell boundary: grass on
 * one side, water on the other, no bank. The kit's earth transition is a blob
 * sheet, so a band of it laid over every cell that touches water reads as the mud
 * a river leaves — which is what the land does next to water on any map, and is
 * therefore the scene's rule rather than a terrain's material.
 */
export const CANDIDATE_SHORE = 'transition-meadow-earth';

/** What washes up on a bank, and what stands out of the shallows. */
export const CANDIDATE_SHORE_DECALS: CandidateDecals = {
  ids: ['mud-patch', 'exposed-roots', 'stone-cluster-a', 'grass-tuft-c'],
  chance: 0.4,
  scale: 0.56,
};

/**
 * Rocks in the channel.
 *
 * A river with nothing in it is a blue ribbon. These are drawn on the water, in
 * the ground layer, so nothing wades over them.
 */
export const CANDIDATE_RIVER_STONES: CandidateDecals = {
  ids: ['stone-cluster-b', 'stone-cluster-c', 'mossy-boulder'],
  chance: 0.22,
  scale: 0.5,
};

/**
 * What stands in the cells *around* a settlement.
 *
 * A keep or a barracks has a painted building on its own cell, and the scenery
 * layer is drawn over the terrain layer — so a prop placed there would stand in
 * front of the thing it is meant to be the life of. It goes in the open ground
 * beside it, which is also where a village keeps its stacks, its fences and its
 * firewood.
 */
export const CANDIDATE_SETTLEMENT_LIFE: CandidateScenery = {
  ids: [
    'wheat-sheaves',
    'wheat-bundles',
    'grain-sacks-baskets',
    'farm-fence-straight',
    'farm-fence-corner',
    'farm-handcart-tools',
    'stone-well',
    'livestock-pen-small',
    'firewood-stack',
    'field-tent-cluster',
    'supply-tent-workyard',
    'abandoned-cart-stop',
  ],
  scale: 0.3,
  chance: 0.42,
  jitter: 7,
};

/** The tags that make a cell a settlement, so its neighbours get dressed. */
export const CANDIDATE_SETTLEMENT_TAGS = ['building', 'outpost'] as const;

/** The conifers that stand outside the field, framing it. */
export const CANDIDATE_FRAME_TREES = [
  'pine-tall',
  'pine-mature-a',
  'pine-mature-b',
  'pine-mature-c',
  'pine-cluster',
  'pine-pair',
  'oak-ancient',
  'mixed-forest-dense',
] as const;

/** The ground a building stands on, so a keep does not sit on open grass. */
export const CANDIDATE_FOUNDATIONS: Readonly<Partial<Record<TerrainId, string>>> = {
  village: 'village-square-foundation',
  barracks: 'gray-camp-ground',
  castle: 'keep-foundation',
  'c01.outpost': 'watch-post-ground',
  'c01.field-post': 'wounded-shelter-ground',
  'c01.keep': 'keep-foundation',
};
