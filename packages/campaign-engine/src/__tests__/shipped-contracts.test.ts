import { describe, expect, it } from 'vitest';
import { CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN } from '@empire/story-candidate-01';
import { CANDIDATE_02_CAMPAIGN_CONTRACT } from '@empire/story-candidate-02';
import { CANDIDATE_03_CAMPAIGN_CONTRACT } from '@empire/story-candidate-03';
import { CampaignRuntime } from '../runtime';
import type { CampaignDefinition } from '../types';

/**
 * The three story shapes the repository ships, put through the campaign engine.
 *
 * `AGENTS.md` says repository-local usage is weak evidence for an export, but a
 * shipped campaign is real evidence — and by that standard candidate-02 and
 * candidate-03 were not evidence at all: two `CampaignDefinition`s that no app
 * imported, no test validated and nothing had ever executed. They exist to show
 * the node algebra expresses more than one kind of story, and the showing had
 * never happened. It happens here.
 *
 * What this suite deliberately does *not* do is play them. Candidate-02 and -03
 * name levels and content packs that are not implemented, so a runtime walk
 * would have to be faked, and a faked walk is a worse claim than none. Playing
 * candidate-01 for real is `campaign-runtime.test.ts`'s job. What all three can
 * honestly promise is that the engine accepts them: construction validates the
 * document and runs every node kind's own reference check, so a duplicate id, an
 * unreachable start or a dangling `next` refuses here rather than in front of a
 * player.
 */
const SHIPPED: readonly [string, CampaignDefinition][] = [
  ['candidate-01 · 灰旗崛起', CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN],
  ['candidate-02 · 星舰编年', CANDIDATE_02_CAMPAIGN_CONTRACT],
  ['candidate-03 · 白炬', CANDIDATE_03_CAMPAIGN_CONTRACT],
];

describe('every shipped campaign contract', () => {
  it.each(SHIPPED)('%s is a definition this engine accepts', (_name, definition) => {
    const runtime = new CampaignRuntime(definition);
    expect(runtime.node().id).toBe(definition.start);
    expect(runtime.state.status).toBe('active');
  });

  it.each(SHIPPED)('%s names only node kinds that have a handler', (_name, definition) => {
    // The kind map is open, so a contract can name a kind whose handler nobody
    // registered — and the failure would otherwise wait for a player to reach
    // that node. Constructing the runtime validates every node, which is where
    // the registry answers.
    const kinds = new Set(definition.nodes.map((node) => node.type));
    expect(kinds.size).toBeGreaterThan(1);
    expect(() => new CampaignRuntime(definition)).not.toThrow();
  });

  it('covers three different themes, so the algebra is doing real work', () => {
    // Three copies of one contract would pass everything above and prove
    // nothing. These have to be genuinely different documents.
    expect(new Set(SHIPPED.map(([, d]) => d.id)).size).toBe(3);
    // Each names its own content pack, which is what makes them different games
    // rather than one game relabelled. Node *ids* are deliberately not compared:
    // they are namespaced per campaign, so two stories may both have an `ending`.
    expect(new Set(SHIPPED.flatMap(([, d]) => Object.keys(d.contentPacks))).size).toBeGreaterThan(3);
    expect(new Set(SHIPPED.map(([, d]) => d.nodes.length)).size).toBeGreaterThan(1);
  });

  it('refuses a contract with a node kind nobody registered', () => {
    // The check above only means something if the registry really does refuse.
    const broken = structuredClone(CANDIDATE_02_CAMPAIGN_CONTRACT) as CampaignDefinition;
    (broken.nodes[0] as { type: string }).type = 'shop';
    expect(() => new CampaignRuntime(broken)).toThrow(/shop/);
  });
});
