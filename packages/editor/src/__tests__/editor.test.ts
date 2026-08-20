// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from 'vitest';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { GENERIC_ART, TILE } from '@empire/game-ui';
import {
  ContentPackInstaller,
  createContentCatalog,
  defineTerrain,
  defineUnit,
  defineWeapon,
  validateLevel,
  type ContentCatalog,
} from '@empire/battle-engine';
import { COMMON_CONTENT_PACK } from '@empire/content-common';
import {
  emptyLevel,
  mapFromLevel,
  normaliseLevel,
  createBattleEngine,
} from '@empire/battle-engine';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires';
import { EditorApp } from '../app';
import { EditorDocument } from '../document';
import { BrushSettings } from '../tools';

import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';
import { CANDIDATE_01_ART } from '@empire/story-candidate-01/presentation';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const TEST_SETUP = {
  rules: createBattleEngine({ content: TEST_CATALOG }).rules,
  art: CANDIDATE_01_ART,
  presets: BUILTIN_LEVELS,
};
const TEST_RULES = TEST_SETUP.rules;

/** Every runtime source of the editor package, by name and text. */
function editorSources(directory = join(import.meta.dirname, '..')): [string, string][] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    if (entry.name === '__tests__' || entry.name === 'styles') return [];
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return editorSources(path);
    return entry.name.endsWith('.ts') ? [[entry.name, readFileSync(path, 'utf8')] as [string, string]] : [];
  });
}

const escapeForRegExp = (value: string) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/**
 * A game the editor has never heard of: no `plain`, no `soldier`.
 *
 * Composed from the common layer only, so it declares its own ground and its
 * own blank terrain — which is exactly the situation the editor's hard-coded
 * ids made unauthorable.
 */
const OTHER_GAME: ContentCatalog = (() => {
  const catalog = createContentCatalog();
  const spear = defineWeapon({ id: 'spear', name: '矛', power: 40, damageType: 'cut' });
  new ContentPackInstaller(catalog).install(COMMON_CONTENT_PACK, {
    id: 'test.other-game',
    version: 1,
    damageTypes: [{ id: 'cut', name: '割', tags: [] }],
    armorClasses: [{ id: 'hide', name: '皮甲', tags: [] }],
    damageMatchups: [{ damageType: 'cut', armorClass: 'hide', multiplier: 1 }],
    terrains: [
      defineTerrain({ id: 'sand', name: '沙地', cost: { foot: 1, cavalry: 1, heavy: 1, flying: 1 } }),
      defineTerrain({ id: 'dune', name: '沙丘', cost: { foot: 2, cavalry: 3, heavy: 3, flying: 1 } }),
    ],
    terrainCharacters: { '_': 'sand', 'n': 'dune' },
    defaultTerrain: 'sand',
    weapons: [spear],
    units: [defineUnit({
      id: 'nomad',
      name: '游牧兵',
      weapons: ['spear'],
      movementClass: 'foot',
      armorClass: 'hide',
      value: 100,
      recruitCosts: [],
    }, new Map([['spear', spear]]))],
  });
  return catalog;
})();

function stubLayout(svg: SVGSVGElement, width: number, height: number): void {
  svg.getBoundingClientRect = () =>
    ({ left: 0, top: 0, width, height, right: width, bottom: height, x: 0, y: 0, toJSON: () => ({}) }) as DOMRect;
}

function stroke(
  el: Element,
  from: { x: number; y: number },
  to = from,
  button = 0,
): void {
  const px = (t: { x: number; y: number }) => ({
    clientX: t.x * TILE + TILE / 2,
    clientY: t.y * TILE + TILE / 2,
  });
  el.dispatchEvent(
    new window.MouseEvent('pointerdown', { bubbles: true, button, ...px(from) }),
  );
  if (to !== from) {
    el.dispatchEvent(
      new window.MouseEvent('pointermove', { bubbles: true, buttons: button === 2 ? 2 : 1, ...px(to) }),
    );
  }
  el.dispatchEvent(new window.MouseEvent('pointerup', { bubbles: true, button, ...px(to) }));
}

describe('map editor', () => {
  let host: HTMLElement;
  let app: EditorApp;
  let board: SVGSVGElement;

  const level = () => normaliseLevel(JSON.parse(JSON.stringify(BUILTIN_LEVELS[0])));

  beforeEach(() => {
    document.body.innerHTML = '<div id="app"></div>';
    host = document.getElementById('app')!;
    localStorage.clear();
    // jsdom lacks pointer capture.
    Element.prototype.setPointerCapture = () => {};
    app = new EditorApp(TEST_SETUP, level());
    app.mount(host);
    board = host.querySelector('svg.editor-board') as SVGSVGElement;
    stubLayout(board, BUILTIN_LEVELS[0].width * TILE, BUILTIN_LEVELS[0].height * TILE);
  });

  /**
   * A control declares an intent in `data-act` or `data-field`; the app holds the
   * tables that answer them. They used to live in different halves of one file
   * with nothing comparing them, so a typo produced a control that looked alive
   * and did nothing.
   */
  it('answers every intent its own markup declares', () => {
    const { commands, fields, genericFieldPrefixes } = app.handledIntents;
    const declared = (attribute: string) =>
      [...host.querySelectorAll(`[${attribute}]`)]
        .map((element) => element.getAttribute(attribute)!)
        .filter((intent) => intent.length > 0);

    const deadControls = [...new Set(declared('data-act'))].filter((act) => !commands.includes(act));
    const deadInputs = [...new Set(declared('data-field'))].filter((field) =>
      !fields.includes(field) && !genericFieldPrefixes.some((prefix) => field.startsWith(prefix)));

    expect({ deadControls, deadInputs }).toEqual({ deadControls: [], deadInputs: [] });
  });

  it('renders the palette, the board and the validation panel', () => {
    expect(host.querySelectorAll('.swatch').length).toBe(TEST_CATALOG.terrains.all().length);
    expect(host.querySelectorAll('.unit-chip').length).toBe(TEST_CATALOG.units.all().length);
    expect(host.querySelector('.swatch[data-arg="c01.scorched"]')).toBeTruthy();
    expect(host.querySelector('.unit-chip[data-arg="c01.laiya"]')).toBeTruthy();
    expect(board.querySelectorAll('.layer-terrain > g').length).toBe(
      BUILTIN_LEVELS[0].width * BUILTIN_LEVELS[0].height,
    );
    expect(host.querySelector('.props')!.textContent).toContain('检查');
  });

  it('paints terrain with the brush and records it in the exported level', () => {
    (host.querySelector('.swatch[data-arg="mountain"]') as HTMLElement).click();
    stroke(board, { x: 5, y: 4 });
    const exported = app.exportLevel();
    expect(exported.terrain[4][5]).toBe('^');
    expect(validateLevel(TEST_RULES, exported).filter((i) => i.severity === 'error')).toEqual([]);
  });

  it('drag-paints a whole run of tiles in one undo step', () => {
    (host.querySelector('.swatch[data-arg="water"]') as HTMLElement).click();
    stroke(board, { x: 3, y: 5 }, { x: 6, y: 5 });
    const before = app.exportLevel();
    expect(before.terrain[5][3]).toBe('~');
    expect(before.terrain[5][6]).toBe('~');

    app.undo();
    const after = app.exportLevel();
    expect(after.terrain[5][3]).not.toBe('~');
    expect(after.terrain[5][6]).not.toBe('~');
  });

  it('places and erases units', () => {
    (host.querySelector('.unit-chip[data-arg="dragon"]') as HTMLElement).click();
    stroke(board, { x: 7, y: 4 });
    expect(app.exportLevel().units.some((u) => u.unit === 'dragon' && u.x === 7 && u.y === 4)).toBe(true);

    (host.querySelector('.btn.tool[data-arg="erase"]') as HTMLElement).click();
    stroke(board, { x: 7, y: 4 });
    expect(app.exportLevel().units.some((u) => u.x === 7 && u.y === 4)).toBe(false);
  });

  it('assigns building ownership with the owner tool', () => {
    (host.querySelector('.btn.tool[data-arg="owner"]') as HTMLElement).click();
    (host.querySelector('.owner-chip[data-arg="2"]') as HTMLElement).click();
    stroke(board, { x: 12, y: 1 }); // the neutral village
    const owners = app.exportLevel().owners;
    expect(owners.find((o) => o.x === 12 && o.y === 1)!.owner).toBe(2);
  });

  it('clears ownership when a building is painted over', () => {
    (host.querySelector('.swatch[data-arg="plain"]') as HTMLElement).click();
    stroke(board, { x: 1, y: 4 }); // player 1's village
    const exported = app.exportLevel();
    expect(exported.terrain[4][1]).toBe('.');
    expect(exported.owners.some((o) => o.x === 1 && o.y === 4)).toBe(false);
  });

  it('flags a unit standing on impassable terrain', () => {
    (host.querySelector('.unit-chip[data-arg="soldier"]') as HTMLElement).click();
    stroke(board, { x: 8, y: 5 }); // river tile
    const issues = validateLevel(TEST_RULES, app.exportLevel());
    expect(issues.some((i) => i.severity === 'error' && i.message.includes('无法站在'))).toBe(true);
  });

  it('resizes the map, keeping the top-left content', () => {
    const before = app.exportLevel();
    app.resize(10, 8);
    const after = app.exportLevel();
    expect(after.width).toBe(10);
    expect(after.height).toBe(8);
    expect(after.terrain[0]).toBe(before.terrain[0].slice(0, 10));
    expect(after.units.every((u) => u.x < 10 && u.y < 8)).toBe(true);
  });

  it('round-trips: exported level loads back into a playable map', () => {
    (host.querySelector('.swatch[data-arg="forest"]') as HTMLElement).click();
    stroke(board, { x: 4, y: 6 }, { x: 4, y: 8 });
    const exported = app.exportLevel();
    const reloaded = normaliseLevel(JSON.parse(JSON.stringify(exported)));
    const map = mapFromLevel(TEST_CATALOG, reloaded);
    expect(map.tiles[6 * map.width + 4]).toBe('forest');
    expect(validateLevel(TEST_RULES, reloaded).filter((i) => i.severity === 'error')).toEqual([]);
  });

  it('autosaves a draft that survives a reload', () => {
    (host.querySelector('.swatch[data-arg="wall"]') as HTMLElement).click();
    stroke(board, { x: 9, y: 9 });
    const draft = localStorage.getItem('empire.editorDraft');
    expect(draft).toBeTruthy();
    const restored = normaliseLevel(JSON.parse(draft!));
    expect(restored.terrain[9][9]).toBe('#');
  });
});

describe('a general editor knows no particular game', () => {
  it('names no content id and imports no story, in any of its sources', () => {
    // It used to import one campaign's levels directly and paint with the
    // literals `plain` and `soldier`, so the "general" editor could only really
    // author ancient-empires maps: a story whose ground is sand had nothing to
    // erase back to, and its chapters never appeared in the open menu — which
    // was already wrong for the shipped editor, whose catalog holds candidate-01.
    //
    // Everything it needs is now asked for: blank ground from the catalog's
    // terrain encoding, the starting brush from the palette's own first entry,
    // and the level list from whoever composed the ruleset.
    const catalog = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
    const ids = [...catalog.terrains.ids(), ...catalog.units.ids(), ...catalog.weapons.ids()];
    const named = new RegExp(`['"\`](?:${ids.map(escapeForRegExp).join('|')})['"\`]`);

    const offenders = editorSources().flatMap(([name, source]) => {
      const code = source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/[^\n]*/g, '');
      const named_ = named.exec(code)?.[0];
      if (named_) return [`${name} names ${named_}`];
      const imported = /from '@empire\/(?:content|story)-[^']*'/.exec(code)?.[0];
      return imported ? [`${name} imports ${imported}`] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('erases to the ground the catalog calls blank, whatever that is', () => {
    // Deliberately a catalog with no `plain` and no `soldier` in it. Asserting
    // this against the shipped test catalog proves nothing: its blank ground is
    // `plain`, so hard-coding `plain` back into the document would pass. This is
    // the second story the editor was supposed to be able to author.
    const other = OTHER_GAME;
    const brush = new BrushSettings(other);
    expect(brush.blank).toBe('sand');
    expect(brush.terrain).toBe('sand');
    expect(brush.unitType).toBe('nomad');

    const document = EditorDocument.fromLevel(other, emptyLevel(other, 4, 4));
    expect(new Set(document.map.tiles)).toEqual(new Set(['sand']));
    document.resize(6, 6);
    expect(new Set(document.map.tiles)).toEqual(new Set(['sand']));
  });

  /**
   * The editor draws with the art it was opened with.
   *
   * It hard-coded `GENERIC_ART` in the board and in the palette, so an author
   * working on the shipped campaign saw thirty-one of its forty unit types as
   * the same soldier — the pack's art was installed in the very application
   * running the editor, and the editor never asked for it. The setup carries the
   * art now, beside the ruleset it is authored against.
   */
  it('draws with the art it was opened with, on the board and in the palette', () => {
    const opened = () => normaliseLevel(JSON.parse(JSON.stringify(BUILTIN_LEVELS[0])));
    const host = document.createElement('div');
    document.body.append(host);
    new EditorApp(TEST_SETUP, opened()).mount(host);

    // A marker only the composed pack's provider can produce.
    expect(host.querySelector('.layer-terrain')!.innerHTML).toContain('data-runtime-raster');
    expect(host.querySelector('.palette')!.innerHTML).toContain('data-runtime-raster');

    const generic = document.createElement('div');
    document.body.append(generic);
    new EditorApp({ ...TEST_SETUP, art: GENERIC_ART }, opened()).mount(generic);
    expect(generic.querySelector('.layer-terrain')!.innerHTML).not.toContain('data-runtime-raster');

    host.remove();
    generic.remove();
  });
});

describe('linting against the ruleset, not just the catalog', () => {
  it('reports a level naming a rule nobody registered', () => {
    // The editor used to hold a content catalog alone, so it could tell you a
    // unit type did not exist but not that a standing order was one the composed
    // rules had never heard of — that only surfaced when the battle started.
    const clean = normaliseLevel(JSON.parse(JSON.stringify(BUILTIN_LEVELS[0])));
    expect(TEST_RULES.referenceChecks.levelIssues(TEST_RULES, clean)).toEqual([]);

    const broken = normaliseLevel(JSON.parse(JSON.stringify(BUILTIN_LEVELS[0])));
    broken.units[0].directive = { mode: 'nonesuch', waypoints: [], cursor: 0 };
    expect(TEST_RULES.referenceChecks.levelIssues(TEST_RULES, broken))
      .toContainEqual(expect.stringContaining('未注册的常驻命令「nonesuch」'));
  });
});
