// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { CANDIDATE_01_ART } from '@empire/story-candidate-01/presentation';
import { FrameClock } from '../../art/__tests__/frame-clock';
import { SvgBoardSurface } from '../../art/svg-board-surface';
import type { BoardSurfaceScene } from '../../art/board-surface';

const clock = new FrameClock();
const topics = ['C01-FX-09', 'C01-FX-10', 'C01-FX-11', 'C01-FX-12'];
const scene: BoardSurfaceScene = {
  width: 64, height: 64, originX: 0, originY: 0,
  shapeRendering: 'crispEdges', backdrop: '', foreground: '',
};
beforeEach(() => clock.install());
afterEach(() => clock.restore());

/** Adapter/renderer contract only: these catalog topics have no authored scene binding. */
describe('candidate ambient FX keep cyclic playback and consumer-owned lifetime', () => {
  it.each(topics)('%s wraps frame 4 to frame 1 and survives the hit lifetime', topic => {
    const surface = new SvgBoardSurface(scene);
    try {
      const picture = CANDIDATE_01_ART.presentation.effect(topic)!;
      expect(picture.strip!.clips).toEqual([
        { id: 'effect', frames: [0, 1, 2, 3], fps: 12, loop: true },
      ]);
      const drawing = surface.effect(picture);
      drawing.place(32, 32);
      const effect = surface.element.querySelector('.fx')!;
      const strip = effect.querySelector('.board-strip')!;
      expect(effect.getAttribute('transform')).toBe('translate(32.00,32.00)');
      expect(strip.getAttribute('x')).toBe('-16');
      expect(strip.getAttribute('y')).toBe('-16');
      expect(strip.getAttribute('width')).toBe('32');
      expect(strip.getAttribute('height')).toBe('32');
      expect(strip.querySelector('image')!.getAttribute('href')).toContain(`/runtime-hd/fx/fx-${topic.slice(-2)}.png`);
      expect(effect.querySelector('mask, clipPath, filter, [data-part]')).toBeNull();
      for (const [ms, frame] of [[0, 0], [83.34, 1], [166.67, 2], [250, 3],
        [333.32, 3], [333.34, 0], [416.67, 1], [420, 1], [666.66, 3],
        [666.67, 0], [999.99, 3], [1000.01, 0], [1500.01, 2], [5000.01, 0]]) {
        clock.at(ms);
        expect(strip.getAttribute('viewBox'), `${topic} at ${ms}ms`).toBe(`${frame * 32} 0 32 32`);
        expect(surface.element.contains(effect)).toBe(true);
      }
      drawing.remove();
      expect(surface.element.querySelector('.fx')).toBeNull();
      expect(clock.pump(3)).toBe(0);
    } finally {
      surface.dispose();
    }
  });

  it.each(topics)('%s starts a new instance at frame 1 after the owner removes the old one', topic => {
    const surface = new SvgBoardSurface(scene);
    try {
      const drawing = surface.effect(CANDIDATE_01_ART.presentation.effect(topic)!);
      clock.at(250);
      expect(surface.element.querySelector('.board-strip')!.getAttribute('viewBox')).toBe('96 0 32 32');
      drawing.remove();
      expect(clock.pump(3)).toBe(0);
      surface.effect(CANDIDATE_01_ART.presentation.effect(topic)!);
      expect(surface.element.querySelector('.board-strip')!.getAttribute('viewBox')).toBe('0 0 32 32');
      surface.dispose();
      expect(clock.pump(3)).toBe(0);
    } finally {
      surface.dispose();
    }
  });
});
