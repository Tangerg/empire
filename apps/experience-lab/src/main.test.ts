// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';

describe('experience lab entry', () => {
  it('presents a seed-player landing before the battle', async () => {
    document.body.innerHTML = '<div id="app"></div>';
    await import('./main');
    expect(document.querySelector('.experience-entry')).toBeTruthy();
    expect(document.body.textContent).toContain('灰旗试炼');
    expect(document.body.textContent).toContain('三条战线');
  });
});
