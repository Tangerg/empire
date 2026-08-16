// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from 'vitest';
import { TILE } from '@empire/game-ui/art/terrain';
import { normaliseLevel } from '@empire/battle-engine/level';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires/levels';
import { EditorApp } from '../app';
import { BrushSettings, EDITOR_TOOLS, EditorToolRegistry, rectTiles, type EditorTool } from '../tools';

import { createTestCatalog } from '@empire/test-content';
import { createBattleEngine } from '@empire/battle-engine/plugins/default';

/** Composed per suite, exactly like an application composition root. */
const TEST_SETUP = {
  rules: createBattleEngine({ content: createTestCatalog() }).rules,
  presets: BUILTIN_LEVELS,
};

/**
 * A tool is a strategy, not a `switch` arm.
 *
 * The behaviour of one tool used to be spread over a label map, a separate
 * hotkey map, an `if` chain for the two-phase tools and a `switch` for the rest.
 * The two maps had already drifted apart.
 */

function stubLayout(svg: SVGSVGElement, width: number, height: number): void {
  svg.getBoundingClientRect = () =>
    ({ left: 0, top: 0, width, height, right: width, bottom: height, x: 0, y: 0, toJSON: () => ({}) }) as DOMRect;
}

function stroke(el: Element, from: { x: number; y: number }, to = from, button = 0): void {
  const px = (t: { x: number; y: number }) => ({
    clientX: t.x * TILE + TILE / 2,
    clientY: t.y * TILE + TILE / 2,
  });
  el.dispatchEvent(new window.MouseEvent('pointerdown', { bubbles: true, button, ...px(from) }));
  if (to !== from) {
    el.dispatchEvent(new window.MouseEvent('pointermove', { bubbles: true, buttons: button === 2 ? 2 : 1, ...px(to) }));
  }
  el.dispatchEvent(new window.MouseEvent('pointerup', { bubbles: true, button, ...px(to) }));
}

const press = (key: string) =>
  document.dispatchEvent(new window.KeyboardEvent('keydown', { key, bubbles: true }));

describe('editor toolbox', () => {
  it('declares every tool once, with a unique hotkey', () => {
    expect(EDITOR_TOOLS.tools.map((tool) => tool.id)).toEqual([
      'terrain', 'rect', 'fill', 'elevation', 'cliff', 'cover', 'unit', 'owner', 'erase', 'pick',
    ]);
    const keys = EDITOR_TOOLS.tools.map((tool) => tool.hotkey);
    expect(new Set(keys).size).toBe(keys.length);
    for (const tool of EDITOR_TOOLS.tools) {
      expect(EDITOR_TOOLS.forHotkey(tool.hotkey.toUpperCase()), tool.id).toBe(tool);
    }
  });

  it('refuses a tool set that would silently shadow a shortcut', () => {
    const twin = (id: string, hotkey: string): EditorTool => ({
      id, name: id, hotkey, icon: 'grid', highlight: (_c, cursor) => [cursor],
    });
    const set = () => new EditorToolRegistry().register(twin('a', 'x'));

    expect(() => set().register(twin('b', 'x'))).toThrow(/share hotkey/);
    expect(() => set().register(twin('a', 'y'))).toThrow(/already registered/);
  });

  /**
   * The tool set is an extension point, and it now has all of one.
   *
   * It used to be a hand-written `Map` with a `get`: an add-on could neither
   * contribute a tool nor swap one, under a comment saying a tool set is meant
   * to grow.
   */
  it('takes a contributed tool and lets one be replaced', () => {
    const probe = (name: string): EditorTool => ({
      id: 'probe', name, hotkey: 'z', icon: 'grid', highlight: (_c, cursor) => [cursor],
    });
    const tools = EDITOR_TOOLS.clone().register(probe('探针'));

    expect(tools.get('probe').name).toBe('探针');
    expect(tools.forHotkey('Z')?.id).toBe('probe');
    expect(tools.replace({ ...probe('改过的探针'), hotkey: 'z' }).get('probe').name).toBe('改过的探针');
    // The shared set is untouched: a clone is what a host composes with.
    expect(EDITOR_TOOLS.tryGet('probe')).toBeUndefined();
    expect(tools.default).toBe(EDITOR_TOOLS.default);
  });

  it('gives every tool exactly one way to act', () => {
    for (const tool of EDITOR_TOOLS.tools) {
      const acts = tool.twoPhase ? tool.commit : tool.paint;
      expect(acts, `${tool.id} must implement its stroke half`).toBeTypeOf('function');
    }
  });

  it('covers an inclusive rectangle from either corner', () => {
    expect(rectTiles({ x: 2, y: 1 }, { x: 0, y: 0 })).toEqual(rectTiles({ x: 0, y: 0 }, { x: 2, y: 1 }));
    expect(rectTiles({ x: 0, y: 0 }, { x: 1, y: 1 })).toHaveLength(4);
  });
});

describe('brush settings', () => {
  it('clips a square brush to the map', () => {
    const brush = new BrushSettings(TEST_SETUP.rules.content);
    brush.size = 3;
    const document = { inBounds: (at: { x: number; y: number }) => at.x >= 0 && at.y >= 0 } as never;
    expect(brush.square(document, { x: 0, y: 0 })).toHaveLength(4);
    expect(brush.size === 3 && brush.square(document, { x: 5, y: 5 })).toHaveLength(9);
  });
});

describe('tools drive the editor', () => {
  let host: HTMLElement;
  let app: EditorApp;
  let board: SVGSVGElement;
  const source = BUILTIN_LEVELS[0];

  beforeEach(() => {
    document.body.innerHTML = '<div id="app"></div>';
    host = document.getElementById('app')!;
    localStorage.clear();
    Element.prototype.setPointerCapture = () => {};
    app = new EditorApp(TEST_SETUP, normaliseLevel(JSON.parse(JSON.stringify(source))));
    app.mount(host);
    board = host.querySelector('svg.editor-board') as SVGSVGElement;
    stubLayout(board, source.width * TILE, source.height * TILE);
  });

  it('selects every advertised tool by its shortcut', () => {
    // The elevation, cliff and cover tools advertised H/J/K in their tooltips
    // while the key handler only knew seven of the ten tools.
    for (const tool of EDITOR_TOOLS.tools) {
      press(tool.hotkey);
      expect(host.querySelector('.btn.tool.active')!.getAttribute('data-arg'), tool.id).toBe(tool.id);
    }
  });

  it('paints a whole rectangle on release, as one undo step', () => {
    (host.querySelector('.btn.tool[data-arg="rect"]') as HTMLElement).click();
    (host.querySelector('.swatch[data-arg="forest"]') as HTMLElement).click();
    stroke(board, { x: 1, y: 1 }, { x: 3, y: 2 });

    const painted = app.exportLevel();
    for (const tile of rectTiles({ x: 1, y: 1 }, { x: 3, y: 2 })) {
      expect(painted.terrain[tile.y][tile.x], `${tile.x},${tile.y}`).toBe(
        painted.terrain[1][1],
      );
    }
    app.undo();
    expect(app.exportLevel().terrain).toEqual(normaliseLevel(JSON.parse(JSON.stringify(source))).terrain);
  });

  it('only cuts a cliff between orthogonal neighbours', () => {
    (host.querySelector('.btn.tool[data-arg="cliff"]') as HTMLElement).click();
    stroke(board, { x: 1, y: 1 }, { x: 3, y: 3 });
    expect(app.exportLevel().cliffs ?? []).toHaveLength(0);

    stroke(board, { x: 1, y: 1 }, { x: 2, y: 1 });
    expect(app.exportLevel().cliffs ?? []).toHaveLength(1);
  });

  it('adopts what it samples into the palette', () => {
    (host.querySelector('.btn.tool[data-arg="terrain"]') as HTMLElement).click();
    (host.querySelector('.swatch[data-arg="forest"]') as HTMLElement).click();
    stroke(board, { x: 2, y: 3 });

    (host.querySelector('.swatch[data-arg="plain"]') as HTMLElement).click();
    (host.querySelector('.btn.tool[data-arg="pick"]') as HTMLElement).click();
    stroke(board, { x: 2, y: 3 });

    expect(host.querySelector('.swatch.active')!.getAttribute('data-arg')).toBe('forest');
  });
});
