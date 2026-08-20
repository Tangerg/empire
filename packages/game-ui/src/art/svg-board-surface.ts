import { FrameAnimationSystem, registerSvgStrip } from './frame-animation';
import {
  BOARD_LAYERS,
  boardPiecesMarkup,
  type BoardDrawing,
  type BoardEffect,
  type BoardPointer,
  type BoardLayer,
  type BoardPiece,
  type BoardRole,
  type BoardState,
  type BoardSurface,
  type BoardSurfaceScene,
  type BoardSurfaceFactory,
  type BoardUnit,
} from './board-surface';
import { clear, fromMarkup, setAttrs, svg } from './svg';

/**
 * A battlefield drawn as one SVG tree.
 *
 * The renderer this project was built on, now behind the port instead of fused
 * into the board. The layer classes and the state classes are load-bearing — the
 * stylesheet and forty-five test assertions read them. `data-unit` is not: it
 * names a unit in `tools/board-digest.ts` output so a human can read a renderer
 * diff, and it sits on the wrapper this surface makes rather than inside the
 * picture, so it costs a texture cache nothing.
 */

/** What a `BoardState` is called in the stylesheet. */
const STATE_CLASS: Readonly<Record<BoardState, string>> = {
  facingLeft: 'face-left',
  moving: 'is-moving',
  attacking: 'is-attacking',
  done: 'is-done',
  selected: 'is-selected',
  hidden: 'is-hidden',
};

/** Half a tile, which is what a drawing swells about. */
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

  constructor(readonly el: SVGGElement) {}

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

  inState(state: BoardState): boolean {
    return state === 'hidden'
      ? this.el.style.display === 'none'
      : this.el.classList.contains(STATE_CLASS[state]);
  }

  part(role: BoardRole): BoardDrawing | null {
    const found = this.el.querySelector<SVGGElement>(`[data-part="${role}"]`);
    return found ? new SvgDrawing(found) : null;
  }

  fill(role: BoardRole, markup: string): void {
    const host = this.el.querySelector<SVGGElement>(`[data-part="${role}"]`);
    if (!host) return;
    clear(host);
    if (markup) host.append(fromMarkup(markup));
  }

  /**
   * Takes the element out of the tree.
   *
   * Not `remove`, and not on `BoardDrawing`. The surface decides when a unit's
   * drawing goes, because it also has a sprite timeline to stop and an entry to
   * forget; an effect gets the public `remove` that does all three.
   */
  detach(): void {
    this.el.remove();
  }

  private write(): void {
    const moved = `translate(${(this.x + this.dx).toFixed(2)},${(this.y + this.dy).toFixed(2)})`;
    const scaled = this.factor === 1
      ? ''
      : ` translate(${TILE_MIDDLE},${TILE_MIDDLE}) scale(${this.factor.toFixed(4)}) translate(${-TILE_MIDDLE},${-TILE_MIDDLE})`;
    setAttrs(this.el, { transform: `${moved}${scaled}` });
  }
}

/** One transient drawing, and the sprite timelines it started. */
class SvgEffect extends SvgDrawing implements BoardEffect {
  constructor(
    el: SVGGElement,
    private readonly animations: FrameAnimationSystem,
    private readonly strips: readonly string[],
  ) {
    super(el);
  }

  remove(): void {
    for (const id of this.strips) this.animations.unregister(id);
    this.detach();
  }
}

/** Which ask produced a `BoardUnit`: the one that made it, or a later one. */
type Ask = 'made' | 'found';

/** What the surface keeps for a unit: its drawing, and its place in the timeline. */
interface UnitRecord {
  readonly drawing: SvgDrawing;
  readonly animationId: string | null;
}

export class SvgBoardSurface implements BoardSurface {
  readonly element: SVGSVGElement;
  private readonly layers: Record<BoardLayer, SVGGElement>;
  private readonly units = new Map<number, UnitRecord>();
  /**
   * What each layer's current content is playing.
   *
   * Replacing a layer discards its strips, so the surface unregisters them. The
   * board used to hold this list itself and unregister before every `setLayer` —
   * cleaning up after the call it was about to make.
   */
  private readonly layerStrips = new Map<BoardLayer, string[]>();
  /**
   * The sprite timeline for everything this surface draws.
   *
   * Owned rather than handed in. It was constructed by the board and passed down,
   * which is what let the board register nothing and unregister everything.
   */
  private readonly animations = new FrameAnimationSystem();
  private effectSerial = 0;

  constructor(private readonly scene: BoardSurfaceScene) {
    this.element = svg('svg', {
      viewBox: `0 0 ${scene.width} ${scene.height}`,
      class: `board${scene.themeClass ? ` ${scene.themeClass}` : ''}`,
      'shape-rendering': scene.shapeRendering,
      'text-rendering': 'optimizeLegibility',
      'data-scene-layout': scene.originX || scene.originY ? 'authored' : 'grid',
    });

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
    for (const id of this.layerStrips.get(layer) ?? []) this.animations.unregister(id);
    this.layerStrips.delete(layer);
    clear(host);
    if (!pieces.length) return;
    // One parse for the whole layer: building four thousand groups by hand is
    // slower than letting the parser do it, and the string is the same one
    // `boardPiecesMarkup` hands to a thumbnail or to the editor's own canvas.
    host.append(fromMarkup(boardPiecesMarkup(pieces)));
    const strips = this.playStrips(host);
    if (strips.length) this.layerStrips.set(layer, strips);
  }

  drawnUnits(): number[] {
    return [...this.units.keys()];
  }

  unit(id: number, draw: () => string): BoardUnit {
    const existing = this.units.get(id);
    if (existing) return this.asUnit(existing, 'found');

    const el = svg('g', { class: 'unit', 'data-unit': id });
    el.append(fromMarkup(draw()));
    // The badges group declares its role, so nothing has to find it by position.
    el.append(svg('g', { class: 'badges', 'data-part': 'badges' }));
    this.layers.units.append(el);

    const strip = el.querySelector('.runtime-frame-strip') as SVGImageElement | null;
    const animationId = strip ? `unit:${id}` : null;
    if (strip && animationId) registerSvgStrip(this.animations, animationId, strip);
    const record: UnitRecord = { drawing: new SvgDrawing(el), animationId };
    this.units.set(id, record);
    return this.asUnit(record, 'made');
  }

  drawnUnit(id: number): BoardUnit | null {
    const record = this.units.get(id);
    return record ? this.asUnit(record, 'found') : null;
  }

  removeUnit(id: number): void {
    const record = this.units.get(id);
    if (!record) return;
    if (record.animationId) this.animations.unregister(record.animationId);
    record.drawing.detach();
    this.units.delete(id);
  }

  effect(markup: string): BoardEffect {
    const group = svg('g', { class: 'fx' });
    group.append(fromMarkup(markup));
    this.layers.effects.append(group);
    return new SvgEffect(group, this.animations, this.playStrips(group));
  }

  resize(width: number, height: number): void {
    this.element.style.width = `${width}px`;
    this.element.style.height = `${height}px`;
  }

  tactical(on: boolean): void {
    this.element.classList.toggle('is-tactical', on);
  }

  rescaling(on: boolean): void {
    this.element.classList.toggle('is-rescaling', on);
  }

  dispose(): void {
    this.animations.dispose();
    this.layerStrips.clear();
    this.units.clear();
  }

  /** A unit as the port describes one: what to change, and what to play on it. */
  private asUnit(record: UnitRecord, ask: Ask): BoardUnit {
    return {
      drawing: record.drawing,
      fresh: ask === 'made',
      play: (clip) => {
        if (record.animationId) this.animations.play(record.animationId, clip);
      },
    };
  }

  /** Registers and starts every self-describing frame strip inside a subtree. */
  private playStrips(root: Element): string[] {
    const ids: string[] = [];
    for (const strip of root.querySelectorAll<SVGImageElement>('.runtime-frame-strip')) {
      const id = `effect:${this.effectSerial++}`;
      registerSvgStrip(this.animations, id, strip);
      const clips = JSON.parse(strip.getAttribute('data-frame-clips') ?? '[]') as Array<{ id?: string }>;
      const clip = clips[0]?.id;
      if (clip) this.animations.play(id, clip);
      ids.push(id);
    }
    return ids;
  }
}

/** This renderer, as the application root selects one. */
export const svgBoardSurface: BoardSurfaceFactory = (scene) => new SvgBoardSurface(scene);
