import { TacticalGrids } from '@empire/battle-engine/tactical-grid';
const SQUARE = TacticalGrids.get('square4');
import { describe, expect, it } from 'vitest';
import { createSceneViewport, scenePointToCell } from '../scene-viewport';

describe('scene viewport', () => {
  it('keeps the tactical lattice inside a larger non-interactive art canvas', () => {
    const viewport = createSceneViewport(SQUARE, 21, 13, 32, {
      insets: { top: 58, right: 66, bottom: 68, left: 66 },
    });

    expect(viewport.fieldWidth).toBe(672);
    expect(viewport.sceneWidth).toBe(804);
    expect(viewport.sceneHeight).toBe(542);
    expect(scenePointToCell(viewport, 66 + 16, 58 + 16)).toEqual({ x: 0, y: 0 });
    expect(scenePointToCell(viewport, 66 + 20 * 32 + 16, 58 + 12 * 32 + 16)).toEqual({ x: 20, y: 12 });
  });

  it('rejects decorative margins instead of leaking them into edge cells', () => {
    const viewport = createSceneViewport(SQUARE, 4, 3, 32, { insets: { top: 20, left: 30 } });

    expect(scenePointToCell(viewport, 29.9, 40)).toBeNull();
    expect(scenePointToCell(viewport, 45, 19.9)).toBeNull();
    expect(scenePointToCell(viewport, 30 + 128, 40)).toBeNull();
  });
});
