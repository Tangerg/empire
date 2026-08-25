import { describe, expect, it } from 'vitest';
import { BrowserPictureTextures } from '../picture-textures';

/**
 * The cache holds work, not failures.
 *
 * Every entry in these three caches is a `Promise`, and a promise that rejected
 * used to stay in them — so one refused `fetch` for one tile's PNG blanked that
 * picture for as long as the cache lived. Which is longer than a battle: the GPU
 * backend keeps one texture cache across every battle of a session, deliberately.
 *
 * The cache is observed through the promise identity it hands back, which is the
 * only thing it promises about caching: the same work in flight is the same
 * promise, and work that failed is not kept. jsdom cannot rasterise an SVG, so
 * every bake here fails — which is exactly the case under test.
 */
describe('a texture cache remembers the work, not the failure', () => {
  const markup = '<image href="/tiles/grass.png" width="32" height="32"/>';

  it('shares one attempt between callers that ask together', () => {
    const textures = new BrowserPictureTextures();
    const first = textures.bake(markup);
    expect(textures.bake(markup)).toBe(first);
    void first.catch(() => {});
    textures.dispose();
  });

  it('lets a picture that failed be asked for again', async () => {
    const textures = new BrowserPictureTextures();
    const failed = textures.bake(markup);
    await expect(failed).rejects.toThrow();

    // A fresh attempt, not the failure handed back a second time.
    const retry = textures.bake(markup);
    expect(retry).not.toBe(failed);
    await expect(retry).rejects.toThrow();
    textures.dispose();
  });

  it('does the same for a strip cut into frames', async () => {
    const textures = new BrowserPictureTextures();
    const strip = {
      href: '/units/knight.png',
      frameWidth: 32,
      frameHeight: 32,
      frameCount: 4,
      x: 0,
      y: 0,
      clips: [{ id: 'idle', frames: [0, 1, 2, 3], fps: 6, loop: true }],
    };
    const failed = textures.frames(strip);
    expect(textures.frames(strip)).toBe(failed);
    await expect(failed).rejects.toThrow();
    expect(textures.frames(strip)).not.toBe(failed);
    textures.dispose();
  });

  it('refuses a resolution that is not a positive number', () => {
    expect(() => new BrowserPictureTextures(0)).toThrow('greater than zero');
    expect(() => new BrowserPictureTextures(Number.NaN)).toThrow('greater than zero');
  });
});
