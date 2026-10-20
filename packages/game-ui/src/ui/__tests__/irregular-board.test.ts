// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest';
import { createBattleEngine } from '@empire/battle-engine';
import { ANCIENT_EMPIRES_LEVELS } from '@empire/content-ancient-empires';
import { createTestCatalog } from '@empire/test-content';
import { BoardView, emptyOverlay } from '../board';
import { GENERIC_ART } from '../../art/direction';
import { SvgBoardSurface } from '../../art/svg-board-surface';
import type { BoardLayer, BoardPiece } from '../../art/board-surface';

describe('terrain-owned irregular board silhouettes', () => {
  it('omits outside terrain, grid and tactical overlays, rejects its clicks, but still allows camera drag', () => {
    const content = createTestCatalog();
    const outside = { ...content.terrains.get('plain'), id: 'test.outside', tags: ['outside'] };
    content.terrains.register(outside);
    const engine = createBattleEngine({ content });
    const state = engine.createState(ANCIENT_EMPIRES_LEVELS[0]);
    state.map.tiles[0] = outside.id;
    const before = JSON.stringify(state);
    const layers = new Map<BoardLayer, readonly BoardPiece[]>();
    const handlers = {
      onTileClick: vi.fn(), onTileEnter: vi.fn(), onLeave: vi.fn(),
      onSecondary: vi.fn(), onScale: vi.fn(), onPan: vi.fn(),
    };
    const board = new BoardView({
      content, grid: engine.rules.grids.get('square4'), art: GENERIC_ART,
      renderer: (scene) => {
        const surface = new SvgBoardSurface(scene);
        const setLayer = surface.setLayer.bind(surface);
        surface.setLayer = (name, pieces) => { layers.set(name, pieces); setLayer(name, pieces); };
        return surface;
      },
    }, state, handlers);
    const overlay = emptyOverlay();
    overlay.move = new Set([0, 1]);
    overlay.visible = new Set([1]);
    overlay.incoming = new Map([[0, 1]]);
    overlay.selected = { x: 0, y: 0 };
    overlay.cursor = { x: 0, y: 0 };
    board.render(overlay);
    expect(board.contains({ x: 0, y: 0 })).toBe(false);
    expect(board.contains({ x: 1, y: 0 })).toBe(true);
    expect(board.contains({ x: -1, y: 0 })).toBe(false);
    for (const layer of ['terrain', 'grid', 'range'] as const) {
      expect(layers.get(layer)!.some((piece) => piece.x === 0 && piece.y === 0)).toBe(false);
    }
    expect(layers.get('cursor')).toEqual([]);
    const svg = board.el as SVGSVGElement;
    const { width, height } = svg.viewBox.baseVal;
    svg.getBoundingClientRect = () => ({ left: 0, top: 0, width, height } as DOMRect);
    const point = (type: string, x: number, y: number) => svg.dispatchEvent(
      new MouseEvent(type, { clientX: x, clientY: y, button: 0, bubbles: true }),
    );
    point('pointermove', 16, 16);
    point('pointerdown', 16, 16);
    point('pointerup', 16, 16);
    expect(handlers.onTileClick).not.toHaveBeenCalled();
    expect(handlers.onTileEnter).not.toHaveBeenCalled();
    point('pointerdown', 16, 16);
    point('pointermove', 26, 16);
    point('pointerup', 26, 16);
    expect(handlers.onPan).toHaveBeenCalledWith({ x: 10, y: 0 });
    expect(handlers.onTileClick).not.toHaveBeenCalled();
    point('pointerdown', 48, 16);
    point('pointerup', 48, 16);
    expect(handlers.onTileClick).toHaveBeenCalledWith({ x: 1, y: 0 });
    expect(JSON.stringify(state)).toBe(before);
    board.dispose();
  });
});
