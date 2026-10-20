import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import manifest from '../assets/final-scifi-v1/manifest-final-scifi-v1.json';
import overrides from '../../../tools/art/frame-overrides.json';

const root = new URL('../../../', import.meta.url);
const sha = (bytes: Buffer) => createHash('sha256').update(bytes).digest('hex');
const topic = 'c02-unit-antler-knight';
const registered = overrides.assets[topic];
const order = ['idle', 'walk-a', 'attack', 'walk-b'];

describe('C02 authored mounted combat quartet', () => {
  it('preserves the original canonical identity, cell geometry and footprint', () => {
    expect(manifest.assets.filter(asset => asset.category === 'combat-unit')).toHaveLength(40);
    const asset = manifest.assets.find(entry => entry.topicId === topic);
    expect(asset).toMatchObject({
      category: 'combat-unit', width: 256, height: 64, frameWidth: 64, frameHeight: 64,
      frames: 4, frameOrder: order, anchor: [32, 63], footprint: [2, 2],
      qualityTier: 'approved-chunky-v5-export-verified',
    });
    expect(registered.placement).toEqual({ renderWidth: 52, origin: [3, 10] });
    expect(Object.keys(registered.frames)).toEqual(order);
  });

  it('binds distinct native sources and measured bounded offsets to the exact one-density output', () => {
    const reports = JSON.parse(readFileSync(new URL(
      'docs/art-rebuild-20261019/approved-v5/scifi-combat-pilot/formal-export.json', root), 'utf8'));
    expect(reports).toHaveLength(1);
    const report = reports[0];
    expect(report.density).toBe(1);
    expect(report.logicalAnchor).toEqual([32, 63]);
    expect(report.size).toEqual([256, 64]);
    expect(report.placement).toEqual(registered.placement);
    const hashes = [];
    for (const [pose, source] of Object.entries(registered.frames)) {
      expect(source.source.startsWith('packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/animation/antler-knight/')).toBe(true);
      const raw = readFileSync(new URL(source.source, root));
      expect(sha(raw)).toBe(source.sha256);
      expect([raw.readUInt32BE(16), raw.readUInt32BE(20), raw[25]]).toEqual([1254, 1254, 6]);
      expect(source.registrationOffset.every(value => Number.isInteger(value) && Math.abs(value) <= 2)).toBe(true);
      expect(report.sources.find((entry: { pose: string }) => entry.pose === pose)).toMatchObject({
        sourceSha256: source.sha256, registrationOffset: source.registrationOffset,
      });
      hashes.push(source.sha256);
    }
    expect(new Set(hashes).size).toBe(4);
    const asset = manifest.assets.find(entry => entry.topicId === topic)!;
    expect(report.output).toBe(`packages/story-candidate-02/assets/final-scifi-v1/${asset.png}`);
    expect(sha(readFileSync(new URL(report.output, root)))).toBe(report.fileSha256);
  });
});
