import { tileHash } from '@empire/battle-engine/grid';
import type { GameMap, TerrainId } from '@empire/battle-engine/types';
import type { ContentCatalog } from '@empire/battle-engine';
import { idx } from '@empire/battle-engine/grid';
import { resolveArt } from './ports';
import { PAL } from './palette';

/** Tile edge length in SVG user units. The board scales via viewBox. */
export const TILE = 32;

interface TileContext {
  x: number;
  y: number;
  /** Presentation theme; ignored by mechanics and generic painters. */
  theme?: string;
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
  let out = `<rect width="32" height="32" fill="${PAL.grass}"/>
    <path d="M0 ${7 + Math.round(h * 5)}h32v3H0z" fill="${PAL.grassLight}" opacity="0.08"/>
    <path d="M0 ${24 - Math.round(h * 4)}h32v5H0z" fill="${PAL.grassDark}" opacity="0.08"/>`;
  // Deterministic 1px clusters make fields feel hand-tiled without visual noise.
  const bits = h < 0.35 ? 3 : h < 0.78 ? 4 : 2;
  for (let i = 0; i < bits; i++) {
    const tx = Math.round(3 + tileHash(x, y, 10 + i) * 25);
    const ty = Math.round(4 + tileHash(x, y, 20 + i) * 23);
    const light = tileHash(x, y, 70 + i) > 0.68;
    out += light
      ? `<rect x="${tx}" y="${ty}" width="2" height="1" fill="${PAL.grassLight}" opacity="0.75"/>`
      : `<path d="M${tx} ${ty + 2}v-3m0 2 2-2" stroke="${PAL.grassDark}" stroke-width="1" fill="none"/>`;
  }
  if (tileHash(x, y, 91) > 0.78) {
    const sx = Math.round(5 + tileHash(x, y, 92) * 21);
    const sy = Math.round(6 + tileHash(x, y, 93) * 19);
    out += `<rect x="${sx}" y="${sy}" width="2" height="1" fill="${PAL.rockLight}" opacity="0.7"/>
      <rect x="${sx + 1}" y="${sy + 1}" width="2" height="1" fill="${PAL.rockDark}" opacity="0.35"/>`;
  }
  return out;
};

const roadBand = (ctx: TileContext, color: string, edge: string): string => {
  const { linked } = ctx;
  const w = 14;
  const a = (32 - w) / 2;
  let out = '';
  const any = linked.n || linked.e || linked.s || linked.w;
  // Centre patch keeps junctions and dead ends solid.
  out += `<rect x="${a}" y="${a}" width="${w}" height="${w}" fill="${color}"/>`;
  if (linked.n || !any) out += `<rect x="${a}" y="0" width="${w}" height="${a + 1}" fill="${color}"/>`;
  if (linked.s || !any) out += `<rect x="${a}" y="${a + w - 1}" width="${w}" height="${a + 1}" fill="${color}"/>`;
  if (linked.w || !any) out += `<rect x="0" y="${a}" width="${a + 1}" height="${w}" fill="${color}"/>`;
  if (linked.e || !any) out += `<rect x="${a + w - 1}" y="${a}" width="${a + 1}" height="${w}" fill="${color}"/>`;
  const h = tileHash(ctx.x, ctx.y, 103);
  const sx = Math.round(a + 2 + h * 7);
  const sy = Math.round(a + 2 + tileHash(ctx.x, ctx.y, 104) * 7);
  out += `<path d="M${a} ${a}h${w}v${w}H${a}z" fill="none" stroke="${edge}" stroke-width="1" opacity="0.25"/>
    <rect x="${sx}" y="${sy}" width="3" height="1" fill="${edge}" opacity="0.32"/>
    <rect x="${Math.max(1, sx - 4)}" y="${Math.min(29, sy + 5)}" width="2" height="1" fill="${PAL.stoneLight}" opacity="0.5"/>`;
  return out;
};

const pine = (cx: number, base: number, scale = 1, light = false): string => {
  const trunkW = r2(2.4 * scale);
  const trunkX = r2(cx - trunkW / 2);
  const crown = light ? PAL.leaf : PAL.leafDark;
  const middle = light ? PAL.leafLight : PAL.leaf;
  const top = r2(base - 18 * scale);
  return `<g>
    <ellipse cx="${cx}" cy="${r2(base + 0.8)}" rx="${r2(6 * scale)}" ry="${r2(2 * scale)}" fill="${PAL.ink}" opacity="0.18"/>
    <rect x="${trunkX}" y="${r2(base - 6 * scale)}" width="${trunkW}" height="${r2(7 * scale)}" fill="${PAL.trunk}"/>
    <path d="M${cx} ${top}l${r2(7 * scale)} ${r2(9 * scale)}h-${r2(3 * scale)}l${r2(5 * scale)} ${r2(7 * scale)}H${r2(cx - 9 * scale)}l${r2(5 * scale)}-${r2(7 * scale)}h-${r2(3 * scale)}z" fill="${crown}" stroke="${PAL.ink}" stroke-width="0.65" stroke-linejoin="round"/>
    <path d="M${cx} ${r2(top + 2 * scale)}v${r2(12 * scale)}l-${r2(5 * scale)} ${r2(3 * scale)}h${r2(3 * scale)}l-${r2(3 * scale)} ${r2(3 * scale)}h${r2(5 * scale)}z" fill="${middle}" opacity="0.92"/>
    <rect x="${r2(cx - 1.2 * scale)}" y="${r2(top + 4 * scale)}" width="${r2(1.5 * scale)}" height="${r2(4 * scale)}" fill="${PAL.grassLight}" opacity="0.35"/>
  </g>`;
};

/* ------------------------------------------------------------------ tiles */

const painters: Record<TerrainId, Painter> = {
  plain: ({ x, y }) => grassBase(x, y),

  road: (ctx) => grassBase(ctx.x, ctx.y) + roadBand(ctx, PAL.dirt, PAL.dirtDark),

  bridge: (ctx) => {
    const base = `<rect width="32" height="32" fill="${PAL.water}"/>
      <path d="M1 7h9m5 6h13M2 25h15" stroke="${PAL.waterLight}" stroke-width="1" opacity="0.55"/>`;
    const horizontal = ctx.linked.w || ctx.linked.e;
    const planks = horizontal
      ? [2, 7, 12, 17, 22, 27].map((px) => `<path d="M${px} 7v18" stroke="${PAL.woodDark}" stroke-width="1" opacity="0.52"/>`).join('')
      : [2, 7, 12, 17, 22, 27].map((py) => `<path d="M7 ${py}h18" stroke="${PAL.woodDark}" stroke-width="1" opacity="0.52"/>`).join('');
    const deck = horizontal
      ? `<rect x="0" y="7" width="32" height="18" fill="${PAL.wood}"/><rect x="0" y="8" width="32" height="2" fill="#a87946" opacity="0.7"/>`
      : `<rect x="7" y="0" width="18" height="32" fill="${PAL.wood}"/><rect x="8" y="0" width="2" height="32" fill="#a87946" opacity="0.7"/>`;
    const rails = horizontal
      ? `<rect x="0" y="5" width="32" height="2" fill="${PAL.woodDark}"/><rect x="0" y="25" width="32" height="2" fill="${PAL.woodDark}"/><g fill="${PAL.woodDark}">${[3, 15, 27].map((x) => `<rect x="${x}" y="3" width="2" height="6"/><rect x="${x}" y="23" width="2" height="6"/>`).join('')}</g>`
      : `<rect x="5" y="0" width="2" height="32" fill="${PAL.woodDark}"/><rect x="25" y="0" width="2" height="32" fill="${PAL.woodDark}"/><g fill="${PAL.woodDark}">${[3, 15, 27].map((y) => `<rect x="3" y="${y}" width="6" height="2"/><rect x="23" y="${y}" width="6" height="2"/>`).join('')}</g>`;
    return base + deck + planks + rails;
  },

  forest: ({ x, y }) => {
    let out = grassBase(x, y);
    const trees: [number, number, number][] = [
      [8, 21, 0.82],
      [22, 19, 0.9],
      [15, 27, 0.72],
    ];
    const jitter = tileHash(x, y, 3);
    trees.forEach(([tx, ty, scale], i) => {
      const dx = (tileHash(x, y, 30 + i) - 0.5) * 3;
      const dy = (tileHash(x, y, 40 + i) - 0.5) * 2;
      const cx = r2(tx + dx);
      out += pine(cx, r2(ty + dy), scale, (i + (jitter > 0.5 ? 1 : 0)) % 2 === 0);
    });
    return out;
  },

  hill: ({ x, y }) => {
    const flip = tileHash(x, y, 5) > 0.5;
    return (
      grassBase(x, y) +
      `<ellipse cx="16" cy="27" rx="14" ry="3" fill="${PAL.ink}" opacity="0.12"/>
       <path d="M2 27 5 20l5-6 6-3 7 4 7 12z" fill="${PAL.grassDark}" stroke="${PAL.ink}" stroke-width="0.6"/>
       <path d="M4 25 7 19l5-4 6-1 5 3 4 8z" fill="${PAL.grassLight}"/>
       <path d="M${flip ? 18 : 7} 21h5v2h-5zM${flip ? 10 : 20} 17h3v1h-3z" fill="${PAL.grass}" opacity="0.9"/>
       <path d="M8 25h15" stroke="${PAL.dirtDark}" stroke-width="1" opacity="0.28"/>`
    );
  },

  mountain: ({ x, y }) => {
    const tall = tileHash(x, y, 7) > 0.5;
    const peak = tall ? 4 : 7;
    return (
      grassBase(x, y) +
      `<ellipse cx="16" cy="29" rx="15" ry="2" fill="${PAL.ink}" opacity="0.2"/>
       <path d="M1 29 12 ${peak + 3} 17 15 24 ${peak} 31 29z" fill="${PAL.rock}" stroke="${PAL.ink}" stroke-width="0.8" stroke-linejoin="round"/>
       <path d="M24 ${peak} 31 29 21 29 18 15z" fill="${PAL.rockDark}"/>
       <path d="M12 ${peak + 3} 17 15 12 27 5 29z" fill="#6f716d" opacity="0.85"/>
       <path d="M24 ${peak} 28 ${peak + 7} 24 ${peak + 6} 21 ${peak + 10}z" fill="${PAL.rockLight}"/>
       <path d="M12 ${peak + 3} 15 ${peak + 8} 12 ${peak + 7} 9 ${peak + 11}z" fill="${PAL.rockLight}"/>
       <path d="M7 26h5v1H7zm16-2h4v1h-4z" fill="${PAL.ink}" opacity="0.25"/>`
    );
  },

  water: ({ x, y }) => {
    const h = tileHash(x, y, 9);
    let out = `<rect width="32" height="32" fill="${PAL.water}"/>
      <path d="M0 ${Math.round(6 + h * 4)}h32v5H0zM0 ${Math.round(21 + h * 3)}h32v4H0z" fill="${PAL.waterDark}" opacity="0.18"/>`;
    for (let i = 0; i < 3; i++) {
      const wy = Math.round(5 + i * 9 + tileHash(x, y, 50 + i) * 3);
      const wx = Math.round(2 + tileHash(x, y, 60 + i) * 15);
      out += `<path d="M${wx} ${wy}h6l2-1h5" stroke="${PAL.waterLight}" stroke-width="1" fill="none" opacity="0.72"/>`;
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
      <ellipse cx="16" cy="29" rx="13" ry="2.4" fill="${PAL.ink}" opacity="0.2"/>
      <path d="M6 28V16l10-7 10 7v12z" fill="${PAL.plaster}" stroke="${PAL.woodDark}" stroke-width="0.8"/>
      <path d="M4 17 16 8l12 9-1.6 2.2L16 11.2 5.6 19.2z" fill="${PAL.roof}" stroke="${PAL.ink}" stroke-width="0.8" stroke-linejoin="round"/>
      <path d="M16 8 28 17l-1.6 2.2L16 11.2z" fill="${PAL.roofDark}"/>
      <path d="M8 16h16M10 13h12M7 18h20" stroke="${PAL.roofDark}" stroke-width="0.7" opacity="0.55"/>
      <path d="M7 17v11m18-11v11M7 22h18M16 12v16" stroke="${PAL.woodDark}" stroke-width="1.2" opacity="0.82"/>
      <rect x="13" y="20" width="6" height="8" fill="${PAL.woodDark}"/>
      <rect x="8.5" y="18" width="4" height="4" fill="#d5ad62" stroke="${PAL.woodDark}" stroke-width="0.8"/>
      <path d="M10.5 18v4M8.5 20h4" stroke="${PAL.woodDark}" stroke-width="0.6"/>
      <rect x="19.5" y="18" width="4" height="4" fill="#d5ad62" stroke="${PAL.woodDark}" stroke-width="0.8"/>
      <path d="M21.5 18v4M19.5 20h4" stroke="${PAL.woodDark}" stroke-width="0.6"/>
      <path d="M6 27h20v2H6z" fill="${PAL.stoneDark}"/>
      <rect x="21" y="8" width="3" height="6" fill="${PAL.stoneDark}" stroke="${PAL.ink}" stroke-width="0.7"/>
      <path d="M22 9h2" stroke="${PAL.stoneLight}" stroke-width="0.7"/>`;
    const banner = `
      <rect x="25" y="6" width="1.4" height="14" fill="${PAL.woodDark}"/>
      <path d="M26.4 6.5h5l-1.6 2.4 1.6 2.4h-5z" fill="${flag}" stroke="${PAL.ink}" stroke-width="0.5"/>
      <ellipse cx="28" cy="27" rx="3" ry="1" fill="${PAL.ink}" opacity="0.16"/>
      <rect x="27" y="23" width="3" height="4" rx="0.5" fill="${PAL.wood}" stroke="${PAL.woodDark}" stroke-width="0.6"/>
      <path d="M27 25h3" stroke="${PAL.woodDark}" stroke-width="0.6"/>`;
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
      `<ellipse cx="16" cy="29" rx="15" ry="2.5" fill="${PAL.ink}" opacity="0.22"/>
       <path d="M3 29V14h26v15z" fill="${PAL.stone}" stroke="${PAL.ink}" stroke-width="0.8"/>
       <path d="M1 15 16 6l15 9-1.5 2.4L16 9.2 2.5 17.4z" fill="${PAL.woodDark}" stroke="${PAL.ink}" stroke-width="0.8" stroke-linejoin="round"/>
       <path d="M5 14h22M8 12h16M12 9h8" stroke="${PAL.wood}" stroke-width="1" opacity="0.75"/>
       <rect x="3" y="14" width="26" height="2" fill="${PAL.stoneDark}" opacity="0.75"/>
       <path d="M7 16v13m18-13v13M3 23h26" stroke="${PAL.woodDark}" stroke-width="1" opacity="0.65"/>
       <path d="M12 29v-8a4 4 0 0 1 8 0v8z" fill="${PAL.ink}" opacity="0.75"/>
       <rect x="5" y="18" width="5" height="4" fill="#d1a454" stroke="${PAL.woodDark}" stroke-width="0.8"/>
       <path d="M7.5 18v4M5 20h5" stroke="${PAL.woodDark}" stroke-width="0.6"/>
       <rect x="22" y="18" width="5" height="4" fill="#d1a454" stroke="${PAL.woodDark}" stroke-width="0.8"/>
       <path d="M24.5 18v4M22 20h5" stroke="${PAL.woodDark}" stroke-width="0.6"/>
       <g>
         <rect x="24.6" y="3" width="1.4" height="12" fill="${PAL.woodDark}"/>
         <path d="M26 3.5h5.5l-1.7 2.6 1.7 2.6H26z" fill="${flag}" stroke="${PAL.ink}" stroke-width="0.5"/>
       </g>
       <path d="M3 27h26v2H3z" fill="${PAL.stoneDark}" opacity="0.72"/>`
    );
  },

  castle: ({ x, y, ownerColor }) => {
    const flag = ownerColor ?? PAL.neutral;
    const merlon = (bx: number, by: number) =>
      `<rect x="${bx}" y="${by}" width="3" height="3.4" fill="${PAL.stoneLight}"/>`;
    return (
      grassBase(x, y) +
      `<ellipse cx="16" cy="30" rx="15" ry="2" fill="${PAL.ink}" opacity="0.24"/>
       <path d="M4 30V12h24v18z" fill="${PAL.stone}" stroke="${PAL.ink}" stroke-width="0.8"/>
       <path d="M4 12h24v3H4z" fill="${PAL.stoneDark}" opacity="0.4"/>
       <path d="M2 30V9h6v21z" fill="${PAL.stoneLight}" stroke="${PAL.ink}" stroke-width="0.7"/>
       <path d="M24 30V9h6v21z" fill="${PAL.stoneLight}" stroke="${PAL.ink}" stroke-width="0.7"/>
       <path d="M24 9h6v21h-2V9z" fill="${PAL.stoneDark}" opacity="0.25"/>
       ${merlon(2, 6)}${merlon(5.2, 6)}${merlon(24, 6)}${merlon(27.2, 6)}
       ${merlon(9.5, 9.2)}${merlon(14.5, 9.2)}${merlon(19.5, 9.2)}
       <path d="M12 30v-9a4 4 0 0 1 8 0v9z" fill="${PAL.ink}" opacity="0.8"/>
       <path d="M13.6 30v-8.4a2.4 2.4 0 0 1 4.8 0V30z" fill="${PAL.woodDark}"/>
       <rect x="3.4" y="16" width="3.2" height="4.5" fill="${PAL.stoneDark}" opacity="0.75"/>
       <rect x="25.4" y="16" width="3.2" height="4.5" fill="${PAL.stoneDark}" opacity="0.75"/>
       <path d="M5 23h4m14 0h4M10 16h4m5 0h4M9 26h3m8 0h3" stroke="${PAL.stoneDark}" stroke-width="0.8" opacity="0.58"/>
       <rect x="15.4" y="1" width="1.3" height="9" fill="${PAL.woodDark}"/>
       <path d="M16.7 1.4h6l-1.8 2.6 1.8 2.6h-6z" fill="${flag}" stroke="${PAL.ink}" stroke-width="0.5"/>
       <path d="M2 30h28" stroke="${PAL.stoneDark}" stroke-width="1.2" opacity="0.4"/>`
    );
  },
};

/**
 * Where a tile is drawn, and what shape it is.
 *
 * A port, satisfied by the board's layout: this module paints tiles and has no
 * business knowing how the battlefield is tiled.
 */
export interface TerrainLayout {
  readonly corners: number;
  origin(at: { x: number; y: number }): { x: number; y: number };
  center(at: { x: number; y: number }): { x: number; y: number };
  outline(at: { x: number; y: number }): string;
}

const CELL_CLIP_ID = 'cell-clip';

/**
 * One clip path, in tile-local coordinates, reused by every tile group.
 *
 * The outline is taken from cell (0,0) and shifted to the group's own origin, so
 * a single definition works for all of them — including the staggered rows of a
 * hex board, whose cells all have the same shape.
 */
function cellClipDefinition(layout: TerrainLayout, map: GameMap): string {
  void map;
  const origin = layout.origin({ x: 0, y: 0 });
  const points = layout.outline({ x: 0, y: 0 })
    .split(' ')
    .map((pair) => {
      const [x, y] = pair.split(',').map(Number);
      return `${(x - origin.x).toFixed(2)},${(y - origin.y).toFixed(2)}`;
    })
    .join(' ');
  return `<defs><clipPath id="${CELL_CLIP_ID}" clipPathUnits="userSpaceOnUse"><polygon points="${points}"/></clipPath></defs>`;
}

/* -------------------------------------------------------------- public API */

export function terrainMarkup(id: TerrainId, ctx: TileContext): string {
  const runtime = resolveArt((provider) => provider.terrainMarkup?.(id, ctx));
  if (runtime) return runtime;
  const painter = painters[id] ?? painters.plain;
  return painter(ctx);
}

/** True when the neighbour should visually connect to this tile. */
function links(content: ContentCatalog, map: GameMap, id: TerrainId, x: number, y: number): boolean {
  if (x < 0 || y < 0 || x >= map.width || y >= map.height) return false;
  const other = map.tiles[idx(map, x, y)];
  if (other === id) return true;
  const a = content.terrains.get(id);
  const b = content.terrains.get(other);
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
  layout: TerrainLayout,
  content: ContentCatalog,
  map: GameMap,
  colorOfPlayer: (id: number) => string | undefined,
  theme?: string,
): string {
  const parts: string[] = [];
  // Painted tiles are square pictures. On a tiling whose cells are not, each one
  // is placed at its cell and clipped to that cell's shape, so the same artwork
  // serves a hex board without a second set of tile painters.
  const clip = layout.corners === 4 ? '' : ` clip-path="url(#${CELL_CLIP_ID})"`;
  if (clip) parts.push(cellClipDefinition(layout, map));
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      const i = idx(map, x, y);
      const id = map.tiles[i];
      const ctx: TileContext = {
        x,
        y,
        theme,
        ownerColor: colorOfPlayer(map.owners[i]),
        linked: {
          n: links(content, map, id, x, y - 1),
          e: links(content, map, id, x + 1, y),
          s: links(content, map, id, x, y + 1),
          w: links(content, map, id, x - 1, y),
        },
      };
      const origin = layout.origin({ x, y });
      parts.push(
        `<g transform="translate(${origin.x.toFixed(2)},${origin.y.toFixed(2)})"${clip} data-tile="${x},${y}">${terrainMarkup(id, ctx)}</g>`,
      );
    }
  }
  return parts.join('');
}

/** Single tile preview, e.g. for the editor palette. */
export function terrainSwatch(id: TerrainId, ownerColor?: string): string {
  return `<svg viewBox="0 0 32 32" width="32" height="32" shape-rendering="crispEdges">${terrainMarkup(
    id,
    { x: 3, y: 5, ownerColor, linked: { n: false, e: false, s: false, w: false } },
  )}</svg>`;
}
