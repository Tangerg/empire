// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TILE } from '../../art/terrain';
import { portraitSvg } from '../../art/portraits';
import { unitIcon } from '../../art/units';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires';
import { GameController } from '../game';
import { BoardView, emptyOverlay, type BoardComposition } from '../board';
import { svgBoardSurface } from '../../art/svg-board-surface';
import { ArtDirection } from '../../art/direction';
import { GENERIC_PRESENTATION } from '../../art/battle-presentation';
import {
  createState,
  DomainInvariantError,
  normaliseLevel,
  createBattleEngine,
} from '@empire/battle-engine';
import { candidate01Level, CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '@empire/story-candidate-01';
import { CANDIDATE_01_ART } from '@empire/story-candidate-01/presentation';

/** Composed per suite, exactly like an application composition root. */
const ART = CANDIDATE_01_ART;

import { createTestCatalog } from '@empire/test-content';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const TEST_ENGINE = createBattleEngine({ content: TEST_CATALOG });



/**
 * What the HUD is telling the player, wherever on the overlay it says it.
 *
 * These assertions used to read `.panel`, which was the sidebar the HUD happened
 * to be laid out as — so a layout change broke eight tests that had no opinion
 * about layout. The question is whether the battle says a thing at all.
 */
const hudSays = (root: Element): string => root.querySelector('.battle-hud')!.textContent ?? '';

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

/**
 * A board composed the way an application root composes one.
 *
 * Four call sites used to thread `content, grid, art` behind the two subjects, and
 * each would have had to grow a fourth for the renderer.
 */
const composition = (art: ArtDirection): BoardComposition => ({
  content: TEST_CATALOG,
  grid: TEST_ENGINE.rules.grids.get('square4'),
  art,
  renderer: svgBoardSurface,
});

describe('svg art', () => {
  it('emits parseable markup for every unit sprite and portrait', () => {
    const parser = new window.DOMParser();
    for (const def of TEST_CATALOG.units.all()) {
      for (const svg of [unitIcon(ART, def, '#3f7fd8'), portraitSvg(ART, def, '#d8483f')]) {
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
    const c = new GameController(level, () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);

    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    expect(board).toBeTruthy();
    // One picture per tile in the terrain layer.
    expect(board.querySelectorAll('.layer-terrain > g').length).toBe(
      level.width * level.height,
    );
    expect(board.querySelectorAll('.layer-units > .unit').length).toBe(level.units.length);
    expect(hudSays(c.root)).toContain(level.name);
    expect(hudSays(c.root)).toContain('作战目标');
    c.dispose();
  });

  /**
   * The battle is a field with an overlay on it, and nothing in between.
   *
   * It used to be a `<header>`, a row holding the board and an `<aside>`, and a
   * modal root — a page, in other words, which is what it looked like. Two
   * children is the whole claim: put the HUD back inside the field, or a strip
   * back above it, and this says so.
   */
  it('lays the overlay over the field and nothing between them', () => {
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);

    expect([...c.root.children].map((child) => child.className)).toEqual(['battlefield', 'battle-hud']);
    expect(c.root.querySelector('.battlefield > svg.board')).not.toBeNull();
    expect(c.root.querySelector('.battle-hud svg.board')).toBeNull();
    c.dispose();
  });

  /**
   * The battle names itself on the field it is fought on.
   *
   * Turn changes have always been announced there; the opening was the one
   * moment the picture said nothing at all and the entire introduction happened
   * in the chrome around it.
   */
  it('announces the battle on the field when it opens', () => {
    const level = BUILTIN_LEVELS[0];
    const c = new GameController(level, () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);

    expect(c.root.querySelector('.layer-effects')!.textContent).toContain(level.name);
    c.dispose();
  });

  /**
   * Committing is one control, it stands alone, and it says which act it is.
   *
   * Confirming a whole battle line used to be the last button in a row of six
   * system glyphs, beside "zoom out" and looking just like it.
   */
  it('keeps the committing control alone in the dock', () => {
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);

    const committing = [...c.root.querySelectorAll('[data-act="end"], [data-act="deploy-done"]')];
    expect(committing).toHaveLength(1);
    expect(committing[0].closest('.hud-dock')).not.toBeNull();
    // And guidance is the field talking, never a menu: no control lives in it.
    const hint = c.root.querySelector('.hud-hint')!;
    expect(hint.textContent).not.toBe('');
    expect(hint.querySelectorAll('[data-act]')).toHaveLength(0);
    c.dispose();
  });

  /**
   * A region is rewritten only when it has something different to say.
   *
   * One `innerHTML` for the whole panel meant every pointer move rebuilt the
   * objectives, the roster and the log, so nothing in the overlay could animate,
   * hold a scroll position, or keep an image from reloading. Same element after
   * a sweep of the cursor is the property that buys all three back.
   */
  it('leaves a region alone while it has nothing new to say', () => {
    const level = BUILTIN_LEVELS[0];
    const c = new GameController(level, () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board, level.width * TILE);

    const objectives = c.root.querySelector('.hud-flank > *')!;
    const commit = c.root.querySelector('[data-act="end"]')!;
    for (let x = 0; x < 6; x++) hover(board, { x, y: 3 });

    expect(c.root.querySelector('.hud-flank > *')).toBe(objectives);
    expect(c.root.querySelector('[data-act="end"]')).toBe(commit);
    // The terrain readout, which is what the cursor actually changed, did move.
    expect(c.root.querySelector('.hud-ledger')!.textContent).not.toBe('');
    c.dispose();
  });

  /**
   * What the player is being asked for, as one word on one element.
   *
   * The battlefield's wash, the overlay's tint and the accent on the committing
   * control are one mood; four stylesheets guessing at it from four different
   * classes is how they come apart.
   */
  it('names the mode the whole screen reacts to', () => {
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    const hud = c.root.querySelector('.battle-hud') as HTMLElement;
    expect(hud.dataset.mode).toBe('commanding');
    c.dispose();

    const deploying = new GameController(deploymentLevel(), () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(deploying.root);
    expect((deploying.root.querySelector('.battle-hud') as HTMLElement).dataset.mode).toBe('deploying');
    deploying.dispose();
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
    const controller = new GameController(level, () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(controller.root);

    const declared = [...controller.root.querySelectorAll('[data-act]')]
      .map((control) => control.getAttribute('data-act')!);
    const unanswered = [...new Set(declared)]
      .filter((intent) => !controller.handledIntents.includes(intent));

    expect(unanswered).toEqual([]);
    controller.dispose();
  });

  /**
   * The field fills the screen it was given, and is never cropped by it.
   *
   * It used to stop growing at 1.25×, so an ordinary 20×14 field on an ordinary
   * screen was a rectangle floating in the middle of a gradient — a picture on a
   * page rather than the thing being played.
   */
  it('fills the tactical viewport with the whole field, uncropped', () => {
    const level = BUILTIN_LEVELS[0];
    const board = new BoardView(composition(ART), createState(TEST_CATALOG, level), {
      onTileClick: () => {},
      onTileEnter: () => {},
      onLeave: () => {},
      onSecondary: () => {},
      onScale: () => {},
    });
    board.fitWithin(540, 380);

    const width = Number.parseFloat(board.el.style.width);
    const height = Number.parseFloat(board.el.style.height);
    expect(width).toBeLessThanOrEqual(540);
    expect(height).toBeLessThanOrEqual(380);
    // Touching one of the two edges is the difference between filling and fitting.
    expect(Math.max(width / 540, height / 380)).toBeCloseTo(1, 2);
    expect(board.zoomLevel).toBeGreaterThanOrEqual(0.5);
  });

  it('keeps authored roads below every tactical actor', () => {
    const level = candidate01Level('c01-01');
    const board = new BoardView(composition(ART), createState(TEST_CATALOG, level), {
      onTileClick: () => {},
      onTileEnter: () => {},
      onLeave: () => {},
      onSecondary: () => {},
      onScale: () => {},
    });
    board.render(emptyOverlay());

    const world = board.el.querySelector('.board-world')!;
    const ground = world.querySelector('.layer-ground')!;
    const units = world.querySelector('.layer-units')!;
    const foreground = world.querySelector('.layer-foreground')!;
    const children = [...world.children];
    expect(children.indexOf(ground)).toBeLessThan(children.indexOf(units));
    expect(children.indexOf(units)).toBeLessThan(children.indexOf(foreground));
    expect(ground.querySelector('.candidate-ground-route')).toBeTruthy();
    // A painted scene draws no lattice. This used to assert one stand node per
    // cell — 12,393 nodes on the largest map — which a rule in `battle.css` then
    // set to `opacity: 0`. The board and the stylesheet were both right and the
    // player saw neither.
    expect(world.querySelector('.layer-grid')!.children).toHaveLength(0);

    /*
     * And it draws no tile where its own ground already covers the cell.
     *
     * This scene answers `''` for every plain cell, which is a different answer
     * from `null`: no tile, rather than no opinion. While the consumer tested that
     * answer for truthiness the pack had to return an invisible non-empty group
     * instead, so the layer held one empty element per cell — 8,352 nodes of
     * nothing on `c01-16`, and 22% of the whole board.
     */
    const terrain = world.querySelector('.layer-terrain')!;
    expect(terrain.children.length).toBeLessThan(level.width * level.height / 10);
    // What is left is the cells this art does draw: its map structures.
    expect(terrain.children.length).toBeGreaterThan(0);
    expect([...terrain.children].every((piece) => piece.innerHTML.trim().length > 0)).toBe(true);
    board.dispose();
  });

  it('selects a unit, previews the path, and shows a move range', () => {
    const level = BUILTIN_LEVELS[0];
    const c = new GameController(level, () => {}, { engine: TEST_ENGINE, art: ART });
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
    const c = new GameController(level, () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board, level.width * TILE);

    click(board, { x: 1, y: 1 }); // player 1's castle, empty at start
    const modal = c.root.querySelector('.hud-veil')!;
    expect(modal.textContent).toContain('征募单位');
    expect(modal.querySelectorAll('.recruit-card').length).toBeGreaterThan(4);
    c.dispose();
  });

  /**
   * Every structure a shipped level places is drawn, and can be read.
   *
   * `c01-15` places a 500 HP `c01.mother-root` and makes destroying it the
   * victory condition. The campaign's art has no topic for that type, the board
   * took `null` for "draw nothing", and the player was asked to break something
   * invisible — with a *terrain* of the same name drawn underneath it, so the
   * cell looked like ordinary silverwood ground.
   */
  it('draws and reports every structure a shipped level places', () => {
    const withStructures = CANDIDATE_01_LEVELS.filter((level) => (level.structures ?? []).length > 0);
    expect(withStructures.length).toBeGreaterThan(2);

    for (const level of withStructures) {
      const c = new GameController(level, () => {}, { engine: TEST_ENGINE, art: ART });
      host.append(c.root);
      const drawn = [...c.root.querySelectorAll('.layer-structures > g')];
      expect(drawn, level.id).toHaveLength(level.structures!.length);
      for (const group of drawn) expect(group.innerHTML.length, level.id).toBeGreaterThan(40);
      c.dispose();
      host.innerHTML = '';
    }
  });

  it('reports the structure under the cursor, with its condition', () => {
    const level = candidate01Level('c01-15');
    const structure = level.structures![0];
    const c = new GameController(level, () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board, level.width * TILE);

    hover(board, structure);
    const ledger = c.root.querySelector('.hud-ledger')!.textContent ?? '';
    expect(ledger).toContain(TEST_CATALOG.structures.get(structure.type).name);
    expect(ledger).toContain(`${structure.hp}`);
    c.dispose();
  });

  /**
   * A presentation cannot hide a field object by declining to draw one.
   *
   * `null` means "no opinion, ask the floor" — the convention every `ArtProvider`
   * method already uses — and the board used to read it as "draw nothing". Tested
   * against art that declines *everything*, because the shipped campaign happens
   * to answer for every marker kind, so its levels cannot exercise the floor and
   * a test written against them passes with the floor taken away.
   */
  it('draws a structure and a mark even when the art declines both', () => {
    const level = BUILTIN_LEVELS[0];
    const state = createState(TEST_CATALOG, level);
    state.markers.push({ id: 1, kind: 'routed', at: { x: 2, y: 2 }, owner: 1, meta: {} });
    state.structures.push({
      id: 'probe', type: TEST_CATALOG.structures.ids()[0], owner: 1,
      x: 3, y: 3, hp: 40, disabled: false, statuses: [],
    });

    const silent = new ArtDirection([], [{
      ...GENERIC_PRESENTATION,
      id: 'declines-everything',
      structure: () => null,
      marker: () => null,
    }]);
    const board = new BoardView(composition(silent), state, {
      onTileClick: () => {},
      onTileEnter: () => {},
      onLeave: () => {},
      onSecondary: () => {},
      onScale: () => {},
    });
    board.render(emptyOverlay());

    for (const layer of ['markers', 'structures']) {
      const drawn = board.el.querySelectorAll(`.layer-${layer} > g`);
      expect(drawn, layer).toHaveLength(1);
      expect(drawn[0].innerHTML.length, layer).toBeGreaterThan(40);
    }
    board.dispose();
  });

  /**
   * An effect is a drawing at a place, not a drawing with a place baked into it.
   *
   * These used to write the cell's scene coordinates into their own markup — the one
   * thing left on the board that a renderer could not treat as a picture positioned
   * somewhere, because it would have had to rasterise a field-sized image to hold a
   * damage number. Moving the position out is also the change most able to put the
   * number in the wrong place, so this pins where it lands.
   */
  it('places an effect at the cell rather than drawing it there', async () => {
    const level = BUILTIN_LEVELS[0];
    const board = new BoardView(composition(ART), createState(TEST_CATALOG, level), {
      onTileClick: () => {},
      onTileEnter: () => {},
      onLeave: () => {},
      onSecondary: () => {},
      onScale: () => {},
    });

    const played = board.animateHit({ x: 3, y: 2 }, 12, false);
    const fx = board.el.querySelector('.fx')!;
    // The middle of cell (3,2) on a 32px lattice.
    expect(fx.getAttribute('transform')).toBe('translate(112.00,80.00)');
    // And nothing inside it names an absolute position any more.
    expect(fx.querySelector('[data-part="number"] text')!.getAttribute('x')).toBeNull();
    expect(fx.querySelector('[data-part="burst"] circle')!.getAttribute('cx')).toBeNull();
    await played;

    const announced = board.announce('第 2 回合', '#3f7fd8');
    const banner = board.el.querySelector('.fx')!;
    // Half the field, less half the band's height — stated as the relationship
    // rather than as a number, so it reads as the rule it is.
    const bandTop = (level.height * TILE) / 2 - 17;
    expect(banner.getAttribute('transform')).toBe(`translate(0.00,${bandTop.toFixed(2)})`);
    await announced;
    board.dispose();
  });

  it('renders every built-in level without throwing', () => {
    for (const level of BUILTIN_LEVELS) {
      const c = new GameController(level, () => {}, { engine: TEST_ENGINE, art: ART });
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
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    expect(c.root.querySelector('.cast-strip')).toBeNull();
    c.dispose();
  });

  it('marks the aimed tiles and counts the charge down', async () => {
    const c = new GameController(castingLevel(), () => {}, { engine: TEST_ENGINE, art: ART });
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
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    expect(c.root.querySelector('.order-strip')).toBeNull();
    c.dispose();
  });

  it('shows the upcoming order, active unit first, under per-unit turns', () => {
    const c = new GameController(initiativeLevel(), () => {}, { engine: TEST_ENGINE, art: ART });
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
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    expect(press(c.root, 'save')).toBeNull();
    expect(press(c.root, 'resume')).toBeNull();
    c.dispose();
  });

  it('saves the turn it is on and resumes into it', () => {
    const saves = slot();
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE, art: ART, saves });
    host.append(c.root);

    expect(press(c.root, 'resume').disabled).toBe(true);
    press(c.root, 'save').click();
    expect(saves.has()).toBe(true);
    expect(hudSays(c.root)).toContain('已保存第 1 回合的进度');

    press(c.root, 'resume').click();
    expect(hudSays(c.root)).toContain('已读取第 1 回合的存档');
    c.dispose();
  });

  it('reports a save this ruleset cannot honour instead of dying on it', () => {
    const saves = slot();
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: TEST_ENGINE, art: ART, saves });
    host.append(c.root);
    // Save first, so the slot is offered, then replace what it holds with a
    // battle this ruleset cannot run.
    press(c.root, 'save').click();
    saves.poison();

    press(c.root, 'resume').click();
    expect(hudSays(c.root)).toContain('无法读取存档');
    // The battle on screen is still the one being played.
    expect(c.root.querySelectorAll('.layer-units > .unit').length).toBe(BUILTIN_LEVELS[0].units.length);
    c.dispose();
  });

  it('does not blame the save for a defect underneath it', () => {
    const saves = slot();
    const broken = createBattleEngine({ content: TEST_CATALOG });
    broken.loadBattle = () => { throw new DomainInvariantError('a rule under the loader is wrong'); };
    const c = new GameController(BUILTIN_LEVELS[0], () => {}, { engine: broken, art: ART, saves });
    host.append(c.root);
    press(c.root, 'save').click();

    // This catch used to swallow everything, so a defect anywhere under
    // `loadBattle` was reported as "your save is unreadable" — and the save,
    // which was fine, looked like the thing to delete. The defect now leaves
    // the handler, which jsdom reports as an uncaught error on the window.
    const escaped: unknown[] = [];
    const record = (event: ErrorEvent) => {
      escaped.push(event.error);
      event.preventDefault();
    };
    window.addEventListener('error', record);
    press(c.root, 'resume').click();
    window.removeEventListener('error', record);

    expect(escaped[0]).toBeInstanceOf(DomainInvariantError);
    expect(hudSays(c.root)).not.toContain('无法读取存档');
    c.dispose();
  });
});

/**
 * A board drawn under another tiling.
 *
 * The picture has to follow the rules: if the engine says two cells are
 * neighbours, the player has to be able to see it. Placement, hit-testing, the
 * grid lines and the facing menu all come from the tiling now, so this checks
 * the four of them on a hex board.
 */
describe('a hex board', () => {
  let host: HTMLElement;

  const hexLevel = () => ({
    ...BUILTIN_LEVELS[0],
    id: 'hex-test',
    rules: { ...BUILTIN_LEVELS[0].rules, grid: 'hex' },
  });

  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) =>
      setTimeout(() => cb(performance.now()), 0) as unknown as number,
    );
    document.body.innerHTML = '<div id="app"></div>';
    host = document.getElementById('app')!;
  });

  it('staggers its rows, clips its tiles and rules its own edges', () => {
    const controller = new GameController(hexLevel(), () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(controller.root);
    const board = controller.root.querySelector('svg.board') as SVGSVGElement;

    // Tiles in the order the layer emits them, row-major. The clip definition is
    // the layer's one unplaced piece, and it is not a tile.
    const tiles = [...board.querySelectorAll('.layer-terrain > g')]
      .filter((piece) => !piece.querySelector('defs'));
    const tile = (x: number, y: number) =>
      tiles[y * hexLevel().width + x].getAttribute('transform')!;
    const xOf = (transform: string) => Number.parseFloat(transform.replace(/[^\d.,-]/g, '').split(',')[0]);

    // An odd row sits half a cell to the right of the even row above it.
    expect(xOf(tile(1, 1)) - xOf(tile(1, 0))).toBeCloseTo(TILE / 2, 1);
    // And rows overlap vertically, or the hexes would not touch.
    const yOf = (transform: string) => Number.parseFloat(transform.replace(/[^\d.,-]/g, '').split(',')[1]);
    expect(yOf(tile(0, 1)) - yOf(tile(0, 0))).toBeLessThan(TILE);

    // Square tile art clipped to the cell's own shape, and edges drawn per cell.
    expect(board.querySelector('clipPath polygon')).toBeTruthy();
    expect(board.querySelector('.layer-grid polygon')).toBeTruthy();
    expect(board.querySelector('.layer-grid line')).toBeNull();
    controller.dispose();
  });

  it('offers the six facings the tiling has, not four', () => {
    const level = hexLevel();
    const controller = new GameController(level, () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(controller.root);
    const board = controller.root.querySelector('svg.board') as SVGSVGElement;
    // Scale 1, so a scene coordinate is a client coordinate.
    stubLayout(board, board.viewBox.baseVal.width);

    const mine = level.units.find((unit) => unit.owner === 1)!;
    const centre = TEST_ENGINE.rules.grids.get('hex').center(mine);
    board.dispatchEvent(new window.MouseEvent('pointerdown', {
      bubbles: true, button: 0, clientX: centre.x * TILE, clientY: centre.y * TILE,
    }));

    const facings = [...controller.root.querySelectorAll('[data-act="facing"]')]
      .map((button) => button.getAttribute('data-arg'));
    expect(facings).toHaveLength(6);
    expect(facings).toContain('hexNortheast');
    expect(facings).not.toContain('north');
    controller.dispose();
  });
});

/**
 * A capability the rules have but no interface can reach is unfinished.
 *
 * A dozen shipped unit types declare formations, the action and its validation
 * have been complete for rounds, and the shared HUD offered no way in — so the
 * capability table said 「未实现」 for the interface column and meant it.
 */
const formationLevel = () => normaliseLevel({
  schema: 2,
  id: 'formation-test',
  name: '阵形',
  width: 4,
  height: 2,
  terrain: ['....', '....'],
  units: [
    { x: 0, y: 0, unit: 'c01.swordsman', owner: 1 },
    { x: 1, y: 0, unit: 'c01.swordsman', owner: 1 },
    { x: 3, y: 1, unit: 'c01.swordsman', owner: 2 },
  ],
  players: [
    { id: 1, name: 'P1', team: 1, color: '#3f7fd8', controller: 'human', resources: {} },
    { id: 2, name: 'P2', team: 2, color: '#d8483f', controller: 'human', resources: {} },
  ],
  rules: {},
  victory: [{ type: 'routEnemies' }],
});

describe('ordering a formation', () => {
  let host: HTMLElement;

  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) =>
      setTimeout(() => cb(performance.now()), 0) as unknown as number,
    );
    document.body.innerHTML = '<div id="app"></div>';
    host = document.getElementById('app')!;
  });

  const formationButtons = (root: Element) =>
    [...root.querySelectorAll('[data-act="formation"]')] as HTMLButtonElement[];

  it('offers the shapes this unit type declares, and takes one', async () => {
    const c = new GameController(formationLevel(), () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board, 4 * TILE);
    click(board, { x: 0, y: 0 });

    const offered = formationButtons(c.root);
    expect(offered.map((button) => button.dataset.arg))
      .toEqual(['formation-line', 'formation-loose']);

    offered[0].click();
    for (let frame = 0; frame < 12; frame++) await new Promise((resolve) => setTimeout(resolve, 0));

    expect(hudSays(c.root)).toContain('线列');
    // And the same button now offers the way back out.
    click(board, { x: 0, y: 0 });
    const current = formationButtons(c.root).find((button) => button.textContent?.includes('解除'));
    expect(current?.dataset.arg).toBe('');
    c.dispose();
  });

  it('shows a shape it cannot hold as unavailable rather than hiding it', () => {
    const level = formationLevel();
    // The lone swordsman on the far side has nobody to line up with.
    const c = new GameController(level, () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board, 4 * TILE);
    click(board, { x: 1, y: 0 });
    expect(formationButtons(c.root).length).toBeGreaterThan(0);

    const alone = new GameController(normaliseLevel({
      ...level, id: 'alone', units: [level.units[0], level.units[2]],
    }), () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(alone.root);
    const board2 = alone.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board2, 4 * TILE);
    click(board2, { x: 0, y: 0 });

    const disabled = [...alone.root.querySelectorAll('.unit-section button.disabled')] as HTMLButtonElement[];
    expect(disabled.some((button) => button.title.includes('相邻友军'))).toBe(true);
    c.dispose();
    alone.dispose();
  });
});

/**
 * A level that opens in the deployment phase.
 *
 * The rules have had `deployUnit` and `finishDeployment` since the phase was
 * introduced, and every interface refused every click until the battle was
 * `playing` — so the one shipped consumer was a lab fixture, and the capability
 * table said 「未实现」 for the interface column and meant it.
 */
const deploymentLevel = () => normaliseLevel({
  schema: 2,
  id: 'deployment-test',
  name: '部署',
  width: 4,
  height: 2,
  terrain: ['..^.', '....'],
  units: [
    { x: 0, y: 0, unit: 'c01.swordsman', owner: 1, key: 'foot' },
    { x: 1, y: 0, unit: 'c01.archer', owner: 1, key: 'bow' },
    { x: 3, y: 1, unit: 'c01.swordsman', owner: 2 },
  ],
  players: [
    { id: 1, name: 'P1', team: 1, color: '#3f7fd8', controller: 'human', resources: {} },
    { id: 2, name: 'P2', team: 2, color: '#d8483f', controller: 'human', resources: {} },
  ],
  scenario: { zones: [{ id: 'blue', cells: [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 0, y: 1 }] }] },
  deployment: { order: [1], zones: [{ player: 1, zone: 'blue' }] },
  rules: {},
  victory: [{ type: 'routEnemies' }],
});

describe('arranging the line before the battle', () => {
  let host: HTMLElement;

  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) =>
      setTimeout(() => cb(performance.now()), 0) as unknown as number,
    );
    // jsdom has no scrolling, and picking from the roster centres the board.
    Element.prototype.scrollTo = () => {};
    document.body.innerHTML = '<div id="app"></div>';
    host = document.getElementById('app')!;
  });

  const settle = async () => {
    for (let frame = 0; frame < 12; frame++) await new Promise((resolve) => setTimeout(resolve, 0));
  };

  it('opens on the deployment panel, moves a unit, and starts the battle', async () => {
    const c = new GameController(deploymentLevel(), () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board, 4 * TILE);

    // The panel is the roster, and the primary control confirms rather than ends.
    expect(hudSays(c.root)).toContain('战前部署');
    const roster = [...c.root.querySelectorAll('[data-act="deploy-pick"]')] as HTMLButtonElement[];
    expect(roster).toHaveLength(2);
    expect(c.root.querySelector('[data-act="deploy-done"]')).not.toBeNull();
    expect(c.root.querySelector('[data-act="end"]')).toBeNull();

    // Picking from the roster is the same act as clicking the unit on the board.
    expect(roster[0].textContent).toContain('0,0');
    roster[0].click();
    await settle();
    click(board, { x: 0, y: 1 });
    await settle();

    const moved = [...c.root.querySelectorAll('[data-act="deploy-pick"]')] as HTMLButtonElement[];
    expect(moved[0].textContent).toContain('0,1');
    // Still deploying: placing a unit is not confirming the arrangement.
    expect(c.root.querySelector('[data-act="deploy-done"]')).not.toBeNull();

    c.dispose();
  });

  it('will not put a unit outside its zone, and hands over on confirm', async () => {
    const c = new GameController(deploymentLevel(), () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board, 4 * TILE);

    click(board, { x: 0, y: 0 });
    await settle();
    // (1,1) is on the board and empty, but outside the deployment zone.
    click(board, { x: 1, y: 1 });
    await settle();
    const roster = [...c.root.querySelectorAll('[data-act="deploy-pick"]')] as HTMLButtonElement[];
    expect(roster[0].textContent).toContain('0,0');

    (c.root.querySelector('[data-act="deploy-done"]') as HTMLButtonElement).click();
    await settle();

    // The battle is under way: the ordinary turn control is back.
    expect(c.root.querySelector('[data-act="end"]')).not.toBeNull();
    expect(c.root.querySelector('[data-act="deploy-done"]')).toBeNull();
    c.dispose();
  });
});

/**
 * A level with a carrier standing beside two units.
 *
 * `transports.ts` has had embark, disembark, passenger loss and save coverage
 * since transports were introduced, and not one shipped unit type declared
 * `transport` — so outside unit tests, nothing in the repository had ever run
 * it. The rules existed only in their committing form, so no menu could be
 * built from them.
 */
const transportLevel = () => normaliseLevel({
  schema: 2,
  id: 'transport-test',
  name: '登载',
  width: 4,
  height: 2,
  terrain: ['....', '....'],
  units: [
    { x: 0, y: 0, unit: 'c01.supply-wagon', owner: 1, key: 'cart' },
    { x: 1, y: 0, unit: 'c01.swordsman', owner: 1, key: 'foot' },
    { x: 0, y: 1, unit: 'c01.knight', owner: 1, key: 'horse' },
    { x: 3, y: 1, unit: 'c01.swordsman', owner: 2 },
  ],
  players: [
    { id: 1, name: 'P1', team: 1, color: '#3f7fd8', controller: 'human', resources: {} },
    { id: 2, name: 'P2', team: 2, color: '#d8483f', controller: 'human', resources: {} },
  ],
  rules: {},
  victory: [{ type: 'routEnemies' }],
});

describe('loading and unloading a transport', () => {
  let host: HTMLElement;

  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) =>
      setTimeout(() => cb(performance.now()), 0) as unknown as number,
    );
    document.body.innerHTML = '<div id="app"></div>';
    host = document.getElementById('app')!;
  });

  const settle = async () => {
    for (let frame = 0; frame < 12; frame++) await new Promise((resolve) => setTimeout(resolve, 0));
  };
  const buttons = (root: Element, act: string) =>
    [...root.querySelectorAll(`[data-act="${act}"]`)] as HTMLButtonElement[];

  it('offers the carrier beside a unit, takes it aboard, and puts it down again', async () => {
    const c = new GameController(transportLevel(), () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board, 4 * TILE);

    click(board, { x: 1, y: 0 });
    const carriers = buttons(c.root, 'embark');
    expect(carriers).toHaveLength(1);

    carriers[0].click();
    await settle();

    // Aboard: selecting the carrier now offers putting the passenger down, and
    // the cell the swordsman stood on has nobody to select.
    click(board, { x: 0, y: 0 });
    const passengers = buttons(c.root, 'disembark');
    expect(passengers).toHaveLength(1);
    expect(passengers[0].textContent).toContain('边境剑士');

    passengers[0].click();
    await settle();
    expect(hudSays(c.root)).toContain('选择目标');

    click(board, { x: 1, y: 0 });
    await settle();
    // Back on the board: the carrier has no passenger left to put down.
    click(board, { x: 0, y: 0 });
    expect(buttons(c.root, 'disembark')).toHaveLength(0);
    c.dispose();
  });

  it('shows a carrier that will not take this unit as unavailable, with the reason', () => {
    const c = new GameController(transportLevel(), () => {}, { engine: TEST_ENGINE, art: ART });
    host.append(c.root);
    const board = c.root.querySelector('svg.board') as SVGSVGElement;
    stubLayout(board, 4 * TILE);

    // The knight is beside the wagon too, and the wagon takes only infantry.
    click(board, { x: 0, y: 1 });
    expect(buttons(c.root, 'embark')).toHaveLength(0);
    const refused = [...c.root.querySelectorAll('.unit-section button.disabled')] as HTMLButtonElement[];
    expect(refused.some((button) => button.title.includes('不接受这个兵种'))).toBe(true);
    c.dispose();
  });
});
