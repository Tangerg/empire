// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';
import {
  GroundBoardDecorations,
  SquareBoardDecorations,
  decorationsFor,
  registerBattlePresentation,
  type BattlePresentation,
} from '../../index';

/**
 * The board used to ask the presentation for its *name* in six places to choose
 * between squares and ground-level ellipses. A strategy asked for its name is
 * not a strategy: a third look was unreachable, and so was any mixture.
 */

const map = { width: 2, height: 2, tiles: [], owners: [], captureProgress: [], elevation: [], cliffs: [], directionalCover: [] };

describe('how a board draws its tactical layer', () => {
  it('keeps the grid for art that states no preference', () => {
    const plain: BattlePresentation = {
      id: 'test.plain',
      matches: () => false,
      sceneProfile: () => ({}),
      sceneFrame: () => ({ backdrop: '', foreground: '' }),
      sceneLayers: () => ({ ground: '', underUnits: '', overUnits: '' }),
      structure: () => null,
      marker: () => '',
      weaponFx: () => null,
      effect: () => '',
    };

    // The safe default is the look that works over anything, which is also what
    // an unrecognised presentation used to *not* get.
    expect(decorationsFor(plain)).toBe(SquareBoardDecorations);
    expect(decorationsFor({ ...plain, decorations: GroundBoardDecorations }))
      .toBe(GroundBoardDecorations);
  });

  it('lets a third look mix painted scenery with ruled tiles', () => {
    const mixed: BattlePresentation = {
      id: 'test.mixed',
      decorations: { ...SquareBoardDecorations, id: 'test.hex', shapeRendering: 'auto' },
      matches: () => false,
      sceneProfile: () => ({}),
      sceneFrame: () => ({ backdrop: '<rect/>', foreground: '' }),
      sceneLayers: () => ({ ground: '<g/>', underUnits: '', overUnits: '' }),
      structure: () => null,
      marker: () => '',
      weaponFx: () => null,
      effect: () => '',
    };
    const release = registerBattlePresentation(mixed);

    expect(decorationsFor(mixed).id).toBe('test.hex');
    expect(decorationsFor(mixed).shapeRendering).toBe('auto');
    release();
  });

  it('draws each decoration from tile coordinates, both ways', () => {
    const cell = { x: 1, y: 2, fill: '#abc', opacity: 0.25, stroke: '#def' };

    expect(SquareBoardDecorations.actionSpot(cell)).toContain('<rect');
    expect(SquareBoardDecorations.actionSpot(cell)).toContain('stroke-opacity="0.78"');
    expect(GroundBoardDecorations.actionSpot(cell)).toContain('candidate-action-spot');
    expect(SquareBoardDecorations.ring({ x: 0, y: 0 }, 'cursor')).toContain('<rect');
    expect(GroundBoardDecorations.ring({ x: 0, y: 0 }, 'cursor')).toContain('candidate-cursor-ring');

    expect(SquareBoardDecorations.gridLines(map as never)).toContain('<line');
    expect(GroundBoardDecorations.gridLines(map as never)).toContain('candidate-stand-node');

    const path = [{ x: 0, y: 0 }, { x: 1, y: 0 }];
    expect(SquareBoardDecorations.movePath(path)).toMatch(/^M\d/);
    expect(GroundBoardDecorations.movePath(path)).toMatch(/^M\d/);
  });
});
