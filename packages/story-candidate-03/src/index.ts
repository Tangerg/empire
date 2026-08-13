import type { CampaignDefinition, CampaignNode } from '@empire/campaign-engine';

function nodes(): CampaignNode[] {
  const result: CampaignNode[] = [];
  for (let chapter = 1; chapter <= 7; chapter++) {
    const story = `chapter-${chapter}-story`;
    const battle = `chapter-${chapter}-battle`;
    result.push({ id: story, type: 'story', presentation: `candidate-03/chapter-${chapter}`, next: battle });
    result.push({
      id: battle,
      type: 'battle',
      level: `c03-campaign-${chapter}`,
      rosterBindings: [{ campaignUnit: 'shen-li', levelUnitKey: 'campaign-hero' }],
      next: { victory: chapter === 7 ? 'ending' : `chapter-${chapter + 1}-story`, defeat: 'ending-failed', retreat: story },
      outcomeEffects: { victory: [{ type: 'addVariable', key: 'victories', amount: 1 }] },
    });
  }
  result.push(
    { id: 'ending', type: 'ending', outcome: 'completed', presentation: 'candidate-03/ending' },
    { id: 'ending-failed', type: 'ending', outcome: 'failed', presentation: 'candidate-03/ending-failed' },
  );
  return result;
}

/** Seven-chapter story contract; historical content never leaks into campaign infrastructure. */
export const CANDIDATE_03_CAMPAIGN_CONTRACT: CampaignDefinition = {
  schema: 1,
  id: 'candidate-03-founding-reign',
  version: 1,
  start: 'chapter-1-story',
  contentPacks: { 'empire.common': 1, 'candidate-03': 1 },
  initial: {
    variables: { victories: 0 },
    roster: [{ id: 'shen-li', unitType: 'soldier', owner: 1, tags: ['hero', 'commander'] }],
  },
  nodes: nodes(),
};

export const CANDIDATE_03_ASSET_PACK = 'final-ancient-china-v1';
