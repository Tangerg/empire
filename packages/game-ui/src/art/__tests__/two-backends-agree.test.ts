// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { Sprite, Texture, type Container } from 'pixi.js';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '@empire/story-candidate-01';
import { CANDIDATE_01_ART } from '@empire/story-candidate-01/presentation';
import { createBattleEngine, mapFromLevel, TacticalGrids, type LevelData } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { FrameClock } from './frame-clock';
import {
  BOARD_LAYERS,
  CELL_FOOT,
  SELECTION_CUE_MATRIX,
  SELECTION_CUE_OPACITY,
  boardScenePieces,
  type BoardPiece,
  type BoardLayer,
  type BoardStrip,
  type BoardSurfaceScene,
} from '../board-surface';
import { terrainLayerPieces } from '../terrain';
import { squareLayout } from '../board-decorations';
import { ArtDirection, GENERIC_ART } from '../direction';
import { createSceneViewport } from '../scene-viewport';
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
/** The story pack composed in, for the scene layers only that pack draws. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
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
  it('orders units by current footing, including movement across a neighbour', () => {
    const scene: BoardSurfaceScene = { width: 320, height: 320, originX: 0, originY: 0,
      shapeRendering: 'crispEdges', backdrop: '', foreground: '' };
    const svg = new SvgBoardSurface(scene);
    const gpu = new PixiBoardSurface(scene, { textures: new InstantTextures(), painter: new IdlePainter() });
    for (const surface of [svg, gpu]) {
      surface.unit(9, () => ({ body: '<rect width="32" height="44"/>' })).place(32, 96);
      surface.unit(2, () => ({ body: '<rect width="32" height="44"/>' })).place(64, 32);
      surface.unit(4, () => ({ body: '<rect width="32" height="44"/>' })).place(0, 32);
    }
    const order = () => svgDisplayList(svg.element).find((layer) => layer.layer === 'units')!.at;
    expect(order()).toEqual(['0.00,32.00', '64.00,32.00', '32.00,96.00']);
    expect(pixiDisplayList(gpu)).toEqual(svgDisplayList(svg.element));
    for (const surface of [svg, gpu]) surface.drawnUnit(9)!.place(32, 0);
    expect(order()).toEqual(['32.00,0.00', '0.00,32.00', '64.00,32.00']);
    expect(pixiDisplayList(gpu)).toEqual(svgDisplayList(svg.element));
    // A strike nudge changes the picture, not the unit's standing depth.
    for (const surface of [svg, gpu]) surface.drawnUnit(9)!.nudge(0, 80);
    expect(order()[0]).toBe('32.00,80.00');
    expect(pixiDisplayList(gpu)).toEqual(svgDisplayList(svg.element));
    for (const surface of [svg, gpu]) { surface.removeUnit(4); surface.dispose(); }
  });

  it('merges still objects and moving actors by foot without resetting clips or tactical overlays', async () => {
    const scene: BoardSurfaceScene = { width: 320, height: 320, originX: 0, originY: 0,
      shapeRendering: 'crispEdges', backdrop: '', foreground: '' };
    const svg = new SvgBoardSurface(scene);
    const gpu = new PixiBoardSurface(scene, { textures: new InstantTextures(), painter: new IdlePainter() });
    const building: BoardPiece = { markup: '<rect width="64" height="96"/>', x: 32, y: 32, foot: CELL_FOOT };
    const tree: BoardPiece = { markup: '<rect x="-30" y="-80" width="60" height="80"/>', x: 48, y: 70,
      foot: { x: 0, y: 0 } };
    const flat: BoardPiece = { markup: '<rect width="64" height="16"/>', x: 0, y: 200 };
    const strip: BoardStrip = { href: 'walking.png', frameWidth: 32, frameHeight: 32, frameCount: 2, x: 0, y: 0,
      clips: [{ id: 'walk', frames: [0, 1], fps: 4, loop: true }], playing: 'walk' };
    const actors = [svg, gpu].map((surface) => {
      // Arrival order does not decide world order.
      surface.setLayer('scenery', [tree, flat]);
      surface.setLayer('terrain', [building]);
      surface.setLayer('range', [flat]);
      surface.setLayer('cursor', [flat]);
      const actor = surface.unit(7, () => ({ body: '', strip }));
      actor.place(32, 0);
      return actor;
    });
    const order = () => svgDisplayList(svg.element).find((layer) => layer.layer === 'units')!.at;
    const same = () => expect(pixiDisplayList(gpu)).toEqual(svgDisplayList(svg.element));
    expect(order()).toEqual(['32.00,0.00', '32.00,32.00', '48.00,70.00']);
    expect(svgDisplayList(svg.element).find((layer) => layer.layer === 'scenery')!.at).toEqual(['0.00,200.00']);
    same();
    const unitNode = svg.element.querySelector('.unit');
    await Promise.resolve();
    clock.at(300);
    const frame = svg.element.querySelector('.board-strip')!.getAttribute('viewBox');
    expect(frame).toBe('32 0 32 32');
    actors.forEach((actor) => actor.place(32, 32));
    expect(order()).toEqual(['32.00,32.00', '32.00,32.00', '48.00,70.00']);
    expect(svg.element.querySelector('.layer-units')!.children[1]).toBe(unitNode);
    same();
    actors.forEach((actor) => actor.place(32, 36));
    expect(order()).toEqual(['32.00,32.00', '32.00,36.00', '48.00,70.00']);
    same();
    actors.forEach((actor) => actor.place(32, 48));
    expect(order()).toEqual(['32.00,32.00', '48.00,70.00', '32.00,48.00']);
    actors.forEach((actor) => { actor.nudge(0, -100); actor.swell(2); });
    expect(order().at(-1)).toBe('32.00,-52.00');
    same();
    for (const surface of [svg, gpu]) {
      surface.setLayer('terrain', [{ ...building, y: 100 }]);
      surface.setLayer('scenery', []);
    }
    expect(order()).toEqual(['32.00,-52.00', '32.00,100.00']);
    expect(svg.element.querySelector('.unit')).toBe(unitNode);
    expect(svg.element.querySelector('.board-strip')!.getAttribute('viewBox')).toBe(frame);
    expect(svg.element.querySelector('.layer-range')!.children).toHaveLength(1);
    expect(svg.element.querySelector('.layer-cursor')!.children).toHaveLength(1);
    same();
    for (const surface of [svg, gpu]) surface.removeUnit(7);
    expect(order()).toEqual(['32.00,100.00']);
    expect(clock.pump(3)).toBe(0);
    same();
    for (const surface of [svg, gpu]) surface.dispose();
  });

  it('projects only a visible selected actor above scenery, on its existing frame and lifetime', async () => {
    const scene: BoardSurfaceScene = { width: 320, height: 320, originX: 0, originY: 0,
      shapeRendering: 'crispEdges', backdrop: '', foreground: '' };
    const svg = new SvgBoardSurface(scene);
    const textures = new InstantTextures();
    const gpu = new PixiBoardSurface(scene, { textures, painter: new IdlePainter() });
    const strip: BoardStrip = { href: 'selected-walk.png', frameWidth: 32, frameHeight: 32, frameCount: 2,
      x: -8, y: -16, clips: [{ id: 'walk', frames: [0, 1], fps: 4, loop: true }], playing: 'walk' };
    const actors = [svg, gpu].map((surface) => {
      surface.setLayer('scenery', [{ markup: '<rect x="-32" y="-96" width="64" height="96"/>',
        x: 48, y: 80, foot: { x: 0, y: 0 } }]);
      const actor = surface.unit(7, () => ({ body: '<ellipse cx="16" cy="31" rx="10" ry="2"/>', strip }));
      actor.place(32, 32);
      return actor;
    });
    const gpuLayer = (name: BoardLayer) => gpu.scene.children[0].children.find((child) => child.label === `layer-${name}`)!;
    const gpuActor = gpuLayer('units').children.find((child) => child.label === 'unit-7')!;
    const gpuSource = gpuActor.children[0].children[1] as Sprite;
    const svgActor = svg.element.querySelector('.unit')!;
    const source = svgActor.querySelector('.board-strip')!;
    const domCue = () => svg.element.querySelector('.selection-cue');
    const gpuCue = () => gpuLayer('effects').children.find((child) => child.label === 'selection-cue');
    const same = () => expect(pixiDisplayList(gpu)).toEqual(svgDisplayList(svg.element));
    expect(domCue()).toBeNull();
    expect(gpuCue()).toBeUndefined();
    actors.forEach((actor) => actor.say('selected', true));
    await Promise.resolve();
    expect(domCue()!.querySelector('.board-strip')!.getAttribute('viewBox')).toBe(source.getAttribute('viewBox'));
    expect(domCue()!.querySelector('image')!.getAttribute('href')).toBe(strip.href);
    expect(domCue()!.querySelectorAll('ellipse')).toHaveLength(0);
    expect(domCue()!.querySelector('feColorMatrix')!.getAttribute('values')).toBe(SELECTION_CUE_MATRIX.join(' '));
    expect(Number(domCue()!.getAttribute('opacity'))).toBe(SELECTION_CUE_OPACITY);
    expect(gpuCue()!.alpha).toBe(SELECTION_CUE_OPACITY);
    expect(gpuCue()!.children[0].children).toHaveLength(1);
    expect((gpuCue()!.children[0].children[0] as Sprite).texture).toBe(gpuSource.texture);
    expect(svgActor.classList.contains('is-selected')).toBe(true);
    expect(gpuActor.children[0].filters).toHaveLength(1);
    expect(svg.element.querySelector('.layer-units')!.firstElementChild).toBe(svgActor);
    same();

    clock.at(300);
    expect(source.getAttribute('viewBox')).toBe('32 0 32 32');
    expect(domCue()!.querySelector('.board-strip')!.getAttribute('viewBox')).toBe(source.getAttribute('viewBox'));
    expect((gpuCue()!.children[0].children[0] as Sprite).texture).toBe(textures.cut.get(strip.href)![1]);
    actors.forEach((actor) => { actor.place(64, 96); actor.nudge(3, -7); actor.swell(1.2); actor.say('facingLeft', true); actor.opacity(0.5); });
    expect(domCue()!.getAttribute('transform')).toBe(svgActor.getAttribute('transform'));
    expect(domCue()!.lastElementChild!.getAttribute('transform')).toBe('translate(32 0) scale(-1 1)');
    expect([gpuCue()!.x, gpuCue()!.y]).toEqual([gpuActor.x, gpuActor.y]);
    expect([gpuCue()!.scale.x, gpuCue()!.scale.y]).toEqual([gpuActor.scale.x, gpuActor.scale.y]);
    expect(gpuCue()!.children[0].scale.x).toBe(-1);
    expect(gpuCue()!.alpha).toBe(SELECTION_CUE_OPACITY * 0.5);
    expect(Number(domCue()!.getAttribute('opacity'))).toBe(gpuCue()!.alpha);
    expect(svg.element.querySelector('.layer-units')!.lastElementChild).toBe(svgActor);
    same();
    const effects = [svg, gpu].map((surface) => surface.effect({ body: '<rect width="4" height="4"/>' }));
    expect(svg.element.querySelector('.layer-effects')!.firstElementChild).toBe(domCue());
    expect(gpuLayer('effects').children[0]).toBe(gpuCue());
    expect(BOARD_LAYERS.indexOf('effects')).toBeLessThan(BOARD_LAYERS.indexOf('cursor'));

    // Hidden wins regardless of the order in which callers say the two states.
    actors.forEach((actor) => { actor.say('hidden', true); actor.say('selected', true); });
    expect(domCue()).toBeNull(); expect(gpuCue()).toBeUndefined();
    expect((svgActor as SVGElement).style.display).toBe('none'); expect(gpuActor.visible).toBe(false);
    actors.forEach((actor) => actor.say('hidden', false));
    expect(domCue()).not.toBeNull(); expect(gpuCue()).toBeDefined();
    actors.forEach((actor) => actor.say('selected', false));
    expect(domCue()).toBeNull(); expect(gpuCue()).toBeUndefined();
    actors.forEach((actor) => { actor.say('hidden', true); actor.say('selected', false); actor.say('hidden', false); });
    expect(domCue()).toBeNull(); expect(gpuCue()).toBeUndefined();
    expect(clock.pump(2)).toBeGreaterThan(0);
    actors.forEach((actor) => actor.say('selected', true));
    effects.forEach((effect) => effect.remove());
    for (const surface of [svg, gpu]) surface.removeUnit(7);
    expect(domCue()).toBeNull(); expect(gpuCue()).toBeUndefined();
    expect(clock.pump(2)).toBe(0);
    for (const surface of [svg, gpu]) surface.dispose();
  });

  it('uses the same selected cue for a body without a strip and releases it on disposal', async () => {
    const scene: BoardSurfaceScene = { width: 64, height: 64, originX: 0, originY: 0,
      shapeRendering: 'crispEdges', backdrop: '', foreground: '' };
    const svg = new SvgBoardSurface(scene);
    const textures = new InstantTextures();
    const gpu = new PixiBoardSurface(scene, { textures, painter: new IdlePainter() });
    const actors = [svg, gpu].map((surface) => surface.unit(4, () => ({ body: '<rect width="16" height="24"/>' })));
    await Promise.resolve();
    const bakes = textures.asked.length;
    actors.forEach((actor) => actor.say('selected', true));
    expect(svg.element.querySelector('.selection-cue rect')).not.toBeNull();
    expect(pixiDisplayList(gpu)).toEqual(svgDisplayList(svg.element));
    expect(SELECTION_CUE_OPACITY).toBeGreaterThan(0);
    expect(SELECTION_CUE_OPACITY).toBeLessThanOrEqual(0.2);
    actors.forEach((actor) => { actor.say('selected', false); actor.say('selected', true); });
    expect(textures.asked).toHaveLength(bakes);
    expect(textures.cut.size).toBe(0);
    expect(clock.pump(2)).toBe(0);
    for (const surface of [svg, gpu]) surface.dispose();
    expect(svg.element.querySelector('.selection-cue')).toBeNull();
  });

  it.each([['c01-02', 'bridge-cleric'], ['c01-11', 'echo-4']])('never projects a hidden selected actor from the actual BoardView overlay (%s)', (levelId, unitKey) => {
    const level = CANDIDATE_01_LEVELS.find((candidate) => candidate.id === levelId)!;
    const engine = createBattleEngine({ content: TEST_CATALOG });
    const state = engine.createState(level);
    const before = JSON.stringify(state);
    const unit = state.units.find((actor) => actor.key === unitKey)!;
    const scene: BoardSurfaceScene = { width: state.map.width * 32, height: state.map.height * 32, originX: 0, originY: 0,
      shapeRendering: 'crispEdges', backdrop: '', foreground: '' };
    const svg = new SvgBoardSurface(scene);
    const gpu = new PixiBoardSurface(scene, { textures: new InstantTextures(), painter: new IdlePainter() });
    const boards = [svg, gpu].map((surface) => new BoardView({ content: TEST_CATALOG,
      grid: engine.rules.grids.get('square4'), art: CANDIDATE_01_ART, renderer: () => surface }, state,
    { onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {}, onSecondary: () => {}, onScale: () => {} }));
    const overlay = emptyOverlay();
    overlay.selected = { x: unit.x, y: unit.y };
    overlay.hiddenUnits.add(unit.id);
    const count = () => svg.element.querySelectorAll('.selection-cue').length;
    boards.forEach((board) => board.render(overlay));
    expect(count()).toBe(0);
    expect(pixiDisplayList(gpu)).toEqual(svgDisplayList(svg.element));
    overlay.hiddenUnits.clear();
    boards.forEach((board) => board.render(overlay));
    expect(count()).toBe(1);
    overlay.hiddenUnits.add(unit.id);
    boards.forEach((board) => board.render(overlay));
    expect(count()).toBe(0);
    overlay.selected = null;
    overlay.hiddenUnits.clear();
    boards.forEach((board) => board.render(overlay));
    expect(count()).toBe(0);
    expect(pixiDisplayList(gpu)).toEqual(svgDisplayList(svg.element));
    expect(JSON.stringify(state)).toBe(before);
    boards.forEach((board) => board.dispose());
  });

  it('draws every c01-02 building, scenery piece and actor through the same depth contract', () => {
    const level = CANDIDATE_01_LEVELS.find((candidate) => candidate.id === 'c01-02')!;
    const engine = createBattleEngine({ content: TEST_CATALOG });
    const state = engine.createState(level);
    const before = JSON.stringify(state);
    const grid = engine.rules.grids.get('square4');
    const scene = CANDIDATE_01_ART.presentation.sceneLayers({ content: TEST_CATALOG, levelId: level.id, map: state.map,
      viewport: createSceneViewport(grid, level.width, level.height, 32, CANDIDATE_01_ART.presentation.sceneProfile(level.id)) });
    const buildings = terrainLayerPieces({ art: CANDIDATE_01_ART, content: TEST_CATALOG, layout: squareLayout },
      state.map, (owner) => state.players.find((player) => player.id === owner)?.color);
    expect(buildings).toHaveLength(9);
    expect(buildings.every((piece) => piece.foot?.x === 16 && piece.foot.y === 31)).toBe(true);
    expect(state.units).toHaveLength(20);
    expect(scene.underUnits).toHaveLength(46);
    expect(scene.overUnits).toEqual([]);
    expect(scene.underUnits.filter((piece) => !piece.foot)).toHaveLength(5);
    const actors = state.units.map((unit) => ({ markup: '', x: unit.x * 32, y: unit.y * 32, foot: CELL_FOOT }));
    const expected = boardScenePieces({ terrain: buildings, scenery: scene.underUnits, units: actors })
      .filter((piece) => piece.foot).map(({ x, y }) => `${x.toFixed(2)},${y.toFixed(2)}`);
    const handlers = { onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {}, onSecondary: () => {}, onScale: () => {} };
    const svg = new SvgBoardSurface({ width: 704, height: 448, originX: 0, originY: 0, shapeRendering: 'crispEdges', backdrop: '', foreground: '' });
    const gpu = new PixiBoardSurface({ width: 704, height: 448, originX: 0, originY: 0, shapeRendering: 'crispEdges', backdrop: '', foreground: '' },
      { textures: new InstantTextures(), painter: new IdlePainter() });
    const boards = [svg, gpu].map((surface) => new BoardView({ content: TEST_CATALOG, grid, art: CANDIDATE_01_ART,
      renderer: () => surface }, state, handlers));
    boards.forEach((board) => board.render(emptyOverlay()));
    expect(svgDisplayList(svg.element).find((layer) => layer.layer === 'units')!.at).toEqual(expected);
    expect(pixiDisplayList(gpu)).toEqual(svgDisplayList(svg.element));
    expect(svg.drawnUnits()).toHaveLength(20);
    expect(svg.element.querySelectorAll('[data-candidate-art="structure"]')).toHaveLength(9);
    expect(JSON.stringify(state)).toBe(before);
    boards.forEach((board) => board.dispose());
  });

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
   * A piece is baked alone, so it has to be whole alone.
   *
   * The GPU backend turns each `BoardPiece` into its own texture from its own
   * markup. A `url(#gradient)` whose `<defs>` live in a *different* piece resolves
   * to nothing there — while the DOM backend, which puts every piece into one
   * document, draws it perfectly. That asymmetry is invisible in a screenshot of
   * the DOM backend and invisible to every test that only reads markup.
   *
   * The hearth glow was written that way first: a piece per lit building with the
   * gradients in a piece of their own. `genericFieldLight` has always been one
   * piece for this reason; this is the rule stated instead of remembered.
   */
  it('leaves no piece pointing at a definition it does not carry', () => {
    const square = TacticalGrids.get('square4');
    const offenders: string[] = [];
    const checkedScenes: string[] = [];
    let references = 0;

    for (const art of [CANDIDATE_01_ART, new ArtDirection()]) {
      const presentation = art.presentation;
      for (const level of CANDIDATE_01_LEVELS.slice(0, 3)) {
        const map = mapFromLevel(TEST_CATALOG, level);
        const viewport = createSceneViewport(
          square,
          map.width,
          map.height,
          32,
          presentation.sceneProfile(level.id),
        );
        const layers = presentation.sceneLayers({
          content: TEST_CATALOG,
          levelId: level.id,
          map,
          viewport,
        });
        expect(Object.values(layers).some(layer => layer.length > 0), `${presentation.id} ${level.id}`).toBe(true);
        checkedScenes.push(`${presentation.id}:${level.id}`);
        for (const [name, layer] of Object.entries(layers)) {
          for (const piece of layer) {
            const defined = new Set([...piece.markup.matchAll(/\sid="([^"]+)"/g)].map(([, id]) => id));
            for (const [, used] of piece.markup.matchAll(/url\(#([^)]+)\)/g)) {
              references += 1;
              if (!defined.has(used)) offenders.push(`${presentation.id} ${name}: url(#${used})`);
            }
          }
        }
      }
    }

    // Area materials intentionally merge many cell pieces; count covered scenes
    // and actual definition references instead of requiring a fragmented drawing.
    expect(new Set(checkedScenes).size).toBe(6);
    expect(references).toBeGreaterThan(0);
    expect([...new Set(offenders)]).toEqual([]);
  });

  /**
   * A battle that has ended stops hearing the mouse.
   *
   * The GPU backend draws every battle of a session onto *one* canvas — that is the
   * point of the shared painter above. Its surface put five pointer listeners on
   * that canvas and `dispose` took none of them off, so the second battle's click
   * also reached the first battle's board, and the third's reached two disposed
   * ones. The DOM backend was safe by accident: its element is new each time.
   */
  it('stops listening on a shared canvas when the battle ends', () => {
    const painter = new IdlePainter();
    const renderer = pixiBoardSurface(painter, new InstantTextures());
    const scene: BoardSurfaceScene = {
      width: 320,
      height: 320,
      originX: 0,
      originY: 0,
      shapeRendering: 'crispEdges',
      backdrop: '',
      foreground: '',
    };
    const canvas = painter.canvas;
    canvas.getBoundingClientRect = () =>
      ({ left: 0, top: 0, width: 320, height: 320, right: 320, bottom: 320, x: 0, y: 0, toJSON: () => ({}) }) as DOMRect;

    const heard: string[] = [];
    const board = (name: string) => ({
      press: () => heard.push(name),
      move: () => {},
      leave: () => {},
      scale: () => {},
    });

    const first = renderer(scene);
    first.listen(board('first'));
    const click = () => canvas.dispatchEvent(
      new window.MouseEvent('pointerdown', { bubbles: true, clientX: 10, clientY: 10, button: 0 }),
    );
    click();
    expect(heard).toEqual(['first']);

    first.dispose();
    const second = renderer(scene);
    second.listen(board('second'));
    heard.length = 0;
    click();

    // One board, not two: the ended battle is not still holding the canvas.
    expect(heard).toEqual(['second']);
    second.dispose();
    heard.length = 0;
    click();
    expect(heard).toEqual([]);
    renderer.dispose();
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
