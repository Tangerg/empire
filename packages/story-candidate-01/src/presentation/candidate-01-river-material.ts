import { escapeAttr, TILE, type BoardPiece } from '@empire/game-ui';
import { CANDIDATE_01_ROAD_MATERIAL } from './candidate-01-road-material';
import { CANDIDATE_01_WATER_MATERIAL } from './candidate-01-water-material';
import type { FixedNumbers } from './candidate-01-documents';

export interface CandidateRiverProfile {
  readonly width: number;
  readonly bankWidth: number;
  readonly knots: readonly FixedNumbers<2>[];
}

/** Vertical tangents keep the authored crossings level while the banks meander. */
export function candidate01RiverPath(profile: CandidateRiverProfile): string {
  if (!(profile.width > 0 && profile.bankWidth > profile.width && profile.bankWidth <= 64 && profile.bankWidth - profile.width <= 10)
    || profile.knots.length < 2 || profile.knots.some(p => !p.every(Number.isFinite))
    || profile.knots.some((p, i) => i > 0 && p[1] <= profile.knots[i - 1][1])) {
    throw new Error('candidate-01: river needs positive bounded widths and strictly downstream knots');
  }
  const points = profile.knots.map(([x, y]) => [x * TILE, y * TILE]);
  return `M ${points[0].join(' ')}` + points.slice(1).map(([x, y], i) => {
    const [px, py] = points[i];
    const dy = (y - py) / 3;
    return ` C ${px} ${py + dy} ${x} ${y - dy} ${x} ${y}`;
  }).join('');
}

/** Terrain owns water permission; the authored line never paints a dry cell. */
export function candidate01RiverMaterialPieces(width: number, height: number,
  occupied: (x: number, y: number) => boolean, profile: CandidateRiverProfile,
  bankAllowed: (x: number, y: number) => boolean = occupied): BoardPiece[] {
  if (![width, height].every(value => Number.isInteger(value) && value > 0)) {
    throw new Error('candidate-01: river map dimensions must be positive integers');
  }
  const path = candidate01RiverPath(profile);
  const cells: readonly (readonly [number, number])[] = Array.from({ length: height }, (_, y) =>
    Array.from({ length: width }, (_, x) => [x, y] as const).filter(([x, y]) => occupied(x, y))).flat();
  const bankCells: readonly (readonly [number, number])[] = Array.from({ length: height }, (_, y) =>
    Array.from({ length: width }, (_, x) => [x, y] as const).filter(([x, y]) => bankAllowed(x, y))).flat();
  const pieces: BoardPiece[] = [];
  for (const [material, strokeWidth, kind] of [
    [CANDIDATE_01_ROAD_MATERIAL, profile.bankWidth, 'river-bank'],
    [CANDIDATE_01_WATER_MATERIAL, profile.width, 'area-water'],
  ] as const) {
    const [periodX, periodY] = material.logicalSize;
    for (let top = 0; top < height * TILE; top += periodY) for (let left = 0; left < width * TILE; left += periodX) {
      const local = (kind === 'river-bank' ? bankCells : cells).filter(([x, y]) => x * TILE >= left && x * TILE < left + periodX
        && y * TILE >= top && y * TILE < top + periodY);
      if (!local.length) continue;
      const w = Math.min(periodX, width * TILE - left), h = Math.min(periodY, height * TILE - top);
      const id = `c01-river-${kind}-${left}-${top}-${strokeWidth}-${profile.knots.map(p => p.join('_')).join('-')}-${local.map(p => p.join('_')).join('-')}-${cells.map(p => p.join('_')).join('-')}`;
      const rim = (profile.bankWidth - profile.width) / 2;
      const limit = cells.map(([x, y]) => `<rect x="${x * TILE - left - rim}" y="${y * TILE - top - rim}" width="${TILE + rim * 2}" height="${TILE + rim * 2}" rx="${rim}"/>`).join('');
      const allowed = local.map(([x, y]) => `<rect x="${x * TILE - left}" y="${y * TILE - top}" width="32" height="32"/>`).join('');
      pieces.push({ x: left, y: top, markup:
        `<svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" overflow="hidden" data-runtime-raster="${kind}">`
        + `<defs><clipPath id="${id}-terrain">${allowed}</clipPath><clipPath id="${id}-rim">${limit}</clipPath><mask id="${id}" maskUnits="userSpaceOnUse" x="0" y="0" width="${w}" height="${h}">`
        + `<g clip-path="url(#${id}-terrain)"><g clip-path="url(#${id}-rim)"><path transform="translate(${-left} ${-top})" d="${path}" fill="none" stroke="white" stroke-width="${strokeWidth}" stroke-linecap="butt"/></g></g>`
        + `</mask></defs><image href="${escapeAttr(material.href)}" width="${periodX}" height="${periodY}" mask="url(#${id})"/></svg>` });
    }
  }
  return pieces;
}
