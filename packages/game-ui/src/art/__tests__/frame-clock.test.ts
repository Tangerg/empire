// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest';
import { FrameAnimationSystem } from '../frame-animation';
import { FrameClock } from './frame-clock';

afterEach(() => vi.unstubAllGlobals());

describe('the test frame clock', () => {
  it('keeps animation phase independent of host delays before play and before RAF', () => {
    let hostTime = 1000;
    vi.stubGlobal('performance', { now: () => hostTime });
    for (const hostDelay of [0, 100_000]) {
      const clock = new FrameClock();
      const frames = new FrameAnimationSystem();
      let frame = -1;
      clock.install();
      try {
        hostTime += hostDelay;
        frames.register('walker', { frameCount: 4, setFrame: value => { frame = value; } },
          [{ id: 'walk', frames: [0, 1, 2, 3], fps: 8, loop: true }]);
        frames.play('walker', 'walk');
        hostTime += hostDelay;
        clock.at(200);
        expect(frame).toBe(1);
        clock.at(500);
        expect(frame).toBe(0);
      } finally {
        frames.dispose();
        clock.restore();
      }
      expect(performance.now()).toBe(hostTime);
    }
  });

  it('gives now() and RAF exactly the same advancing time and restores the host functions', () => {
    const originalNow = performance.now;
    const originalRequest = requestAnimationFrame;
    const originalCancel = cancelAnimationFrame;
    const clock = new FrameClock();
    const times: number[][] = [];
    clock.install();
    try {
      const tick = (time: number) => {
        times.push([time, performance.now()]);
        if (times.length < 3) requestAnimationFrame(tick);
      };
      requestAnimationFrame(tick);
      clock.at(200);
      clock.pump(2);
      expect(times).toEqual([[200, 200], [216, 216], [232, 232]]);
    } finally {
      clock.restore();
    }
    expect(performance.now).toBe(originalNow);
    expect(requestAnimationFrame).toBe(originalRequest);
    expect(cancelAnimationFrame).toBe(originalCancel);
  });
});
