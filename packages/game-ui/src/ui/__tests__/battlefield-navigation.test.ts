// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createBattleEngine } from '@empire/battle-engine';
import { ANCIENT_EMPIRES_LEVELS } from '@empire/content-ancient-empires';
import { createTestCatalog } from '@empire/test-content';
import { GameController } from '../game';

const content = createTestCatalog();
const engine = createBattleEngine({ content });
let controller: GameController | null = null;
afterEach(() => {
  controller?.dispose();
  controller = null;
  vi.unstubAllGlobals();
  document.body.innerHTML = '';
});

function fixture() {
  const frames = new Map<number, FrameRequestCallback>();
  let frameId = 0;
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
    frames.set(++frameId, callback);
    return frameId;
  });
  vi.stubGlobal('cancelAnimationFrame', (id: number) => frames.delete(id));
  let resize: ResizeObserverCallback = () => {};
  vi.stubGlobal('ResizeObserver', class {
    constructor(callback: ResizeObserverCallback) { resize = callback; }
    observe() {}
    disconnect() {}
  });
  const original = ANCIENT_EMPIRES_LEVELS[0];
  const level = {
    ...original, width: 36, height: 24,
    elevation: Array.from({ length: 36 * 24 }, () => 0),
    terrain: [
      ...original.terrain.map((row) => row.padEnd(36, '.')),
      ...Array.from({ length: 24 - original.height }, () => '.'.repeat(36)),
    ],
  };
  controller = new GameController(level, () => {}, { engine });
  document.body.append(controller.root);
  const viewport = controller.root.querySelector('.battlefield') as HTMLElement;
  const board = viewport.querySelector('.board') as SVGSVGElement;
  viewport.style.padding = '60px 120px 80px 100px';
  const size = { width: 800, height: 600 };
  const boardWidth = () => Number.parseFloat(board.style.width);
  const boardHeight = () => Number.parseFloat(board.style.height);
  Object.defineProperties(viewport, {
    clientWidth: { get: () => size.width }, clientHeight: { get: () => size.height },
    scrollWidth: { get: () => Math.max(size.width, boardWidth() + 220) },
    scrollHeight: { get: () => Math.max(size.height, boardHeight() + 140) },
  });
  viewport.scrollTo = vi.fn((options?: ScrollToOptions | number, _y?: number) => {
    if (typeof options !== 'object') throw new Error('expected camera scroll options');
    viewport.scrollLeft = options.left ?? viewport.scrollLeft;
    viewport.scrollTop = options.top ?? viewport.scrollTop;
  });
  viewport.getBoundingClientRect = () => ({ left: 0, top: 0 } as DOMRect);
  board.getBoundingClientRect = () => ({
    left: 100 - viewport.scrollLeft, top: 60 - viewport.scrollTop,
    width: boardWidth(), height: boardHeight(),
  } as DOMRect);
  const resized = () => resize([{ contentRect: { width: size.width - 220, height: size.height - 140 } } as ResizeObserverEntry], {} as ResizeObserver);
  const frame = () => {
    const callbacks = [...frames.values()];
    frames.clear();
    callbacks.forEach((callback) => callback(16));
  };
  const pointer = (type: string, x: number, y: number) => board.dispatchEvent(new MouseEvent(type, {
    bubbles: true, button: 0, clientX: x, clientY: y,
  }));
  return { viewport, board, size, resized, frame, pointer, boardWidth };
}

describe('battlefield navigation through GameController', () => {
  it('reveals the keyboard cursor beyond the visible area without selecting a unit', () => {
    const { viewport, board, resized, pointer } = fixture();
    resized();
    pointer('pointermove', 660, 300);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }));
    expect(viewport.scrollLeft).toBe(60);
    expect(board.querySelector('.unit.is-selected')).toBeNull();
  });

  it('opens an oversized world at 40px/cell, pans without selecting, and retains scale and position on resize', () => {
    const { viewport, board, size, resized, pointer, boardWidth } = fixture();
    resized();
    expect(boardWidth()).toBe(36 * 40);
    expect(board.querySelector('.unit.is-selected')).toBeNull();
    pointer('pointerdown', 200, 200);
    pointer('pointermove', 100, 100);
    pointer('pointerup', 100, 100);
    expect(viewport.scrollLeft).toBe(100);
    expect(viewport.scrollTop).toBe(100);
    expect(board.querySelector('.unit.is-selected')).toBeNull();
    size.width = 900;
    size.height = 700;
    resized();
    expect(boardWidth()).toBe(36 * 40);
    expect(viewport.scrollLeft).toBe(100);
    expect(viewport.scrollTop).toBe(100);
    pointer('pointerdown', 100, 100);
    pointer('pointerup', 100, 100);
    expect(board.querySelector('.unit.is-selected')).not.toBeNull();
  });

  it('keeps modifier-wheel zoom and its centre intact after panning, including a later resize', async () => {
    const { viewport, board, resized, frame, pointer, boardWidth } = fixture();
    resized();
    pointer('pointerdown', 200, 200);
    pointer('pointermove', 0, 0);
    pointer('pointerup', 0, 0);
    const before = { x: viewport.scrollLeft + 290, y: viewport.scrollTop + 230 };
    board.dispatchEvent(new WheelEvent('wheel', { ctrlKey: true, deltaY: -100, cancelable: true }));
    frame();
    await Promise.resolve();
    expect(boardWidth()).toBeCloseTo(36 * 32 * 1.35);
    expect(viewport.scrollLeft + 290).toBeCloseTo(before.x * 1.35 / 1.25);
    expect(viewport.scrollTop + 230).toBeCloseTo(before.y * 1.35 / 1.25);
    resized();
    expect(boardWidth()).toBeCloseTo(36 * 32 * 1.35);
  });
});
