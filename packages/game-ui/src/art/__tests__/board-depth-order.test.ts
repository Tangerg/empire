import { describe, expect, it } from 'vitest';
import { BoardDepthOrder, boardScenePieces, CELL_FOOT, type BoardPiece } from '../board-surface';

const piece = (markup: string, x: number, y: number, foot?: { x: number; y: number }): BoardPiece =>
  ({ markup, x, y, ...(foot ? { foot } : {}) });

describe('standing scene depth', () => {
  it('uses contact points across layers rather than image origins or source order', () => {
    const building = piece('building', 40, 10, CELL_FOOT);
    const tree = piece('tree', 56, 42, { x: 0, y: 0 });
    const actor = piece('actor', 40, 12, CELL_FOOT);
    const farProp = piece('far-prop', 56, 39, { x: 0, y: 0 });
    const flat = piece('bridge', 0, 1000);
    const light = piece('light', 0, 0);
    const range = piece('range', 0, 0);
    const cursor = piece('cursor', 0, 0);
    expect(boardScenePieces({ terrain: [building], scenery: [tree, farProp, flat, light], units: [actor],
      range: [range], cursor: [cursor] }).map(({ markup }) => markup))
      .toEqual(['bridge', 'light', 'range', 'far-prop', 'building', 'tree', 'actor', 'cursor']);
  });

  it('keeps unannotated pieces in their source layer and source order', () => {
    const layers = { terrain: [piece('t1', 90, 90), piece('t2', 0, 0)],
      scenery: [piece('s1', 2, 2), piece('s2', 1, 1)] };
    expect(boardScenePieces(layers)).toEqual([...layers.terrain, ...layers.scenery]);
  });

  it('breaks equal foot ties by source layer/index and actor id, independent of update order', () => {
    const order = new BoardDepthOrder<string>();
    order.placeUnit(9, 'unit9', 0, 0);
    order.placeUnit(2, 'unit2', 0, 0);
    order.replaceLayer('scenery', [{ drawing: 'scenery', piece: piece('', 16, 31, { x: 0, y: 0 }) }]);
    order.replaceLayer('terrain', [{ drawing: 't1', piece: piece('', 0, 0, CELL_FOOT) },
      { drawing: 't2', piece: piece('', 0, 0, CELL_FOOT) }]);
    expect(order.ordered('units')).toEqual(['t1', 't2', 'scenery', 'unit2', 'unit9']);
    order.placeUnit(2, 'unit2', 0, 0);
    expect(order.ordered('units')).toEqual(['t1', 't2', 'scenery', 'unit2', 'unit9']);
    expect(order.replaceLayer('terrain', [])).toEqual(['t1', 't2']);
    order.removeUnit(9);
    expect(order.ordered('units')).toEqual(['scenery', 'unit2']);
    order.clear();
    expect(order.ordered('units')).toEqual([]);
  });
});
