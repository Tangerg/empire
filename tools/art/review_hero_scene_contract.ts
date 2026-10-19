/** Capture every actual campaign BoardView before/after an appearance contract change. */
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { gzipSync } from 'node:zlib';
import { content, engine, CANDIDATE_01_ART, emptyOverlay } from '../board-harness';
import { CANDIDATE_01_LEVELS } from '../../packages/story-candidate-01/src/levels';
import { BoardView, SvgBoardSurface } from '@empire/game-ui';

const hash = (value: string) => createHash('sha256').update(value).digest('hex');
const archive = process.env.HERO_SCENE_SVG_DIR;
if (archive) mkdirSync(archive, { recursive: true });
const scenes = CANDIDATE_01_LEVELS.map(level => {
  const state = engine.createState(level);
  const before = JSON.stringify(state);
  const board = new BoardView({ content, grid: engine.rules.grids.get('square4'), art: CANDIDATE_01_ART,
    renderer: scene => new SvgBoardSurface(scene) }, state,
  { onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {}, onSecondary: () => {}, onScale: () => {} });
  try {
    board.render(emptyOverlay());
    if (before !== JSON.stringify(state)) throw new Error(`BoardView mutated state: ${level.id}`);
    const svg = board.el.outerHTML;
    if (archive) writeFileSync(resolve(archive, `${level.id}.svg.gz`), gzipSync(svg));
    return { levelId: level.id, stateSha256: hash(before), svgSha256: hash(svg),
      actorDrawings: board.el.querySelectorAll('.layer-units > .unit').length,
      structureDrawings: board.el.querySelectorAll('[data-candidate-art="structure"]').length,
      viewBox: board.el.getAttribute('viewBox'), stateUnchanged: true };
  } finally { board.dispose(); }
});
writeFileSync(process.env.HERO_SCENES_OUTPUT ?? '/tmp/empire-hero-scenes.json', JSON.stringify(scenes, null, 2) + '\n');
