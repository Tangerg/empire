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
 * - a frame strip as the frames and clips it is, instead of as a sprite-sheet trick
 *
 * That last pair is what makes a non-DOM backend possible at all. Every animation
 * the board plays — walking, striking, dying, arriving, a damage number rising, a
 * burst opening, a turn banner sweeping — is `place`, `nudge`, `swell` and
 * `opacity`. And every appearance that used to be a class name the board reached in
 * and toggled is now a state the drawing is *told* it is in, which a DOM backend
 * spells as a class and a GPU backend spells as a transform or a tint.
 */

import type { Direction, TacticalGrid } from '@empire/battle-engine';
import type { FrameAnimationClip } from './frame-animation';
import { escapeAttr } from './svg';

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
 *
 * There were six, and three of them were not appearances. `done` and `attacking`
 * were set on every render and every strike and read by nothing at all — no
 * stylesheet, no code, in any package; a unit that has acted is dimmed by a badge
 * the board draws, not by this. `moving` was read back by the board through
 * `inState`, which is not a look but the board keeping its own animation
 * bookkeeping inside the renderer. A GPU backend would have had to implement three
 * states that mean nothing, and guess which three.
 *
 * What is left is what a second backend can be held to: a mirror, a highlight, and
 * not drawn at all.
 */
export type BoardState = 'facingLeft' | 'selected' | 'hidden';

/**
 * Whether a unit facing this way is drawn mirrored.
 *
 * Asked of the tiling, not compared to a name. Both boards wrote
 * `facing === 'west'`, which is one board's vocabulary: `square8` also faces
 * northwest and southwest, and a hex board has no west at all — it has
 * `hexWest`. So on a diagonal board a unit facing away to the left was drawn
 * looking right, and on a hex board every unit was.
 *
 * The same file already asked the tiling properly three lines away, for the
 * facing badge, which is how the two ways of answering one question sat side by
 * side without anybody noticing.
 *
 * A facing the tiling does not know — or a document that states none at all —
 * mirrors nothing, rather than throwing: a level may name a direction from a
 * board it is no longer played on, and that is the validator's complaint to
 * make, not the renderer's.
 */
export function facesLeft(grid: TacticalGrid, facing: Direction | undefined): boolean {
  const known = grid.directions.find((direction) => direction.id === facing);
  return known !== undefined && grid.step({ x: 0, y: 0 }, known.id).x < 0;
}

/**
 * A part of a drawing, by the role its own markup declares with `data-part`.
 *
 * The board used to reach into markup it had just written and pull nodes out by tag
 * name — `querySelector('circle')` for the burst of a hit, `querySelector('text')`
 * for the number over it. Which shape happens to be first is not a contract, and
 * it is not one a second backend could honour.
 */
export type BoardRole = 'badges' | 'burst' | 'number' | 'band';

/**
 * One part of a picture, by the role that says what it is for.
 *
 * Depth is the array's order, after the body — the same rule `BoardPiece` uses.
 */
export interface BoardPart {
  readonly role: BoardRole;
  readonly markup: string;
}

/**
 * A strip of frames, and the clips that play on it.
 *
 * The fifth time the same defect appeared, and the one that kept a GPU backend from
 * animating anything. A strip used to cross this seam as markup carrying its own
 * description — `data-frame-width`, `data-frame-count`, `data-frame-initial`,
 * `data-frame-clips="[{…}]"` — which the renderer found with `querySelector`, read
 * back with `Number(getAttribute(…))` and `JSON.parse`, and then *validated* as if
 * it might be malformed. It was written by us, six lines earlier, from exactly this
 * data.
 *
 * Declared, it is what a texture atlas already is: one image, a frame's worth at a
 * time. A DOM backend shows a frame by moving a window over the image; a GPU
 * backend cuts the image into frame-sized textures once and swaps which one a
 * sprite holds. Both are driven by the same timeline, from the same clips.
 */
export interface BoardStrip {
  /** The image the frames are cut from, laid out left to right. */
  readonly href: string;
  /** One frame's size, in scene units. */
  readonly frameWidth: number;
  readonly frameHeight: number;
  readonly frameCount: number;
  /** Where the frame window's top-left corner sits in the picture. */
  readonly x: number;
  readonly y: number;
  /**
   * Every clip that may be played on it, by name.
   *
   * A unit's strip is played by `BoardView` through the three names a battle has
   * for what a body is doing — `idle`, `walk` and `attack` — so a strip drawn for a
   * unit declares those. Playing a name that is not here is a defect and throws.
   */
  readonly clips: readonly FrameAnimationClip[];
  /**
   * A clip that is already playing when the picture is drawn.
   *
   * An explosion animates because it exists, not because anything told it to. The
   * SVG backend used to spell that as "register every strip you can find in this
   * subtree, then play whichever clip happens to come first in the JSON" — a guess,
   * and not one a second backend could be held to.
   */
  readonly playing?: string;
}

/** The window a strip shows one frame through, in the image's own coordinates. */
export const boardStripWindow = (strip: BoardStrip, frame: number): string =>
  `${frame * strip.frameWidth} 0 ${strip.frameWidth} ${strip.frameHeight}`;

/**
 * A strip standing still on one frame, as SVG.
 *
 * The frame is chosen by moving the window rather than by shifting the image, so
 * the one attribute an animation changes belongs to the element that holds it —
 * which is the element whoever built it already has.
 */
export const boardStripMarkup = (strip: BoardStrip, frame = 0): string =>
  `<svg class="board-strip" x="${strip.x}" y="${strip.y}"`
  + ` width="${strip.frameWidth}" height="${strip.frameHeight}"`
  + ` viewBox="${boardStripWindow(strip, frame)}" overflow="hidden">`
  + `<image href="${escapeAttr(strip.href)}" width="${strip.frameWidth * strip.frameCount}"`
  + ` height="${strip.frameHeight}" preserveAspectRatio="none"/></svg>`;

/**
 * A picture: a body, a strip that animates, and parts that move on their own.
 *
 * `effect(markup)` used to take one string with `<g data-part="burst">` groups
 * inside it, and the renderer found them again with `querySelector`. But a hit is
 * three pictures that animate separately — the weapon's flash, a burst that swells,
 * a number that rises — and the board knew that when it wrote them out. It threw
 * the structure into a string and the renderer parsed it back: the same defect as a
 * layer crossing the seam as a document, one level down.
 *
 * A backend that bakes markup into a texture cannot `querySelector` inside one.
 */
export interface BoardPicture {
  readonly body: string;
  readonly strip?: BoardStrip | undefined;
  readonly parts?: readonly BoardPart[];
  /**
   * Lifted off the ground with a dark relief, so it reads over busy terrain.
   *
   * Declared here because it was an appearance *only one backend could produce*:
   * a three-layer `drop-shadow` on `.layer-units .unit` in the shared stylesheet.
   * The DOM backend got it for nothing by being the DOM; the GPU backend could
   * not see it, and the port never said it existed — so the difference was
   * invisible in the code and visible only on a screen.
   *
   * The port carries it now and the filter is defined once, below, in markup that
   * both backends read: the SVG tree renders it directly, and the texture baker
   * bakes it in. `drop-shadow(dx dy r c)` is defined in terms of `feDropShadow`
   * with `stdDeviation = r / 2`, so the two are the same effect rather than two
   * attempts at one look.
   */
  readonly relief?: boolean | undefined;
}

/**
 * The id every relief filter is referenced by.
 *
 * Fixed rather than serialised, for the reason the field gradients are: the
 * definition is identical wherever it appears, so two boards in one document
 * sharing it is the outcome we want rather than a collision.
 */
const RELIEF_ID = 'board-relief';

/** How far the relief spills past the geometry: two down, one across, one blur. */
export const RELIEF_SPILL = 4;

const RELIEF_DEFS = `<defs><filter id="${RELIEF_ID}" x="-20%" y="-20%" width="140%" height="140%">`
  + '<feDropShadow dx="0" dy="1" stdDeviation="0" flood-color="#000" flood-opacity="0.8"/>'
  + '<feDropShadow dx="1" dy="0" stdDeviation="0" flood-color="#000" flood-opacity="0.42"/>'
  + '<feDropShadow dx="0" dy="2" stdDeviation="1" flood-color="#000" flood-opacity="0.36"/>'
  + '</filter></defs>';

/**
 * A picture's body as a backend should draw it: relief applied if it asked for it.
 *
 * Both backends call this instead of reading `body` straight, which is what makes
 * the appearance one declaration rather than one stylesheet and one omission.
 */
export const drawableBody = (picture: BoardPicture): string =>
  picture.relief
    ? `${RELIEF_DEFS}<g filter="url(#${RELIEF_ID})">${picture.body}</g>`
    : picture.body;

/** Whether baked markup carries a relief, and so needs room for it. */
export const hasRelief = (markup: string): boolean => markup.includes(`url(#${RELIEF_ID})`);

/**
 * How visible the lattice is at rest and while the player is measuring with it.
 *
 * Both backends had these two numbers: the GPU one as `grid.alpha = on ? 0.72 :
 * 0.3` with a comment calling them "the whole rule", and the DOM one as two
 * `opacity` declarations in a stylesheet. `tactical(on)` is a port method both
 * implement, so the rule is the port's to state and neither backend's to hold a
 * copy of. The stylesheet keeps the transition, which is how a DOM tree animates
 * and is nobody else's business.
 */
export const GRID_ALPHA = { resting: 0.3, tactical: 0.72 } as const;

/**
 * A picture as one SVG string: body, then strip, then parts.
 *
 * The still spelling, and the counterpart to `boardPiecesMarkup`. A HUD icon, a
 * level thumbnail and the editor's palette all draw units with no surface to draw
 * them on, so their strips stand on frame 0.
 */
export const boardPictureMarkup = (picture: BoardPicture): string =>
  picture.body
  + (picture.strip ? boardStripMarkup(picture.strip) : '')
  + (picture.parts ?? []).map((part) => part.markup).join('');

/** A drawing that is on the board and can still be changed. */
export interface BoardDrawing {
  /** Where the drawing's origin sits, in scene units. Replaces any previous place. */
  place(x: number, y: number): void;
  /** An offset from wherever it was placed, in scene units. */
  nudge(dx: number, dy: number): void;
  /**
   * Scale about this drawing's own middle, without moving the placement.
   *
   * Which point that is belongs to whoever made the drawing, not to the caller. A
   * unit fills a tile and is placed at the cell's *origin*, so it swells about the
   * tile's middle; an effect is placed at a cell's *centre*, so its origin already
   * is its middle. One pivot served both for a while, and the burst of a hit drifted
   * 13px up and left while it grew.
   */
  swell(factor: number): void;
  /** 0 is invisible, 1 is as drawn. */
  opacity(value: number): void;
  /** Puts the drawing into, or out of, a named visual state. */
  say(state: BoardState, on: boolean): void;
  /** The part of this drawing that declared itself as `role`, if it has one. */
  part(role: BoardRole): BoardDrawing | null;
  /** Replaces everything the drawing holds under `role`. */
  fill(role: BoardRole, markup: string): void;
  /**
   * Plays a named clip on this drawing's strip.
   *
   * A picture with no strip has nothing to play, and this does nothing — the
   * deliberate silence the board used to spell as `if (animations.has(id))`. An
   * unknown clip on a picture that *does* have one still throws.
   *
   * On the drawing rather than on a unit-shaped wrapper, because a strip belongs to
   * a picture and both kinds of drawing can hold one. There was a `BoardUnit` whose
   * whole reason to exist was carrying this method and a `fresh` flag saying whether
   * this ask had made the drawing — bookkeeping about the call, kept so that the
   * board could start the idle clip exactly once. A picture that says what it
   * arrives playing needs neither.
   */
  play(clip: string): void;
}

/**
 * A drawing whose life the caller owns: made to play once, then taken away.
 *
 * Only an effect is one of these. A unit's drawing is not, and that is stated in
 * the type rather than in a comment: `remove` used to be on every drawing, so
 * taking a unit off the board was `drawing.remove()` *and* `dropUnit(id)` — two
 * calls in an order nobody enforced, either of which alone left a ghost element or
 * a leaked entry. `removeUnit` is the one act, and a unit's drawing cannot be
 * removed behind the surface's back because it has no way to be.
 *
 * `remove` also stops whatever sprite timelines this drawing started. The board
 * used to be handed a list of animation ids with every effect and every layer, for
 * no purpose but to unregister them later — cleaning up after the thing that
 * registered them.
 */
export interface BoardEffect extends BoardDrawing {
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
  readonly themeClass?: string | undefined;
  /** Crisp pixels suit a grid; painted scenes do not want them. */
  readonly shapeRendering: string;
  /** Art outside the tactical field, behind and in front of everything. */
  readonly backdrop: string;
  readonly foreground: string;
  /** The CSS this board's pictures are drawn under. See `SceneFrameMarkup`. */
  readonly style?: string | undefined;
}

/**
 * How a battle gets a renderer.
 *
 * A factory rather than a class, because the choice belongs to the application
 * root. `BoardView` used to write `new SvgBoardSurface(…)` in its constructor,
 * which is the same defect as an engine assembling its own defaults: a seam that
 * only one thing can ever be on the other side of is not a seam.
 */
export type BoardSurfaceFactory = (scene: BoardSurfaceScene) => BoardSurface;

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
  /** Replaces a layer's whole content, and whatever the old content was playing. */
  setLayer(layer: BoardLayer, pieces: readonly BoardPiece[]): void;
  /** A persistent drawing for one unit, made on first ask. */
  unit(id: number, draw: () => BoardPicture): BoardDrawing;
  /**
   * The drawing this unit already has, or `null`.
   *
   * Asking and committing are different acts. This was five call sites of
   * `unit(id, () => '')` — a call that *makes* a drawing, used to fetch one, with a
   * factory that would have drawn an empty unit had the id not been there. Each was
   * kept honest by a preceding `hasUnit`, which is a query written twice.
   */
  drawnUnit(id: number): BoardDrawing | null;
  /** Takes a unit off the board, stops its sprite, and forgets it. */
  removeUnit(id: number): void;
  /** Every unit that currently has a drawing. */
  drawnUnits(): number[];
  /** A transient drawing in the effects layer, removed by whoever played it. */
  effect(picture: BoardPicture): BoardEffect;
  /** The pixel size the picture is presented at. */
  resize(width: number, height: number): void;
  /** Whether the player is measuring with the board right now. */
  tactical(on: boolean): void;
  /** Whether the scale is currently moving, so a backend can spend less on it. */
  rescaling(on: boolean): void;
  dispose(): void;
}
