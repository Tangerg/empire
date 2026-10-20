import document from '../../assets/final-fantasy-v1/registered-crossings-v1/manifest-crossing-v1.json';
import verticalDocument from '../../assets/final-fantasy-v1/registered-crossings-v1/manifest-crossing-vertical-v1.json';
import junctionDocument from '../../assets/final-fantasy-v1/registered-crossings-v1/manifest-junction-v1.json';
import { escapeAttr, TILE } from '@empire/game-ui';
import { fixedNumbers } from './candidate-01-documents';
import { candidate01CrossingJunction, type CandidateCrossingComponent } from './candidate-01-crossing-topology';

const urls = import.meta.glob<string>(
  '../../assets/final-fantasy-v1/registered-crossings-v1/runtime/*.png',
  { eager: true, import: 'default', query: '?url' },
);

/** The approved full canvas and registered deck contact, without a 96px atlas crop. */
function readCrossing(document: typeof verticalDocument, orientation: 'horizontal' | 'vertical') {
  const sourceSize = fixedNumbers(2, document.sourceSize, 'sourceSize', document.id);
  const sourceFoot = fixedNumbers(2, document.sourceFoot, 'sourceFoot', document.id);
  const scale = document.logicalPerSourcePixel;
  if (document.schemaVersion !== 1 || document.orientation !== orientation
    || !Number.isFinite(scale) || scale <= 0
    || sourceSize.some((value) => !Number.isInteger(value) || value <= 0)
    || sourceFoot.some((value, axis) => value < 0 || value > sourceSize[axis])) {
    throw new Error(`candidate-01: crossing requires an authored ${orientation} source and valid registration`);
  }
  const logicalSize = fixedNumbers(2, sourceSize.map((size) => size * scale), 'logicalSize', document.id);
  const anchor = fixedNumbers(2, sourceFoot.map((value) => value * scale), 'anchor', document.id);
  const href = urls[`../../assets/final-fantasy-v1/registered-crossings-v1/${document.png2x}`];
  if (!href || !urls[`../../assets/final-fantasy-v1/registered-crossings-v1/${document.png}`]) throw new Error(`candidate-01: missing approved ${orientation} crossing raster`);
  return { ...document, sourceSize, sourceFoot, logicalSize, anchor, href };
}

export const CANDIDATE_01_HORIZONTAL_CROSSING = readCrossing(document, 'horizontal');
export const CANDIDATE_01_VERTICAL_CROSSING = readCrossing(verticalDocument, 'vertical');

export function readCandidate01StoneJunction(raw: typeof junctionDocument) {
  if (raw.schemaVersion !== 1 || raw.centerMask !== 15 || raw.axisLength !== 3
    || raw.horizontal.id !== CANDIDATE_01_HORIZONTAL_CROSSING.id
    || raw.vertical.id !== CANDIDATE_01_VERTICAL_CROSSING.id) {
    throw new Error('candidate-01: stone junction requires both registered sources and all four ports');
  }
  const reach = raw.axisLength * TILE / 2;
  const ports = [[0, 0], [0, -reach], [reach, 0], [0, reach], [-reach, 0]];
  const cuts = (windows: number[][], source: typeof CANDIDATE_01_HORIZONTAL_CROSSING) => {
    if (windows.length !== 2) throw new Error('candidate-01: each junction axis requires exactly two rail windows');
    return windows.map(window => {
      const rect = fixedNumbers(4, window, 'cutout', raw.id);
      const [x, y, width, height] = rect;
      if (width <= 0 || height <= 0
        || x < -source.anchor[0] || y < -source.anchor[1]
        || x + width > source.logicalSize[0] - source.anchor[0]
        || y + height > source.logicalSize[1] - source.anchor[1]) {
        throw new Error('candidate-01: junction window must stay inside its complete source canvas');
      }
      if (ports.some(([px, py]) => px >= x && px <= x + width && py >= y && py <= y + height)) {
        throw new Error('candidate-01: junction windows must preserve the center and all four outer ports');
      }
      return rect;
    });
  };
  const horizontal = cuts(raw.horizontal.cutouts, CANDIDATE_01_HORIZONTAL_CROSSING);
  const vertical = cuts(raw.vertical.cutouts, CANDIDATE_01_VERTICAL_CROSSING);
  const [north, south] = horizontal, [west, east] = vertical;
  const [nx, ny, nw, nh] = north, [sx, sy, sw] = south;
  const [wx, wy, ww, wh] = west, [ex, ey, ew, eh] = east;
  if (nx !== sx || nw !== sw || nx >= 0 || nx + nw <= 0 || ny + nh >= 0 || sy <= 0
    || wx + ww >= 0 || ex <= 0 || wy !== ey || wh !== eh
    || wy !== ny + nh || wy + wh !== sy
    || nx > wx + ww || nx + nw < ex
    || nx < -TILE || nx + nw > TILE || wx < -TILE || ex + ew > TILE
    || wh >= TILE) {
    throw new Error('candidate-01: junction windows must be complementary local parapet openings');
  }
  return { ...raw, horizontal, vertical };
}
export const CANDIDATE_01_STONE_JUNCTION = readCandidate01StoneJunction(junctionDocument);

/** One picture reused at the existing run midpoint; no gameplay or run-length scaling. */
export function candidate01HorizontalCrossingMarkup(): string {
  const { anchor, logicalSize, href } = CANDIDATE_01_HORIZONTAL_CROSSING;
  return `<g class="candidate-environment-prop is-standing" transform="translate(${-anchor[0]} ${-anchor[1]})">`
    + `<image href="${escapeAttr(href)}" width="${logicalSize[0]}" height="${logicalSize[1]}" preserveAspectRatio="none"/></g>`;
}

/** Both directions belong to one lying picture, with source-alpha-only openings. */
export function candidate01StoneJunctionMarkup(component: CandidateCrossingComponent): string {
  const junction = candidate01CrossingJunction(component);
  if (!junction) throw new Error('candidate-01: stone junction requires the complete five-cell plus and registered shared pivot');
  const id = `c01-stone-junction-${junction.center.join('-')}`;
  const picture = (source: typeof CANDIDATE_01_HORIZONTAL_CROSSING, axis: 'horizontal' | 'vertical') => {
    const [width, height] = source.logicalSize, [ax, ay] = source.anchor;
    const maskId = `${id}-${axis}`;
    const rectangle = (x: number, y: number, w: number, h: number) => `M${x} ${y}h${w}v${h}h${-w}Z`;
    const cutouts = CANDIDATE_01_STONE_JUNCTION[axis].map(([x, y, w, h]) => rectangle(x, y, w, h)).join('');
    // The definitions are local to this BoardPiece for the GPU texture baker.
    // Geometric holes only; no luminance, RGB filter or replacement alpha.
    // The image's own alpha remains authoritative everywhere outside the holes.
    return `<defs><clipPath id="${maskId}" clipPathUnits="userSpaceOnUse">`
      + `<path clip-rule="evenodd" d="${rectangle(-ax, -ay, width, height)}${cutouts}"/></clipPath></defs>`
      + `<image href="${escapeAttr(source.href)}" x="${-ax}" y="${-ay}" width="${width}" height="${height}" preserveAspectRatio="none" clip-path="url(#${maskId})"/>`;
  };
  return `<g class="candidate-environment-prop is-standing" data-crossing-junction="15">`
    + picture(CANDIDATE_01_HORIZONTAL_CROSSING, 'horizontal')
    + picture(CANDIDATE_01_VERTICAL_CROSSING, 'vertical') + '</g>';
}
