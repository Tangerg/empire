// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest';
import { BattlefieldCamera } from '../battlefield-camera';

function fixture() {
  const viewport = document.createElement('div');
  const board = document.createElement('div');
  viewport.append(board);
  viewport.style.padding = '60px 120px 80px 100px';
  const size = { width: 800, height: 600, sceneWidth: 1440, sceneHeight: 960 };
  Object.defineProperties(viewport, {
    clientWidth: { get: () => size.width },
    clientHeight: { get: () => size.height },
    scrollWidth: { get: () => Math.max(size.width, size.sceneWidth + 220) },
    scrollHeight: { get: () => Math.max(size.height, size.sceneHeight + 140) },
  });
  viewport.scrollTo = vi.fn((options?: ScrollToOptions | number, _y?: number) => {
    if (typeof options !== 'object') throw new Error('expected camera scroll options');
    viewport.scrollLeft = options.left ?? viewport.scrollLeft;
    viewport.scrollTop = options.top ?? viewport.scrollTop;
  });
  viewport.getBoundingClientRect = () => ({ left: 0, top: 0 } as DOMRect);
  board.getBoundingClientRect = () => ({
    left: 100 + Math.max(0, (size.width - 220 - size.sceneWidth) / 2) - viewport.scrollLeft,
    top: 60 + Math.max(0, (size.height - 140 - size.sceneHeight) / 2) - viewport.scrollTop,
  } as DOMRect);
  const camera = new BattlefieldCamera(viewport, board);
  return { viewport, board, size, camera };
}

describe('scroll-owned battlefield camera', () => {
  it('centres a point in the unobscured area, accounting for unequal HUD bands', () => {
    const { viewport, camera } = fixture();
    camera.centerOn({ x: 500, y: 400 }, 'instant');
    expect(viewport.scrollLeft).toBe(210);
    expect(viewport.scrollTop).toBe(170);
    expect(camera.position).toEqual({ x: 500, y: 400 });
  });

  it('moves with a drag and clamps all four world edges', () => {
    const { viewport, camera } = fixture();
    camera.centerOn({ x: 500, y: 400 }, 'instant');
    camera.pan({ x: -100, y: -50 });
    expect(viewport.scrollLeft).toBe(310);
    expect(viewport.scrollTop).toBe(220);
    camera.pan({ x: -10000, y: -10000 });
    expect(viewport.scrollLeft).toBe(860);
    expect(viewport.scrollTop).toBe(500);
    camera.pan({ x: 10000, y: 10000 });
    expect(viewport.scrollLeft).toBe(0);
    expect(viewport.scrollTop).toBe(0);
  });

  it('keeps a camera position on resize unless the new bounds require clamping', () => {
    const { viewport, camera, size } = fixture();
    camera.centerOn({ x: 500, y: 400 }, 'instant');
    size.width = 900;
    camera.resize();
    expect(viewport.scrollLeft).toBe(210);
    expect(viewport.scrollTop).toBe(170);
    size.width = 1800;
    size.height = 1200;
    camera.resize();
    expect(viewport.scrollLeft).toBe(0);
    expect(viewport.scrollTop).toBe(0);
  });

  it('preserves the world centre when the board scale changes', () => {
    const { camera, size } = fixture();
    camera.centerOn({ x: 500, y: 400 }, 'instant');
    const before = camera.position;
    size.sceneWidth *= 1.2;
    size.sceneHeight *= 1.2;
    camera.centerOn({ x: before.x * 1.2, y: before.y * 1.2 }, 'instant');
    expect(camera.position).toEqual({ x: 600, y: 480 });
  });

  it('keeps a smaller board centred and refuses negative scroll ranges', () => {
    const { camera, size, viewport } = fixture();
    size.sceneWidth = 400;
    size.sceneHeight = 300;
    camera.pan({ x: -100, y: 100 });
    expect(viewport.scrollLeft).toBe(0);
    expect(viewport.scrollTop).toBe(0);
    expect(camera.position).toEqual({ x: 200, y: 150 });
  });

  it('reveals offscreen keyboard cells without moving for cells already visible', () => {
    const { camera, viewport } = fixture();
    camera.centerOn({ x: 500, y: 400 }, 'instant');
    const before = [viewport.scrollLeft, viewport.scrollTop];
    camera.reveal({ x: 520, y: 420 }, 20);
    expect([viewport.scrollLeft, viewport.scrollTop]).toEqual(before);
    camera.reveal({ x: 1000, y: 800 }, 20);
    expect(viewport.scrollLeft).toBe(440);
    expect(viewport.scrollTop).toBe(360);
    camera.reveal({ x: 100, y: 100 }, 20);
    expect(viewport.scrollLeft).toBe(80);
    expect(viewport.scrollTop).toBe(80);
  });
});
