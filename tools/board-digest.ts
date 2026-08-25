/**
 * What the board draws, for every shipped level, as one text file.
 *
 * The safety net for changing the renderer. A board is 5,575 SVG nodes of scenery,
 * tiles, sprites and overlays, and no test asserts most of it — so extracting a
 * renderer port under it could quietly move a thousand things by a pixel and every
 * test would still pass. This dumps the mounted markup instead: run it before the
 * change, run it after, and diff.
 *
 * Deliberately a probe rather than a test. It pins the current picture exactly,
 * which is the right thing to hold still during a refactor and the wrong thing to
 * assert forever — a fixture this size fails on every intended change and teaches
 * nobody anything.
 *
 *   npx vite-node tools/board-digest.ts > /tmp/board-before.txt
 */
import { describeEveryShippedBoard } from './board-harness';

console.log(describeEveryShippedBoard((root, out) => out.push(root.outerHTML)));
