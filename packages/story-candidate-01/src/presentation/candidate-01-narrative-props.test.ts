import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import canonical from '../../assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import hdManifest from '../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json';
import { candidate01Asset, candidate01AssetUrl, validateCandidate01HdAsset } from './candidate-01-assets';

const topics = ['C01-PROP-LEFT-GLOVE', 'C01-PROP-EXILE-HARDTACK',
  'C01-PROP-IVRA-BROKEN-BRIDLE', 'C01-PROP-CROWN-FRAGMENT'];
const pack = new URL('../../assets/final-fantasy-v1/', import.meta.url);

// This verifies the real catalog resolver, not a shipped prop presentation surface.
describe('narrative key-prop catalog contracts', () => {
  it.each(topics)('resolves %s at its unchanged logical size without tactical anchors', topic => {
    const asset = candidate01Asset(topic);
    const logical = canonical.assets.find(record => record.topicId === topic);
    expect(asset).toMatchObject({ ...logical, category: 'narrative-static', kind: 'key-prop', width: 128, height: 128 });
    for (const field of ['anchor', 'frames', 'variants', 'frameWidth', 'frameHeight']) {
      expect(asset).not.toHaveProperty(field);
    }
    const physical = hdManifest.assets.find(record => record.topicId === topic);
    if (physical) {
      expect([physical.width, physical.height, physical.pixelDensity]).toEqual([256, 256, 2]);
      expect(() => validateCandidate01HdAsset(asset, physical)).not.toThrow();
    }
    const path = physical?.png ?? asset.png;
    expect(asset.url).toContain(path);
    expect(candidate01AssetUrl(topic)).toBe(asset.url);
    const png = readFileSync(new URL(path, pack));
    expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual(physical ? [256, 256] : [128, 128]);
    expect(png[25]).toBe(6);
  });

  it.each(topics)('supports only explicit 2x physical geometry for %s', topic => {
    const asset = candidate01Asset(topic);
    const before = JSON.stringify(asset);
    const physical = { topicId: topic, png: `runtime-hd/narrative-static/${topic}.png`,
      width: 256, height: 256, pixelDensity: 2 };
    expect(() => validateCandidate01HdAsset(asset, physical)).not.toThrow();
    expect(() => validateCandidate01HdAsset(asset, { ...physical, width: 384, height: 384,
      pixelDensity: 3 })).toThrow('must remain 2');
    expect(() => validateCandidate01HdAsset(asset, { ...physical, height: 255 })).toThrow('disagree');
    expect(JSON.stringify(asset)).toBe(before);
  });
});
