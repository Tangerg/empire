// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TILE } from '../../art/terrain';
import { portraitSvg } from '../../art/portraits';
import { unitIcon } from '../../art/units';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires/levels';
import { GameController } from '../game';
import { BoardView, emptyOverlay } from '../board';
import { createState } from '@empire/battle-engine/state';
import { candidate01Level } from '@empire/story-candidate-01/levels';
import { registerCandidate01Presentation } from '@empire/story-candidate-01/presentation';

import { createBattleEngine } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const TEST_ENGINE = createBattleEngine({ content: TEST_CATALOG });

registerCandidate01Presentation();

/** jsdom has no layout, so give the board a deterministic box for hit-testing. */
function stubLayout(svg: SVGSVGElement, width: number): void {
  const viewBox = svg.viewBox.baseVal;
  const height = width * (viewBox.height / viewBox.width);
  svg.getBoundingClientRect = () =>
    ({ left: 0, top: 0, width, height, right: width, bottom: height, x: 0, y: 0, toJSON: () => ({}) }) as DOMRect;
}

function click(el: Element, tile: { x: number; y: number }, button = 0): void {
  const ev = new window.MouseEvent('pointerdown', {
    bubbles: true,
    clientX: tile.x * TILE + TILE / 2,
    clientY: tile.y * TILE + TILE / 2,
    button,
  });
  el.dispatchEvent(ev);
}

function hover(el: Element, tile: { x: number; y: number }): void {
  el.dispatchEvent(
    new window.MouseEvent('pointermove', {
      bubbles: true,
      clientX: tile.x * TILE + TILE / 2,
      clientY: tile.y * TILE + TILE / 2,
    }),
  );
}

describe('svg art', () => {
  it('emits parseable markup for every unit sprite and portrait', () => {
    const parser = new window.DOMParser();
    for (const def of TEST_CATALOG.units.all()) {
      for (const svg of [unitIcon(def.id, '#3f7fd8'), portraitSvg(def.id, '#d8483f')]) {
        const doc = parser.parseFromString(svg, 'image/svg+xml');
        expect(doc.querySelector('parsererror'), `${def.id}: ${svg.slice(0, 80)}`).toBeNull();
        expect(doc.documentElement.childElementCount).toBeGreaterThan(0);
      }
    }
  });
});

describe('game controller', () => {
  let host: HTMLElement;

  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) =>
      setTimeout(() => cb(performance.now()), 0) as unknown as number,
    );
    document.body.innerHTML = '<div id="app"></div>';
    host = document.getElementById('app')!;
  });

  it('mounts a level, draws the board and fills the HUD', () => {
    const level = BUILTIN_LEVELS[0];
    const c = new GameController(level, () => {}, { engine: TEST_ENGINE });
    host.append(c.root);

    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    expect(board).toBeTruthy();
    // One group per tile in the terrain layer.
    expect(board.querySelectorAll('.layer-terrain > g > g[data-tile]').length).toBe(
      level.width * level.height,
    );
    expect(board.querySelectorAll('.layer-units > .unit').length).toBe(level.units.length);
    expect(c.root.querySelector('.topbar')!.textContent).toContain(level.name);
    expect(c.root.querySelector('.panel')!.textContent).toContain('作战目标');
    c.dispose();
  });

  it('fits a large board into the available tactical viewport', () => {
    const level = BUILTIN_LEVELS[0];
    const board = new BoardView(createState(level, TEST_CATALOG), {
      onTileClick: () => {},
      onTileEnter: () => {},
      onLeave: () => {},
      onSecondary: () => {},
    }, TEST_CATALOG);
    board.fitWithin(540, 380);
    expect(Number.parseFloat(board.el.style.width)).toBeLessThanOrEqual(508);
    expect(Number.parseFloat(board.el.style.height)).toBeLessThanOrEqual(348);
    expect(board.zoomLevel).toBeGreaterThanOrEqual(0.5);
  });

  it('keeps authored roads below every tactical actor', () => {
    const level = candidate01Level('c01-01');
    const board = new BoardView(createState(level, TEST_CATALOG), {
      onTileClick: () => {},
      onTileEnter: () => {},
      onLeave: () => {},
      onSecondary: () => {},
    }, TEST_CATALOG);
    board.render(emptyOverlay());

    const world = board.el.querySelector('.board-world')!;
    const ground = world.querySelector('.layer-ground')!;
    const units = world.querySelector('.layer-units')!;
    const foreground = world.querySelector('.layer-foreground')!;
    const children = [...world.children];
    expect(children.indexOf(ground)).toBeLessThan(children.indexOf(units));
    expect(children.indexOf(units)).toBeLessThan(children.indexOf(foreground));
    expect(ground.querySelector('.candidate-ground-route')).toBeTruthy();
    expect(world.querySelector('.layer-grid line')).toBeFalsy();
    expect(world.querySelectorAll('.candidate-stand-node')).toHaveLength(level.width * level.height);
    board.dispose();
  });

  it('selects a unit, previews the path, and shows a move range', () => {
    const level = BUILTIN_LEVELS[0];
    const c = new GameController(level, () => {}, { engine: TEST_ENGINE });
    host.append(c.root);
    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board, level.width * TILE);

    const mine = level.units.find((u) => u.owner === 1)!;
    click(board, mine);
    expect(board.querySelectorAll('.layer-range rect').length).toBeGreaterThan(3);

    hover(board, { x: mine.x, y: mine.y + 1 });
    expect(board.querySelectorAll('.layer-path path').length).toBeGreaterThan(0);
    c.dispose();
  });

  it('opens the recruit modal on an owned castle and lists affordable units', () => {
    const level = BUILTIN_LEVELS[0];
    const c = new GameController(level, () => {}, { engine: TEST_ENGINE });
    host.append(c.root);
    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board, level.width * TILE);

    click(board, { x: 1, y: 1 }); // player 1's castle, empty at start
    const modal = c.root.querySelector('.modal-root')!;
    expect(modal.textContent).toContain('征募单位');
    expect(modal.querySelectorAll('.recruit-card').length).toBeGreaterThan(4);
    c.dispose();
  });

  it('renders every built-in level without throwing', () => {
    for (const level of BUILTIN_LEVELS) {
      const c = new GameController(level, () => {}, { engine: TEST_ENGINE });
      host.append(c.root);
      expect(c.root.querySelector('svg.board')).toBeTruthy();
      c.dispose();
      host.innerHTML = '';
    }
  });
});
