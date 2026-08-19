/**
 * Where a battle is drawn.
 *
 * `BoardView` used to be two jobs fused together: deciding what the battlefield
 * looks like — which layers, what goes in each, where every actor sits, what
 * animates — and putting SVG elements in the DOM. The first job is all of the game
 * knowledge and none of the rendering; the second is a backend. A field of 5,575
 * nodes with a colour matrix over the whole of it is the wrong shape for the DOM
 * and the right shape for a GPU, and there was no seam to put one behind.
 *
 * This is the seam. What crosses it is deliberately small:
 *
 * - a layer's whole content, as pictures at places, in a declared depth order
 * - a persistent drawing per unit, and transient ones for effects
 * - four continuous properties every animation is expressed in
 * - named visual states, instead of CSS classes poked from outside
 *
 * That last pair is what makes a non-DOM backend possible at all. Every animation
 * the board plays — walking, striking, dying, arriving, a damage number rising, a
 * burst opening, a turn banner sweeping — is `place`, `nudge`, `swell` and
 * `opacity`. And every appearance that used to be a class name the board reached in
 * and toggled is now a state the drawing is *told* it is in, which a DOM backend
 * spells as a class and a GPU backend spells as a transform or a tint.
 */

/**
 * The layers a board has, deepest first.
 *
 * Order is the depth contract, and it is stated here rather than in a renderer so
 * that two backends cannot disagree about what covers what.
 */
export const BOARD_LAYERS = [
  'ground',
  'terrain',
  'scenery',
  'spatial',
  'grid',
  'range',
  'path',
  'structures',
  'markers',
  'units',
  'foreground',
  'effects',
  'cursor',
] as const;

export type BoardLayer = (typeof BOARD_LAYERS)[number];

/**
 * One picture, and where its origin sits in scene units.
 *
 * A layer used to cross this seam as a single string, and the producers built it
 * by wrapping each picture in `<g transform="translate(…)">` before joining. That
 * threw away the structure they already had: the renderer received a document and
 * had to parse the places back out of it to learn anything.
 *
 * What it cost is measurable, and `tools/board-scale.ts` measures it. A picture's
 * *identity* is what makes a texture cache work — the same tile drawn four
 * thousand times is one texture — and identity was fused with placement and with
 * a `data-tile="x,y"` handle that nothing but four tests ever read. On a painted
 * 81×51 field the terrain layer draws four distinct pictures across 4,131 cells,
 * and a renderer looking at the string saw 4,131 different ones.
 *
 * So placement is the renderer's, exactly as it is for an effect and for a unit.
 * Markup that has no place — line work across the whole field, a clip definition —
 * is a piece at the origin. That is the same rule, not a second one.
 */
export interface BoardPiece {
  readonly markup: string;
  readonly x: number;
  readonly y: number;
}

/** Markup with no place of its own, as the one piece a layer is made of. */
export const wholeField = (markup: string): readonly BoardPiece[] =>
  markup ? [{ markup, x: 0, y: 0 }] : [];

/**
 * The pieces as one SVG string.
 *
 * The canonical spelling of "a picture at a place", used by the SVG surface and
 * by everyone who draws a board without one — a level thumbnail, the editor's own
 * canvas. Two call sites used to spell the same translate differently, one with a
 * comma and one with a space.
 */
export const boardPiecesMarkup = (pieces: readonly BoardPiece[]): string =>
  pieces
    .map((piece) => `<g transform="translate(${piece.x.toFixed(2)},${piece.y.toFixed(2)})">${piece.markup}</g>`)
    .join('');

/**
 * A visual state a drawing is in.
 *
 * Told, not inferred. These were CSS classes that `BoardView` toggled on elements
 * it had queried out of the tree, which meant the appearance of a unit lived in a
 * stylesheet the renderer could not see and a backend without stylesheets could not
 * reproduce.
 */
export type BoardState = 'facingLeft' | 'moving' | 'attacking' | 'done' | 'selected' | 'hidden';

/**
 * A part of a drawing, by the role its own markup declares with `data-part`.
 *
 * The board used to reach into markup it had just written and pull nodes out by tag
 * name — `querySelector('circle')` for the burst of a hit, `querySelector('text')`
 * for the number over it. Which shape happens to be first is not a contract, and
 * it is not one a second backend could honour.
 */
export type BoardRole = 'badges' | 'burst' | 'number' | 'band';

/** A drawing that is on the board and can still be changed. */
export interface BoardDrawing {
  /** Where the drawing's origin sits, in scene units. Replaces any previous place. */
  place(x: number, y: number): void;
  /** An offset from wherever it was placed, in scene units. */
  nudge(dx: number, dy: number): void;
  /** Scale about the middle of a tile, without moving the placement. */
  swell(factor: number): void;
  /** 0 is invisible, 1 is as drawn. */
  opacity(value: number): void;
  /** Puts the drawing into, or out of, a named visual state. */
  say(state: BoardState, on: boolean): void;
  /** Whether it is currently in that state. */
  inState(state: BoardState): boolean;
  /** The part of this drawing that declared itself as `role`, if it has one. */
  part(role: BoardRole): BoardDrawing | null;
  /** Replaces everything the drawing holds under `role`. */
  fill(role: BoardRole, markup: string): void;
  remove(): void;
}

/**
 * Where a pointer is, in scene units.
 *
 * The surface converts screen to scene because that is the half it knows — the
 * element's box on the page and the scale it is presented at. Scene to cell is the
 * board's half, because that is the tiling's answer. `BoardView` used to do both,
 * which is why it held `getBoundingClientRect` and a scale calculation.
 */
export interface BoardPointer {
  press(at: { x: number; y: number }, button: number): void;
  move(at: { x: number; y: number } | null): void;
  leave(): void;
  /**
   * The player asked for a different scale, in notches.
   *
   * Here rather than in the shell because reading it means knowing that a wheel
   * with a modifier held is a zoom and that the page must be told not to scroll —
   * both facts about the input device, and neither about the battle.
   */
  scale(notches: number): void;
}

/** What a surface is asked to be when it is created. */
export interface BoardSurfaceScene {
  /** Size of the whole picture, in scene units. */
  readonly width: number;
  readonly height: number;
  /** Where the tactical field starts inside it. */
  readonly originX: number;
  readonly originY: number;
  /** A theme's own name for this board, for whatever a backend does with it. */
  readonly themeClass?: string;
  /** Crisp pixels suit a grid; painted scenes do not want them. */
  readonly shapeRendering: string;
  /** Art outside the tactical field, behind and in front of everything. */
  readonly backdrop: string;
  readonly foreground: string;
}

/**
 * A drawn battlefield, with a way to change every part of it.
 *
 * Implementations own their own idea of what a drawing *is*: an SVG group, a Pixi
 * container, something else. Nothing above this interface may assume.
 */
export interface BoardSurface {
  /** What gets mounted. */
  readonly element: SVGElement | HTMLElement;
  /** Reports where the pointer is, in scene units. */
  listen(pointer: BoardPointer): void;
  /** Replaces a layer's whole content. Returns the animation ids it registered. */
  setLayer(layer: BoardLayer, pieces: readonly BoardPiece[]): string[];
  /** A persistent drawing for one unit, created on first ask. */
  unit(id: number, draw: () => string): { drawing: BoardDrawing; animationId: string | null };
  /** Whether a unit already has a drawing, without making one. */
  hasUnit(id: number): boolean;
  /** Forgets a unit's drawing. */
  dropUnit(id: number): void;
  /** Every unit that currently has a drawing. */
  drawnUnits(): number[];
  /** A transient drawing in the effects layer. Returns the animation ids too. */
  effect(markup: string): { drawing: BoardDrawing; animationIds: string[] };
  /** The pixel size the picture is presented at. */
  resize(width: number, height: number): void;
  /** Whether the player is measuring with the board right now. */
  tactical(on: boolean): void;
  /** Whether the scale is currently moving, so a backend can spend less on it. */
  rescaling(on: boolean): void;
  dispose(): void;
}
