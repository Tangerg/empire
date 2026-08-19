// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';
import {
  GroundBoardDecorations,
  SquareBoardDecorations,
  boardPiecesMarkup,
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
    const tint = { fill: '#abc', opacity: 0.25, stroke: '#def' };

    expect(SquareBoardDecorations.actionSpot(squareLayout, tint)).toContain('<rect');
    expect(SquareBoardDecorations.actionSpot(squareLayout, tint)).toContain('stroke-opacity="0.78"');
    expect(GroundBoardDecorations.actionSpot(squareLayout, tint)).toContain('candidate-action-spot');
    expect(SquareBoardDecorations.ring(squareLayout, 'cursor')).toContain('<rect');
    expect(GroundBoardDecorations.ring(squareLayout, 'cursor')).toContain('candidate-cursor-ring');

    expect(boardPiecesMarkup(SquareBoardDecorations.gridLines(squareLayout, map as never)))
      .toContain('<line');
    expect(boardPiecesMarkup(GroundBoardDecorations.gridLines(squareLayout, map as never)))
      .toContain('<circle');

    const path = [{ x: 0, y: 0 }, { x: 1, y: 0 }];
    expect(SquareBoardDecorations.movePath(squareLayout, path)).toMatch(/^M\d/);
    expect(GroundBoardDecorations.movePath(squareLayout, path)).toMatch(/^M\d/);
  });

  /**
   * The point of `BoardPiece`: a decoration is a picture, and the cell is where it
   * goes.
   *
   * Every one of these used to bake the cell's scene coordinates into its own
   * markup, so a field of N identical stand nodes was N different strings and a
   * fog of war over a large map was thousands of them. `tools/board-scale.ts`
   * measures what that costs a renderer that wants to cache anything.
   */
  it('draws one picture at many places rather than many pictures', () => {
    const nodes = GroundBoardDecorations.gridLines(squareLayout, map as never);
    expect(nodes).toHaveLength(map.width * map.height);
    expect(new Set(nodes.map((piece) => piece.markup)).size).toBe(1);
    expect(nodes.map((piece) => `${piece.x},${piece.y}`)).toEqual(['0,0', '32,0', '0,32', '32,32']);

    // And the same for a tint: one shape, whatever it is spread over.
    const tint = { fill: '#0b1020', opacity: 0.55 };
    expect(SquareBoardDecorations.actionSpot(squareLayout, tint))
      .toBe(SquareBoardDecorations.actionSpot(squareLayout, tint));
    expect(SquareBoardDecorations.actionSpot(squareLayout, tint)).not.toContain('translate');
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
      shape: () => '0,0 10,0 15,9 10,18 0,18 -5,9',
      neighbour: (at) => ({ x: at.x * 32 + 48, y: at.y * 28 + 18 }),
    };
    const tint = { fill: '#abc', opacity: 0.2 };

    const lattice = boardPiecesMarkup(SquareBoardDecorations.gridLines(hex, map as never));
    expect(lattice).toContain('<polygon');
    expect(lattice).not.toContain('<line');
    expect(SquareBoardDecorations.actionSpot(hex, tint)).toContain('<polygon');
    expect(SquareBoardDecorations.ring(hex, 'selection')).toContain('<polygon');
    // The painted look needs no second version because it never looked at the
    // cell's shape — and now that it does not look at the cell's place either,
    // the two tilings get the same picture.
    expect(GroundBoardDecorations.actionSpot(hex, tint))
      .toBe(GroundBoardDecorations.actionSpot(squareLayout, tint));
  });
});
