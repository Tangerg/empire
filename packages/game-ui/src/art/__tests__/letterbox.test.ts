// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest';
import { listenForPointer } from '../letterbox';

function fixture() {
  const element = document.createElement('div');
  const box = { left: 100, top: 50, width: 640, height: 320 };
  element.getBoundingClientRect = () => ({ ...box, right: box.left + box.width, bottom: box.top + box.height,
    x: box.left, y: box.top, toJSON: () => ({}) });
  const pointer = { press: vi.fn(), move: vi.fn(), leave: vi.fn(), scale: vi.fn(), pan: vi.fn() };
  const dispose = listenForPointer(element, { width: 320, height: 160 }, pointer);
  const send = (type: string, x = 140, y = 90, button = 0, pointerId = 1, isPrimary = true) => {
    const event = new MouseEvent(type, { clientX: x, clientY: y, button, bubbles: true, cancelable: true });
    Object.defineProperty(event, 'pointerId', { value: pointerId });
    Object.defineProperty(event, 'isPrimary', { value: isPrimary });
    element.dispatchEvent(event);
    return event;
  };
  return { element, pointer, dispose, send, box };
}

describe('shared battlefield pointer gestures', () => {
  it('waits for release before committing a primary click in scene coordinates', () => {
    const { pointer, send, dispose } = fixture();
    send('pointerdown');
    expect(pointer.press).not.toHaveBeenCalled();
    send('pointerup');
    expect(pointer.press).toHaveBeenCalledTimes(1);
    expect(pointer.press).toHaveBeenCalledWith({ x: 20, y: 20 }, 0);
    expect(pointer.pan).not.toHaveBeenCalled();
    dispose();
  });

  it('tolerates a small click tremor but turns a 6px movement into a pan', () => {
    const { pointer, send, dispose } = fixture();
    send('pointerdown');
    send('pointermove', 143, 92);
    send('pointerup', 143, 92);
    expect(pointer.press).toHaveBeenCalledTimes(1);
    pointer.press.mockClear();
    send('pointerdown');
    send('pointermove', 146, 90);
    send('pointerup', 146, 90);
    expect(pointer.press).not.toHaveBeenCalled();
    expect(pointer.pan).toHaveBeenCalledWith({ x: 6, y: 0 });
    dispose();
  });

  it('pans in screen pixels at any zoom and never selects after dragging back', () => {
    const { element, pointer, send, dispose } = fixture();
    send('pointerdown');
    send('pointermove', 180, 110);
    expect(pointer.pan).toHaveBeenLastCalledWith({ x: 40, y: 20 });
    expect(element.classList.contains('is-panning')).toBe(true);
    expect(pointer.leave).toHaveBeenCalledTimes(1);
    send('pointerleave');
    send('pointermove', 140, 90);
    send('pointerup');
    expect(pointer.pan).toHaveBeenCalledWith({ x: -40, y: -20 });
    expect(pointer.press).not.toHaveBeenCalled();
    expect(pointer.move).not.toHaveBeenCalled();
    expect(element.classList.contains('is-panning')).toBe(false);
    dispose();
  });

  it('also detects a drag when only the release position changed', () => {
    const { pointer, send, dispose } = fixture();
    send('pointerdown');
    send('pointerup', 180, 90);
    expect(pointer.pan).toHaveBeenCalledWith({ x: 40, y: 0 });
    expect(pointer.press).not.toHaveBeenCalled();
    dispose();
  });

  it('supports middle-button panning and preserves immediate right-click cancellation', () => {
    const { pointer, send, dispose } = fixture();
    send('pointerdown', 140, 90, 1);
    send('pointermove', 160, 90, 1);
    send('pointerup', 160, 90, 1);
    expect(pointer.pan).toHaveBeenCalledWith({ x: 20, y: 0 });
    expect(pointer.press).not.toHaveBeenCalled();
    send('pointerdown', 140, 90, 2);
    expect(pointer.press).toHaveBeenCalledTimes(1);
    expect(pointer.press).toHaveBeenCalledWith({ x: 20, y: 20 }, 2);
    dispose();
  });

  it.each(['pointercancel', 'lostpointercapture'])('cancels %s without a click, then accepts a fresh gesture', (type) => {
    const { pointer, send, dispose } = fixture();
    send('pointerdown');
    send(type);
    send('pointerup');
    expect(pointer.press).not.toHaveBeenCalled();
    send('pointerdown');
    send('pointerup');
    expect(pointer.press).toHaveBeenCalledTimes(1);
    dispose();
  });

  it('ignores another pointer while a gesture owns the board', () => {
    const { pointer, send, dispose } = fixture();
    send('pointerdown');
    send('pointerdown', 180, 90, 0, 2);
    send('pointermove', 240, 90, 0, 2);
    send('pointerup', 240, 90, 0, 2);
    expect(pointer.pan).not.toHaveBeenCalled();
    expect(pointer.press).not.toHaveBeenCalled();
    send('pointerup');
    expect(pointer.press).toHaveBeenCalledTimes(1);
    expect(pointer.press).toHaveBeenCalledWith({ x: 20, y: 20 }, 0);
    dispose();
  });

  it('ignores secondary touches even before a primary gesture starts', () => {
    const { pointer, send, dispose } = fixture();
    send('pointerdown', 140, 90, 0, 2, false);
    send('pointerup', 140, 90, 0, 2, false);
    expect(pointer.press).not.toHaveBeenCalled();
    expect(pointer.pan).not.toHaveBeenCalled();
    dispose();
  });

  it('uses the current board position for the next click after a pan', () => {
    const { pointer, send, box, dispose } = fixture();
    pointer.pan.mockImplementation(({ x, y }) => { box.left += x; box.top += y; });
    send('pointerdown');
    send('pointermove', 160, 100);
    send('pointerup', 160, 100);
    send('pointerdown', 180, 110);
    send('pointerup', 180, 110);
    expect(pointer.press).toHaveBeenCalledTimes(1);
    expect(pointer.press).toHaveBeenCalledWith({ x: 30, y: 25 }, 0);
    dispose();
  });

  it('keeps ordinary wheel scrolling native and reserves modifier-wheel for zoom', () => {
    const { element, pointer, dispose } = fixture();
    const wheel = (options: WheelEventInit) => {
      const event = new WheelEvent('wheel', { deltaY: 100, cancelable: true, ...options });
      element.dispatchEvent(event);
      return event;
    };
    expect(wheel({ deltaX: 20 }).defaultPrevented).toBe(false);
    expect(pointer.scale).not.toHaveBeenCalled();
    expect(wheel({ ctrlKey: true }).defaultPrevented).toBe(true);
    expect(pointer.scale).toHaveBeenLastCalledWith(-1);
    expect(wheel({ metaKey: true, deltaY: -100 }).defaultPrevented).toBe(true);
    expect(pointer.scale).toHaveBeenLastCalledWith(1);
    dispose();
  });

  it('removes every listener and active drag when a surface is disposed', () => {
    const { element, pointer, send, dispose } = fixture();
    send('pointerdown');
    send('pointermove', 160);
    dispose();
    Object.values(pointer).forEach((callback) => callback.mockClear());
    send('pointerup', 160);
    send('pointerdown');
    send('pointermove', 180);
    send('pointerup', 180);
    element.dispatchEvent(new WheelEvent('wheel', { ctrlKey: true, deltaY: 1 }));
    expect(element.classList.contains('is-panning')).toBe(false);
    Object.values(pointer).forEach((callback) => expect(callback).not.toHaveBeenCalled());
  });
});
