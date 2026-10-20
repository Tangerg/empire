// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest';

// Synthetic URLs make the activated path observable without registering unfinished art.
vi.mock('./candidate-01-hero-appearances', async importOriginal => {
  const actual = await importOriginal<typeof import('./candidate-01-hero-appearances')>();
  const { candidate01Asset } = await import('./candidate-01-assets');
  const base = { ...candidate01Asset('C01-UNIT-SWORDSMAN'), url: '/test-torren-stage01.png', preserveAuthoredPalette: true };
  return { ...actual, CANDIDATE_01_HERO_APPEARANCES: {
    appearances: new Map([...actual.CANDIDATE_01_HERO_APPEARANCES.appearances, ['test-torren-stage01', base]]),
    teamVariants: new Map([['test-torren-stage01:#a8493f', { ...base, url: '/test-torren-stage01-red.png' }]]),
  } };
});
vi.mock('./candidate-01-actor-art', async importOriginal => {
  const actual = await importOriginal<typeof import('./candidate-01-actor-art')>();
  const { CANDIDATE_01_HERO_APPEARANCES } = await import('./candidate-01-hero-appearances');
  const unitArt = { topicId: 'C01-UNIT-SWORDSMAN', appearanceId: 'test-torren-stage01' };
  return { ...actual, CANDIDATE_01_ACTOR_ART: actual.readCandidate01ActorArt(CANDIDATE_01_HERO_APPEARANCES, {
    'campaign-torren': { expectedUnitType: 'c01.swordsman', unitArt, characterTopicId: 'C01-CHAR-TORREN-01' },
    'test-frame-only': { expectedUnitType: 'c01.swordsman', unitArt },
  }) };
});

import { CANDIDATE_01_ART } from './index';
import { createBattleEngine } from '@empire/battle-engine';
import { BoardView, emptyOverlay, svgBoardSurface } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '../index';
import { candidate01AssetUrl } from './candidate-01-assets';
import { candidate01UnitPicture, candidate01UnitIcon } from './candidate-01-runtime';

const named = { actorKey: 'campaign-torren' };
const other = { actorKey: 'ridge-guard' };
const portrait = (context?: { actorKey: string }) => CANDIDATE_01_ART.resolve(provider =>
  provider.portraitMarkup?.('c01.swordsman', '#A8493F', context));

describe('activated fixture through the real candidate provider', () => {
  it('chooses one identity across picture, icon and explicit card without changing a same-type soldier', () => {
    expect(candidate01UnitPicture('c01.swordsman', '#A8493F', named)?.strip?.href).toBe('/test-torren-stage01-red.png');
    expect(candidate01UnitIcon('c01.swordsman', '#A8493F', 32, named)).toContain('/test-torren-stage01-red.png');
    expect(portrait(named)).toContain(candidate01AssetUrl('C01-CHAR-TORREN-01'));
    expect(portrait(named)).not.toContain(candidate01AssetUrl('C01-CHAR-TORREN-02'));
    expect(candidate01UnitPicture('c01.swordsman', '#ffffff', named)?.strip?.href).toBe('/test-torren-stage01.png');
    expect(candidate01UnitPicture('c01.swordsman', '#A8493F', named)?.strip?.tint).toBeUndefined();
    expect(candidate01UnitPicture('c01.swordsman', '#A8493F', other)).toEqual(candidate01UnitPicture('c01.swordsman', '#A8493F'));
    expect(candidate01UnitIcon('c01.swordsman', '#A8493F', 32, other)).toBe(candidate01UnitIcon('c01.swordsman', '#A8493F', 32));
    expect(portrait(other)).toBe(portrait());
    expect(portrait(other)).not.toContain('/test-torren-stage01');
  });

  it('projects its own one-frame portrait when no card is declared', () => {
    const markup = portrait({ actorKey: 'test-frame-only' });
    expect(markup).toContain('/test-torren-stage01-red.png');
    expect(markup).not.toContain('unit-swordsman-red@2x.png');
  });

  it('draws the named fixture through actual BoardView while a hidden selection stays hidden', () => {
    const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
    const engine = createBattleEngine({ content });
    const state = engine.createState(candidate01Level('c01-01'));
    const before = JSON.stringify(state);
    const torren = state.units.find(actor => actor.key === 'campaign-torren')!;
    const board = new BoardView({ content, grid: engine.rules.grids.get('square4'), art: CANDIDATE_01_ART,
      renderer: svgBoardSurface }, state,
    { onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {}, onSecondary: () => {}, onScale: () => {}, onPan: () => {} });
    try {
      const overlay = emptyOverlay();
      board.render(overlay);
      const image = board.el.querySelector('image[href="/test-torren-stage01.png"]')!;
      expect(image).not.toBeNull();
      expect(board.el.querySelector('image[href*="unit-swordsman"]')).not.toBeNull();
      const actor = image.closest('.unit') as SVGElement;
      expect(actor.style.display).not.toBe('none');
      overlay.selected = { x: torren.x, y: torren.y };
      overlay.cursor = overlay.selected;
      overlay.hiddenUnits.add(torren.id);
      board.render(overlay);
      expect(actor.style.display).toBe('none');
      expect(board.el.querySelector('.selection-cue')).toBeNull();
      overlay.hiddenUnits.clear();
      board.render(overlay);
      expect(actor.style.display).not.toBe('none');
      expect(board.el.querySelector('.selection-cue')).not.toBeNull();
      expect(JSON.stringify(state)).toBe(before);
    } finally { board.dispose(); }
  });

  it('rejects the declared key with the wrong rule type through all three consumers', () => {
    expect(() => candidate01UnitPicture('c01.archer', '#ffffff', named)).toThrow('expected c01.swordsman');
    expect(() => candidate01UnitIcon('c01.archer', '#ffffff', 32, named)).toThrow('expected c01.swordsman');
    expect(() => CANDIDATE_01_ART.resolve(provider => provider.portraitMarkup?.('c01.archer', '#ffffff', named)))
      .toThrow('expected c01.swordsman');
  });
});
