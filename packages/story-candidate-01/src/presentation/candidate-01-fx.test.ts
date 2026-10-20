import { afterEach, describe, expect, it, vi } from 'vitest';
import manifest from '../../assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import * as assets from './candidate-01-assets';
import { candidate01FxPicture } from './candidate-01-runtime';

afterEach(() => vi.restoreAllMocks());

describe('candidate-01 effect pivot ownership', () => {
  const topics = Array.from({ length: 24 }, (_, index) => `C01-FX-${String(index + 1).padStart(2, '0')}`);

  it('covers exactly the 24 canonical effects', () => {
    expect(manifest.assets.filter(asset => asset.category === 'fx').map(asset => asset.topicId)).toEqual(topics);
  });

  it.each(topics)('preserves the complete pre-anchor BoardPicture for %s', topic => {
    const record = assets.candidate01Asset(topic);
    expect(record.anchor).toEqual([16, 16]);
    expect(record.blendMode).toBe('normal');
    expect(record.url).toContain(`/runtime-hd/fx/fx-${topic.slice(-2)}.png`);
    // This is the complete old adapter output, including target-relative placement.
    expect(candidate01FxPicture(topic)).toEqual({
      body: '',
      strip: {
        href: record.url,
        frameWidth: 32,
        frameHeight: 32,
        frameCount: 4,
        x: -16,
        y: -16,
        clips: [{ id: 'effect', frames: [0, 1, 2, 3], fps: 12,
          loop: ['C01-FX-09', 'C01-FX-10', 'C01-FX-11', 'C01-FX-12'].includes(topic) }],
        playing: 'effect',
      },
    });
  });

  it('uses a non-centered metadata pivot instead of a second centering rule', () => {
    const record = assets.candidate01Asset('C01-FX-01');
    const baseline = candidate01FxPicture(record.topicId);
    vi.spyOn(assets, 'candidate01Asset').mockReturnValue({ ...record, anchor: [7.5, 23] });
    expect(candidate01FxPicture(record.topicId)).toEqual({
      ...baseline, strip: { ...baseline.strip!, x: -7.5, y: -23 },
    });
  });

  it('rejects a missing pivot rather than silently centering', () => {
    const { anchor: _anchor, ...record } = assets.candidate01Asset('C01-FX-01');
    vi.spyOn(assets, 'candidate01Asset').mockReturnValue(record);
    expect(() => candidate01FxPicture(record.topicId)).toThrow('no valid anchor');
  });

  it.each<[number, number]>([[Number.NaN, 16], [16, Infinity], [-1, 16], [32, 16], [16, -1], [16, 32]])(
    'rejects an invalid pivot [%s, %s]', (x, y) => {
      const record = assets.candidate01Asset('C01-FX-01');
      vi.spyOn(assets, 'candidate01Asset').mockReturnValue({ ...record, anchor: [x, y] });
      expect(() => candidate01FxPicture(record.topicId)).toThrow('no valid anchor');
    },
  );
});
