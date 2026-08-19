// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { createBattleEngine } from '@empire/battle-engine';
import { candidate01Level, CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';
import { CANDIDATE_01_ART } from '@empire/story-candidate-01/presentation';
import { createTestCatalog } from '@empire/test-content';
import { GameController } from '../game';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const TEST_ENGINE = createBattleEngine({ content: TEST_CATALOG });

const STYLES = join(import.meta.dirname, '..', '..', 'styles');
const stylesheets = () =>
  ['app.css', 'battle.css'].map((file) => readFileSync(join(STYLES, file), 'utf8')).join('\n');

const wheel = (el: Element, deltaY: number) =>
  el.dispatchEvent(new window.WheelEvent('wheel', { bubbles: true, cancelable: true, ctrlKey: true, deltaY }));

/**
 * Counts writes to an element's `style`, one per mutation.
 *
 * Deliberately per *record*: a `MutationObserver` callback fires once per
 * microtask however many mutations it carries, so counting invocations reports
 * "1" for twelve resizes — which is what the assertion was looking for, and it
 * passed with the batching taken away. The first version of both tests below was
 * theatre for exactly that reason.
 */
function countStyleWrites(el: Element): { writes: () => number; stop: () => void } {
  let seen = 0;
  const observer = new MutationObserver((records) => {
    seen += records.length;
  });
  observer.observe(el, { attributes: true, attributeFilter: ['style'] });
  return {
    writes: () => {
      seen += observer.takeRecords().length;
      return seen;
    },
    stop: () => observer.disconnect(),
  };
}

/** Lets the pending animation frame run, which is when a zoom is applied. */
const nextFrame = () => new Promise((resolve) => setTimeout(resolve, 0));

/** One applied scale is two attribute writes: the width and the height. */
const ONE_APPLICATION = 2;

describe('rescaling the field', () => {
  let host: HTMLElement;

  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) =>
      setTimeout(() => cb(performance.now()), 0) as unknown as number,
    );
    vi.stubGlobal('cancelAnimationFrame', (handle: number) => clearTimeout(handle));
    document.body.innerHTML = '<div id="app"></div>';
    host = document.getElementById('app')!;
  });

  const open = () => {
    const controller = new GameController(candidate01Level('c01-01'), () => {}, {
      engine: TEST_ENGINE,
      art: CANDIDATE_01_ART,
    });
    host.append(controller.root);
    return { controller, board: controller.root.querySelector('svg.board') as SVGSVGElement };
  };

  /**
   * A trackpad sends wheel events far faster than the screen refreshes, and each
   * one used to resize the board on the spot — so one flick asked the browser to
   * re-render a 5,000-node field a dozen times to show one result.
   */
  it('changes the scale at most once per frame, however fast the input arrives', async () => {
    const { controller, board } = open();
    const counted = countStyleWrites(board);

    for (let tick = 0; tick < 12; tick++) wheel(board, -120);
    expect(counted.writes()).toBe(0);
    await nextFrame();

    expect(counted.writes()).toBe(ONE_APPLICATION);
    counted.stop();
    controller.dispose();
  });

  /**
   * A field held against the zoom clamp still settles.
   *
   * The flatten is supposed to last a gesture. Every wheel event used to retrigger
   * the settle timer whether or not the scale had moved, so holding the wheel at
   * maximum zoom kept the board flattened until the player gave up.
   *
   * Asserted on the settling rather than on the DOM: the first version of this
   * counted style writes, and a sabotage run would not confirm it, because writing
   * a size the element already has is not a mutation and browsers compare before
   * invalidating anyway. The claim was wrong, so the test measures the effect that
   * is real instead.
   */
  it('settles even while the wheel keeps turning against the clamp', async () => {
    vi.useFakeTimers();
    const { controller, board } = open();
    for (let tick = 0; tick < 40; tick++) {
      wheel(board, -120);
      await vi.advanceTimersByTimeAsync(1);
    }
    const atCeiling = board.style.width;
    await vi.advanceTimersByTimeAsync(200);
    expect(board.classList.contains('is-rescaling')).toBe(false);

    // Still turning, still at the clamp: nothing moves and nothing flattens.
    for (let tick = 0; tick < 6; tick++) {
      wheel(board, -120);
      await vi.advanceTimersByTimeAsync(1);
      expect(board.classList.contains('is-rescaling')).toBe(false);
    }
    expect(board.style.width).toBe(atCeiling);
    controller.dispose();
    vi.useRealTimers();
  });

  it('says while it is rescaling, and stops saying it once it settles', async () => {
    vi.useFakeTimers();
    const { controller, board } = open();
    board.classList.remove('is-rescaling');

    controller.handledIntents; // the HUD is what offers the zoom controls
    (controller.root.querySelector('[data-act="zoom"]') as HTMLElement).click();
    await vi.advanceTimersByTimeAsync(1);
    expect(board.classList.contains('is-rescaling')).toBe(true);

    await vi.advanceTimersByTimeAsync(200);
    expect(board.classList.contains('is-rescaling')).toBe(false);
    controller.dispose();
    vi.useRealTimers();
  });

  /**
   * Nothing draws a filter while the scale is moving.
   *
   * This was a hand-written list of expensive classes, then a test that derived the
   * list from the stylesheets because the hand-written one had missed both colour
   * grades — the two most expensive things on the board. Both are gone: the rule is
   * universal, so it cannot go stale, and it cannot fail to know about a filter a
   * content pack invented in its own stylesheet.
   */
  it('suspends filters universally rather than by a list that can go stale', () => {
    const css = stylesheets();
    expect(css).toMatch(/\.board\.is-rescaling \*\s*\{[^}]*filter:\s*none\s*!important/);

    // And no shared stylesheet names a filter it would have had to list.
    const rules = [...css.matchAll(/([^{}]+)\{([^}]*)\}/g)];
    const named = rules
      .filter(([, selector]) => /is-rescaling/.test(selector))
      .flatMap(([, selector]) => [...selector.matchAll(/is-rescaling\s+\.([\w-]+)/g)].map(([, name]) => name));
    expect(named).toEqual([]);
  });
});
