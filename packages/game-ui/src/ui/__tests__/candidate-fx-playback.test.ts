// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { createBattleEngine } from '@empire/battle-engine';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '@empire/story-candidate-01';
import { CANDIDATE_01_ART } from '@empire/story-candidate-01/presentation';
import { createTestCatalog } from '@empire/test-content';
import { FrameClock } from '../../art/__tests__/frame-clock';
import { svgBoardSurface } from '../../art/svg-board-surface';
import { BoardView } from '../board';

const clock = new FrameClock();
beforeEach(() => clock.install());
afterEach(() => clock.restore());

/** Headless adapter/timing evidence, not browser texture or live combat acceptance. */
describe('Gray Flag registered hit phase playback', () => {
  it('plays the approved slash once at 12 fps, holds its fade, and removes it at 420 ms', async () => {
    const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
    const engine = createBattleEngine({ content });
    const state = engine.createState(CANDIDATE_01_LEVELS[0]);
    const before = JSON.stringify(state);
    const board = new BoardView({ content, grid: engine.rules.grids.get('square4'),
      art: CANDIDATE_01_ART, renderer: svgBoardSurface }, state, {
      onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {},
      onSecondary: () => {}, onScale: () => {},
    });
    try {
      const at = { x: state.units[0].x, y: state.units[0].y };
      const played = board.animateHit(at, 12, false, 'c01.border-blade');
      const effect = board.el.querySelector('.fx')!;
      const strip = effect.querySelector('svg')!;
      expect(effect.getAttribute('transform')).toBe(`translate(${(at.x * 32 + 16).toFixed(2)},${(at.y * 32 + 16).toFixed(2)})`);
      expect(strip.querySelector('image')!.getAttribute('href')).toContain('/runtime-hd/fx/fx-01.png');
      expect(strip.getAttribute('x')).toBe('-16');
      expect(strip.getAttribute('y')).toBe('-16');
      expect(effect.querySelectorAll('[data-part="burst"] circle')).toHaveLength(1);
      expect(effect.querySelector('[data-part="number"]')!.textContent).toBe('-12');
      for (const [ms, frame] of [[0, 0], [83.34, 1], [166.67, 2], [250, 3], [419, 3]]) {
        clock.at(ms);
        await Promise.resolve();
        expect(strip.getAttribute('viewBox')).toBe(`${frame * 32} 0 32 32`);
        expect(board.el.contains(effect)).toBe(true);
      }
      clock.at(420);
      await played;
      expect(board.el.querySelector('.fx')).toBeNull();
      expect(JSON.stringify(state)).toBe(before);
    } finally {
      board.dispose();
    }
  });
});


describe('Gray Flag healing effect lifetime', () => {
  it('holds the terminal heal phase through 499 ms and removes it at 500 ms, independently of hit timing', async () => {
    const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
    const engine = createBattleEngine({ content });
    const state = engine.createState(CANDIDATE_01_LEVELS[0]);
    const before = JSON.stringify(state);
    const board = new BoardView({ content, grid: engine.rules.grids.get('square4'),
      art: CANDIDATE_01_ART, renderer: svgBoardSurface }, state, {
      onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {},
      onSecondary: () => {}, onScale: () => {},
    });
    try {
      const at = { x: state.units[0].x, y: state.units[0].y };
      const played = board.animateHeal(at, 9);
      const effect = board.el.querySelector('.fx')!;
      const strip = effect.querySelector('svg')!;
      expect(strip.querySelector('image')!.getAttribute('href')).toContain('/runtime-hd/fx/fx-17.png');
      expect([strip.getAttribute('x'), strip.getAttribute('y')]).toEqual(['-16', '-16']);
      expect(effect.getAttribute('transform')).toBe(`translate(${(at.x * 32 + 16).toFixed(2)},${(at.y * 32 + 16).toFixed(2)})`);
      expect(effect.querySelector('[data-part="number"]')!.textContent).toBe('+9');
      for (const [ms, frame] of [[0, 0], [83.34, 1], [166.67, 2], [250, 3], [420, 3], [499, 3]]) {
        clock.at(ms);
        await Promise.resolve();
        expect(strip.getAttribute('viewBox')).toBe(`${frame * 32} 0 32 32`);
        expect(board.el.contains(effect)).toBe(true);
      }
      clock.at(500);
      await played;
      expect(board.el.querySelector('.fx')).toBeNull();
      expect(JSON.stringify(state)).toBe(before);
    } finally {
      board.dispose();
    }
  });
});
