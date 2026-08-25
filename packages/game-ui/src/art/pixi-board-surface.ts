import { autoDetectRenderer, ColorMatrixFilter, Container, Sprite, Texture } from 'pixi.js';
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
  type BoardStrip,
  type BoardSurface,
  type BoardSurfaceFactory,
  type BoardSurfaceScene,
} from './board-surface';
import { FrameAnimationSystem } from './frame-animation';
import { scenePointOf, shownAt } from './letterbox';
import { BrowserPictureTextures, type BakedPicture, type PictureTextures } from './picture-textures';

/**
 * A battlefield drawn as one Pixi scene graph.
 *
 * The second backend the port was extracted for. What it draws is decided entirely
 * by `BoardView`, exactly as for the SVG one; what is different is that a picture
 * has to become a texture, and a texture is not a document — which is why the
 * rounds before this one existed. A layer arrives as pictures at places rather than
 * a string to parse. An effect's separately-animated parts are declared rather than
 * marked up. A frame strip arrives as the frames and clips it is, so it can be a
 * spritesheet here rather than an `<image>` shifted behind a clip. A campaign's
 * shadows and colour grades travel inside its art rather than in a stylesheet that
 * is not in the room when a picture is rasterised. And an effect is drawn about its
 * own origin instead of at scene coordinates, so a damage number is a small texture
 * rather than a field-sized one.
 *
 * Two things it deliberately does not reproduce, both of which live in `app.css`
 * and so are not in the picture: the drop-shadow every unit wears, and the glow of
 * a selected one. The first is absent; the second is a brightness filter, which is
 * a highlight but not the same highlight.
 */

/** What a Pixi drawing needs to turn a picture into a scene graph. */
export interface DrawingTools {
  readonly textures: PictureTextures;
  /** Shared by everything one surface draws, so one loop serves the board. */
  readonly animations: FrameAnimationSystem;
}

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
  /** Stops painting this scene if it is the one currently attached. */
  detach(scene: Container): void;
  /** The pixel size of the drawing buffer. */
  resize(width: number, height: number): void;
  dispose(): void;
}

/** What a Pixi surface needs from the machine it runs on. */
export interface PixiBoardTools {
  readonly textures: PictureTextures;
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
  /** Whether this drawing has a strip on the timeline. */
  private playing = false;

  constructor(
    private readonly tools: DrawingTools,
    readonly root: Container,
    private readonly pivot: number,
    private readonly id: string,
    picture: BoardPicture,
  ) {
    root.pivot.set(pivot, pivot);
    this.figure.pivot.set(TILE_MIDDLE, TILE_MIDDLE);
    this.figure.position.set(TILE_MIDDLE, TILE_MIDDLE);
    root.addChild(this.figure);
    this.write();
    this.body(picture.body);
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
    this.parts.get(role)?.body(markup);
  }

  play(clip: string): void {
    if (this.playing) this.tools.animations.play(this.id, clip);
  }

  /** Declares a part of this drawing, drawn over whatever came before it. */
  addPart(role: BoardRole, markup: string): PixiDrawing {
    const part = new PixiDrawing(
      this.tools,
      new Container(),
      this.pivot,
      `${this.id}/${role}`,
      { body: markup },
    );
    this.root.addChild(part.root);
    this.parts.set(role, part);
    return part;
  }

  /** Takes this drawing, and whatever it was playing, off the board. */
  destroy(): void {
    if (this.playing) this.tools.animations.unregister(this.id);
    this.playing = false;
    this.root.destroy({ children: true });
  }

  /**
   * Replaces the picture under this drawing, leaving its parts and strip alone.
   *
   * A sprite goes in immediately and its texture arrives when the bake finishes, so
   * nothing waits on a decode to know where things are. `generation` is what makes
   * that safe: a picture replaced while its predecessor was still baking must not
   * be overwritten by it.
   */
  body(markup: string): void {
    const generation = ++this.generation;
    this.figure.removeChildren().forEach((child) => child.destroy());
    if (!markup) return;
    const sprite = new Sprite(Texture.EMPTY);
    sprite.scale.set(1 / this.tools.textures.resolution);
    this.figure.addChild(sprite);
    // A bake that fails must not leave a silently blank sprite: the rejection is
    // re-thrown with the picture that caused it, so it reaches the console as
    // something a person can act on.
    void this.tools.textures.bake(markup).then(
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
    sprite.scale.set(1 / this.tools.textures.resolution);
  }

  /**
   * Draws the strip, and puts it on the timeline.
   *
   * This is what a spritesheet is on a GPU, and why it was worth declaring: the
   * frames are one texture cut into rectangles, so a walk cycle costs nothing beyond
   * the image every unit of that type already shares, and advancing a frame is
   * assigning `sprite.texture`.
   *
   * The frames arrive after the timeline has already started asking for them, so the
   * wanted frame is remembered and shown when they land. Sizing waits for them too:
   * a sprite holding `Texture.EMPTY` is one pixel wide, and giving *that* a width
   * would scale it by the whole frame.
   */
  private draw(strip: BoardStrip): void {
    const sprite = new Sprite(Texture.EMPTY);
    sprite.position.set(strip.x, strip.y);
    this.figure.addChild(sprite);

    let frames: readonly Texture[] = [];
    let wanted = 0;
    const show = (frame: number): void => {
      sprite.texture = frames[frame];
      sprite.setSize(strip.frameWidth, strip.frameHeight);
    };
    void this.tools.textures.frames(strip).then(
      (cut) => {
        if (sprite.destroyed) return;
        frames = cut;
        show(wanted);
      },
      (cause: unknown) => {
        throw new Error(`cannot cut the strip ${strip.href}`, { cause });
      },
    );

    this.tools.animations.register(this.id, {
      frameCount: strip.frameCount,
      setFrame: (frame) => {
        wanted = frame;
        if (frames.length) show(frame);
      },
    }, strip.clips);
    this.playing = true;
    if (strip.playing) this.tools.animations.play(this.id, strip.playing);
  }

  private write(): void {
    this.root.position.set(this.x + this.dx + this.pivot, this.y + this.dy + this.pivot);
    this.root.scale.set(this.factor);
  }
}

/** One transient drawing, whose life belongs to whoever played it. */
class PixiEffect extends PixiDrawing implements BoardEffect {
  remove(): void {
    this.destroy();
  }
}

export class PixiBoardSurface implements BoardSurface {
  readonly element: HTMLCanvasElement;
  /** The whole display list, and the only thing a test needs to see. */
  readonly scene = new Container();
  private readonly world = new Container();
  private readonly layers: Record<BoardLayer, Container>;
  private readonly units = new Map<number, PixiDrawing>();
  /** The same timeline the DOM backend uses, driving textures instead of attributes. */
  private readonly drawingTools: DrawingTools;
  private serial = 0;

  constructor(
    private readonly scenery: BoardSurfaceScene,
    private readonly tools: PixiBoardTools,
  ) {
    this.drawingTools = { textures: tools.textures, animations: new FrameAnimationSystem() };
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
      // A layer's pictures are still, so none of them needs a place on the timeline.
      const drawing = this.drawing(new Container(), 0, { body: piece.markup });
      drawing.place(piece.x, piece.y);
      host.addChild(drawing.root);
    }
  }

  unit(id: number, draw: () => BoardPicture): BoardDrawing {
    const existing = this.units.get(id);
    if (existing) return existing;

    const drawing = this.drawing(new Container({ label: `unit-${id}` }), TILE_MIDDLE, draw());
    // The badges part is the surface's own, exactly as in the SVG tree: it is what
    // `fill('badges', …)` replaces every render, and it must not mirror with the
    // sprite when the unit faces west.
    drawing.addPart('badges', '');
    this.layers.units.addChild(drawing.root);
    this.units.set(id, drawing);
    return drawing;
  }

  drawnUnit(id: number): BoardDrawing | null {
    return this.units.get(id) ?? null;
  }

  removeUnit(id: number): void {
    const drawing = this.units.get(id);
    if (!drawing) return;
    drawing.destroy();
    this.units.delete(id);
  }

  drawnUnits(): number[] {
    return [...this.units.keys()];
  }

  effect(picture: BoardPicture): BoardEffect {
    // An effect is placed at a cell's centre, so its origin is already its middle.
    const drawing = new PixiEffect(
      this.drawingTools,
      new Container({ label: 'fx' }),
      0,
      `strip:${this.serial++}`,
      picture,
    );
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

  /**
   * Whether the player is measuring with the board right now.
   *
   * The SVG backend spells this as a class and `battle.css` decides the two numbers.
   * They used to be three: a painted scene's lattice was hidden outright by a rule
   * naming one campaign's board class, which this backend was not told about and so
   * did not do. A painted scene's decorations draw no lattice at all now, so there
   * is nothing to hide and these two numbers are the whole rule.
   */
  tactical(on: boolean): void {
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
    this.drawingTools.animations.dispose();
    this.tools.painter.detach(this.scene);
    this.units.clear();
    this.scene.destroy({ children: true });
  }

  /** Art outside the tactical field, as one baked picture at the scene's origin. */
  private picture(markup: string): Container {
    return this.drawing(new Container(), 0, { body: markup }).root;
  }

  /** One drawing of this surface's, with its own place on the shared timeline. */
  private drawing(root: Container, pivot: number, picture: BoardPicture): PixiDrawing {
    return new PixiDrawing(this.drawingTools, root, pivot, `strip:${this.serial++}`, picture);
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
async function preparePixiPainter(options: {
  /** Passed through to Pixi. Antialiasing off suits pixel art. */
  readonly antialias?: boolean | undefined;
} = {}): Promise<ScenePainter> {
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
    detach: (root) => {
      if (scene === root) scene = null;
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
 * A reusable Pixi renderer owned by the application root.
 *
 * A surface owns its display list and animation loop registrations. The factory
 * owns the shared GPU context and texture cache; destroying one battle must not
 * invalidate the factory that the next battle will use.
 */
export interface ManagedBoardSurfaceFactory extends BoardSurfaceFactory {
  dispose(): void;
}

export const pixiBoardSurface = (
  painter: ScenePainter,
  textures: PictureTextures,
): ManagedBoardSurfaceFactory => {
  let disposed = false;
  const create = (scene: BoardSurfaceScene) => {
    if (disposed) throw new Error('Pixi board renderer has been disposed');
    return new PixiBoardSurface(scene, { painter, textures });
  };
  const dispose = () => {
    if (disposed) return;
    disposed = true;
    painter.dispose();
    textures.dispose();
  };
  return Object.assign(create, { dispose });
};

/**
 * The canonical async composition path for the optional GPU renderer.
 *
 * The caller keeps the returned factory for as many sequential battles as it
 * needs and disposes it once when the application session ends.
 */
export async function preparePixiBoardSurface(options: {
  readonly antialias?: boolean | undefined;
  readonly textureResolution?: number | undefined;
} = {}): Promise<ManagedBoardSurfaceFactory> {
  const painter = await preparePixiPainter({ antialias: options.antialias });
  return pixiBoardSurface(painter, new BrowserPictureTextures(options.textureResolution));
}
