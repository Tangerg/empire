// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';
import {
  FrameAnimationSystem,
  type FrameAnimationDriver,
} from '../frame-animation';

class ManualDriver implements FrameAnimationDriver {
  time = 0;
  callback: FrameRequestCallback | null = null;

  now(): number {
    return this.time;
  }

  request(callback: FrameRequestCallback): number {
    this.callback = callback;
    return 1;
  }

  cancel(): void {
    this.callback = null;
  }

  advance(ms: number): void {
    this.time += ms;
    const callback = this.callback;
    this.callback = null;
    callback?.(this.time);
  }
}

describe('frame animation system', () => {
  it('shares one clock across looping and one-shot clips', () => {
    const driver = new ManualDriver();
    const rendered: number[] = [];
    const system = new FrameAnimationSystem(driver);
    system.register(
      'actor',
      { frameCount: 4, setFrame: (frame) => rendered.push(frame) },
      [
        { id: 'walk', frames: [1, 3], fps: 10, loop: true },
        { id: 'strike', frames: [0, 2], fps: 10 },
      ],
    );

    system.play('actor', 'walk');
    driver.advance(100);
    driver.advance(100);
    system.play('actor', 'strike');
    driver.advance(100);
    driver.advance(100);

    expect(rendered).toEqual([0, 1, 3, 1, 0, 2]);
    expect(driver.callback).toBeNull();
  });

  /*
   * There was a test here for `registerSvgStrip`, which read a strip's description
   * out of `data-frame-*` attributes on an `<image>` and shifted the element's `x`.
   * Both the reader and the attributes are gone: a strip is declared, and each
   * backend registers a target that advances the frame however it draws one. The
   * two of them are held to the same frame in `two-backends-agree.test.ts`.
   */
});
