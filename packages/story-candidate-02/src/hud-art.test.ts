import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import manifest from '../assets/final-scifi-v1/manifest-final-scifi-v1.json';
import overrides from '../../../tools/art/source-overrides.json';

const root = new URL('../../../', import.meta.url);
const sha = (bytes: Buffer) => createHash('sha256').update(bytes).digest('hex');
const prefix = 'packages/story-candidate-02/assets/final-scifi-v1/';
const registered = Object.entries(overrides.assets).filter(([topic]) => topic.startsWith('c02-hud-'));

describe('C02 registered HUD artwork catalog', () => {
  it('preserves all 16 canonical HUD identities and their icon contracts', () => {
    const icons = manifest.assets.filter(asset => asset.category === 'hud');
    expect(icons).toHaveLength(16);
    expect(new Set(icons.map(asset => asset.topicId)).size).toBe(16);
    for (const hud of icons) {
      expect([hud.width, hud.height]).toEqual([32, 32]);
      expect(hud.anchor).toEqual([16, 16]);
    }
  });

  it.each(registered)('binds reviewed source and exact output bytes for %s', (topic, spec) => {
    if (!('source' in spec)) throw new Error(`${topic}: expected a static source`);
    expect(spec.source.startsWith(`${prefix}masters/approved-v5/hud/${topic}/`)).toBe(true);
    const source = readFileSync(new URL(spec.source, root));
    expect(sha(source)).toBe(spec.sha256);
    expect([source.readUInt32BE(16), source.readUInt32BE(20), source[25]]).toEqual([1254, 1254, 6]);
    const asset = manifest.assets.find(entry => entry.topicId === topic);
    if (!asset) throw new Error(`${topic}: missing canonical identity`);
    expect(asset.qualityTier).toBe('approved-chunky-v5-export-verified');
    const output = readFileSync(new URL(`${prefix}${asset.png}`, root));
    expect([output.readUInt32BE(16), output.readUInt32BE(20), output[25]]).toEqual([32, 32, 6]);
    const reports = JSON.parse(readFileSync(new URL(`docs/art-rebuild-20261019/approved-v5/scifi-hud/${topic}/formal-export.json`, root), 'utf8'));
    expect(reports).toHaveLength(1);
    expect(reports[0].topicId).toBe(topic);
    expect(reports[0].sourceSha256).toBe(spec.sha256);
    expect(reports[0].output).toBe(`${prefix}${asset.png}`);
    expect(reports[0].size).toEqual([32, 32]);
    expect(reports[0].fileSha256).toBe(sha(output));
  });
});
