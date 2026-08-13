// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';

describe('engine capability demo', () => {
  it('runs forecasts and committed actions through the real engine', async () => {
    document.body.innerHTML = '<div id="app"></div>';
    await import('./main');

    // Four rule plugins plus the content plugin the composition root supplies.
    expect(document.querySelectorAll('.plugin-card')).toHaveLength(5);
    expect(document.querySelectorAll('.demo-tile')).toHaveLength(35);

    (document.querySelector('[data-act="forecast-hero"]') as HTMLButtonElement).click();
    expect(document.querySelectorAll('.demo-tile.affected')).toHaveLength(5);
    expect(document.querySelector('.status-card')!.textContent).toContain('状态未发生变化');
    expect(document.querySelectorAll('.entity-account')[1].textContent).toContain('135 / 150');

    (document.querySelector('[data-act="execute-hero"]') as HTMLButtonElement).click();
    expect(document.querySelector('.status-card')!.textContent).toContain('正式行动管线提交');
    expect(document.querySelectorAll('.entity-account')[1].textContent).toContain('123 / 150');
    expect(document.querySelector('.event-card')!.textContent).toContain('resourceChanged');
    expect(document.querySelector('.event-card')!.textContent).toContain('areaAttack');

    (document.querySelector('[data-act="recruit"]') as HTMLButtonElement).click();
    expect(document.querySelectorAll('.entity-account')[0].textContent).toContain('资金350');
    expect(document.querySelector('.event-card')!.textContent).toContain('招募单位');
  });
});
