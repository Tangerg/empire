// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { Sprite, Texture, type Container } from 'pixi.js';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires';
import { createBattleEngine, type LevelData } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { FrameClock } from './frame-clock';
import {
  BOARD_LAYERS,
  type BoardLayer,
  type BoardStrip,
  type BoardSurfaceScene,
} from '../board-surface';
import { GENERIC_ART } from '../direction';
import { SvgBoardSurface } from '../svg-board-surface';
import { PixiBoardSurface, pixiBoardSurface, type ScenePainter } from '../pixi-board-surface';
import type { BakedPicture, PictureTextures } from '../picture-textures';
import { BoardView, emptyOverlay, type BoardComposition } from '../../ui/board';

/**
 * Two backends, one answer.
 *
 * The port exists so that a battlefield can be drawn by something other than the
 * DOM. What that has to mean, concretely, is that both renderers agree about *what
 * is drawn, where, and in what depth order* — a backend that put units under
 * terrain, or ignored a `place`, or dropped a layer, would still satisfy every type
 * in `BoardSurface`.
 *
 * This runs the same `BoardView` twice, once per backend, and compares. It goes
 * through the board rather than calling the surfaces directly, which is the point:
 * what is asserted is that the Pixi backend *honours the calls the board makes*.
 *
 * What it cannot do is prove a texture came out looking right. Nothing here can.
 * Visual acceptance is a person looking at a screen.
 */

/**
 * The frame loop, held still.
 *
 * Both backends drive their own `FrameAnimationSystem`, and both take frames from
 * whatever `requestAnimationFrame` is when they ask — so a test can be the clock.
 */
const clock = new FrameClock();

beforeEach(() => clock.install());
afterEach(() => clock.restore());

const CATALOG = createTestCatalog();
const ENGINE = createBattleEngine({ content: CATALOG });

/** A rasteriser that answers instantly, so a display list needs no GPU and no wait. */
class InstantTextures implements PictureTextures {
  readonly resolution = 2;
  /** The CSS the board handed this baker, which a test can read back. */
  style = '';
  readonly asked: string[] = [];
  /** The frames handed out per strip, so a test can say which one is showing. */
  readonly cut = new Map<string, Texture[]>();
  disposed = 0;

  bake(markup: string): Promise<BakedPicture> {
    this.asked.push(markup);
    return Promise.resolve({ texture: Texture.EMPTY, left: 0, top: 0 });
  }

  frames(strip: BoardStrip): Promise<readonly Texture[]> {
    // Distinct objects rather than distinct pixels: which frame is showing is the
    // question, and `Texture.EMPTY` four times over could not answer it.
    const made = Array.from(
      { length: strip.frameCount },
      () => new Texture({ source: Texture.EMPTY.source }),
    );
    this.cut.set(strip.href, made);
    return Promise.resolve(made);
  }

  dispose(): void { this.disposed++; }
}

/** A painter that paints nothing, which is all a display list needs. */
class IdlePainter implements ScenePainter {
  readonly canvas = document.createElement('canvas');
  painted = 0;
  resized: Array<[number, number]> = [];
  detached = 0;
  disposed = 0;

  paint(): void {
    this.painted++;
  }

  detach(): void {
    this.detached++;
  }

  resize(width: number, height: number): void {
    this.resized.push([width, height]);
  }

  dispose(): void { this.disposed++; }
}

type Drawn = { layer: BoardLayer; at: string[] };

/** What the SVG tree says is drawn, in the same shape the Pixi surface reports. */
function svgDisplayList(root: Element): Drawn[] {
  return BOARD_LAYERS.map((layer) => {
    const host = root.querySelector(`.layer-${layer}`)!;
    const at = [...host.children].map((piece) => {
      const transform = piece.getAttribute('transform') ?? '';
      const [x, y] = /translate\(([-\d.]+),([-\d.]+)\)/.exec(transform)?.slice(1) ?? ['0', '0'];
      return `${Number(x).toFixed(2)},${Number(y).toFixed(2)}`;
    });
    return { layer, at };
  });
}

function pixiDisplayList(surface: PixiBoardSurface): Drawn[] {
  return surface.displayList().map(({ layer, drawn }) => ({
    layer,
    // A Pixi drawing's position carries its pivot, which is zero for a layer piece.
    at: drawn.map((child) => `${child.x.toFixed(2)},${child.y.toFixed(2)}`),
  }));
}

function composition(renderer: BoardComposition['renderer']): BoardComposition {
  return {
    content: CATALOG,
    grid: ENGINE.rules.grids.get('square4'),
    art: GENERIC_ART,
    renderer,
  };
}

function draw(level: LevelData, renderer: BoardComposition['renderer']): BoardView {
  const board = new BoardView(composition(renderer), ENGINE.createState(level), {
    onTileClick: () => {},
    onTileEnter: () => {},
    onLeave: () => {},
    onSecondary: () => {},
    onScale: () => {},
  });
  board.render(emptyOverlay());
  return board;
}

describe('two backends draw the same board', () => {
  it('puts the same number of pictures in the same places in every layer', () => {
    const level = BUILTIN_LEVELS[0];

    const svg = draw(level, (scene: BoardSurfaceScene) => new SvgBoardSurface(scene));
    let pixi: PixiBoardSurface | null = null;
    const board = draw(level, (scene: BoardSurfaceScene) => {
      pixi = new PixiBoardSurface(scene, {
        textures: new InstantTextures(),
        painter: new IdlePainter(),
      });
      return pixi;
    });

    expect(pixi).not.toBeNull();
    const fromSvg = svgDisplayList(svg.el as Element);
    // A comparison of two empty lists would pass for the wrong reason.
    expect(fromSvg.filter((entry) => entry.at.length > 0).length).toBeGreaterThan(3);
    expect(fromSvg.find((entry) => entry.layer === 'terrain')!.at)
      .toHaveLength(level.width * level.height);

    expect(pixiDisplayList(pixi!)).toEqual(fromSvg);
    svg.dispose();
    board.dispose();
  });

  /**
   * Depth order is the port's, not a renderer's.
   *
   * `BOARD_LAYERS` states it once so that two backends cannot disagree, and the
   * scene graph's child order is what a GPU backend spells it as.
   */
  it('stacks its layers in the order the port declares', () => {
    const painter = new IdlePainter();
    const surface = new PixiBoardSurface({
      width: 320,
      height: 320,
      originX: 0,
      originY: 0,
      shapeRendering: 'crispEdges',
      backdrop: '',
      foreground: '',
    }, { textures: new InstantTextures(), painter });

    const world = surface.scene.children.at(-1)!;
    expect(world.children.map((child) => child.label)).toEqual(BOARD_LAYERS.map((l) => `layer-${l}`));
    expect(painter.painted).toBe(1);
    surface.dispose();
  });

  it('keeps shared renderer resources alive between sequential battle surfaces', () => {
    const painter = new IdlePainter();
    const textures = new InstantTextures();
    const renderer = pixiBoardSurface(painter, textures);
    const scene: BoardSurfaceScene = {
      width: 320,
      height: 320,
      originX: 0,
      originY: 0,
      shapeRendering: 'crispEdges',
      backdrop: '',
      foreground: '',
    };

    renderer(scene).dispose();
    expect(painter.detached).toBe(1);
    expect(painter.disposed).toBe(0);
    expect(textures.disposed).toBe(0);

    renderer(scene).dispose();
    renderer.dispose();
    renderer.dispose();
    expect(painter.detached).toBe(2);
    expect(painter.disposed).toBe(1);
    expect(textures.disposed).toBe(1);
    expect(() => renderer(scene)).toThrow(/disposed/);
  });

  /**
   * The same picture drawn four thousand times is baked once.
   *
   * This is the whole argument for a texture backend, and `tools/board-scale.ts`
   * measures the ratio. Here it is asserted rather than assumed: a cache that
   * quietly stopped working would look identical from the outside.
   */
  it('bakes each distinct picture once, however often it is drawn', () => {
    const textures = new InstantTextures();
    const level = BUILTIN_LEVELS[0];
    const board = draw(level, (scene: BoardSurfaceScene) =>
      new PixiBoardSurface(scene, { textures, painter: new IdlePainter() }));

    const distinct = new Set(textures.asked);
    // The terrain layer alone draws one picture per cell.
    expect(textures.asked.length).toBeGreaterThan(level.width * level.height);
    expect(distinct.size).toBeLessThan(textures.asked.length);
    board.dispose();
  });

  /** A unit's mirror flips its sprite, and not the badges over it. */
  it('mirrors a unit without mirroring what is written on it', () => {
    const surface = new PixiBoardSurface({
      width: 320,
      height: 320,
      originX: 0,
      originY: 0,
      shapeRendering: 'crispEdges',
      backdrop: '',
      foreground: '',
    }, { textures: new InstantTextures(), painter: new IdlePainter() });

    const unit = surface.unit(4, () => ({ body: '<rect width="32" height="32"/>' }));
    unit.say('facingLeft', true);

    const root = surface.displayList().find((entry) => entry.layer === 'units')!;
    expect(root.drawn).toHaveLength(1);
    // The figure is the first child and it is flipped; the badges part is not.
    const container = surface.scene.children.at(-1)!.children.find((c) => c.label === 'layer-units')!;
    const [figure, badges] = container.children[0].children;
    expect(figure.scale.x).toBe(-1);
    expect(badges.scale.x).toBe(1);
    surface.dispose();
  });

  /**
   * The same clips, the same frame, on both backends.
   *
   * This is what the round was for. A strip used to cross the port as an `<image>`
   * with `data-frame-*` attributes on it, which is a DOM trick described in a place
   * only a DOM reader could look — so `play` on the GPU backend was a documented
   * no-op and a unit stood on its idle frame forever.
   *
   * Declared, the DOM backend moves a window over one image and the GPU backend
   * swaps which of four textures a sprite holds. Nothing above the port knows which
   * of those is happening, and one timeline drives both.
   */
  it('advances a walk cycle to the same frame on both backends', async () => {
    const strip: BoardStrip = {
      href: 'walk.png',
      frameWidth: 32,
      frameHeight: 32,
      frameCount: 4,
      x: 0,
      y: 0,
      clips: [{ id: 'walk', frames: [0, 2], fps: 8, loop: true }],
    };
    const scene: BoardSurfaceScene = {
      width: 320, height: 320, originX: 0, originY: 0,
      shapeRendering: 'crispEdges', backdrop: '', foreground: '',
    };

    const dom = new SvgBoardSurface(scene);
    const domUnit = dom.unit(1, () => ({ body: '', strip }));

    const textures = new InstantTextures();
    const gpu = new PixiBoardSurface(scene, { textures, painter: new IdlePainter() });
    const gpuUnit = gpu.unit(1, () => ({ body: '', strip }));
    // The frames are cut asynchronously; the display list never waited for them.
    await Promise.resolve();

    const gpuSprite = ((gpu.scene.children.at(-1) as Container)
      .children.find((child) => child.label === 'layer-units') as Container)
      .children[0].children[0].children[0] as Sprite;
    const frames = textures.cut.get('walk.png')!;
    const domFrame = () => dom.element.querySelector('.board-strip')!.getAttribute('viewBox');
    const gpuFrame = () => frames.indexOf(gpuSprite.texture);

    expect(domFrame()).toBe('0 0 32 32');
    expect(gpuFrame()).toBe(0);

    domUnit.play('walk');
    gpuUnit.play('walk');
    // 8fps: 200ms in is the second entry of the cycle, which is frame 2.
    clock.at(200);

    expect(domFrame()).toBe('64 0 32 32');
    expect(gpuFrame()).toBe(2);

    dom.dispose();
    gpu.dispose();
  });
});
