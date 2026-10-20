import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import sourceOverrides from '../../../../tools/art/source-overrides.json';
import hdManifest from '../../assets/final-fantasy-v1/manifest-tactical-runtime-hd.json';
import { candidate01Asset } from './candidate-01-assets';
import { CANDIDATE_01_PORTRAITS } from './candidate-01-story';

const root = new URL('../../../../', import.meta.url);
const topics = ['C01-CHAR-IVRA-01', 'C01-CHAR-IVRA-02', 'C01-CHAR-IVRA-03'] as const;

describe('Ivra growth portrait registration', () => {
  it.each(topics)('preserves %s whole transparent source and shared card scale', topic => {
    const source = sourceOverrides.assets[topic];
    expect(source.kind).toBe('character-card-composite-v1');
    expect(source.placement).toEqual({ renderWidth: 176, origin: [8, 2] });
    const bytes = readFileSync(new URL(source.character.source, root));
    expect(createHash('sha256').update(bytes).digest('hex')).toBe(source.character.sha256);
    expect([bytes.readUInt32BE(16), bytes.readUInt32BE(20), bytes[25]]).toEqual([1122, 1402, 6]);
    const card = candidate01Asset(topic);
    expect([card.width, card.height]).toEqual([192, 224]);
    expect(card.qualityTier).toBe('approved-chunky-v5-export-verified');
    expect(hdManifest.assets.some(record => record.topicId === topic)).toBe(false);
  });

  it('retains the authored juvenile selector without inventing automatic story-stage switching', () => {
    expect(CANDIDATE_01_PORTRAITS.ivra).toBe(candidate01Asset(topics[0]).url);
    expect(Object.values(CANDIDATE_01_PORTRAITS)).not.toContain(candidate01Asset(topics[1]).url);
    expect(Object.values(CANDIDATE_01_PORTRAITS)).not.toContain(candidate01Asset(topics[2]).url);
    expect(new Set(topics.map(topic => sourceOverrides.assets[topic].character.sha256)).size).toBe(3);
  });
});
