/**
 * The GPU backend, as its own entry point.
 *
 * Separate from the barrel for one measured reason: `pixi.js` is 492 KB, and a
 * static import of it from the main entry put all of that into the game's bundle
 * whether or not a session ever asked for this renderer. The other three apps were
 * unaffected — they never name it, and the barrel tree-shakes — but the one app
 * that offers the choice paid for it up front.
 *
 * So whoever wants it imports it dynamically, and the bundler gives it a chunk of
 * its own that loads when the choice is made.
 */
export { preparePixiBoardSurface } from './art/pixi-board-surface';
export type { ManagedBoardSurfaceFactory } from './art/pixi-board-surface';
