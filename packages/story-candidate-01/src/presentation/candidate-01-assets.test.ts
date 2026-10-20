import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import manifest from '../../assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import teamVariants from '../../assets/final-fantasy-v1/manifest-authored-team-variants.json';
import hdManifest from '../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json';
import { candidate01Asset, readCandidate01AssetRecord } from './candidate-01-assets';
import { runtimeAtlasCellMarkup } from '@empire/game-ui';

import sourceOverrides from '../../../../tools/art/source-overrides.json';

const pack = new URL('../../assets/final-fantasy-v1/', import.meta.url);
const approved = new URL('../../../../docs/art-rebuild-20261019/approved-v5/', import.meta.url);
const bundles = [
  { topic: 'C01-MISSION-MINER', source: 'mission/miner', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'carry-or-work', 'walk-b'] },
  { topic: 'C01-MISSION-EVACUATION-DRIVER', source: 'mission/evacuation-driver', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'carry-or-work', 'walk-b'] },
  { topic: 'C01-UNIT-SHAMAN', source: 'animation/shaman', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-WYVERN-RIDER', source: 'animation/wyvern-rider', size: [128, 80], footprint: [2, 2], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-MISSION-BAKER', source: 'mission/baker', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'carry-or-work', 'walk-b'] },
  { topic: 'C01-MISSION-CITY-DOCTOR', source: 'mission/city-doctor', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'carry-or-work', 'walk-b'] },
  { topic: 'C01-MISSION-FORGE-ARTISAN', source: 'mission/forge-artisan', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'carry-or-work', 'walk-b'] },
  { topic: 'C01-UNIT-ANCIENT-DRAGON', source: 'animation/ancient-dragon', size: [128, 88], footprint: [2, 2], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-EAGLE-SCOUT', source: 'animation/eagle-scout', size: [128, 80], footprint: [2, 2], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-MISSION-BORDER-FARMER', source: 'mission/border-farmer', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'carry-or-work', 'walk-b'] },
  { topic: 'C01-UNIT-SPIRIT-FIRE', source: 'animation/spirit-fire', size: [116, 76], footprint: [2, 2], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-MISSION-REFUGEE-CHILD', source: 'mission/refugee-child', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'carry-or-work', 'walk-b'] },
  { topic: 'C01-UNIT-LANCE-CAVALRY', source: 'animation/lance-cavalry', size: [128, 80], footprint: [2, 2], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-WHITE-STAG-RIDER', source: 'animation/white-stag-rider', size: [128, 80], footprint: [2, 2], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },

  { topic: 'C01-UNIT-TROLL', source: 'animation/troll', size: [128, 80], footprint: [2, 2], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-JAVELIN-HUNTER', source: 'animation/javelin-hunter', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-CANNON-WAGON', source: 'animation/cannon-wagon', size: [112, 72], footprint: [2, 2], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-STONE-GOLEM', source: 'animation/stone-golem', size: [144, 104], footprint: [2, 2], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-CEMETERY-COLOSSUS', source: 'animation/cemetery-colossus', size: [128, 80], footprint: [2, 2], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-SKELETON-GUARD', source: 'animation/skeleton-guard', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-GHOST', source: 'animation/ghost', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-WOLF-RIDER', source: 'animation/wolf-rider', size: [128, 80], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-IVRA-GROWTH', source: 'animation/ivra-growth', size: [128, 80], footprint: [2, 2], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-WOODLAND-WALKER', source: 'animation/woodland-walker', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-SILVER-LONGBOW', source: 'animation/silver-longbow', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-RUNE-ARTIFICER', source: 'animation/rune-artificer', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-DRUID', source: 'animation/druid', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-FORGE-GUN-POSITION', source: 'buildings/forge-gun-position', size: [116, 116], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-UNIT-BERSERKER', source: 'animation/berserker', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-AXE-BREAKER', source: 'animation/axe-breaker', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-RUNE-SHIELD', source: 'animation/rune-shield', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-INQUISITOR', source: 'animation/inquisitor', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-GRAY-OATH-NOTICE', source: 'buildings/gray-oath-notice', size: [116, 116], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-WALL-LIFT-GATE', source: 'buildings/wall-lift-gate', size: [128, 128], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-UNIT-TEMPLAR', source: 'animation/templar', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-ASSASSIN', source: 'animation/assassin', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-CAPITAL-GATE', source: 'buildings/capital-gate', size: [128, 128], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-UNIT-RANGER', source: 'animation/ranger', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-OLD-CITY-TUNNEL', source: 'buildings/old-city-tunnel', size: [116, 116], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-UNIT-BALLISTA', source: 'animation/ballista', size: [104, 64], footprint: [2, 2], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-GRAVEKEEPER', source: 'animation/gravekeeper', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-KNIGHT', source: 'animation/knight', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-NAME-MEMORIAL', source: 'buildings/name-memorial', size: [118, 118], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-PUBLIC-RATION-POINT', source: 'buildings/public-ration-point', size: [124, 124], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-GRAY-FLAG-POINT', source: 'buildings/gray-flag-point', size: [106, 106], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-BALLISTA-EMPLACEMENT', source: 'buildings/ballista-emplacement', size: [116, 116], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-ARCHIVE-FURNACE', source: 'buildings/archive-furnace', size: [128, 128], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-HOLY-MEDICINE-SHED', source: 'buildings/holy-medicine-shed', size: [120, 120], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-MISSION-BRIDGE-LABORER', source: 'mission/bridge-laborer', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'carry-or-work', 'walk-b'] },
  { topic: 'C01-MISSION-REFUGEE-ADULT', source: 'mission/refugee-adult', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'carry-or-work', 'walk-b'] },
  { topic: 'C01-UNIT-LEGION-SHIELD', source: 'animation/legion-shield', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-FORGE-WORKSHOP', source: 'buildings/forge-workshop', size: [124, 124], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-FIELD-HOSPITAL', source: 'buildings/field-hospital', size: [128, 128], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-UNIT-ENGINEER', source: 'animation/engineer', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-BATTLE-MAGE', source: 'animation/battle-mage', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-WOUNDED-SHELTER', source: 'buildings/wounded-shelter', size: [128, 128], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-UNIT-MAGE', source: 'animation/mage', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-THREE-BRIDGE-SPAN', source: 'buildings/three-bridge-span', size: [128, 128], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-WASTELAND-CHEVAUX', source: 'buildings/wasteland-chevaux', size: [128, 128], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-UNIT-CLERIC', source: 'animation/cleric', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-HEAVY-KNIGHT', source: 'animation/heavy-knight', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-SUNKEN-BELL-SLUICE', source: 'buildings/sunken-bell-sluice', size: [128, 128], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-MERCENARY-DEPOT', source: 'buildings/mercenary-depot', size: [128, 128], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-OATH-TOWER-CONSOLE', source: 'buildings/oath-tower-console', size: [112, 112], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-UNIT-BANNER-GUARD', source: 'animation/banner-guard', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-REFUGEE-SHELTER', source: 'buildings/refugee-shelter', size: [160, 160], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-MILITARY-REGISTRY', source: 'buildings/military-registry', size: [160, 160], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-UNIT-SPEARMAN', source: 'animation/spearman', size: [112, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-JOINT-GRANARY', source: 'buildings/joint-granary', size: [172, 172], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-UNIT-SWORDSMAN', source: 'animation/swordsman', size: [96, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-UNIT-ARCHER', source: 'animation/archer', size: [104, 64], footprint: [1, 1], frames: ['idle', 'walk-a', 'attack', 'walk-b'] },
  { topic: 'C01-STRUCT-LORNE-KEEP', source: 'buildings/lorne-keep', size: [160, 160], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
  { topic: 'C01-STRUCT-REPAIRABLE-GATE', source: 'buildings/repairable-gate', size: [160, 160], footprint: [2, 2], frames: ['normal', 'damaged', 'captured'] },
];

describe('approved runtime asset documents', () => {
  it('covers every formally bound approved sequence with an explicit geometry and byte-equality contract', () => {
    const bound = manifest.assets.filter(record => record.qualityTier === 'approved-chunky-v5-export-verified' && record.frames !== undefined).map(record => record.topicId).sort();
    expect(bundles.map(bundle => bundle.topic).sort()).toEqual(bound);
  });
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


describe('authored red sequence file contract', () => {
  it('keeps both densities byte-identical to the registered red export without adding a canonical topic', () => {
    const variant = teamVariants.variants[0]!;
    const sequence = JSON.parse(readFileSync(new URL('animation/red-swordsman/export/sequence.json', approved), 'utf8'));
    expect(variant.anchor).toEqual(sequence.logicalPivot);
    expect(manifest.assets.filter(a => a.topicId === variant.topicId)).toHaveLength(1);
    for (const density of [1, 2]) {
      const png = readFileSync(new URL(density === 1 ? variant.png : variant.hdPng, pack));
      const exported = readFileSync(new URL(`animation/red-swordsman/export/sheet@${density}x.png`, approved));
      expect(png.equals(exported)).toBe(true);
      expect(png.readUInt32BE(16)).toBe(variant.frameWidth * variant.frames * density);
      expect(png.readUInt32BE(20)).toBe(variant.frameHeight * density);
    }
  });
});


describe('approved static asset bindings', () => {
  const props = Object.entries(sourceOverrides.assets)
    .filter(([topic, entry]) => manifest.assets.some(asset => asset.topicId === topic)
      && 'source' in entry
      && (entry.source.includes('/masters/approved-v5/props/') || entry.source.includes('/masters/approved-v5/equipment/')))
    .map(([topic]) => topic);
  it.each(props)('resolves %s through its exact canonical density contract', (topic) => {
    const record = candidate01Asset(topic);
    const hd = hdManifest.assets.find(entry => entry.topicId === topic);
    expect(record.qualityTier).toBe('approved-chunky-v5-export-verified');
    const size = topic.startsWith('C01-EQUIP-') ? 48 : 64;
    expect([record.width, record.height]).toEqual([size, size]);
    const outputs: Array<readonly [string, number]> = [[record.png, size]];
    if (size === 64) {
      expect(hd).toBeDefined();
      if (!hd) throw new Error(`missing prop HD ${topic}`);
      expect(record.url).toContain(hd.png);
      outputs.push([hd.png, 128]);
    } else {
      expect(hd).toBeUndefined();
      expect(record.url).toContain(record.png);
    }
    for (const [path, size] of outputs) {
      const bytes = readFileSync(new URL(path, pack));
      expect([bytes.readUInt32BE(16), bytes.readUInt32BE(20)]).toEqual([size, size]);
      expect(bytes[25]).toBe(6); // PNG RGBA colour type, not an opaque RGB export.
    }
  });
});
