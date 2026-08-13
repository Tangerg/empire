// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from 'vitest';
import { TILE } from '@empire/game-ui/art/terrain';
import { mapFromLevel, normaliseLevel, validateLevel } from '@empire/battle-engine/mapio';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires/levels';
import { Terrains } from '@empire/battle-engine/data/terrain';
import { UnitTypes } from '@empire/battle-engine/data/units';
import { EditorApp } from '../app';

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
    app = new EditorApp(level());
    app.mount(host);
    board = host.querySelector('svg.editor-board') as SVGSVGElement;
    stubLayout(board, BUILTIN_LEVELS[0].width * TILE, BUILTIN_LEVELS[0].height * TILE);
  });

  it('renders the palette, the board and the validation panel', () => {
    expect(host.querySelectorAll('.swatch').length).toBe(Terrains.all().length);
    expect(host.querySelectorAll('.unit-chip').length).toBe(UnitTypes.all().length);
    expect(host.querySelector('.swatch[data-arg="c01.scorched"]')).toBeTruthy();
    expect(host.querySelector('.unit-chip[data-arg="c01.laiya"]')).toBeTruthy();
    expect(board.querySelectorAll('.layer-terrain g[data-tile]').length).toBe(
      BUILTIN_LEVELS[0].width * BUILTIN_LEVELS[0].height,
    );
    expect(host.querySelector('.props')!.textContent).toContain('检查');
  });

  it('paints terrain with the brush and records it in the exported level', () => {
    (host.querySelector('.swatch[data-arg="mountain"]') as HTMLElement).click();
    stroke(board, { x: 5, y: 4 });
    const exported = app.exportLevel();
    expect(exported.terrain[4][5]).toBe('^');
    expect(validateLevel(exported).filter((i) => i.severity === 'error')).toEqual([]);
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
    const issues = validateLevel(app.exportLevel());
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
    const map = mapFromLevel(reloaded);
    expect(map.tiles[6 * map.width + 4]).toBe('forest');
    expect(validateLevel(reloaded).filter((i) => i.severity === 'error')).toEqual([]);
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
