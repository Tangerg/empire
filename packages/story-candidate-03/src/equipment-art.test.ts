import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import manifest from '../assets/final-ancient-china-v1/manifest-final-ancient-china-v1.json';
import overrides from '../../../tools/art/source-overrides.json';
import pilotReports from '../../../docs/art-rebuild-20261019/approved-v5/chinese-equipment-pilot/formal-export.json';

import batchTwoReports from '../../../docs/art-rebuild-20261019/approved-v5/chinese-equipment-batch-02/formal-export.json';

import batchThreeReports from '../../../docs/art-rebuild-20261019/approved-v5/chinese-equipment-batch-03/formal-export.json';

import batchFourReports from '../../../docs/art-rebuild-20261019/approved-v5/chinese-equipment-batch-04/formal-export.json';

import batchFiveReports from '../../../docs/art-rebuild-20261019/approved-v5/chinese-equipment-batch-05/formal-export.json';

const reports = [...pilotReports, ...batchTwoReports, ...batchThreeReports, ...batchFourReports, ...batchFiveReports];
const root = new URL('../../../', import.meta.url);
const prefix = 'packages/story-candidate-03/assets/final-ancient-china-v1/';
const sha = (bytes: Buffer) => createHash('sha256').update(bytes).digest('hex');
const registered = Object.entries(overrides.assets).filter(([topic, spec]) =>
  topic.startsWith('c03-equip-') && 'source' in spec && spec.source.includes('/approved-v5/'));

describe('C03 native equipment catalog', () => {
  it('keeps all48 identities and twenty independently reviewed bindings', () => {
    expect(manifest.assets.filter(asset => asset.category === 'equipment')).toHaveLength(48);
    expect(registered).toHaveLength(20);
    expect(reports).toHaveLength(20);
  });
  it.each(registered)('preserves %s source ownership, geometry and runtime bytes', (topic, spec) => {
    if (!('source' in spec)) throw new Error(`${topic}: expected static source`);
    expect(spec.source.startsWith(`${prefix}masters/approved-v5/equipment/${topic}/`)).toBe(true);
    const source = readFileSync(new URL(spec.source, root));
    expect(sha(source)).toBe(spec.sha256);
    expect(source[25]).toBe(6);
    const asset = manifest.assets.find(entry => entry.topicId === topic)!;
    expect(asset.qualityTier).toBe('approved-chunky-v5-export-verified');
    expect([asset.width, asset.height]).toEqual([48, 48]);
    expect(asset.anchor).toEqual([24, 24]);
    const matches = reports.filter(report => report.topicId === topic);
    expect(matches).toHaveLength(1);
    const report = matches[0];
    expect(report.sourceSha256).toBe(spec.sha256);
    expect(report.output).toBe(`${prefix}${asset.png}`);
    expect(report.size).toEqual([48, 48]);
    expect(report.outputAlpha.edgeMaxAlpha).toBe(0);
    expect(report.fileSha256).toBe(sha(readFileSync(new URL(report.output, root))));
  });
});
