// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest';
import { createBattleEngine } from '@empire/battle-engine';
import { BoardView, SvgBoardSurface, emptyOverlay } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '../index';
import { CANDIDATE_01_ART } from './index';
import { CANDIDATE_01_MAP_STRUCTURE_ART } from './candidate-01-bindings';
import { CANDIDATE_01_MAP_STRUCTURE_PRESENTATIONS, resolveCandidate01MapStructureArt } from './candidate-01-map-structure-presentations';
import contract from './fixtures/c01-04-presentation-contract.json';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const engine = createBattleEngine({ content });
const handlers = { onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {}, onSecondary: () => {}, onScale: () => {} };

function mount(levelId: string) {
  const state = engine.createState(candidate01Level(levelId));
  let surface!: SvgBoardSurface;
  const board = new BoardView({ content, grid: engine.rules.grids.get('square4'), art: CANDIDATE_01_ART,
    renderer: scene => surface = new SvgBoardSurface(scene) }, state, handlers);
  board.render(emptyOverlay());
  return { state, board, surface };
}

describe('field-post consumers through the complete existing board path', () => {
  it.each(contract.fieldPosts)('draws every actor/building and all field-posts in $id without changing state', ({ id, cells }) => {
    const { state, board, surface } = mount(id);
    try {
      const before = JSON.stringify(state);
      const record = resolveCandidate01MapStructureArt(CANDIDATE_01_MAP_STRUCTURE_PRESENTATIONS, CANDIDATE_01_MAP_STRUCTURE_ART['c01.field-post']!);
      expect(surface.element.querySelectorAll('.layer-units > .unit')).toHaveLength(state.units.length);
      const buildings = surface.element.querySelectorAll('[data-candidate-art="structure"]');
      expect(buildings).toHaveLength(state.map.tiles.filter(id => content.terrains.get(id).capturable).length + state.structures.length);
      const posts = [...buildings].filter(node => node.querySelector('image')?.getAttribute('href') === record.url);
      expect(posts).toHaveLength(cells.length);
      board.render(emptyOverlay());
      expect(surface.element.querySelectorAll('.layer-units > .unit')).toHaveLength(state.units.length);
      expect(JSON.stringify(state)).toBe(before);
    } finally { board.dispose(); }
  });

  it.each(['campaign-mirelle', 'south-patrol', 'scout-1'])('keeps c01-04 %s hidden through repeated selection/movement and restores its standing placement', async key => {
    const { state, board, surface } = mount('c01-04');
    try {
      const before = JSON.stringify(state);
      const actor = state.units.find(unit => unit.key === key)!;
      const drawing = surface.drawnUnit(actor.id)!;
      const place = vi.spyOn(drawing, 'place');
      const overlay = emptyOverlay();
      overlay.selected = { x: actor.x, y: actor.y };
      const visible = () => [...surface.element.querySelectorAll<SVGElement>('.layer-units > .unit')].filter(node => node.style.display !== 'none').length;
      for (const hidden of [true, false, true, false]) {
        overlay.hiddenUnits = new Set(hidden ? [actor.id] : []);
        board.render(overlay);
        expect(visible()).toBe(hidden ? 18 : 19);
        expect(surface.element.querySelectorAll('.selection-cue')).toHaveLength(hidden ? 0 : 1);
        await board.animateMove(actor, [{ x: actor.x, y: actor.y }, { x: actor.x + 1, y: actor.y }], 1);
        expect(visible()).toBe(hidden ? 18 : 19);
      }
      overlay.selected = null;
      board.render(overlay);
      expect(place).toHaveBeenLastCalledWith(actor.x * 32, actor.y * 32);
      expect(surface.element.querySelectorAll('.selection-cue')).toHaveLength(0);
      expect(JSON.stringify(state)).toBe(before);
      expect(surface.element.querySelectorAll('[data-candidate-art="structure"]')).toHaveLength(25);
    } finally { board.dispose(); }
  }, 15000);
});
