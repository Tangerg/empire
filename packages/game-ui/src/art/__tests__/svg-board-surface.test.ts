// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { SvgBoardSurface } from '../svg-board-surface';
import { runtimeFrameStripMarkup } from '../runtime-raster';
import type { BoardSurfaceScene } from '../board-surface';

/**
 * What the surface owns: the lifetime and the motion of everything it draws.
 *
 * The board used to clean up after it in four places — unregistering sprite strips
 * it had never registered, using an animation id it reconstructed from a format
 * written in both files. A drawing that outlives its element is not a visible bug;
 * it is a frame loop that never stops, spinning on a node no longer in the tree.
 * That is what these observe.
 */

const SCENE: BoardSurfaceScene = {
  width: 320,
  height: 320,
  originX: 0,
  originY: 0,
  shapeRendering: 'crispEdges',
  backdrop: '',
  foreground: '',
};

/** A sprite that never finishes: four frames, looping, so the timeline keeps asking. */
const WALKING = runtimeFrameStripMarkup({
  href: 'sprite.png',
  frameWidth: 32,
  frameHeight: 32,
  frameCount: 4,
  clips: [{ id: 'walk', frames: [0, 1, 2, 3], fps: 8, loop: true }],
});

let pending: Array<(time: number) => void> = [];
let requests = 0;
let realRequest: typeof requestAnimationFrame;
let realCancel: typeof cancelAnimationFrame;

/** Runs whatever the timeline asked for, and counts what it asks for next. */
function pump(frames: number): number {
  requests = 0;
  for (let i = 0; i < frames; i++) {
    const due = pending;
    pending = [];
    for (const callback of due) callback(performance.now() + i * 16);
  }
  return requests;
}

beforeEach(() => {
  realRequest = globalThis.requestAnimationFrame;
  realCancel = globalThis.cancelAnimationFrame;
  pending = [];
  requests = 0;
  globalThis.requestAnimationFrame = ((callback: (time: number) => void) => {
    requests++;
    pending.push(callback);
    return requests;
  }) as typeof requestAnimationFrame;
  globalThis.cancelAnimationFrame = (() => {}) as typeof cancelAnimationFrame;
});

afterEach(() => {
  globalThis.requestAnimationFrame = realRequest;
  globalThis.cancelAnimationFrame = realCancel;
});

describe('the surface owns what it draws', () => {
  /**
   * Asking and committing are different acts.
   *
   * Fetching a unit's drawing used to be `unit(id, () => '')` — the call that
   * *makes* one, with a factory that would have drawn an empty unit had the id not
   * been there. Five call sites, each kept honest by a preceding `hasUnit`.
   */
  it('never makes a drawing for a unit that is only asked about', () => {
    const surface = new SvgBoardSurface(SCENE);

    expect(surface.drawnUnit(7)).toBeNull();
    expect(surface.drawnUnits()).toEqual([]);
    expect(surface.element.querySelectorAll('.unit')).toHaveLength(0);

    const made = surface.unit(7, () => '<rect width="32" height="32"/>');
    expect(made.fresh).toBe(true);
    expect(surface.drawnUnit(7)?.fresh).toBe(false);
    expect(surface.drawnUnits()).toEqual([7]);
    surface.dispose();
  });

  it('stops a unit\'s sprite when the unit leaves the board', () => {
    const surface = new SvgBoardSurface(SCENE);
    const walker = surface.unit(3, () => WALKING);
    walker.play('walk');
    expect(pump(2)).toBeGreaterThan(0);

    surface.removeUnit(3);

    expect(surface.drawnUnit(3)).toBeNull();
    expect(surface.element.querySelectorAll('.unit')).toHaveLength(0);
    // Nothing left asking for frames. A clip left registered on a detached element
    // keeps the loop alive forever, which is the leak `dropUnit` could not prevent
    // because it only forgot the entry.
    expect(pump(3)).toBe(0);
    surface.dispose();
  });

  it('stops what a layer was playing when the layer is replaced', () => {
    const surface = new SvgBoardSurface(SCENE);
    // A layer's own strips start themselves, so this is running from here.
    surface.setLayer('scenery', [{ markup: WALKING, x: 0, y: 0 }]);
    expect(pump(2)).toBeGreaterThan(0);

    surface.setLayer('scenery', []);

    expect(surface.element.querySelectorAll('.layer-scenery .runtime-frame-strip')).toHaveLength(0);
    expect(pump(3)).toBe(0);
    surface.dispose();
  });

  /**
   * A drawing swells about its own middle, and the two kinds have different ones.
   *
   * `swell` was specified as "about the middle of a tile", which is a unit-shaped
   * assumption: a unit fills a tile and is placed at the cell's origin, but an
   * effect is placed at the cell's *centre*, so its origin already is its middle.
   * With one pivot for both, the white burst of a hit drifted 13.3px up and left
   * over the 420ms it took to grow.
   */
  it('swells an effect about its origin and a unit about the tile middle', () => {
    const surface = new SvgBoardSurface(SCENE);

    const effect = surface.effect({ body: '', parts: [{ role: 'burst', markup: '<circle r="12"/>' }] });
    effect.place(112, 80);
    effect.part('burst')!.swell(1.8);
    expect(surface.element.querySelector('[data-part="burst"]')!.getAttribute('transform'))
      .toBe('translate(0.00,0.00) scale(1.8000)');

    const unit = surface.unit(1, () => '<rect width="32" height="32"/>');
    unit.drawing.place(64, 32);
    unit.drawing.swell(0.6);
    expect(surface.element.querySelector('.unit')!.getAttribute('transform'))
      .toBe('translate(64.00,32.00) translate(16,16) scale(0.6000) translate(-16,-16)');

    surface.dispose();
  });

  /**
   * A part is declared, not embedded in a string for the renderer to find again.
   *
   * The board wrote `<g data-part="burst">…</g>` into one markup string and the
   * surface pulled it back out with `querySelector`. A backend that bakes markup
   * into a texture cannot look inside one.
   */
  it('keeps a picture\'s parts addressable and in the order they were given', () => {
    const surface = new SvgBoardSurface(SCENE);
    const effect = surface.effect({
      body: '<rect class="flash" width="8" height="8"/>',
      parts: [
        { role: 'burst', markup: '<circle r="12"/>' },
        { role: 'number', markup: '<text>-7</text>' },
      ],
    });

    expect(effect.part('burst')).not.toBeNull();
    expect(effect.part('number')).not.toBeNull();
    expect(effect.part('band')).toBeNull();
    // Body first, then the parts in the order given: that is the depth order.
    const fx = surface.element.querySelector('.fx')!;
    expect([...fx.children].map((child) => child.getAttribute('data-part') ?? 'body'))
      .toEqual(['body', 'burst', 'number']);
    surface.dispose();
  });

  it('stops what an effect was playing when the effect is removed', () => {
    const surface = new SvgBoardSurface(SCENE);
    const effect = surface.effect({ body: WALKING });
    expect(pump(2)).toBeGreaterThan(0);

    effect.remove();

    expect(surface.element.querySelectorAll('.layer-effects .fx')).toHaveLength(0);
    expect(pump(3)).toBe(0);
    surface.dispose();
  });
});
