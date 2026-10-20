// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createBattleEngine, type LevelData } from '@empire/battle-engine';
import { ANCIENT_EMPIRES_LEVELS } from '@empire/content-ancient-empires';
import { createTestCatalog } from '@empire/test-content';
import { ArtDirection } from '../../art/direction';
import type { UnitArtContext } from '../../art/ports';
import { unitIcon, unitPicture } from '../../art/units';
import { portraitMarkup, portraitSvg } from '../../art/portraits';
import { svgBoardSurface } from '../../art/svg-board-surface';
import { BoardView, emptyOverlay } from '../board';
import { GameController } from '../game';

const content = createTestCatalog();
const engine = createBattleEngine({ content });
const level: LevelData = { ...ANCIENT_EMPIRES_LEVELS[0], units: [
  { unit: 'soldier', owner: 1, x: 2, y: 2, key: 'campaign-torren' },
  { unit: 'soldier', owner: 1, x: 3, y: 2, key: 'ordinary-soldier' },
  { unit: 'soldier', owner: 2, x: 5, y: 2, key: 'enemy-soldier' },
] };
const dispose: Array<() => void> = [];
afterEach(() => { dispose.splice(0).forEach(fn => fn()); vi.unstubAllGlobals(); document.body.innerHTML = ''; });

function recordingArt() {
  const calls: Array<{ kind: string; type: string; context: UnitArtContext | undefined }> = [];
  const record = (kind: string, type: string, context?: UnitArtContext) => {
    calls.push({ kind, type, context });
    return `<g data-context="${context?.actorKey ?? 'type-only'}"/>`;
  };
  const art = new ArtDirection([{ id: 'context-observer',
    unitPicture: (type, _team, context) => ({ body: record('picture', type, context) }),
    unitIcon: (type, _team, _size, context) => `<svg>${record('icon', type, context)}</svg>`,
    portraitMarkup: (type, _team, context) => record('portrait', type, context),
  }]);
  return { art, calls };
}

function point(svg: SVGSVGElement, at: { x: number; y: number }, event: string) {
  const { width, height } = svg.viewBox.baseVal;
  svg.getBoundingClientRect = () => ({ left: 0, top: 0, width, height, right: width, bottom: height,
    x: 0, y: 0, toJSON: () => ({}) });
  svg.dispatchEvent(new MouseEvent(event, { bubbles: true, clientX: at.x * 32 + 16, clientY: at.y * 32 + 16, button: 0 }));
}

describe('explicit unit art context through existing UI paths', () => {
  it('forwards the same optional context through wrappers and picture fallback', () => {
    const { art, calls } = recordingArt();
    const definition = content.units.get('soldier');
    const context = { actorKey: 'campaign-torren' };
    unitPicture(art, definition, '#ffffff', context);
    unitIcon(art, definition, '#ffffff', 40, context);
    portraitMarkup(art, definition, '#ffffff', context);
    portraitSvg(art, definition, '#ffffff', 84, context);
    expect(calls.map(call => call.kind)).toEqual(['picture', 'icon', 'portrait', 'portrait']);
    expect(calls.every(call => call.context === context)).toBe(true);
    const pictureProvider = art.providers[0].unitPicture;
    if (!pictureProvider) throw new Error('missing fixture picture provider');
    const fallback = new ArtDirection([{ id: 'picture-only', unitPicture: pictureProvider }]);
    expect(unitIcon(fallback, definition, '#ffffff', 40, context)).toContain('campaign-torren');
    calls.length = 0;
    unitPicture(art, definition, '#ffffff');
    unitIcon(art, definition, '#ffffff');
    portraitSvg(art, definition, '#ffffff');
    expect(calls.every(call => call.context === undefined)).toBe(true);
  });

  it('draws distinct actor keys without adding presentation fields to state or saves', () => {
    const { art, calls } = recordingArt();
    const state = engine.createState(level);
    const before = JSON.stringify(engine.saveBattle(state, '2026-10-19T00:00:00.000Z'));
    const board = new BoardView({ content, grid: engine.rules.grids.get('square4'), art, renderer: svgBoardSurface }, state,
      { onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {}, onSecondary: () => {}, onScale: () => {}, onPan: () => {} });
    dispose.push(() => board.dispose());
    board.render(emptyOverlay());
    expect(calls.filter(call => call.kind === 'picture').map(call => call.context?.actorKey))
      .toEqual(['campaign-torren', 'ordinary-soldier', 'enemy-soldier']);
    expect(board.el.querySelector('[data-context="campaign-torren"]')).not.toBeNull();
    expect(board.el.querySelector('[data-context="ordinary-soldier"]')).not.toBeNull();
    expect(JSON.stringify(engine.saveBattle(state, '2026-10-19T00:00:00.000Z'))).toBe(before);
    expect(engine.loadBattle(JSON.parse(before))).toEqual(state);
  });

  it('passes actor identity to hover inspector and initiative, while recruitment stays type-only', () => {
    vi.stubGlobal('requestAnimationFrame', () => 1);
    vi.stubGlobal('cancelAnimationFrame', () => {});
    const { art, calls } = recordingArt();
    const controller = new GameController(level, () => {}, { engine, art });
    dispose.push(() => controller.dispose());
    document.body.append(controller.root);
    const svg = controller.root.querySelector('svg.board') as SVGSVGElement;
    point(svg, { x: 2, y: 2 }, 'pointermove');
    expect(controller.root.querySelector('.portrait [data-context="campaign-torren"]')).not.toBeNull();
    point(svg, { x: 3, y: 2 }, 'pointermove');
    expect(controller.root.querySelector('.portrait [data-context="ordinary-soldier"]')).not.toBeNull();
    calls.length = 0;
    point(svg, { x: 1, y: 1 }, 'pointerdown');
    point(svg, { x: 1, y: 1 }, 'pointerup');
    expect(controller.root.querySelectorAll('.recruit-card').length).toBeGreaterThan(0);
    expect(calls.filter(call => call.kind === 'icon').length).toBeGreaterThan(0);
    expect(calls.filter(call => call.kind === 'icon').every(call => call.context === undefined)).toBe(true);
    calls.length = 0;
    const initiative = new GameController({ ...level, rules: { ...level.rules, turnOrder: 'initiative' } }, () => {}, { engine, art });
    dispose.push(() => initiative.dispose());
    expect(initiative.root.querySelector('.order-strip [data-context="campaign-torren"]')).not.toBeNull();
    expect(initiative.root.querySelector('.order-strip [data-context="ordinary-soldier"]')).not.toBeNull();
    expect(calls.filter(call => call.kind === 'icon').every(call => call.context?.actorKey !== undefined)).toBe(true);
  });
});
