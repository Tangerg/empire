// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TILE } from '../../art/terrain';
import { portraitSvg } from '../../art/portraits';
import { unitIcon } from '../../art/units';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires/levels';
import { GameController } from '../game';
import { BoardView, emptyOverlay } from '../board';
import { createState } from '@empire/battle-engine/state';
import { normaliseLevel } from '@empire/battle-engine/level';
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

  /**
   * A control declares an intent in `data-act`; the HUD holds the table that
   * answers it. They used to be a template and a thirteen-case switch in
   * different halves of the file, with nothing comparing them — and the
   * disabled variants declared a fake `noop` intent to opt out, which meant the
   * comparison could not have been made even if someone had tried.
   */
  it('answers every intent its own markup declares', () => {
    const level = BUILTIN_LEVELS[0];
    const controller = new GameController(level, () => {}, { engine: TEST_ENGINE });
    host.append(controller.root);

    const declared = [...controller.root.querySelectorAll('[data-act]')]
      .map((control) => control.getAttribute('data-act')!);
    const unanswered = [...new Set(declared)]
      .filter((intent) => !controller.handledIntents.includes(intent));

    expect(unanswered).toEqual([]);
    controller.dispose();
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

/** A mage in meteor range of a lone defender, with nothing else to decide. */
const castingLevel = () => normaliseLevel({
  schema: 2,
  id: 'cast-test',
  name: '咏唱',
  width: 6,
  height: 1,
  terrain: ['......'],
  units: [
    { x: 0, y: 0, unit: 'mage', owner: 1 },
    { x: 3, y: 0, unit: 'soldier', owner: 2 },
  ],
  players: [
    { id: 1, name: 'P1', team: 1, color: '#3f7fd8', controller: 'human', resources: {} },
    { id: 2, name: 'P2', team: 2, color: '#d8483f', controller: 'human', resources: {} },
  ],
  rules: {},
  victory: [{ type: 'routEnemies' }],
});

describe('charge time presentation', () => {
  let host: HTMLElement;

  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) =>
      setTimeout(() => cb(performance.now()), 0) as unknown as number,
    );
    document.body.innerHTML = '<div id="app"></div>';
    host = document.getElementById('app')!;
  });

  /** Dispatch is animated, so a click settles over a few frames. */
  const settle = async () => {
    for (let frame = 0; frame < 12; frame++) await new Promise((resolve) => setTimeout(resolve, 0));
  };

  it('shows nothing while no strike is charging', () => {
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE });
    host.append(c.root);
    expect(c.root.querySelector('.cast-strip')).toBeNull();
    c.dispose();
  });

  it('marks the aimed tiles and counts the charge down', async () => {
    const c = new GameController(castingLevel(), () => {}, { engine: TEST_ENGINE });
    host.append(c.root);
    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board, 6 * TILE);

    click(board, { x: 0, y: 0 });                                   // the mage
    click(board, { x: 0, y: 0 });                                   // stay put
    // The lone defender is the only aim point, so the cast commits on the spot.
    (c.root.querySelector('[data-act="command"][data-arg="attack:mage_meteor"]') as HTMLElement).click();
    await settle();

    const strip = c.root.querySelector('.cast-strip');
    expect(strip, 'a charge nobody can see is a trap').toBeTruthy();
    expect(strip!.querySelector('.cast-slot b')!.textContent).toBe('2');
    // cross1 marks the aimed tile and the neighbours it splashes.
    expect(c.root.querySelectorAll('.layer-range .incoming-count').length).toBeGreaterThan(1);
    c.dispose();
  });
});

describe('initiative presentation', () => {
  let host: HTMLElement;

  beforeEach(() => {
    document.body.innerHTML = '<div id="app"></div>';
    host = document.getElementById('app')!;
  });

  const initiativeLevel = () => ({
    ...BUILTIN_LEVELS[0],
    rules: { ...BUILTIN_LEVELS[0].rules, turnOrder: 'initiative' },
  });

  it('shows no order strip under side turns', () => {
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE });
    host.append(c.root);
    expect(c.root.querySelector('.order-strip')).toBeNull();
    c.dispose();
  });

  it('shows the upcoming order, active unit first, under per-unit turns', () => {
    const c = new GameController(initiativeLevel(), () => {}, { engine: TEST_ENGINE });
    host.append(c.root);

    const strip = c.root.querySelector('.order-strip');
    expect(strip).toBeTruthy();
    const slots = strip!.querySelectorAll('.order-slot');
    expect(slots.length).toBeGreaterThan(1);
    expect(slots[0].classList.contains('is-active')).toBe(true);
    expect([...slots].slice(1).some((slot) => slot.classList.contains('is-active'))).toBe(false);
    c.dispose();
  });
});

/**
 * Putting a battle down and picking it up.
 *
 * The slot is a port the shell supplies, so the controller shows the entry only
 * when there is somewhere to keep a battle — the campaign shell passes none,
 * because a campaign battle resumes through the campaign's own save.
 */
describe('a battle you can come back to', () => {
  let host: HTMLElement;

  const slot = () => {
    let stored: string | null = null;
    return {
      write: (save: unknown) => { stored = JSON.stringify(save); },
      read: () => (stored === null ? null : JSON.parse(stored) as unknown),
      has: () => stored !== null,
      forget: () => { stored = null; },
      poison: () => { stored = JSON.stringify({ schema: 1, battle: { levelId: 'x' }, savedAt: '', state: { units: [] } }); },
    };
  };

  const press = (root: Element, act: string) =>
    (root.querySelector(`[data-act="${act}"]`) as HTMLButtonElement);

  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) =>
      setTimeout(() => cb(performance.now()), 0) as unknown as number,
    );
    document.body.innerHTML = '<div id="app"></div>';
    host = document.getElementById('app')!;
  });

  it('offers no slot when the shell keeps none', () => {
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE });
    host.append(c.root);
    expect(press(c.root, 'save')).toBeNull();
    expect(press(c.root, 'resume')).toBeNull();
    c.dispose();
  });

  it('saves the turn it is on and resumes into it', () => {
    const saves = slot();
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE, saves });
    host.append(c.root);

    expect(press(c.root, 'resume').disabled).toBe(true);
    press(c.root, 'save').click();
    expect(saves.has()).toBe(true);
    expect(c.root.querySelector('.panel')!.textContent).toContain('已保存第 1 回合的进度');

    press(c.root, 'resume').click();
    expect(c.root.querySelector('.panel')!.textContent).toContain('已读取第 1 回合的存档');
    c.dispose();
  });

  it('reports a save this ruleset cannot honour instead of dying on it', () => {
    const saves = slot();
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE, saves });
    host.append(c.root);
    // Save first, so the slot is offered, then replace what it holds with a
    // battle this ruleset cannot run.
    press(c.root, 'save').click();
    saves.poison();

    press(c.root, 'resume').click();
    expect(c.root.querySelector('.panel')!.textContent).toContain('无法读取存档');
    // The battle on screen is still the one being played.
    expect(c.root.querySelectorAll('.layer-units > .unit').length).toBe(BUILTIN_LEVELS[0].units.length);
    c.dispose();
  });
});
