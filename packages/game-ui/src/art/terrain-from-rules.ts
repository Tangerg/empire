import type { TerrainDef } from '@empire/battle-engine';
import { tileHash } from '@empire/battle-engine';
import { GROUND_TONES, PAL, shade } from './palette';
import { nameHash, pick, r2 } from './variation';
import { groundShadow } from './shading';

/**
 * A tile drawn from what the rules can see about it.
 *
 * The generic art layer used to answer an unfamiliar terrain with a specific
 * lie: `painters[id] ?? painters.plain`, so every terrain nobody hand-drew came
 * out as grass. Eleven of the twenty-two terrains this repository ships hit that
 * line, and the map editor — which draws with the generic art — showed the whole
 * campaign's ground as one meadow: molten rock, a graveyard and a keep all
 * indistinguishable from an open field.
 *
 * The fix is not a longer table. A general engine cannot have one, because the
 * next content pack's terrain has not been named yet. What it can do is draw
 * what the rules themselves read: whether anything may enter, whether crossing
 * is slow, whether something stands high enough to block a shot, whether the
 * tile can be held, and whether it produces. Every one of those is a field on
 * `TerrainDef`, and every one of them is something the player needs at a glance.
 *
 * So the picture follows the rules — the same contract the tiling already has
 * with the board. A tile that reads as impassable *is* impassable.
 */

/**
 * What the rules can say about standing on a tile, and nothing else.
 *
 * Deliberately free of movement-class names: `MovementClass` is an open string
 * so a pack may ship hover, phase or burrow, and a painter that looks for
 * `foot` is one game's vocabulary written into the general layer.
 */
interface TerrainReading {
  /** Nothing at all may enter. */
  readonly sealed: boolean;
  /** Fewer than half the ways of moving may cross it, but some can. */
  readonly specialised: boolean;
  /** How much slower than open ground, 0 (free) upward. */
  readonly rough: number;
  /** How tall the thing standing here is, in tile-eighths. */
  readonly raised: number;
  /** Somebody can own it. */
  readonly held: boolean;
  /** Something is built here: it recruits, or losing it loses the battle. */
  readonly works: boolean;
}

const RAISED_BY_COVER = { none: 0, half: 1.6, full: 3.2 } as const;

function readTerrain(terrain: TerrainDef): TerrainReading {
  const costs = Object.values(terrain.cost);
  const passable = costs.filter((cost): cost is number => cost !== null);
  const dearest = passable.length === 0 ? 0 : Math.max(...passable);
  return {
    sealed: costs.length > 0 && passable.length === 0,
    specialised: passable.length > 0 && passable.length * 2 <= costs.length,
    rough: Math.max(0, dearest - 1),
    raised: Math.max(terrain.obstructionHeight, RAISED_BY_COVER[terrain.cover]),
    held: terrain.capturable,
    works: terrain.hq || terrain.produces.length > 0,
  };
}


/** The base tone, chosen by what the tile is and varied by what it is called. */
function toneOf(terrain: TerrainDef, reading: TerrainReading): string {
  const family = reading.sealed
    ? GROUND_TONES.stone
    : reading.specialised
      ? GROUND_TONES.liquid
      : reading.rough > 0
        ? GROUND_TONES.broken
        : GROUND_TONES.open;
  // Denser cover reads as deeper ground, so two tiles of the same family still
  // separate when the rules say one of them is better to stand behind.
  return shade(pick(family, nameHash(terrain.id, 1)), -0.1 * Math.min(1, terrain.defense * 2));
}

/** Speckle that keeps a field of one terrain from reading as a painted rectangle. */
function grain(tone: string, x: number, y: number, seed: number): string {
  const light = shade(tone, 0.16);
  const dark = shade(tone, -0.16);
  const bits = 3 + Math.round(nameHash(String(seed), 2) * 2);
  let out = `<rect width="32" height="32" fill="${tone}"/>
    <path d="M0 ${6 + Math.round(tileHash(x, y, 1) * 6)}h32v4H0z" fill="${light}" opacity="0.14"/>
    <path d="M0 ${23 - Math.round(tileHash(x, y, 2) * 5)}h32v5H0z" fill="${dark}" opacity="0.16"/>`;
  for (let index = 0; index < bits; index++) {
    const tx = Math.round(3 + tileHash(x, y, 10 + index) * 25);
    const ty = Math.round(4 + tileHash(x, y, 20 + index) * 23);
    out += tileHash(x, y, 70 + index) > 0.5
      ? `<rect x="${tx}" y="${ty}" width="2" height="1" fill="${light}" opacity="0.7"/>`
      : `<rect x="${tx}" y="${ty}" width="1" height="1" fill="${dark}" opacity="0.6"/>`;
  }
  return out;
}

/** Ripples: a surface most ways of moving cannot cross. */
function surface(tone: string, x: number, y: number): string {
  const crest = shade(tone, 0.34);
  let out = '';
  for (let index = 0; index < 3; index++) {
    const wy = Math.round(5 + index * 9 + tileHash(x, y, 50 + index) * 3);
    const wx = Math.round(2 + tileHash(x, y, 60 + index) * 15);
    out += `<path d="M${wx} ${wy}h6l2-1h5" stroke="${crest}" stroke-width="1" fill="none" opacity="0.6"/>`;
  }
  return out;
}

/** Rubble and tufts: passable, but it costs you. */
function broken(tone: string, x: number, y: number, rough: number): string {
  const clumps = Math.min(5, 1 + Math.round(rough * 2));
  let out = '';
  for (let index = 0; index < clumps; index++) {
    const cx = r2(4 + tileHash(x, y, 80 + index) * 24);
    const cy = r2(8 + tileHash(x, y, 90 + index) * 18);
    out += `<path d="M${cx} ${r2(cy + 2)}v-3l-1.6 1m1.6-1 1.6 1" stroke="${shade(tone, -0.34)}" stroke-width="1" fill="none" opacity="0.75"/>
      <rect x="${r2(cx - 2)}" y="${r2(cy + 2)}" width="3" height="1" fill="${shade(tone, 0.2)}" opacity="0.5"/>`;
  }
  return out;
}

/** The mass that stands here, sized by how far it reaches above the cell. */
function standing(terrain: TerrainDef, tone: string, reading: TerrainReading): string {
  const { raised, sealed } = reading;
  const height = Math.min(20, 5 + raised * 3.2);
  const lean = nameHash(terrain.id, 3) > 0.5 ? 1 : -1;
  const face = shade(tone, sealed ? 0.22 : 0.12);
  const shadowSide = shade(tone, -0.3);
  const peak = r2(28 - height);
  const crest = r2(16 + lean * 3);
  return groundShadow({ cx: 16, cy: 29 }, { rx: 13, ry: 2.4 }) + `
    <path d="M3 29 ${r2(crest - 7)} ${r2(peak + 5)} ${crest} ${peak} ${r2(crest + 8)} ${r2(peak + 6)} 29 29z"
      fill="${shadowSide}" stroke="${PAL.ink}" stroke-width="0.7" stroke-linejoin="round"/>
    <path d="M${r2(crest - 7)} ${r2(peak + 5)} ${crest} ${peak} ${r2(crest + 2)} 29 ${r2(crest - 10)} 29z" fill="${face}"/>`;
}

/** A roof and a door: something is run from here, and a keep is run from more. */
function works(terrain: TerrainDef): string {
  const left = terrain.hq ? 4 : 7;
  const width = terrain.hq ? 24 : 18;
  const eaves = terrain.hq ? 12 : 15;
  return groundShadow({ cx: 16, cy: 29.5 }, { rx: terrain.hq ? 14 : 11, ry: 2.2 }) + `
    <path d="M${left} 29V${eaves + 3}h${width}V29z" fill="${PAL.plaster}" stroke="${PAL.woodDark}" stroke-width="0.8"/>
    <path d="M${left - 2} ${eaves + 4} 16 ${eaves - 4} ${left + width + 2} ${eaves + 4} ${left + width} ${eaves + 6} 16 ${eaves - 1} ${left} ${eaves + 6}z"
      fill="${PAL.roof}" stroke="${PAL.ink}" stroke-width="0.8" stroke-linejoin="round"/>
    <path d="M16 ${eaves - 4} ${left + width + 2} ${eaves + 4} ${left + width} ${eaves + 6} 16 ${eaves - 1}z" fill="${PAL.roofDark}"/>
    <rect x="13" y="21" width="6" height="8" fill="${PAL.woodDark}"/>
    <rect x="${left + 2}" y="${eaves + 6}" width="4" height="4" fill="#d5ad62" stroke="${PAL.woodDark}" stroke-width="0.7"/>`;
}

/**
 * Whose ground this is, said the way every hand-drawn holding says it.
 *
 * One pole, tall enough to clear a keep's roof and to read on open ground, so
 * the flag does not need to be told what it is planted on.
 */
function banner(flag: string): string {
  return `<rect x="25" y="2" width="1.4" height="20" fill="${PAL.woodDark}"/>
    <path d="M26.4 2.5h5l-1.6 2.4 1.6 2.4h-5z" fill="${flag}" stroke="${PAL.ink}" stroke-width="0.5"/>`;
}

/**
 * The whole tile. Layers are added in the order the rules become visible:
 * ground, what makes it hard to cross, what stands on it, who holds it.
 */
export function terrainFromRules(
  terrain: TerrainDef,
  cell: { x: number; y: number; ownerColor?: string },
): string {
  const reading = readTerrain(terrain);
  const tone = toneOf(terrain, reading);
  let out = grain(tone, cell.x, cell.y, terrain.id.length);
  if (reading.specialised) out += surface(tone, cell.x, cell.y);
  else if (reading.rough > 0) out += broken(tone, cell.x, cell.y, reading.rough);
  if (reading.works) out += works(terrain);
  else if (reading.raised > 0 || reading.sealed) out += standing(terrain, tone, reading);
  if (reading.held) out += banner(cell.ownerColor ?? PAL.neutral);
  return out;
}
