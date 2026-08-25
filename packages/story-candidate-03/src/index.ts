import type { CampaignDefinition } from '@empire/campaign-engine';

/**
 * A campaign that branches, so the node algebra is shown doing something.
 *
 * This used to be candidate-02's generator with the strings changed: the same
 * `story → battle` links in a line, the same single failure ending, the same
 * sixteen nodes. Two copies of one shape are not two kinds of story — and the
 * suite that puts all three shipped contracts through the engine says in its own
 * comment that three copies of one contract "would pass everything above and prove
 * nothing". Two of the three were, in every way but their names.
 *
 * So this one is written out rather than generated, and it is shaped like the thing
 * a line cannot be: the oath forks, both branches rejoin, the siege forks again
 * with one arm reaching the capital directly and the other going the long way
 * round. That exercises `choice`, which no contract but candidate-01 used, and it
 * gives the graph what a chain has not — a node with two ways in and a node with
 * two ways out.
 */
const HERO = 'shen-li';

/** One battle of this campaign: the hero leads it, and losing scatters the host. */
const battle = (id: string, level: string, victory: string) => ({
  id,
  type: 'battle' as const,
  level,
  rosterBindings: [{ campaignUnit: HERO, levelUnitKey: 'campaign-hero' }],
  next: { victory, defeat: 'ending-scattered', retreat: 'oath' },
  outcomeEffects: { victory: [{ type: 'addVariable' as const, key: 'victories', amount: 1 }] },
});

/** Branching story contract; historical content never leaks into campaign infrastructure. */
export const CANDIDATE_03_CAMPAIGN_CONTRACT: CampaignDefinition = {
  schema: 1,
  id: 'candidate-03-founding-reign',
  version: 1,
  start: 'opening',
  contentPacks: { 'empire.common': 1, 'candidate-03': 1 },
  initial: {
    variables: { victories: 0 },
    roster: [{ id: HERO, unitType: 'soldier', owner: 1, tags: ['hero', 'commander'] }],
  },
  nodes: [
    { id: 'opening', type: 'story', presentation: 'candidate-03/opening', next: 'oath' },
    {
      id: 'oath',
      type: 'choice',
      presentation: 'candidate-03/oath',
      choices: [
        { id: 'march-north', next: 'north', effects: [{ type: 'addVariable', key: 'boldness', amount: 1 }] },
        { id: 'hold-the-pass', next: 'pass' },
      ],
    },
    battle('north', 'c03-northern-road', 'reunion'),
    battle('pass', 'c03-stone-pass', 'reunion'),
    // Two ways in, which is the thing a chain cannot have.
    { id: 'reunion', type: 'story', presentation: 'candidate-03/reunion', next: 'siege' },
    {
      id: 'siege',
      type: 'choice',
      presentation: 'candidate-03/siege',
      choices: [
        { id: 'storm-the-gate', next: 'capital' },
        { id: 'starve-the-city', next: 'blockade', effects: [{ type: 'addVariable', key: 'patience', amount: 1 }] },
      ],
    },
    // The long way round rejoins the short one, so the capital has two ways in too.
    battle('blockade', 'c03-river-blockade', 'winter'),
    { id: 'winter', type: 'story', presentation: 'candidate-03/winter', next: 'capital' },
    battle('capital', 'c03-white-capital', 'crowning'),
    { id: 'crowning', type: 'story', presentation: 'candidate-03/crowning', next: 'ending-crowned' },
    { id: 'ending-crowned', type: 'ending', outcome: 'completed', presentation: 'candidate-03/ending-crowned' },
    { id: 'ending-scattered', type: 'ending', outcome: 'failed', presentation: 'candidate-03/ending-scattered' },
  ],
};
