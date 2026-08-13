import { describe, expect, it } from 'vitest';
import { runtimeAtlasCellMarkup, runtimeGridAtlasCellMarkup, runtimeUnitMarkup } from '../runtime-raster';

describe('runtime raster adapter', () => {
  it('grounds a 32x48 four-frame unit strip in a 32x32 board cell', () => {
    const markup = runtimeUnitMarkup(
      {
        href: '/assets/unit.png?a=1&b=2',
        frameWidth: 32,
        frameHeight: 48,
        frameCount: 4,
        anchor: { x: 16, y: 47 },
      },
      '#3f7fd8',
    );

    expect(markup).toContain('x="0" y="-16" width="32" height="48"');
    expect(markup).toContain('width="128" height="48"');
    expect(markup).toContain('href="/assets/unit.png?a=1&amp;b=2"');
    expect(markup).toContain('stroke="#3f7fd8"');
    expect(markup).toContain('runtime-unit-contact-shadow');
    expect(markup).toContain('runtime-unit-figure');
    expect(markup).toContain('id="runtime-unit-frame-32-48"');
    expect(markup).toContain('clip-path="url(#runtime-unit-frame-32-48)"');
    expect(markup).toContain('data-frame-width="32"');
    expect(markup).toContain('&quot;id&quot;:&quot;walk&quot;');
    expect(markup).toContain('&quot;frames&quot;:[1,3]');
  });

  it('selects a fixed atlas cell without scaling it', () => {
    const markup = runtimeAtlasCellMarkup(
      { href: '/assets/terrain.png', cellWidth: 32, cellHeight: 32, columns: 4, rows: 2 },
      6,
    );

    expect(markup).toContain('x="-64" y="-32" width="128" height="64"');
    expect(markup).toContain('viewBox="0 0 32 32"');
  });

  it('crops fractional cells from a generated grid atlas', () => {
    const markup = runtimeGridAtlasCellMarkup(
      { href: '/assets/forest.png?a=1&b=2', width: 1254, height: 1254, columns: 4, rows: 4 },
      6,
      72,
      72,
      'forest sprite',
    );

    expect(markup).toContain('viewBox="627 313.5 313.5 313.5"');
    expect(markup).toContain('width="72" height="72"');
    expect(markup).toContain('href="/assets/forest.png?a=1&amp;b=2"');
    expect(markup).toContain('class="forest sprite"');
  });

  it('rejects cells and frames outside their declared sheet', () => {
    expect(() =>
      runtimeAtlasCellMarkup(
        { href: '/assets/terrain.png', cellWidth: 32, cellHeight: 32, columns: 2, rows: 2 },
        4,
      ),
    ).toThrow(/capacity/);

    expect(() =>
      runtimeUnitMarkup(
        {
          href: '/assets/unit.png',
          frameWidth: 32,
          frameHeight: 48,
          frameCount: 4,
          anchor: { x: 16, y: 47 },
        },
        '#fff',
        4,
      ),
    ).toThrow(/exceeds/);
  });
});
