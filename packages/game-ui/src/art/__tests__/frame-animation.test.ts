// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';
import {
  FrameAnimationSystem,
  registerSvgStrip,
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

  it('drives a self-describing SVG strip without knowing its asset topic', () => {
    const driver = new ManualDriver();
    const system = new FrameAnimationSystem(driver);
    const image = document.createElementNS('http://www.w3.org/2000/svg', 'image');
    image.setAttribute('data-frame-width', '32');
    image.setAttribute('data-frame-count', '4');
    image.setAttribute('data-frame-initial', '0');
    image.setAttribute('data-frame-clips', JSON.stringify([{ id: 'work', frames: [0, 2, 3], fps: 10 }]));

    registerSvgStrip(system, 'worker', image);
    system.play('worker', 'work');
    driver.advance(200);

    expect(image.getAttribute('x')).toBe('-96');
  });
});
