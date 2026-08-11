import { describe, expect, it } from 'vitest';
import { runtimeAtlasCellMarkup, runtimeUnitMarkup } from '../runtime-raster';

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
  });

  it('selects a fixed atlas cell without scaling it', () => {
    const markup = runtimeAtlasCellMarkup(
      { href: '/assets/terrain.png', cellWidth: 32, cellHeight: 32, columns: 4, rows: 2 },
      6,
    );

    expect(markup).toContain('x="-64" y="-32" width="128" height="64"');
    expect(markup).toContain('viewBox="0 0 32 32"');
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
