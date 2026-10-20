import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import manifest from '../assets/final-scifi-v1/manifest-final-scifi-v1.json';
import overrides from '../../../tools/art/canonical-terrain-overrides.json';

const root = new URL('../../../', import.meta.url);
const sha = (bytes: Buffer) => createHash('sha256').update(bytes).digest('hex');
const registered = Object.entries(overrides.assets).filter(([topic]) => topic.startsWith('c02-'));
const historicalReports = ['scifi-terrain-pilot', 'scifi-hesha-terrain', 'scifi-next-terrain', 'scifi-terrain-batch-04', 'scifi-terrain-batch-05', 'scifi-terrain-batch-06', 'scifi-terrain-batch-07', 'scifi-terrain-batch-08'].flatMap(folder => JSON.parse(readFileSync(new URL(
  `docs/art-rebuild-20261019/approved-v5/${folder}/formal-export.json`, root), 'utf8')));
const correctionReports = JSON.parse(readFileSync(new URL('docs/art-rebuild-20261019/approved-v5/soler-matte-corrections/formal-export.json', root), 'utf8'));
const reports = [...new Map([...historicalReports, ...correctionReports].map(report => [report.topicId, report])).values()];

describe('C02 native-density terrain catalog', () => {
  it('retains the complete 32-topic inventory and thirty-two source-reviewed bindings', () => {
    expect(manifest.assets.filter(asset => asset.category === 'terrain')).toHaveLength(32);
    expect(registered).toHaveLength(32);
    expect(reports).toHaveLength(32);
  });

  it.each(registered)('preserves %s geometry, source ownership and exact runtime bytes', (topic, source) => {
    const asset = manifest.assets.find(entry => entry.topicId === topic)!;
    expect(asset).toBeDefined();
    expect(asset.qualityTier).toBe('approved-chunky-v5-export-verified');
    expect(source.source.startsWith('packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/canonical-terrain/')).toBe(true);
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
    expect(report.output).toBe(`packages/story-candidate-02/assets/final-scifi-v1/${asset.png}`);
    expect(report.authoredMaterialCount).toBe(1);
    expect(report.environmentCellCountIncrement).toBe(0);
    expect(report.derivedCellCount).toBe(asset.tileMode === 'variants' ? 4 : 16);
    expect(sha(readFileSync(new URL(report.output, root)))).toBe(report.fileSha256);
  });
});
