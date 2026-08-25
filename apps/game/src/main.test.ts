// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';

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
    expect(document.querySelectorAll('.level-card').length).toBe(4);
    // Each card paints a real minimap: the ground the scene composes, then the
    // tiles the per-cell painters draw, then a dot for every unit on the map.
    expect(document.querySelectorAll('.level-thumb svg > g').length).toBeGreaterThan(100);

    (document.querySelector('[data-act="closeModal"]') as HTMLElement).click();
    expect(document.querySelector('.modal-root .modal')).toBeNull();
  });
});
