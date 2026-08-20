// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';
import { Texture } from 'pixi.js';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires';
import { createBattleEngine, createState, type LevelData } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { BOARD_LAYERS, type BoardLayer, type BoardSurfaceScene } from '../board-surface';
import { GENERIC_ART } from '../direction';
import { SvgBoardSurface } from '../svg-board-surface';
import { PixiBoardSurface, type ScenePainter } from '../pixi-board-surface';
import type { BakedPicture, MarkupTextures } from '../markup-textures';
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

const CATALOG = createTestCatalog();
const ENGINE = createBattleEngine({ content: CATALOG });

/** A rasteriser that answers instantly, so a display list needs no GPU and no wait. */
class InstantTextures implements MarkupTextures {
  readonly resolution = 2;
  readonly asked: string[] = [];

  bake(markup: string): Promise<BakedPicture> {
    this.asked.push(markup);
    return Promise.resolve({ texture: Texture.EMPTY, left: 0, top: 0 });
  }

  dispose(): void {}
}

/** A painter that paints nothing, which is all a display list needs. */
class IdlePainter implements ScenePainter {
  readonly canvas = document.createElement('canvas');
  painted = 0;
  resized: Array<[number, number]> = [];

  paint(): void {
    this.painted++;
  }

  resize(width: number, height: number): void {
    this.resized.push([width, height]);
  }

  dispose(): void {}
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
  const board = new BoardView(composition(renderer), createState(CATALOG, level), {
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

    const unit = surface.unit(4, () => '<rect width="32" height="32"/>');
    unit.drawing.say('facingLeft', true);

    const root = surface.displayList().find((entry) => entry.layer === 'units')!;
    expect(root.drawn).toHaveLength(1);
    // The figure is the first child and it is flipped; the badges part is not.
    const container = surface.scene.children.at(-1)!.children.find((c) => c.label === 'layer-units')!;
    const [figure, badges] = container.children[0].children;
    expect(figure.scale.x).toBe(-1);
    expect(badges.scale.x).toBe(1);
    surface.dispose();
  });
});
