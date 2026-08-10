import { tileHash } from '../core/grid';
import type { GameMap, TerrainId } from '../core/types';
import { Terrains } from '../core/data/terrain';
import { idx } from '../core/grid';
import { PAL } from './palette';

/** Tile edge length in SVG user units. The board scales via viewBox. */
export const TILE = 32;

interface TileContext {
  x: number;
  y: number;
  /** Owner colour for buildings; undefined = neutral. */
  ownerColor?: string;
  /** Same-terrain neighbours, used to knit roads/water together. */
  linked: { n: boolean; e: boolean; s: boolean; w: boolean };
}

type Painter = (ctx: TileContext) => string;

const r2 = (n: number) => Math.round(n * 100) / 100;

/* --------------------------------------------------------------- primitives */

const grassBase = (x: number, y: number): string => {
  const h = tileHash(x, y, 1);
  let out = `<rect width="32" height="32" fill="${PAL.grass}"/>`;
  // A couple of deterministic tufts so large fields do not look flat.
  const tufts = h < 0.45 ? 2 : h < 0.8 ? 3 : 1;
  for (let i = 0; i < tufts; i++) {
    const tx = r2(3 + tileHash(x, y, 10 + i) * 26);
    const ty = r2(4 + tileHash(x, y, 20 + i) * 24);
    out += `<path d="M${tx} ${ty}q1.6-3.4 3.2 0" stroke="${PAL.grassDark}" stroke-width="1.2" fill="none" stroke-linecap="round"/>`;
  }
  return out;
};

const roadBand = (ctx: TileContext, color: string, edge: string): string => {
  const { linked } = ctx;
  const w = 12;
  const a = (32 - w) / 2;
  let out = '';
  const any = linked.n || linked.e || linked.s || linked.w;
  // Centre patch keeps junctions and dead ends solid.
  out += `<rect x="${a}" y="${a}" width="${w}" height="${w}" fill="${color}"/>`;
  if (linked.n || !any) out += `<rect x="${a}" y="0" width="${w}" height="${a + 1}" fill="${color}"/>`;
  if (linked.s || !any) out += `<rect x="${a}" y="${a + w - 1}" width="${w}" height="${a + 1}" fill="${color}"/>`;
  if (linked.w || !any) out += `<rect x="0" y="${a}" width="${a + 1}" height="${w}" fill="${color}"/>`;
  if (linked.e || !any) out += `<rect x="${a + w - 1}" y="${a}" width="${a + 1}" height="${w}" fill="${color}"/>`;
  out += `<rect x="${a}" y="${a}" width="${w}" height="${w}" fill="none" stroke="${edge}" stroke-width="0.6" opacity="0.35"/>`;
  return out;
};

/* ------------------------------------------------------------------ tiles */

const painters: Record<TerrainId, Painter> = {
  plain: ({ x, y }) => grassBase(x, y),

  road: (ctx) => grassBase(ctx.x, ctx.y) + roadBand(ctx, PAL.dirt, PAL.dirtDark),

  bridge: (ctx) => {
    const base = `<rect width="32" height="32" fill="${PAL.water}"/>
      <path d="M0 8h32M0 22h32" stroke="${PAL.waterLight}" stroke-width="1.4" opacity="0.5"/>`;
    const horizontal = ctx.linked.w || ctx.linked.e;
    const planks = horizontal
      ? [8, 13, 18, 23].map((px) => `<rect x="${px}" y="6" width="3" height="20" fill="${PAL.woodDark}" opacity="0.55"/>`).join('')
      : [8, 13, 18, 23].map((py) => `<rect x="6" y="${py}" width="20" height="3" fill="${PAL.woodDark}" opacity="0.55"/>`).join('');
    const deck = horizontal
      ? `<rect x="0" y="7" width="32" height="18" fill="${PAL.wood}"/>`
      : `<rect x="7" y="0" width="18" height="32" fill="${PAL.wood}"/>`;
    const rails = horizontal
      ? `<rect x="0" y="6" width="32" height="1.6" fill="${PAL.woodDark}"/><rect x="0" y="24.4" width="32" height="1.6" fill="${PAL.woodDark}"/>`
      : `<rect x="6" y="0" width="1.6" height="32" fill="${PAL.woodDark}"/><rect x="24.4" y="0" width="1.6" height="32" fill="${PAL.woodDark}"/>`;
    return base + deck + planks + rails;
  },

  forest: ({ x, y }) => {
    let out = grassBase(x, y);
    const trees: [number, number, number][] = [
      [9, 20, 7],
      [22, 17, 6],
      [15, 25, 5.5],
    ];
    const jitter = tileHash(x, y, 3);
    trees.forEach(([tx, ty, r], i) => {
      const dx = (tileHash(x, y, 30 + i) - 0.5) * 3;
      const dy = (tileHash(x, y, 40 + i) - 0.5) * 2;
      const cx = r2(tx + dx);
      const cy = r2(ty + dy);
      out += `<rect x="${r2(cx - 1.2)}" y="${r2(cy - 1)}" width="2.4" height="${r2(r * 0.8)}" rx="1" fill="${PAL.trunk}"/>`;
      out += `<circle cx="${cx}" cy="${r2(cy - r * 0.7)}" r="${r2(r)}" fill="${jitter > 0.5 ? PAL.leaf : PAL.leafDark}"/>`;
      out += `<circle cx="${r2(cx - r * 0.35)}" cy="${r2(cy - r * 0.95)}" r="${r2(r * 0.62)}" fill="${PAL.leafLight}" opacity="0.85"/>`;
    });
    return out;
  },

  hill: ({ x, y }) => {
    const flip = tileHash(x, y, 5) > 0.5;
    return (
      grassBase(x, y) +
      `<path d="M2 26q6-14 14-14t14 14z" fill="${PAL.grassLight}"/>
       <path d="M2 26q6-14 14-14 4 0 7 4-8 1-12 10z" fill="${PAL.grassDark}" opacity="${flip ? 0.5 : 0.3}"/>
       <path d="M8 24q4-6 8-6" stroke="${PAL.grassDark}" stroke-width="1" fill="none" opacity="0.5"/>`
    );
  },

  mountain: ({ x, y }) => {
    const tall = tileHash(x, y, 7) > 0.5;
    const peak = tall ? 4 : 7;
    return (
      grassBase(x, y) +
      `<path d="M1 29 12 ${peak + 3} 18 15 24 ${peak} 31 29z" fill="${PAL.rock}"/>
       <path d="M24 ${peak} 31 29 22 29z" fill="${PAL.rockDark}"/>
       <path d="M12 ${peak + 3} 17 16 8 22z" fill="${PAL.rockDark}" opacity="0.7"/>
       <path d="M24 ${peak} 27 ${peak + 5} 21 ${peak + 5}z" fill="${PAL.rockLight}"/>
       <path d="M12 ${peak + 3} 14.5 ${peak + 7} 9.5 ${peak + 7}z" fill="${PAL.rockLight}" opacity="0.8"/>`
    );
  },

  water: ({ x, y }) => {
    const h = tileHash(x, y, 9);
    let out = `<rect width="32" height="32" fill="${PAL.water}"/>
      <rect width="32" height="32" fill="${PAL.waterDark}" opacity="${r2(0.15 + h * 0.2)}"/>`;
    for (let i = 0; i < 2; i++) {
      const wy = r2(7 + i * 12 + tileHash(x, y, 50 + i) * 5);
      const wx = r2(3 + tileHash(x, y, 60 + i) * 14);
      out += `<path d="M${wx} ${wy}q3-2.2 6 0t6 0" stroke="${PAL.waterLight}" stroke-width="1.3" fill="none" opacity="0.7" stroke-linecap="round"/>`;
    }
    return out;
  },

  wall: ({ x, y, linked }) => {
    let out = `<rect width="32" height="32" fill="${PAL.stoneDark}"/>
      <rect x="1" y="1" width="30" height="30" fill="${PAL.stone}"/>`;
    // Brick courses, offset every other row.
    for (let row = 0; row < 4; row++) {
      const yy = 2 + row * 7.5;
      out += `<line x1="1" y1="${yy}" x2="31" y2="${yy}" stroke="${PAL.stoneDark}" stroke-width="0.9" opacity="0.6"/>`;
      const off = row % 2 === 0 ? 8 : 16;
      out += `<line x1="${off}" y1="${yy}" x2="${off}" y2="${yy + 7.5}" stroke="${PAL.stoneDark}" stroke-width="0.9" opacity="0.6"/>`;
      out += `<line x1="${off + 12}" y1="${yy}" x2="${off + 12}" y2="${yy + 7.5}" stroke="${PAL.stoneDark}" stroke-width="0.9" opacity="0.5"/>`;
    }
    if (!linked.n) {
      // Battlements on the exposed top edge.
      out += `<g fill="${PAL.stoneLight}">${[1, 9, 17, 25]
        .map((bx) => `<rect x="${bx}" y="0" width="6" height="4"/>`)
        .join('')}</g>`;
    }
    if (tileHash(x, y, 11) > 0.7) {
      out += `<circle cx="24" cy="20" r="1.6" fill="${PAL.stoneDark}" opacity="0.5"/>`;
    }
    return out;
  },

  village: ({ x, y, ownerColor }) => {
    const flag = ownerColor ?? PAL.neutral;
    const mirrored = tileHash(x, y, 13) > 0.5;
    const body = `
      <path d="M6 28V16l10-7 10 7v12z" fill="${PAL.plaster}"/>
      <path d="M4 17 16 8l12 9-1.6 2.2L16 11.2 5.6 19.2z" fill="${PAL.roof}"/>
      <path d="M16 8 28 17l-1.6 2.2L16 11.2z" fill="${PAL.roofDark}"/>
      <rect x="13" y="20" width="6" height="8" fill="${PAL.woodDark}"/>
      <rect x="8.5" y="18" width="4" height="4" fill="${PAL.wood}" opacity="0.8"/>
      <rect x="19.5" y="18" width="4" height="4" fill="${PAL.wood}" opacity="0.8"/>
      <path d="M6 28h20" stroke="${PAL.stoneDark}" stroke-width="1" opacity="0.4"/>`;
    const banner = `
      <rect x="25" y="6" width="1.4" height="14" fill="${PAL.woodDark}"/>
      <path d="M26.4 6.5h5l-1.6 2.4 1.6 2.4h-5z" fill="${flag}"/>`;
    return (
      grassBase(x, y) +
      `<g transform="${mirrored ? 'translate(32,0) scale(-1,1)' : ''}">${body}</g>` +
      banner
    );
  },

  barracks: ({ x, y, ownerColor }) => {
    const flag = ownerColor ?? PAL.neutral;
    return (
      grassBase(x, y) +
      `<path d="M3 29V14h26v15z" fill="${PAL.stone}"/>
       <path d="M1 15 16 6l15 9-1.5 2.4L16 9.2 2.5 17.4z" fill="${PAL.woodDark}"/>
       <rect x="3" y="14" width="26" height="2" fill="${PAL.stoneDark}" opacity="0.5"/>
       <path d="M12 29v-8a4 4 0 0 1 8 0v8z" fill="${PAL.ink}" opacity="0.75"/>
       <rect x="5.5" y="18" width="4.5" height="4.5" fill="${PAL.stoneDark}" opacity="0.8"/>
       <rect x="22" y="18" width="4.5" height="4.5" fill="${PAL.stoneDark}" opacity="0.8"/>
       <g>
         <rect x="24.6" y="3" width="1.4" height="12" fill="${PAL.woodDark}"/>
         <path d="M26 3.5h5.5l-1.7 2.6 1.7 2.6H26z" fill="${flag}"/>
       </g>
       <path d="M8 29h16" stroke="${PAL.stoneDark}" stroke-width="1" opacity="0.4"/>`
    );
  },

  castle: ({ x, y, ownerColor }) => {
    const flag = ownerColor ?? PAL.neutral;
    const merlon = (bx: number, by: number) =>
      `<rect x="${bx}" y="${by}" width="3" height="3.4" fill="${PAL.stoneLight}"/>`;
    return (
      grassBase(x, y) +
      `<path d="M4 30V12h24v18z" fill="${PAL.stone}"/>
       <path d="M4 12h24v3H4z" fill="${PAL.stoneDark}" opacity="0.4"/>
       <path d="M2 30V9h6v21z" fill="${PAL.stoneLight}"/>
       <path d="M24 30V9h6v21z" fill="${PAL.stoneLight}"/>
       <path d="M24 9h6v21h-2V9z" fill="${PAL.stoneDark}" opacity="0.25"/>
       ${merlon(2, 6)}${merlon(5.2, 6)}${merlon(24, 6)}${merlon(27.2, 6)}
       ${merlon(9.5, 9.2)}${merlon(14.5, 9.2)}${merlon(19.5, 9.2)}
       <path d="M12 30v-9a4 4 0 0 1 8 0v9z" fill="${PAL.ink}" opacity="0.8"/>
       <path d="M13.6 30v-8.4a2.4 2.4 0 0 1 4.8 0V30z" fill="${PAL.woodDark}"/>
       <rect x="3.4" y="16" width="3.2" height="4.5" fill="${PAL.stoneDark}" opacity="0.75"/>
       <rect x="25.4" y="16" width="3.2" height="4.5" fill="${PAL.stoneDark}" opacity="0.75"/>
       <rect x="15.4" y="1" width="1.3" height="9" fill="${PAL.woodDark}"/>
       <path d="M16.7 1.4h6l-1.8 2.6 1.8 2.6h-6z" fill="${flag}"/>
       <path d="M2 30h28" stroke="${PAL.stoneDark}" stroke-width="1.2" opacity="0.4"/>`
    );
  },
};

/* -------------------------------------------------------------- public API */

export function terrainMarkup(id: TerrainId, ctx: TileContext): string {
  const painter = painters[id] ?? painters.plain;
  return painter(ctx);
}

/** True when the neighbour should visually connect to this tile. */
function links(map: GameMap, id: TerrainId, x: number, y: number): boolean {
  if (x < 0 || y < 0 || x >= map.width || y >= map.height) return false;
  const other = map.tiles[idx(map, x, y)];
  if (other === id) return true;
  const a = Terrains.get(id);
  const b = Terrains.get(other);
  const roadish = (t: typeof a) => t.tags.includes('road') || t.tags.includes('building');
  if (a.tags.includes('road') && roadish(b)) return true;
  if (a.tags.includes('blocking') && b.tags.includes('blocking')) return true;
  return false;
}

/**
 * Renders the whole static terrain layer as one markup string. Building tiles
 * take the owner colour so flags flip the instant a town changes hands.
 */
export function terrainLayerMarkup(
  map: GameMap,
  colorOfPlayer: (id: number) => string | undefined,
): string {
  const parts: string[] = [];
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      const i = idx(map, x, y);
      const id = map.tiles[i];
      const ctx: TileContext = {
        x,
        y,
        ownerColor: colorOfPlayer(map.owners[i]),
        linked: {
          n: links(map, id, x, y - 1),
          e: links(map, id, x + 1, y),
          s: links(map, id, x, y + 1),
          w: links(map, id, x - 1, y),
        },
      };
      parts.push(
        `<g transform="translate(${x * TILE},${y * TILE})" data-tile="${x},${y}">${terrainMarkup(id, ctx)}</g>`,
      );
    }
  }
  return parts.join('');
}

/** Single tile preview, e.g. for the editor palette. */
export function terrainSwatch(id: TerrainId, ownerColor?: string): string {
  return `<svg viewBox="0 0 32 32" width="32" height="32" shape-rendering="geometricPrecision">${terrainMarkup(
    id,
    { x: 3, y: 5, ownerColor, linked: { n: false, e: false, s: false, w: false } },
  )}</svg>`;
}
