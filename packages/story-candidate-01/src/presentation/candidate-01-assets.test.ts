import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import manifest from '../../assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import hdManifest from '../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json';
import { candidate01Asset, readCandidate01AssetRecord } from './candidate-01-assets';
import { runtimeAtlasCellMarkup } from '@empire/game-ui';

const pack = new URL('../../assets/final-fantasy-v1/', import.meta.url);
const approved = new URL('../../../../docs/art-rebuild-20261019/approved-v5/', import.meta.url);
const bundles = [
  { topic: 'C01-UNIT-SPEARMAN', source: 'animation/spearman', size: [112, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-JOINT-GRANARY', source: 'buildings/joint-granary', size: [172, 172], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-UNIT-SWORDSMAN', source: 'animation/swordsman', size: [96, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-ARCHER', source: 'animation/archer', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-LORNE-KEEP', source: 'buildings/lorne-keep', size: [160, 160], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-REPAIRABLE-GATE', source: 'buildings/repairable-gate', size: [160, 160], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
];

describe('approved runtime asset documents', () => {
  it.each(bundles)('binds $topic to byte-identical approved sheets and registered logical geometry', ({ topic, source, size, footprint, frames }) => {
    const record = candidate01Asset(topic);
    const hd = hdManifest.assets.find((entry) => entry.topicId === topic);
    if (!hd) throw new Error(`missing HD record: ${topic}`);
    const sequence = JSON.parse(readFileSync(new URL(`${source}/export/sequence.json`, approved), 'utf8'));
    expect(record.frameWidth).toBe(size[0]);
    expect(record.frameHeight).toBe(size[1]);
    expect(record.frames).toBe(frames.length);
    expect(record.frameOrder ?? record.stateOrder).toEqual(frames);
    expect(record.anchor).toEqual(sequence.logicalPivot);
    expect(record.footprint).toEqual(footprint);
    expect(sequence.config.frameSize).toEqual(size);
    expect(record.url).toContain(hd.png);
    expect(record.url).not.toContain('/runtime-hd/');
    expect(hd.pixelDensity).toBe(2);
    for (const [density, descriptor] of [[1, record], [2, hd]] as const) {
      const png = readFileSync(new URL(descriptor.png, pack));
      const exported = readFileSync(new URL(`${source}/export/sheet@${density}x.png`, approved));
      const hash = (bytes: Buffer) => createHash('sha256').update(bytes).digest('hex');
      expect(hash(png)).toBe(hash(exported));
      expect(png.subarray(1, 4).toString()).toBe('PNG');
      expect(png.readUInt32BE(16)).toBe(size[0] * frames.length * density);
      expect(png.readUInt32BE(20)).toBe(size[1] * density);
      expect([descriptor.width, descriptor.height]).toEqual([png.readUInt32BE(16), png.readUInt32BE(20)]);
    }
  });

  it('reads all three Keep states as distinct full-size atlas windows', () => {
    const record = candidate01Asset('C01-STRUCT-LORNE-KEEP');
    const atlas = { href: record.url, cellWidth: 160, cellHeight: 160, columns: 3, rows: 1 };
    for (const [frame, state] of ['normal', 'damaged', 'captured'].entries()) {
      expect(record.stateOrder?.indexOf(state)).toBe(frame);
      expect(runtimeAtlasCellMarkup(atlas, frame)).toContain(`x="${-frame * 160}" y="0" width="480" height="160"`);
    }
  });

  it('rejects stale sheet dimensions, duplicate frame roles and malformed pivots at the document boundary', () => {
    const raw = manifest.assets.find((entry) => entry.topicId === 'C01-UNIT-SWORDSMAN');
    if (!raw || raw.frameOrder === undefined) throw new Error('missing swordsman frame record');
    expect(readCandidate01AssetRecord(raw).anchor).toEqual([48.44444444444444, 53.27777777777778]);
    expect(() => readCandidate01AssetRecord({ ...raw, width: 128 })).toThrow('inconsistent frame dimensions');
    expect(() => readCandidate01AssetRecord({ ...raw, frameWidth: 32 })).toThrow('inconsistent frame dimensions');
    expect(() => readCandidate01AssetRecord({ ...raw, frameOrder: ['idle', 'walk-a', 'attack', 'attack'] })).toThrow('inconsistent frame order');
    expect(() => readCandidate01AssetRecord({ ...raw, anchor: [48] })).toThrow('expected 2');
    expect(() => readCandidate01AssetRecord({ ...raw, anchor: [48, Number.NaN] })).toThrow('expected 2');
  });
});
