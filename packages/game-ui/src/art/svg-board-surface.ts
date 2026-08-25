import { FrameAnimationSystem } from './frame-animation';
import {
  drawableBody,
  GRID_ALPHA,
  BOARD_LAYERS,
  boardPiecesMarkup,
  boardStripMarkup,
  boardStripWindow,
  type BoardDrawing,
  type BoardEffect,
  type BoardPointer,
  type BoardLayer,
  type BoardPiece,
  type BoardPicture,
  type BoardRole,
  type BoardState,
  type BoardStrip,
  type BoardSurface,
  type BoardSurfaceScene,
  type BoardSurfaceFactory,
} from './board-surface';
import { clear, fromMarkup, setAttrs, svg } from './svg';

/**
 * A battlefield drawn as one SVG tree.
 *
 * The renderer this project was built on, now behind the port instead of fused
 * into the board. The layer classes and the state classes are load-bearing — the
 * stylesheet and forty-five test assertions read them. `data-part` names a role a
 * test asserts. There was a `data-unit` here too, kept so that a human could tell
 * which unit a block of `tools/board-digest.ts` output belonged to; the only code
 * in the repository that mentioned it was `tools/board-scale.ts`, stripping it back
 * out because it made every unit's wrapper a distinct picture. A label a tool has
 * to work around is not a label anything reads.
 *
 * Layers hold still pictures. A `BoardPiece` is markup at a place and carries no
 * strip, so replacing a layer cannot orphan a running clip — which is what the
 * `layerStrips` map here, and the identical one the board kept before it, existed
 * to prevent. Only units and effects animate, because only they declare a strip.
 */

/**
 * What a `BoardState` is called in the stylesheet.
 *
 * `hidden` is not in here. It is `display: none`, and the entry that used to name
 * `is-hidden` was unreachable — both `say` and `inState` branched on `hidden`
 * before they ever reached this table.
 */
const STATE_CLASS: Readonly<Record<Exclude<BoardState, 'hidden'>, string>> = {
  facingLeft: 'face-left',
  selected: 'is-selected',
};

/** Half a tile: the middle of a unit's drawing, which fills one. */
const TILE_MIDDLE = 16;

/**
 * One SVG group, with the transform it is currently under.
 *
 * Placement, offset and scale are held apart and composed on write. They used to be
 * concatenated into transform strings at seven call sites, each with its own idea of
 * whether coordinates are separated by a comma or a space, and each having to
 * remember what the other had already put there.
 */
class SvgDrawing implements BoardDrawing {
  private x = 0;
  private y = 0;
  private dx = 0;
  private dy = 0;
  private factor = 1;
  /**
   * What a mirror flips, so a unit's badges do not flip with its sprite.
   *
   * The stylesheet used to reach for `.unit.face-left > g:first-child` — whichever
   * element the picture's markup happened to start with. For a hand-drawn sprite
   * that is the whole figure; for one drawn from the rules it is the first of
   * several groups, and only that one would have mirrored. Both backends name the
   * thing that flips now.
   */
  private readonly figure = svg('g', { class: 'figure' });
  private readonly parts = new Map<BoardRole, SvgDrawing>();
  /** The strip this drawing registered on the timeline, if it drew one. */
  private playing = false;

  /**
   * @param pivot the drawing's own middle, which is what `swell` scales about.
   * @param id this drawing's place on the shared timeline.
   */
  constructor(
    private readonly animations: FrameAnimationSystem,
    readonly el: SVGGElement,
    private readonly pivot: number,
    private readonly id: string,
    picture: BoardPicture,
  ) {
    el.append(this.figure);
    this.body(drawableBody(picture));
    if (picture.strip) this.draw(picture.strip);
    for (const part of picture.parts ?? []) this.addPart(part.role, part.markup);
  }

  place(x: number, y: number): void {
    this.x = x;
    this.y = y;
    this.write();
  }

  nudge(dx: number, dy: number): void {
    this.dx = dx;
    this.dy = dy;
    this.write();
  }

  swell(factor: number): void {
    this.factor = factor;
    this.write();
  }

  opacity(value: number): void {
    this.el.style.opacity = value >= 1 ? '' : String(value);
  }

  say(state: BoardState, on: boolean): void {
    if (state === 'hidden') {
      this.el.style.display = on ? 'none' : '';
      return;
    }
    this.el.classList.toggle(STATE_CLASS[state], on);
  }

  part(role: BoardRole): BoardDrawing | null {
    return this.parts.get(role) ?? null;
  }

  fill(role: BoardRole, markup: string): void {
    this.parts.get(role)?.body(markup);
  }

  play(clip: string): void {
    if (this.playing) this.animations.play(this.id, clip);
  }

  /** Declares a part of this drawing, drawn over whatever came before it. */
  addPart(role: BoardRole, markup: string): SvgDrawing {
    // A part is drawn in its parent's coordinates, so it has the same middle, and it
    // cannot itself hold a strip — nothing declares one, so nothing plays here.
    const part = new SvgDrawing(
      this.animations,
      svg('g', { 'data-part': role }),
      this.pivot,
      `${this.id}/${role}`,
      { body: markup },
    );
    this.el.append(part.el);
    this.parts.set(role, part);
    return part;
  }

  /** Replaces the picture under this drawing, leaving its parts alone. */
  body(markup: string): void {
    clear(this.figure);
    if (markup) this.figure.append(fromMarkup(markup));
  }

  /**
   * Takes the element out of the tree, and off the timeline.
   *
   * Not `remove`, and not on `BoardDrawing`. The surface decides when a unit's
   * drawing goes, because it also has an entry to forget; an effect gets the public
   * `remove` that does both.
   */
  detach(): void {
    if (this.playing) this.animations.unregister(this.id);
    this.playing = false;
    this.el.remove();
  }

  /**
   * Draws the strip, and puts it on the timeline.
   *
   * The window is the element that moves, so this holds the one node it needs and
   * looks nothing up. What it used to do was append markup, find the `<image>` in
   * it again by class name, and read the frame geometry back out of the attributes
   * the markup had just serialised.
   */
  private draw(strip: BoardStrip): void {
    const window = fromMarkup(boardStripMarkup(strip)).firstElementChild as SVGSVGElement;
    this.figure.append(window);
    this.animations.register(this.id, {
      frameCount: strip.frameCount,
      setFrame: (frame) => setAttrs(window, { viewBox: boardStripWindow(strip, frame) }),
    }, strip.clips);
    this.playing = true;
    if (strip.playing) this.animations.play(this.id, strip.playing);
  }

  private write(): void {
    const moved = `translate(${(this.x + this.dx).toFixed(2)},${(this.y + this.dy).toFixed(2)})`;
    const scaled = this.factor === 1
      ? ''
      : this.pivot === 0
        ? ` scale(${this.factor.toFixed(4)})`
        : ` translate(${this.pivot},${this.pivot}) scale(${this.factor.toFixed(4)}) translate(${-this.pivot},${-this.pivot})`;
    setAttrs(this.el, { transform: `${moved}${scaled}` });
  }
}

/** One transient drawing, whose life belongs to whoever played it. */
class SvgEffect extends SvgDrawing implements BoardEffect {
  remove(): void {
    this.detach();
  }
}

export class SvgBoardSurface implements BoardSurface {
  readonly element: SVGSVGElement;
  private readonly layers: Record<BoardLayer, SVGGElement>;
  private readonly units = new Map<number, SvgDrawing>();
  /**
   * The sprite timeline for everything this surface draws.
   *
   * Owned rather than handed in. It was constructed by the board and passed down,
   * which is what let the board register nothing and unregister everything.
   */
  private readonly animations = new FrameAnimationSystem();
  private serial = 0;

  constructor(private readonly scene: BoardSurfaceScene) {
    this.element = svg('svg', {
      viewBox: `0 0 ${scene.width} ${scene.height}`,
      class: `board${scene.themeClass ? ` ${scene.themeClass}` : ''}`,
      'shape-rendering': scene.shapeRendering,
      'text-rendering': 'optimizeLegibility',
    });

    // The board's own CSS, in the tree, before anything it applies to.
    if (scene.style) this.element.append(fromMarkup(scene.style));
    if (scene.backdrop) this.element.append(fromMarkup(scene.backdrop));
    const world = svg('g', {
      class: 'board-world',
      transform: `translate(${scene.originX} ${scene.originY})`,
    });
    // Child order is the depth contract, and `BOARD_LAYERS` is where it is stated.
    const layers = {} as Record<BoardLayer, SVGGElement>;
    for (const name of BOARD_LAYERS) {
      const group = svg('g', { class: `layer layer-${name}` });
      layers[name] = group;
      world.append(group);
    }
    this.layers = layers;
    this.element.append(world);
    if (scene.foreground) this.element.append(fromMarkup(scene.foreground));
  }

  /**
   * Screen to scene, which is the only geometry this renderer owns.
   *
   * The picture is letterboxed inside whatever box it is presented at, so the
   * scale is the smaller ratio and the leftover is split evenly. That arithmetic
   * lived in the board, where it was the one thing in it that knew about pixels.
   */
  listen(pointer: BoardPointer): void {
    const scenePoint = (event: MouseEvent): { x: number; y: number } | null => {
      const rect = this.element.getBoundingClientRect();
      if (rect.width <= 0 || rect.height <= 0) return null;
      const scale = Math.min(rect.width / this.scene.width, rect.height / this.scene.height);
      const shownWidth = this.scene.width * scale;
      const shownHeight = this.scene.height * scale;
      return {
        x: (event.clientX - rect.left - (rect.width - shownWidth) / 2) / scale,
        y: (event.clientY - rect.top - (rect.height - shownHeight) / 2) / scale,
      };
    };

    this.element.addEventListener('pointerdown', (event) => {
      const at = scenePoint(event);
      if (at) pointer.press(at, event.button);
    });
    this.element.addEventListener('contextmenu', (event) => event.preventDefault());
    this.element.addEventListener('pointermove', (event) => pointer.move(scenePoint(event)));
    this.element.addEventListener('pointerleave', () => pointer.leave());
    this.element.addEventListener('wheel', (event) => {
      if (!event.ctrlKey && !event.metaKey) return;
      event.preventDefault();
      pointer.scale(-Math.sign(event.deltaY));
    }, { passive: false });
  }

  setLayer(layer: BoardLayer, pieces: readonly BoardPiece[]): void {
    const host = this.layers[layer];
    clear(host);
    if (!pieces.length) return;
    // One parse for the whole layer: building four thousand groups by hand is
    // slower than letting the parser do it, and the string is the same one
    // `boardPiecesMarkup` hands to a thumbnail or to the editor's own canvas.
    // The parsed pieces, not the group the parser wrapped them in. That wrapper made
    // a layer filled by `setLayer` a different shape from the units layer, whose
    // drawings are direct children — two spellings of "the pictures in a layer",
    // which is one more than a second backend can be held to.
    host.append(...[...fromMarkup(boardPiecesMarkup(pieces)).childNodes]);
  }

  drawnUnits(): number[] {
    return [...this.units.keys()];
  }

  unit(id: number, draw: () => BoardPicture): BoardDrawing {
    const existing = this.units.get(id);
    if (existing) return existing;

    const picture = draw();
    const drawing = new SvgDrawing(
      this.animations,
      svg('g', { class: 'unit' }),
      TILE_MIDDLE,
      `strip:${this.serial++}`,
      picture,
    );
    // The badges group is the surface's own, not the picture's: it is what
    // `fill('badges', …)` replaces every render, and it must not mirror with the
    // sprite when the unit faces west.
    drawing.addPart('badges', '');
    this.layers.units.append(drawing.el);
    this.units.set(id, drawing);
    return drawing;
  }

  drawnUnit(id: number): BoardDrawing | null {
    return this.units.get(id) ?? null;
  }

  removeUnit(id: number): void {
    const drawing = this.units.get(id);
    if (!drawing) return;
    drawing.detach();
    this.units.delete(id);
  }

  effect(picture: BoardPicture): BoardEffect {
    // An effect is placed at a cell's centre, so its origin is already its middle.
    const drawing = new SvgEffect(
      this.animations,
      svg('g', { class: 'fx' }),
      0,
      `strip:${this.serial++}`,
      picture,
    );
    this.layers.effects.append(drawing.el);
    return drawing;
  }

  resize(width: number, height: number): void {
    this.element.style.width = `${width}px`;
    this.element.style.height = `${height}px`;
  }

  tactical(on: boolean): void {
    this.element.classList.toggle('is-tactical', on);
    // The number comes from the port, not from a stylesheet: `tactical` is a
    // method both backends implement, so both read the same two values.
    this.layers.grid.style.opacity = String(on ? GRID_ALPHA.tactical : GRID_ALPHA.resting);
  }

  rescaling(on: boolean): void {
    this.element.classList.toggle('is-rescaling', on);
  }

  dispose(): void {
    this.animations.dispose();
    this.units.clear();
  }
}

/** This renderer, as the application root selects one. */
export const svgBoardSurface: BoardSurfaceFactory = (scene) => new SvgBoardSurface(scene);
