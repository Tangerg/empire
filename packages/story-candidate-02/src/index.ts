import type { CampaignDefinition, CampaignNode } from '@empire/campaign-engine';

function nodes(): CampaignNode[] {
  const result: CampaignNode[] = [];
  for (let chapter = 1; chapter <= 7; chapter++) {
    const story = `chapter-${chapter}-story`;
    const battle = `chapter-${chapter}-battle`;
    result.push({ id: story, type: 'story', presentation: `candidate-02/chapter-${chapter}`, next: battle });
    result.push({
      id: battle,
      type: 'battle',
      level: `c02-sector-${chapter}`,
      rosterBindings: [{ campaignUnit: 'stellar-lead', levelUnitKey: 'campaign-hero' }],
      next: { victory: chapter === 7 ? 'ending' : `chapter-${chapter + 1}-story`, defeat: 'ending-failed', retreat: story },
      outcomeEffects: { victory: [{ type: 'addVariable', key: 'victories', amount: 1 }] },
    });
  }
  result.push(
    { id: 'ending', type: 'ending', outcome: 'completed', presentation: 'candidate-02/ending' },
    { id: 'ending-failed', type: 'ending', outcome: 'failed', presentation: 'candidate-02/ending-failed' },
  );
  return result;
}

/** Seven-chapter story contract; concrete starship combat content remains isolated here. */
export const CANDIDATE_02_CAMPAIGN_CONTRACT: CampaignDefinition = {
  schema: 1,
  id: 'candidate-02-stellar-chronicle',
  version: 1,
  start: 'chapter-1-story',
  contentPacks: { 'empire.common': 1, 'candidate-02': 1 },
  initial: {
    variables: { victories: 0 },
    roster: [{ id: 'stellar-lead', unitType: 'soldier', owner: 1, tags: ['hero', 'pilot'] }],
  },
  nodes: nodes(),
};
