import { ColorMatrixFilter, Container, Sprite, Texture } from 'pixi.js';
import {
  BOARD_LAYERS,
  type BoardDrawing,
  type BoardEffect,
  type BoardLayer,
  type BoardPiece,
  type BoardPicture,
  type BoardPointer,
  type BoardRole,
  type BoardState,
  type BoardSurface,
  type BoardSurfaceFactory,
  type BoardSurfaceScene,
  type BoardUnit,
} from './board-surface';
import { scenePointOf, shownAt } from './letterbox';
import type { BakedPicture, MarkupTextures } from './markup-textures';

/**
 * A battlefield drawn as one Pixi scene graph.
 *
 * The second backend the port was extracted for. What it draws is decided entirely
 * by `BoardView`, exactly as for the SVG one; what is different is that a picture
 * has to become a texture, and a texture is not a document — which is why the four
 * rounds before this one existed. A layer arrives as pictures at places rather than
 * a string to parse. An effect's separately-animated parts are declared rather than
 * marked up. A campaign's shadows and colour grades travel inside its art rather
 * than in a stylesheet that is not in the room when a picture is rasterised. And an
 * effect is drawn about its own origin instead of at scene coordinates, so a damage
 * number is a small texture rather than a field-sized one.
 *
 * Two things it deliberately does not reproduce, both of which live in `app.css`
 * and so are not in the picture: the drop-shadow every unit wears, and the glow of
 * a selected one. The first is absent; the second is a brightness filter, which is
 * a highlight but not the same highlight.
 */

/** Half a tile: the middle of a unit's drawing, which fills one. */
const TILE_MIDDLE = 16;

/** How much brighter a selected unit is drawn, standing in for a glow. */
const SELECTED_BRIGHTNESS = 1.32;

/**
 * What paints a scene graph.
 *
 * Separate from the surface because painting needs a GPU and a display list does
 * not. That split is what makes this backend testable at all: everything about
 * *what is drawn, where, and in what order* is asserted with no renderer present,
 * and only the pixels need a machine with a screen.
 */
export interface ScenePainter {
  /**
   * The canvas this painter draws on.
   *
   * The painter's, not the surface's. A Pixi renderer is created asynchronously and
   * makes its own canvas, so a surface that made one and handed it over would have
   * an async hole in its constructor and nowhere to report a failure to. Whoever
   * composes a battle awaits a painter first; after that there is nothing to wait
   * for and nothing to swallow.
   */
  readonly canvas: HTMLCanvasElement;
  /** Begins painting this scene, and keeps painting it. */
  paint(scene: Container): void;
  /** The pixel size of the drawing buffer. */
  resize(width: number, height: number): void;
  dispose(): void;
}

/** What a Pixi surface needs from the machine it runs on. */
export interface PixiBoardTools {
  readonly textures: MarkupTextures;
  readonly painter: ScenePainter;
}

/**
 * One container, with the transform it is currently under.
 *
 * The same four continuous properties the SVG drawing composes, composed the same
 * way. `swell` scales about the drawing's own middle, which for a unit is the tile's
 * middle and for an effect is its origin: with `pivot` set to that point, Pixi's
 * `tx = position - pivot * scale` reproduces
 * `translate(P) translate(m,m) scale(f) translate(-m,-m)` exactly.
 */
class PixiDrawing implements BoardDrawing {
  private x = 0;
  private y = 0;
  private dx = 0;
  private dy = 0;
  private factor = 1;
  /** Which picture is current, so a slower bake cannot overwrite a newer one. */
  private generation = 0;
  /** What a mirror flips, so a unit's badges do not flip with its sprite. */
  readonly figure = new Container();
  private readonly parts = new Map<BoardRole, PixiDrawing>();
  private highlight: ColorMatrixFilter | null = null;

  constructor(
    readonly root: Container,
    private readonly pivot: number,
    private readonly textures: MarkupTextures,
  ) {
    root.pivot.set(pivot, pivot);
    this.figure.pivot.set(TILE_MIDDLE, TILE_MIDDLE);
    this.figure.position.set(TILE_MIDDLE, TILE_MIDDLE);
    root.addChild(this.figure);
    this.write();
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
    this.root.alpha = value;
  }

  say(state: BoardState, on: boolean): void {
    if (state === 'hidden') {
      this.root.visible = !on;
      return;
    }
    if (state === 'facingLeft') {
      this.figure.scale.x = on ? -1 : 1;
      return;
    }
    // A glow needs a blur pass this backend does not run. Brighter is a highlight,
    // and being plainly not the same one is better than pretending.
    if (!on) {
      this.figure.filters = [];
      return;
    }
    this.highlight ??= new ColorMatrixFilter();
    this.highlight.brightness(SELECTED_BRIGHTNESS, false);
    this.figure.filters = [this.highlight];
  }

  part(role: BoardRole): BoardDrawing | null {
    return this.parts.get(role) ?? null;
  }

  fill(role: BoardRole, markup: string): void {
    const part = this.parts.get(role);
    if (!part) return;
    part.paint(markup);
  }

  /** Declares a part of this drawing, drawn over whatever came before it. */
  addPart(role: BoardRole, markup: string): PixiDrawing {
    const part = new PixiDrawing(new Container(), this.pivot, this.textures);
    this.root.addChild(part.root);
    this.parts.set(role, part);
    if (markup) part.paint(markup);
    return part;
  }

  /**
   * Replaces this drawing's picture.
   *
   * A sprite goes in immediately and its texture arrives when the bake finishes, so
   * nothing waits on a decode to know where things are. `generation` is what makes
   * that safe: a picture replaced while its predecessor was still baking must not
   * be overwritten by it.
   */
  paint(markup: string): void {
    const generation = ++this.generation;
    this.figure.removeChildren().forEach((child) => child.destroy());
    if (!markup) return;
    const sprite = new Sprite(Texture.EMPTY);
    sprite.scale.set(1 / this.textures.resolution);
    this.figure.addChild(sprite);
    // A bake that fails must not leave a silently blank sprite: the rejection is
    // re-thrown with the picture that caused it, so it reaches the console as
    // something a person can act on.
    void this.textures.bake(markup).then(
      (picture) => {
        if (generation !== this.generation || sprite.destroyed) return;
        this.show(sprite, picture);
      },
      (cause: unknown) => {
        throw new Error(`cannot bake a picture: ${markup.slice(0, 120)}`, { cause });
      },
    );
  }

  private show(sprite: Sprite, picture: BakedPicture): void {
    sprite.texture = picture.texture;
    sprite.position.set(picture.left, picture.top);
    sprite.scale.set(1 / this.textures.resolution);
  }

  private write(): void {
    this.root.position.set(this.x + this.dx + this.pivot, this.y + this.dy + this.pivot);
    this.root.scale.set(this.factor);
  }
}

/** One transient drawing, whose life belongs to whoever played it. */
class PixiEffect extends PixiDrawing implements BoardEffect {
  remove(): void {
    this.root.destroy({ children: true });
  }
}

/** Which ask produced a `BoardUnit`: the one that made it, or a later one. */
type Ask = 'made' | 'found';

export class PixiBoardSurface implements BoardSurface {
  readonly element: HTMLCanvasElement;
  /** The whole display list, and the only thing a test needs to see. */
  readonly scene = new Container();
  private readonly world = new Container();
  private readonly layers: Record<BoardLayer, Container>;
  private readonly units = new Map<number, PixiDrawing>();

  constructor(
    private readonly scenery: BoardSurfaceScene,
    private readonly tools: PixiBoardTools,
  ) {
    this.element = tools.painter.canvas;
    this.element.classList.add('board');

    if (scenery.backdrop) this.scene.addChild(this.picture(scenery.backdrop));
    this.world.position.set(scenery.originX, scenery.originY);
    // Child order is the depth contract, and `BOARD_LAYERS` is where it is stated.
    const layers = {} as Record<BoardLayer, Container>;
    for (const name of BOARD_LAYERS) {
      const container = new Container({ label: `layer-${name}` });
      layers[name] = container;
      this.world.addChild(container);
    }
    this.layers = layers;
    this.scene.addChild(this.world);
    if (scenery.foreground) this.scene.addChild(this.picture(scenery.foreground));

    this.tools.painter.paint(this.scene);
  }

  listen(pointer: BoardPointer): void {
    const at = (event: MouseEvent) =>
      scenePointOf(this.element.getBoundingClientRect(), this.scenery, event);

    this.element.addEventListener('pointerdown', (event) => {
      const point = at(event);
      if (point) pointer.press(point, event.button);
    });
    this.element.addEventListener('contextmenu', (event) => event.preventDefault());
    this.element.addEventListener('pointermove', (event) => pointer.move(at(event)));
    this.element.addEventListener('pointerleave', () => pointer.leave());
    this.element.addEventListener('wheel', (event) => {
      if (!event.ctrlKey && !event.metaKey) return;
      event.preventDefault();
      pointer.scale(-Math.sign(event.deltaY));
    }, { passive: false });
  }

  setLayer(layer: BoardLayer, pieces: readonly BoardPiece[]): void {
    const host = this.layers[layer];
    host.removeChildren().forEach((child) => child.destroy({ children: true }));
    for (const piece of pieces) {
      const drawing = new PixiDrawing(new Container(), 0, this.tools.textures);
      drawing.place(piece.x, piece.y);
      drawing.paint(piece.markup);
      host.addChild(drawing.root);
    }
  }

  unit(id: number, draw: () => string): BoardUnit {
    const existing = this.units.get(id);
    if (existing) return this.asUnit(existing, 'found');

    const drawing = new PixiDrawing(new Container({ label: `unit-${id}` }), TILE_MIDDLE, this.tools.textures);
    drawing.paint(draw());
    // The badges part is the surface's own, exactly as in the SVG tree: it is what
    // `fill('badges', …)` replaces every render, and it must not mirror with the
    // sprite when the unit faces west.
    drawing.addPart('badges', '');
    this.layers.units.addChild(drawing.root);
    this.units.set(id, drawing);
    return this.asUnit(drawing, 'made');
  }

  drawnUnit(id: number): BoardUnit | null {
    const drawing = this.units.get(id);
    return drawing ? this.asUnit(drawing, 'found') : null;
  }

  removeUnit(id: number): void {
    const drawing = this.units.get(id);
    if (!drawing) return;
    drawing.root.destroy({ children: true });
    this.units.delete(id);
  }

  drawnUnits(): number[] {
    return [...this.units.keys()];
  }

  effect(picture: BoardPicture): BoardEffect {
    // An effect is placed at a cell's centre, so its origin is already its middle.
    const drawing = new PixiEffect(new Container({ label: 'fx' }), 0, this.tools.textures);
    if (picture.body) drawing.paint(picture.body);
    for (const part of picture.parts ?? []) drawing.addPart(part.role, part.markup);
    this.layers.effects.addChild(drawing.root);
    return drawing;
  }

  /**
   * The pixel size the picture is presented at.
   *
   * An SVG `viewBox` letterboxes for free; here the same arithmetic is applied to
   * the scene's own transform, from the one place both backends read it.
   */
  resize(width: number, height: number): void {
    this.element.style.width = `${width}px`;
    this.element.style.height = `${height}px`;
    const shown = shownAt({ width, height }, this.scenery);
    if (shown) {
      this.scene.position.set(shown.left, shown.top);
      this.scene.scale.set(shown.scale);
    }
    this.tools.painter.resize(width, height);
  }

  /** Whether the player is measuring with the board right now. */
  tactical(on: boolean): void {
    // The SVG backend spells this as a class and a stylesheet decides the numbers,
    // including a painted scene's "never show the grid" — which this backend is not
    // told and therefore does not do.
    this.layers.grid.alpha = on ? 0.72 : 0.3;
  }

  /**
   * Whether the scale is currently moving.
   *
   * Nothing to do. The SVG backend suspends CSS filters here because rescaling
   * recomputes every one of them; a scene graph is drawn at whatever transform it
   * is under, so there is nothing to spend less on.
   */
  rescaling(): void {}

  dispose(): void {
    this.tools.painter.dispose();
    this.tools.textures.dispose();
    this.units.clear();
    this.scene.destroy({ children: true });
  }

  /** Art outside the tactical field, as one baked picture at the scene's origin. */
  private picture(markup: string): Container {
    const drawing = new PixiDrawing(new Container(), 0, this.tools.textures);
    drawing.paint(markup);
    return drawing.root;
  }

  private asUnit(drawing: PixiDrawing, ask: Ask): BoardUnit {
    return {
      drawing,
      fresh: ask === 'made',
      // Frame strips are a DOM sprite-sheet trick: the picture is one wide image
      // shifted behind a clip. Baked into a texture the shift is already in it, so
      // there is nothing here to advance yet, and a unit stands on its first frame.
      play: () => {},
    };
  }

  /**
   * What is drawn, where, and in what order — the display list, as plain data.
   *
   * The place a drawing was put, not the container's raw position: those differ by
   * the pivot, which is a tile's middle for a unit and zero for a layer piece.
   */
  displayList(): Array<{ layer: BoardLayer; drawn: Array<{ x: number; y: number; label: string }> }> {
    return BOARD_LAYERS.map((layer) => ({
      layer,
      drawn: this.layers[layer].children.map((child) => ({
        x: child.x - child.pivot.x,
        y: child.y - child.pivot.y,
        label: child.label ?? '',
      })),
    }));
  }
}

/**
 * A painter backed by a real Pixi renderer, prepared before a battle is composed.
 *
 * Asynchronous because `autoDetectRenderer` is: it decides between WebGPU and WebGL
 * and builds a context. Awaiting it here rather than inside the surface is what
 * keeps the failure reportable — a GPU that will not come up is something the
 * application root can answer for, and a promise nobody holds is not.
 */
export async function preparePixiPainter(options: {
  /** Passed through to Pixi. Antialiasing off suits pixel art. */
  readonly antialias?: boolean;
} = {}): Promise<ScenePainter> {
  const { autoDetectRenderer } = await import('pixi.js');
  const renderer = await autoDetectRenderer({
    antialias: options.antialias ?? false,
    backgroundAlpha: 0,
    // The buffer is device pixels and the canvas is CSS pixels, which is what
    // `autoDensity` reconciles. A texture's own resolution is a separate question,
    // answered by whatever bakes it.
    resolution: globalThis.devicePixelRatio || 1,
    autoDensity: true,
  });

  let scene: Container | null = null;
  let frame: number | null = null;
  const draw = () => {
    frame = requestAnimationFrame(draw);
    if (scene) renderer.render(scene);
  };

  return {
    canvas: renderer.canvas as HTMLCanvasElement,
    paint: (root) => {
      scene = root;
      if (frame === null) frame = requestAnimationFrame(draw);
    },
    resize: (width, height) => renderer.resize(width, height),
    dispose: () => {
      if (frame !== null) cancelAnimationFrame(frame);
      frame = null;
      scene = null;
      renderer.destroy();
    },
  };
}

/**
 * This renderer, as the application root selects one.
 *
 * A painter has to be awaited, so unlike `svgBoardSurface` this is a function of
 * one: `renderer: pixiBoardSurface(await preparePixiPainter())`.
 */
export const pixiBoardSurface = (
  painter: ScenePainter,
  textures: MarkupTextures,
): BoardSurfaceFactory => (scene) => new PixiBoardSurface(scene, { painter, textures });
