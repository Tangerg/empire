import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import manifest from '../../assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import overrides from '../../../../tools/art/source-overrides.json';
import { candidate01Asset } from './candidate-01-assets';

const root = new URL('../../../../', import.meta.url);
const cards = manifest.assets.filter(record => record.kind === 'character-card');
const catalog = overrides.assets as Record<string, unknown>;
type Source = { source: string; sha256: string };
type Card = { kind: string; character: Source; background: Source };

describe('complete Gray Flag character-card source ownership', () => {
  it('covers every existing character-card identity without converting narrative scenes or props', () => {
    expect(cards).toHaveLength(22);
    expect(new Set(cards.map(card => card.topicId)).size).toBe(22);
  });
  it.each(cards)('binds $topicId to an immutable RGBA person source and separate opaque card backing', card => {
    const entry = catalog[card.topicId] as Card;
    expect(entry.kind).toBe('character-card-composite-v1');
    expect(entry.character.source).not.toBe(entry.background.source);
    for (const source of [entry.character, entry.background]) {
      const bytes = readFileSync(new URL(source.source, root));
      expect(createHash('sha256').update(bytes).digest('hex')).toBe(source.sha256);
      expect(bytes.subarray(1, 4).toString()).toBe('PNG');
      expect(bytes[25]).toBe(source === entry.character ? 6 : 2);
    }
    const resolved = candidate01Asset(card.topicId);
    expect(resolved.qualityTier).toBe('approved-chunky-v5-export-verified');
    expect([resolved.width, resolved.height]).toEqual([192, 224]);
  });
});
