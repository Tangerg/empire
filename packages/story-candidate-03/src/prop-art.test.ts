import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import manifest from '../assets/final-ancient-china-v1/manifest-final-ancient-china-v1.json';
import overrides from '../../../tools/art/source-overrides.json';
import pilotReports from '../../../docs/art-rebuild-20261019/approved-v5/chinese-prop-pilot/formal-export.json';
import batch02Reports from '../../../docs/art-rebuild-20261019/approved-v5/chinese-prop-batch-02/formal-export.json';
const reports = [...pilotReports, ...batch02Reports];

const root = new URL('../../../', import.meta.url);
const prefix = 'packages/story-candidate-03/assets/final-ancient-china-v1/';
const sha = (bytes: Buffer) => createHash('sha256').update(bytes).digest('hex');
const registered = Object.entries(overrides.assets).filter(([topic, spec]) =>
  topic.startsWith('c03-battle-prop-') && 'source' in spec && spec.source.includes('/approved-v5/'));

describe('C03 reviewed native battle props', () => {
  it('keeps eight independent source bindings and reports', () => {
    expect(registered).toHaveLength(8);
    expect(reports).toHaveLength(8);
  });
  it.each(registered)('binds original source and native alpha output for %s', (topic, spec) => {
    if (!('source' in spec)) throw new Error(`${topic}: expected static source`);
    expect(spec.source.startsWith(`${prefix}masters/approved-v5/props/${topic}/`)).toBe(true);
    const source = readFileSync(new URL(spec.source, root));
    expect(sha(source)).toBe(spec.sha256);
    expect(source[25]).toBe(6);
    const asset = manifest.assets.find(entry => entry.topicId === topic)!;
    expect(asset.qualityTier).toBe('approved-chunky-v5-export-verified');
    expect([asset.width, asset.height]).toEqual([64, 64]);
    const report = reports.filter(entry => entry.topicId === topic);
    expect(report).toHaveLength(1);
    expect(report[0].sourceSha256).toBe(spec.sha256);
    expect(report[0].output).toBe(`${prefix}${asset.png}`);
    expect(report[0].size).toEqual([64, 64]);
    expect(report[0].outputAlpha.edgeMaxAlpha).toBe(0);
    expect(report[0].fileSha256).toBe(sha(readFileSync(new URL(report[0].output, root))));
  });
});
