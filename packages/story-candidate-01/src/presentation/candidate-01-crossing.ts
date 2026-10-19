import document from '../../assets/final-fantasy-v1/registered-crossings-v1/manifest-crossing-v1.json';
import { escapeAttr } from '@empire/game-ui';
import { fixedNumbers } from './candidate-01-documents';

const urls = import.meta.glob<string>(
  '../../assets/final-fantasy-v1/registered-crossings-v1/runtime/*.png',
  { eager: true, import: 'default', query: '?url' },
);

/** The approved full canvas and registered deck contact, without a 96px atlas crop. */
function readCrossing() {
  const sourceSize = fixedNumbers(2, document.sourceSize, 'sourceSize', document.id);
  const sourceFoot = fixedNumbers(2, document.sourceFoot, 'sourceFoot', document.id);
  const scale = document.logicalPerSourcePixel;
  if (document.schemaVersion !== 1 || document.orientation !== 'horizontal'
    || !Number.isFinite(scale) || scale <= 0
    || sourceSize.some((value) => !Number.isInteger(value) || value <= 0)
    || sourceFoot.some((value, axis) => value < 0 || value > sourceSize[axis])) {
    throw new Error('candidate-01: crossing requires an authored horizontal source and valid registration');
  }
  const logicalSize = fixedNumbers(2, sourceSize.map((size) => size * scale), 'logicalSize', document.id);
  const anchor = fixedNumbers(2, sourceFoot.map((value) => value * scale), 'anchor', document.id);
  const href = urls[`../../assets/final-fantasy-v1/registered-crossings-v1/${document.png2x}`];
  if (!href || !urls[`../../assets/final-fantasy-v1/registered-crossings-v1/${document.png}`]) throw new Error('candidate-01: missing approved horizontal crossing raster');
  return { ...document, sourceSize, sourceFoot, logicalSize, anchor, href };
}

export const CANDIDATE_01_HORIZONTAL_CROSSING = readCrossing();

/** One picture reused at the existing run midpoint; no gameplay or run-length scaling. */
export function candidate01HorizontalCrossingMarkup(): string {
  const { anchor, logicalSize, href } = CANDIDATE_01_HORIZONTAL_CROSSING;
  return `<g class="candidate-environment-prop is-standing" transform="translate(${-anchor[0]} ${-anchor[1]})">`
    + `<image href="${escapeAttr(href)}" width="${logicalSize[0]}" height="${logicalSize[1]}" preserveAspectRatio="none"/></g>`;
}
