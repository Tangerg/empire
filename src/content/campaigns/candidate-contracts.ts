import type { CampaignDefinition, CampaignNode, CampaignRosterSeed } from '../../campaign';

interface CandidateContractOptions {
  id: string;
  chapterCount: number;
  hero: CampaignRosterSeed;
  contentPacks: Record<string, number>;
  levelPrefix: string;
  presentationPrefix: string;
}

/** Story adapters contain locators and data only; the campaign FSM stays theme-neutral. */
function candidateContract(options: CandidateContractOptions): CampaignDefinition {
  const nodes: CampaignNode[] = [];
  for (let chapter = 1; chapter <= options.chapterCount; chapter++) {
    const story = `chapter-${chapter}-story`;
    const battle = `chapter-${chapter}-battle`;
    const next = chapter === options.chapterCount ? 'ending' : `chapter-${chapter + 1}-story`;
    nodes.push({
      id: story,
      type: 'story',
      presentation: `${options.presentationPrefix}/chapter-${chapter}`,
      next: battle,
    });
    nodes.push({
      id: battle,
      type: 'battle',
      level: `${options.levelPrefix}-${chapter}`,
      rosterBindings: [{ campaignUnit: options.hero.id, levelUnitKey: 'campaign-hero' }],
      next: { victory: next, defeat: 'ending-failed', retreat: story },
      outcomeEffects: { victory: [{ type: 'addVariable', key: 'victories', amount: 1 }] },
    });
  }
  nodes.push(
    { id: 'ending', type: 'ending', outcome: 'completed', presentation: `${options.presentationPrefix}/ending` },
    { id: 'ending-failed', type: 'ending', outcome: 'failed', presentation: `${options.presentationPrefix}/ending-failed` },
  );
  return {
    schema: 1,
    id: options.id,
    version: 1,
    start: 'chapter-1-story',
    contentPacks: options.contentPacks,
    initial: { variables: { victories: 0 }, roster: [options.hero] },
    nodes,
  };
}

export const CANDIDATE_01_CAMPAIGN_CONTRACT = candidateContract({
  id: 'candidate-01-gray-banner',
  chapterCount: 7,
  hero: { id: 'laiya', unitType: 'soldier', owner: 1, tags: ['hero', 'commander'] },
  contentPacks: { 'empire.common': 1, 'candidate-01': 1 },
  levelPrefix: 'c01-chapter',
  presentationPrefix: 'candidate-01',
});

export const CANDIDATE_02_CAMPAIGN_CONTRACT = candidateContract({
  id: 'candidate-02-stellar-chronicle',
  chapterCount: 7,
  hero: { id: 'stellar-lead', unitType: 'soldier', owner: 1, tags: ['hero', 'pilot'] },
  contentPacks: { 'empire.common': 1, 'candidate-02': 1 },
  levelPrefix: 'c02-sector',
  presentationPrefix: 'candidate-02',
});

export const CANDIDATE_03_CAMPAIGN_CONTRACT = candidateContract({
  id: 'candidate-03-founding-reign',
  chapterCount: 7,
  hero: { id: 'shen-li', unitType: 'soldier', owner: 1, tags: ['hero', 'commander'] },
  contentPacks: { 'empire.common': 1, 'candidate-03': 1 },
  levelPrefix: 'c03-campaign',
  presentationPrefix: 'candidate-03',
});

export const STORY_CAMPAIGN_CONTRACTS = [
  CANDIDATE_01_CAMPAIGN_CONTRACT,
  CANDIDATE_02_CAMPAIGN_CONTRACT,
  CANDIDATE_03_CAMPAIGN_CONTRACT,
] as const;
