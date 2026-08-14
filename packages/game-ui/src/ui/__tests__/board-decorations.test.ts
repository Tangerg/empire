// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';
import {
  GroundBoardDecorations,
  SquareBoardDecorations,
  decorationsFor,
  squareLayout,
  type BoardLayout,
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
    expect(decorationsFor(mixed).id).toBe('test.hex');
    expect(decorationsFor(mixed).shapeRendering).toBe('auto');
  });

  it('draws each decoration where the tiling puts the cell', () => {
    const cell = { x: 1, y: 2, fill: '#abc', opacity: 0.25, stroke: '#def' };

    expect(SquareBoardDecorations.actionSpot(squareLayout, cell)).toContain('<rect');
    expect(SquareBoardDecorations.actionSpot(squareLayout, cell)).toContain('stroke-opacity="0.78"');
    expect(GroundBoardDecorations.actionSpot(squareLayout, cell)).toContain('candidate-action-spot');
    expect(SquareBoardDecorations.ring(squareLayout, { x: 0, y: 0 }, 'cursor')).toContain('<rect');
    expect(GroundBoardDecorations.ring(squareLayout, { x: 0, y: 0 }, 'cursor'))
      .toContain('candidate-cursor-ring');

    expect(SquareBoardDecorations.gridLines(squareLayout, map as never)).toContain('<line');
    expect(GroundBoardDecorations.gridLines(squareLayout, map as never))
      .toContain('candidate-stand-node');

    const path = [{ x: 0, y: 0 }, { x: 1, y: 0 }];
    expect(SquareBoardDecorations.movePath(squareLayout, path)).toMatch(/^M\d/);
    expect(GroundBoardDecorations.movePath(squareLayout, path)).toMatch(/^M\d/);
  });

  /**
   * A cell that is not a square cannot be decorated with a square.
   *
   * The ruled look draws the lattice as two families of lines, which is only a
   * lattice for four-cornered cells; a six-cornered one draws its own edges.
   */
  it('draws a six-cornered cell as its own outline', () => {
    const hex: BoardLayout = {
      tileSize: 32,
      corners: 6,
      origin: (at) => ({ x: at.x * 32, y: at.y * 28 }),
      center: (at) => ({ x: at.x * 32 + 16, y: at.y * 28 + 18 }),
      outline: () => '0,0 10,0 15,9 10,18 0,18 -5,9',
    };

    expect(SquareBoardDecorations.gridLines(hex, map as never)).toContain('<polygon');
    expect(SquareBoardDecorations.gridLines(hex, map as never)).not.toContain('<line');
    expect(SquareBoardDecorations.actionSpot(hex, { x: 1, y: 1, fill: '#abc', opacity: 0.2 }))
      .toContain('<polygon');
    expect(SquareBoardDecorations.ring(hex, { x: 1, y: 1 }, 'selection')).toContain('<polygon');
    // The painted look is centre-based already, so it needs no second version.
    expect(GroundBoardDecorations.actionSpot(hex, { x: 1, y: 1, fill: '#abc', opacity: 0.2 }))
      .toContain('cx="48"');
  });
});
