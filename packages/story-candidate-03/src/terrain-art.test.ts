import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import manifest from '../assets/final-ancient-china-v1/manifest-final-ancient-china-v1.json';
import overrides from '../../../tools/art/canonical-terrain-overrides.json';

const root = new URL('../../../', import.meta.url);
const sha = (bytes: Buffer) => createHash('sha256').update(bytes).digest('hex');
const registered = Object.entries(overrides.assets).filter(([topic]) => topic.startsWith('c03-'));
const reports = JSON.parse(readFileSync(new URL('docs/art-rebuild-20261019/approved-v5/chinese-terrain-batch-01/formal-export.json', root), 'utf8'));

describe('C03 native-density terrain catalog', () => {
  it('retains the complete 32-topic inventory and four source-reviewed bindings', () => {
    expect(manifest.assets.filter(asset => asset.category === 'terrain')).toHaveLength(32);
    expect(registered).toHaveLength(4);
    expect(reports).toHaveLength(4);
  });

  it.each(registered)('preserves %s geometry, source ownership and exact runtime bytes', (topic, source) => {
    const asset = manifest.assets.find(entry => entry.topicId === topic)!;
    expect(asset).toBeDefined();
    expect(asset.qualityTier).toBe('approved-chunky-v5-export-verified');
    expect(source.source.startsWith('packages/story-candidate-03/assets/final-ancient-china-v1/masters/approved-v5/canonical-terrain/')).toBe(true);
    const bytes = readFileSync(new URL(source.source, root));
    expect(sha(bytes)).toBe(source.sha256);
    expect([bytes.readUInt32BE(16), bytes.readUInt32BE(20)]).toEqual(source.registration.sourceSize);
    const matches = reports.filter((entry: { topicId: string }) => entry.topicId === topic);
    expect(matches).toHaveLength(1);
    const report = matches[0];
    expect(report.density).toBe(1);
    expect(report.tileMode).toBe(asset.tileMode);
    expect(report.size).toEqual([asset.width, asset.height]);
    expect(report.source.sourceSha256).toBe(source.sha256);
    expect(report.source.registration).toEqual(source.registration);
    expect(report.output).toBe(`packages/story-candidate-03/assets/final-ancient-china-v1/${asset.png}`);
    expect(report.authoredMaterialCount).toBe(1);
    expect(report.environmentCellCountIncrement).toBe(0);
    expect(report.derivedCellCount).toBe(asset.tileMode === 'variants' ? 4 : 16);
    expect(sha(readFileSync(new URL(report.output, root)))).toBe(report.fileSha256);
  });
});
