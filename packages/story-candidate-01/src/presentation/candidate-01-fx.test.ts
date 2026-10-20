import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import fxOverrides from '../../../../tools/art/fx-overrides.json';
import hdManifest from '../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json';
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


describe('approved FX source and export binding', () => {
  const root = new URL('../../../../', import.meta.url);
  const hash = (bytes: Buffer) => createHash('sha256').update(bytes).digest('hex');
  const phaseOrders: Record<string, readonly string[]> = {
    'C01-FX-01': ['onset', 'sweep', 'peak', 'dissipate'],
    'C01-FX-02': ['onset', 'sweep', 'peak', 'dissipate'],
    'C01-FX-03': ['approach', 'impact', 'burst', 'dissipate'],
    'C01-FX-04': ['contact', 'expand', 'peak', 'settle'],
    'C01-FX-05': ['ignite', 'rise', 'flare', 'embers'],
    'C01-FX-06': ['low-ring', 'rising-ring', 'sealed-ring', 'fading-fragments'],
    'C01-FX-07': ['airborne-burst', 'falling-pieces', 'falling-remnants', 'settled-heap'],
    'C01-FX-08': ['clustered-echoes', 'rising-echoes', 'dissolving-echoes', 'wisp-remnants'],
    'C01-FX-13': ['cracked-earth', 'ember-rise', 'flame-peak', 'ember-residue'],
    'C01-FX-12': ['heat-curl-a', 'heat-curl-b', 'heat-split', 'heat-renewal'],
    'C01-FX-11': ['gust-curl-a', 'gust-curl-b', 'gust-breakup', 'gust-renewal'],
    'C01-FX-09': ['rainfall', 'splash-onset', 'splash-crown', 'rain-renewal'],
    'C01-FX-10': ['ripple-near', 'ripple-growing', 'ripple-wide', 'ripple-renewal'],
  };
  it.each(Object.entries(fxOverrides.assets))('binds %s to its reviewed full-canvas phases at both densities', (topic, override) => {
    const record = assets.candidate01Asset(topic);
    expect(record.qualityTier).toBe('approved-chunky-v5-export-verified');
    expect(phaseOrders[topic]).toBeDefined();
    expect(override.phases.map(phase => phase.phase)).toEqual(phaseOrders[topic]);
    expect(override.placement).toEqual({ scale: 28 / 1254, origin: [2, 2], sourcePivot: [627, 627] });
    for (const phase of override.phases) {
      expect(hash(readFileSync(new URL(phase.source, root)))).toBe(phase.sha256);
    }
    const report = JSON.parse(readFileSync(new URL(`docs/art-rebuild-20261019/approved-v5/fx/fx-${topic.slice(-2)}/formal-export.json`, root), 'utf8'));
    expect(report.map((entry: { density: number }) => entry.density)).toEqual([1, 2]);
    const canonical = manifest.assets.find(asset => asset.topicId === topic)!;
    const hd = hdManifest.assets.find(asset => asset.topicId === topic)!;
    for (const entry of report) {
      const descriptor = entry.density === 1 ? canonical : hd;
      const png = readFileSync(new URL(entry.output, root));
      expect(entry.output).toBe(`packages/story-candidate-01/assets/final-fantasy-v1/${descriptor.png}`);
      expect(hash(png)).toBe(entry.fileSha256);
      expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual([128 * entry.density, 32 * entry.density]);
      expect(entry.placement).toEqual(override.placement);
      expect(entry.frames.map((frame: { phase: string }) => frame.phase)).toEqual(override.phases.map(phase => phase.phase));
      expect(entry.sources.map((source: { sourceSha256: string }) => source.sourceSha256)).toEqual(override.phases.map(phase => phase.sha256));
      expect(entry.independentFrameFit).toBe(false);
    }
    expect(record.url).toContain(hd.png);
    expect(hd.pixelDensity).toBe(2);
  });
});
