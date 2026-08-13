import type { Direction, GameMap } from '@empire/battle-engine/types';
import { resolveArt } from './ports';
import { TILE } from './terrain';

const DIRECTIONS = ['north', 'east', 'south', 'west'] as const satisfies readonly Direction[];

const featureColor = {
  elevationBackground: '#2a211a',
  elevationText: '#f2c76a',
  cliff: '#f0b24f',
  halfCover: '#4f9bc7',
  fullCover: '#d85c4c',
} as const;

/** Stable cache key for everything painted by the terrain and feature layers. */
export function battlefieldRenderKey(map: GameMap): string {
  const cliffs = map.cliffs
    .map(({ from, to }) => `${from.x},${from.y}>${to.x},${to.y}`)
    .join(';');
  const cover = map.directionalCover
    .map(({ at, sides }) =>
      `${at.x},${at.y}:${DIRECTIONS.map((side) => sides[side]?.[0] ?? '-').join('')}`,
    )
    .join(';');
  return [
    `${map.width}x${map.height}`,
    map.tiles.join(','),
    map.owners.join(','),
    map.elevation.join(','),
    cliffs,
    cover,
  ].join('|');
}

function cliffMarkup(map: GameMap): string[] {
  return map.cliffs.map((cliff) => {
    const ax = cliff.from.x * TILE + TILE / 2;
    const ay = cliff.from.y * TILE + TILE / 2;
    const bx = cliff.to.x * TILE + TILE / 2;
    const by = cliff.to.y * TILE + TILE / 2;
    const mx = (ax + bx) / 2;
    const my = (ay + by) / 2;
    return cliff.from.x !== cliff.to.x
      ? `<line x1="${mx}" y1="${my - TILE / 2}" x2="${mx}" y2="${my + TILE / 2}" stroke="${featureColor.cliff}" stroke-width="3"/>`
      : `<line x1="${mx - TILE / 2}" y1="${my}" x2="${mx + TILE / 2}" y2="${my}" stroke="${featureColor.cliff}" stroke-width="3"/>`;
  });
}

function coverEdge(x: number, y: number, side: Direction, color: string): string {
  if (side === 'north') return `<line x1="${x + 3}" y1="${y + 3}" x2="${x + TILE - 3}" y2="${y + 3}" stroke="${color}" stroke-width="3"/>`;
  if (side === 'south') return `<line x1="${x + 3}" y1="${y + TILE - 3}" x2="${x + TILE - 3}" y2="${y + TILE - 3}" stroke="${color}" stroke-width="3"/>`;
  if (side === 'west') return `<line x1="${x + 3}" y1="${y + 3}" x2="${x + 3}" y2="${y + TILE - 3}" stroke="${color}" stroke-width="3"/>`;
  return `<line x1="${x + TILE - 3}" y1="${y + 3}" x2="${x + TILE - 3}" y2="${y + TILE - 3}" stroke="${color}" stroke-width="3"/>`;
}

/** Shared renderer for elevation badges, cliffs and directional cover. */
export function battlefieldFeatureMarkup(map: GameMap): string {
  const parts: string[] = [];
  for (let i = 0; i < map.elevation.length; i++) {
    const value = map.elevation[i];
    if (value === 0) continue;
    const cellX = i % map.width;
    const cellY = Math.floor(i / map.width);
    const sameToWest = cellX > 0 && map.elevation[i - 1] === value;
    const sameToNorth = cellY > 0 && map.elevation[i - map.width] === value;
    // One label identifies a continuous plateau; repeating it in every cell
    // obscures both the authored terrain and the units standing on it.
    if (sameToWest || sameToNorth) continue;
    const x = cellX * TILE;
    const y = cellY * TILE;
    parts.push(
      `<g class="elevation-badge">` +
      `<circle cx="${x + 26}" cy="${y + 7}" r="5" fill="${featureColor.elevationBackground}" opacity="0.8"/>` +
      `<text x="${x + 26}" y="${y + 9.5}" text-anchor="middle" font-size="7" fill="${featureColor.elevationText}">${value}</text>` +
      `</g>`,
    );
  }
  parts.push(...cliffMarkup(map));
  for (const cover of map.directionalCover) {
    const x = cover.at.x * TILE;
    const y = cover.at.y * TILE;
    const levels = DIRECTIONS.flatMap((side) => cover.sides[side] ? [cover.sides[side]] : []);
    if (levels.length > 0) {
      const strongest = levels.includes('full') ? 'full' : 'half';
      const art = resolveArt((provider) => provider.coverMarkup?.(strongest));
      if (art) parts.push(`<g transform="translate(${x} ${y})">${art}</g>`);
    }
    for (const side of DIRECTIONS) {
      const level = cover.sides[side];
      if (!level) continue;
      parts.push(coverEdge(x, y, side, level === 'full' ? featureColor.fullCover : featureColor.halfCover));
    }
  }
  return parts.join('');
}
