/**
 * What the game looks like, as a PNG somebody can open.
 *
 * The other three rulers describe the board in text: `board-digest` pins the
 * markup, `board-flat` resolves the placement, `board-scale` counts the distinct
 * pictures. All three answer "did this change move anything", and none of them
 * answers "does it look right" — which is the only question an art or layout
 * change is actually about. That question kept getting deferred to "a person has
 * to look at a screen", and a check nobody here can run is a check that does not
 * happen: the appearance drifted for as long as nothing in the repository could
 * see it.
 *
 * So: mount the screen the same way the other rulers mount a board, inline the
 * stylesheets the application loads, and let a headless browser take the picture.
 * A probe, not a test — nothing asserts a pixel. It exists so an art change can be
 * looked at without starting a dev server and clicking through a campaign.
 *
 *   npm run ui:shot                  # every shipped board, plus the battle screen
 *   npm run ui:shot -- siege         # only screens whose label contains "siege"
 *
 * Output lands in `.shots/`, which is ignored.
 */
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import {
  busyOverlay,
  CANDIDATE_01_ART,
  content,
  engine,
  mountBoard,
  SHIPPED_BOARDS,
  shippedLevel,
} from './board-harness';
import { GameController } from '@empire/game-ui';

/** The browser that takes the picture. A shot tool needs a renderer, not a stub. */
const CHROME = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';

/** Where shots land, relative to the repository root. */
const OUT = resolve('.shots');

/**
 * The stylesheets the game app loads, in the order it loads them.
 *
 * Stated here rather than discovered, because order is cascade and a shot taken
 * under a different cascade is a shot of a different game.
 */
const STYLESHEETS = [
  'packages/game-ui/src/styles/app.css',
  'packages/game-ui/src/styles/battle.css',
  'packages/story-candidate-01/src/styles/candidate-01.css',
];

/** The window the shot is taken in: a laptop, which is what this game is played on. */
const SHOT = { width: 1440, height: 900 };

const css = STYLESHEETS.map((path) => readFileSync(resolve(path), 'utf8')).join('\n');

/**
 * One thing worth photographing: a label and the markup of it.
 *
 * Markup, not the live element. Every mounted screen owns a running sprite
 * timeline, and a timeline that is never disposed keeps this process alive after
 * the last shot is taken. So each subject is mounted, read, and put down.
 */
interface Screen {
  readonly label: string;
  readonly html: string;
  /** What the application puts this root inside of. A board hangs in the field. */
  readonly wrapper: string;
}

/** Every subject this tool can photograph, by label, mounted only when asked. */
const SUBJECTS: readonly { label: string; shoot: () => Screen }[] = [
  // A board shot is about the drawing: terrain, props, sprites, overlays.
  ...SHIPPED_BOARDS.map(({ label, level, art }) => ({
    label: `board/${label}`,
    shoot: (): Screen => {
      const board = mountBoard(level, art);
      board.render(busyOverlay(level));
      const html = (board.el as Element).outerHTML;
      board.dispose();
      return { label: `board/${label}`, html, wrapper: 'battlefield' };
    },
  })),
  // A battle shot is about the layout: what the board shares the window with, and
  // how much of the window it gets.
  {
    label: 'screen/battle',
    shoot: (): Screen => {
      const battle = new GameController(shippedLevel('three-bridges'), () => {}, {
        engine,
        art: CANDIDATE_01_ART,
      });
      const html = battle.root.outerHTML;
      battle.dispose();
      return { label: 'screen/battle', html, wrapper: '' };
    },
  },
];

const wanted = process.argv.slice(2).filter((arg) => arg !== '--');
const chosen = SUBJECTS.filter(
  (subject) => !wanted.length || wanted.some((needle) => subject.label.includes(needle)),
);
if (!chosen.length) throw new Error(`no screen matches ${wanted.join(' ')}`);
// Content is loaded for its side effect on the harness; naming it keeps that honest.
void content;

/**
 * Asset references, pointed at the working copy.
 *
 * A campaign's art is raster: `import.meta.glob(…, '?url')` hands the markup
 * root-absolute paths like `/packages/story-candidate-01/assets/…`, which a dev
 * server serves and a `file://` page resolves against the filesystem root. Left
 * alone, every tile, sprite and structure in a themed shot is a broken image —
 * and a shot of a board with no ground in it is worse than no shot, because it
 * looks like a finding.
 */
const withLocalAssets = (markup: string): string =>
  markup.replace(/(\b(?:href|src)=")\//g, `$1file://${resolve('.')}/`);

const page = (screen: Screen): string =>
  `<!doctype html><html lang="zh"><head><meta charset="utf-8"><style>${css}
  html,body{margin:0;padding:0;width:${SHOT.width}px;height:${SHOT.height}px;overflow:hidden}
  .shot{width:100%;height:100%;position:relative;display:flex}
  .shot > *{width:100%;height:100%}
  /* A surface sized by a JSDOM measurement measured zero. The browser sizes it. */
  .shot .board{width:auto!important;height:auto!important;max-width:100%;max-height:100%}
  </style></head><body><div class="shot ${screen.wrapper}">${withLocalAssets(screen.html)}</div></body></html>`;

mkdirSync(OUT, { recursive: true });
for (const subject of chosen) {
  const screen = subject.shoot();
  const name = screen.label.replace(/[^\w-]+/g, '-');
  const html = resolve(OUT, `${name}.html`);
  const png = resolve(OUT, `${name}.png`);
  writeFileSync(html, page(screen));
  execFileSync(CHROME, [
    '--headless',
    '--disable-gpu',
    '--hide-scrollbars',
    // The page is a file and so is every sprite on it.
    '--allow-file-access-from-files',
    `--window-size=${SHOT.width},${SHOT.height}`,
    '--virtual-time-budget=3000',
    `--screenshot=${png}`,
    `file://${html}`,
  ], { stdio: 'ignore' });
  console.log(png);
}
