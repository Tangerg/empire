// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires';
import { saveCustomLevel } from '@empire/game-ui';

/** Boots the real page entry to catch module-level runtime errors. */
describe('game entry point', () => {
  /**
   * The entry to the game is a picture with a menu on it.
   *
   * This used to assert `.menu` and the words 内置关卡, and count four level cards
   * on the first screen — the shape of a scrolling document with two headings and a
   * card grid below the fold. The level lists are an overlay now, the same way the
   * battle's interface lies over its field, so what the title screen owes the
   * player is a way in to each of them.
   */
  it('offers every way into the game from the title screen', async () => {
    document.body.innerHTML = '<div id="app"></div>';
    /*
     * A level on a tiling the shipped art cannot paint, saved before the entry
     * loads, so its card is composed along with the built-in ones.
     *
     * A card used to be measured on a hardcoded `square4`. Every shipped level is
     * square, so it looked right — and this one would have had its ground composed
     * at square coordinates, or crashed the menu outright, since a painted scene
     * refuses a tiling whose cells its sheets are not cut to.
     */
    const square = BUILTIN_LEVELS[0]!;
    saveCustomLevel({
      ...square,
      id: 'hex-probe',
      name: 'hex probe',
      rules: { ...square.rules, grid: 'hex' },
    });

    await import('./main');

    const title = document.querySelector('.title');
    expect(title).toBeTruthy();
    expect(title!.textContent).toContain('远古帝国');

    const items = [...document.querySelectorAll('.title-item')];
    const acts = items.map((item) => item.getAttribute('data-act') ?? item.getAttribute('href'));
    // No campaign save in a fresh document, so 继续战役 is absent by design.
    expect(acts).toEqual([
      'campaignNew',
      'skirmish',
      'codex',
      '../editor/index.html',
      '../engine-demo/index.html',
    ]);

    // And the levels behind 单场战斗, each with a real minimap of itself. One
    // `it`, because the entry is a module and importing it twice imports a cache.
    (document.querySelector('[data-act="skirmish"]') as HTMLElement).click();
    const modal = document.querySelector('.modal-root .modal');

    expect(modal).toBeTruthy();
    expect(modal!.textContent).toContain('我的关卡');
    // Four built-in levels and the hex one, each with a picture of itself.
    const cards = [...document.querySelectorAll('.level-card')];
    expect(cards).toHaveLength(5);
    expect(cards.some((card) => card.textContent?.includes('hex probe'))).toBe(true);
    // The card's own picture, not every nested `<svg>` an atlas cell is drawn in.
    expect(document.querySelectorAll('.level-thumb > svg')).toHaveLength(5);
    // Each card paints a real minimap: the ground the scene composes, then the
    // tiles the per-cell painters draw, then a dot for every unit on the map.
    expect(document.querySelectorAll('.level-thumb svg > g').length).toBeGreaterThan(100);

    (document.querySelector('[data-act="closeModal"]') as HTMLElement).click();
    expect(document.querySelector('.modal-root .modal')).toBeNull();
  });
});
