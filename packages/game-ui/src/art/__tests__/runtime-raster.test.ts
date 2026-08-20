import { describe, expect, it } from 'vitest';
import { runtimeAtlasCellMarkup, runtimeGridAtlasCellMarkup, runtimeUnitPicture } from '../runtime-raster';
import { boardPictureMarkup } from '../board-surface';

describe('runtime raster adapter', () => {
  /**
   * A strip is data here, not a string with its own description written into it.
   *
   * What this used to assert is the giveaway: `data-frame-width="32"` and
   * `&quot;frames&quot;:[1,3]` — a test reading JSON out of an HTML attribute that
   * the function under test had just serialised, so that a renderer could parse it
   * back. The frames and the clips are the return value now.
   */
  it('grounds a 32x48 four-frame unit strip in a 32x32 board cell', () => {
    const picture = runtimeUnitPicture(
      {
        href: '/assets/unit.png?a=1&b=2',
        frameWidth: 32,
        frameHeight: 48,
        frameCount: 4,
        anchor: { x: 16, y: 47 },
      },
      '#3f7fd8',
    );

    expect(picture.body).toContain('stroke="#3f7fd8"');
    expect(picture.body).toContain('runtime-unit-contact-shadow');
    // The sheet's anchor is the figure's feet; the cell's floor is one pixel up.
    expect(picture.strip).toMatchObject({
      href: '/assets/unit.png?a=1&b=2',
      frameWidth: 32,
      frameHeight: 48,
      frameCount: 4,
      x: 0,
      y: -16,
      playing: 'idle',
    });
    // The three a battle has names for, and nothing else.
    expect(picture.strip!.clips.map((clip) => clip.id)).toEqual(['idle', 'walk', 'attack']);
    expect(picture.strip!.clips.find((clip) => clip.id === 'walk')!.frames).toEqual([1, 3]);

    // A URL is data in the declaration and escaped where it becomes markup.
    const still = boardPictureMarkup(picture);
    expect(still).toContain('href="/assets/unit.png?a=1&amp;b=2"');
    expect(still).toContain('x="0" y="-16" width="32" height="48"');
    expect(still).toContain('width="128" height="48"');
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
      runtimeUnitPicture(
        {
          href: '/assets/unit.png',
          frameWidth: 32,
          frameHeight: 48,
          frameCount: 4,
          anchor: { x: 16, y: 47 },
          attackFrame: 4,
        },
        '#fff',
      ),
    ).toThrow(/exceeds/);
  });
});
