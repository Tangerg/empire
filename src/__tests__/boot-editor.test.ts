// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';

describe('editor entry point', () => {
  it('boots onto a blank map with the palette mounted', async () => {
    document.body.innerHTML = '<div id="app"></div>';
    localStorage.clear();
    await import('../editor/main');

    expect(document.querySelector('.editor-root')).toBeTruthy();
    expect(document.querySelectorAll('.palette .swatch').length).toBe(11);
    expect(document.querySelector('svg.editor-board')).toBeTruthy();
    expect(document.querySelector('.props')!.textContent).toContain('通用胜利条件');
  });
});
