// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';

/** Boots the real page entry to catch module-level runtime errors. */
describe('game entry point', () => {
  it('renders the level menu with thumbnails', async () => {
    document.body.innerHTML = '<div id="app"></div>';
    await import('../game/main');

    const menu = document.querySelector('.menu');
    expect(menu).toBeTruthy();
    expect(menu!.textContent).toContain('内置关卡');
    expect(document.querySelectorAll('.level-card').length).toBe(4);
    // Each card paints a real minimap.
    expect(document.querySelectorAll('.level-thumb svg g[data-tile]').length).toBeGreaterThan(100);
  });
});
