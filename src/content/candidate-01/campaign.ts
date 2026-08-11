import type { BattleRequest, BattleResult, CampaignDefinition, CampaignEffect, CampaignNode, CampaignState } from '../../campaign';
import { CANDIDATE_01_LEVELS, CANDIDATE_01_ROSTER_BINDINGS } from './levels';

interface ChoiceSpec {
  before: number;
  id: string;
  options: Array<{ id: string; effects: CampaignEffect[] }>;
}

const CHOICES: readonly ChoiceSpec[] = [
  {
    before: 1, id: 'first-command', options: [
      { id: 'rush-north-hill', effects: [{ type: 'setFlag', flag: 'twin_hills_rushed' }, { type: 'addVariable', key: 'boldness', amount: 1 }] },
      { id: 'steady-advance', effects: [{ type: 'setFlag', flag: 'twin_hills_steady' }, { type: 'addVariable', key: 'restraint', amount: 1 }] },
    ],
  },
  {
    before: 2, id: 'bridge-truce', options: [
      { id: 'accept-truce', effects: [{ type: 'setFlag', flag: 'accepted_bridge_truce' }, { type: 'changeRelation', faction: 'cain', amount: 1 }] },
      { id: 'no-formal-promise', effects: [{ type: 'setFlag', flag: 'bridge_no_promise' }, { type: 'addVariable', key: 'independence', amount: 1 }] },
    ],
  },
  {
    before: 4, id: 'black-camp-plan', options: [
      { id: 'rescue-and-evidence', effects: [{ type: 'setFlag', flag: 'black_camp_rescue' }, { type: 'addVariable', key: 'witnesses', amount: 2 }] },
      { id: 'split-the-force', effects: [{ type: 'setFlag', flag: 'black_camp_split' }, { type: 'addVariable', key: 'evidence', amount: 2 }] },
    ],
  },
  {
    before: 6, id: 'white-river-priority', options: [
      { id: 'people-first', effects: [{ type: 'setFlag', flag: 'white_river_people_first' }, { type: 'changeRelation', faction: 'refugees', amount: 2 }] },
      { id: 'save-supplies', effects: [{ type: 'setFlag', flag: 'white_river_supplies_first' }, { type: 'addResource', resource: 'supplies', amount: 3 }] },
    ],
  },
  {
    before: 8, id: 'mercenary-contract', options: [
      { id: 'pay-what-is-owed', effects: [{ type: 'setFlag', flag: 'mercenary_debt_paid' }, { type: 'addResource', resource: 'treasury', amount: -2 }, { type: 'changeRelation', faction: 'tasha', amount: 2 }] },
      { id: 'create-pension-ledger', effects: [{ type: 'setFlag', flag: 'pension_ledger_created' }, { type: 'addVariable', key: 'institutions', amount: 1 }, { type: 'changeRelation', faction: 'tasha', amount: 1 }] },
    ],
  },
  {
    before: 9, id: 'free-ivra', options: [
      { id: 'mirelle-ritual', effects: [{ type: 'setFlag', flag: 'ivra_freed_by_ritual' }, { type: 'changeRelation', faction: 'mirelle', amount: 1 }] },
      { id: 'break-chain-nodes', effects: [{ type: 'setFlag', flag: 'ivra_freed_by_engineering' }, { type: 'changeRelation', faction: 'tasha', amount: 1 }] },
    ],
  },
  {
    before: 10, id: 'old-banner-terms', options: [
      { id: 'offer-exit-right', effects: [{ type: 'setFlag', flag: 'old_soldiers_may_leave' }, { type: 'addVariable', key: 'institutions', amount: 1 }] },
      { id: 'demand-surrender', effects: [{ type: 'setFlag', flag: 'old_soldiers_must_surrender' }, { type: 'addVariable', key: 'authority', amount: 1 }] },
    ],
  },
  {
    before: 12, id: 'bell-tower-control', options: [
      { id: 'joint-control-with-cain', effects: [{ type: 'setFlag', flag: 'bell_tower_joint_control' }, { type: 'changeRelation', faction: 'cain', amount: 2 }] },
      { id: 'race-for-the-fragment', effects: [{ type: 'setFlag', flag: 'bell_tower_fragment_race' }, { type: 'addVariable', key: 'crown_fragments', amount: 1 }] },
    ],
  },
  {
    before: 14, id: 'forge-record', options: [
      { id: 'publish-the-record', effects: [{ type: 'setFlag', flag: 'forge_record_public' }, { type: 'addVariable', key: 'institutions', amount: 1 }, { type: 'changeRelation', faction: 'mountain-forge', amount: -1 }] },
      { id: 'seal-dangerous-pages', effects: [{ type: 'setFlag', flag: 'forge_record_sealed' }, { type: 'changeRelation', faction: 'mountain-forge', amount: 1 }] },
    ],
  },
  {
    before: 15, id: 'silverwood-price', options: [
      { id: 'end-cost-transfer', effects: [{ type: 'setFlag', flag: 'silverwood_end_transfer' }, { type: 'changeRelation', faction: 'silverwood', amount: -1 }] },
      { id: 'phase-out-gradually', effects: [{ type: 'setFlag', flag: 'silverwood_compromise' }, { type: 'changeRelation', faction: 'silverwood', amount: 1 }, { type: 'addVariable', key: 'institutions', amount: 1 }] },
    ],
  },
  {
    before: 16, id: 'unflagged-personhood', options: [
      { id: 'recognize-a-new-person', effects: [{ type: 'setFlag', flag: 'unflagged_recognized' }, { type: 'changeRelation', faction: 'named-dead', amount: 2 }] },
      { id: 'protect-until-heard', effects: [{ type: 'setFlag', flag: 'unflagged_protected' }, { type: 'changeRelation', faction: 'named-dead', amount: 1 }, { type: 'addVariable', key: 'restraint', amount: 1 }] },
    ],
  },
];

const choiceBefore = new Map(CHOICES.map((choice) => [choice.before, choice]));

function nextBrief(order: number): string {
  return order === CANDIDATE_01_LEVELS.length ? 'chapter-three-ending' : `brief-${String(order + 1).padStart(2, '0')}`;
}

function buildNodes(): CampaignNode[] {
  const nodes: CampaignNode[] = [{ id: 'prologue', type: 'story', presentation: 'c01/prologue', next: 'brief-01' }];
  for (const level of CANDIDATE_01_LEVELS) {
    const order = Number(level.extra?.order);
    const suffix = String(order).padStart(2, '0');
    const choice = choiceBefore.get(order);
    const battle = `battle-${suffix}`;
    nodes.push({ id: `brief-${suffix}`, type: 'story', presentation: `c01/brief-${suffix}`, next: choice ? `choice-${choice.id}` : battle });
    if (choice) {
      nodes.push({
        id: `choice-${choice.id}`,
        type: 'choice',
        presentation: `c01/choice-${choice.id}`,
        choices: choice.options.map((option) => ({ id: option.id, next: battle, effects: option.effects })),
      });
    }
    const chapterFinale = level.extra?.chapterFinale === true;
    const victoryEffects: CampaignEffect[] = [
      { type: 'addVariable', key: 'victories', amount: 1 },
      { type: 'addResource', resource: 'supplies', amount: 1 },
      { type: 'setFlag', flag: `battle_${suffix}_won` },
    ];
    if (chapterFinale) victoryEffects.push({ type: 'setFlag', flag: `chapter_${level.extra?.chapter}_complete` });
    nodes.push({
      id: battle,
      type: 'battle',
      level: level.id,
      rosterBindings: CANDIDATE_01_ROSTER_BINDINGS[level.id].map((binding) => ({ ...binding })),
      next: { victory: `aftermath-${suffix}`, defeat: 'campaign-failed', retreat: `brief-${suffix}` },
      outcomeEffects: { victory: victoryEffects },
    });
    nodes.push({ id: `aftermath-${suffix}`, type: 'story', presentation: `c01/aftermath-${suffix}`, next: nextBrief(order) });
  }
  nodes.push(
    { id: 'chapter-three-ending', type: 'ending', outcome: 'completed', presentation: 'c01/chapter-three-ending' },
    { id: 'campaign-failed', type: 'ending', outcome: 'failed', presentation: 'c01/campaign-failed' },
  );
  return nodes;
}

export const CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN: CampaignDefinition = {
  schema: 1,
  id: 'candidate-01-gray-banner-chapters-1-3',
  version: 1,
  start: 'prologue',
  contentPacks: { 'empire.common': 1, 'empire.ancient-empires': 1, 'candidate-01': 1 },
  initial: {
    variables: { victories: 0, boldness: 0, restraint: 0, institutions: 0, authority: 0, evidence: 0, witnesses: 0, crown_fragments: 0 },
    resources: { supplies: 5, treasury: 5 },
    relations: { cain: 0, mirelle: 0, tasha: 0, refugees: 0, silverwood: 0, 'mountain-forge': 0, 'named-dead': 0 },
    roster: [
      { id: 'laiya', unitType: 'c01.laiya', owner: 1, rank: 0, tags: ['hero', 'commander'] },
      { id: 'torren', unitType: 'c01.swordsman', owner: 1, rank: 0, tags: ['retinue', 'infantry'] },
      { id: 'elin', unitType: 'c01.archer', owner: 1, rank: 0, tags: ['retinue', 'ranged'] },
      { id: 'mirelle', unitType: 'c01.mirelle', owner: 1, rank: 0, tags: ['hero', 'support'] },
      { id: 'bran', unitType: 'c01.bran', owner: 1, rank: 0, tags: ['hero', 'scout'] },
      { id: 'tasha', unitType: 'c01.tasha', owner: 1, rank: 0, tags: ['hero', 'engineer'] },
      { id: 'ivra', unitType: 'c01.ivra', owner: 1, rank: 0, tags: ['hero', 'dragon'] },
    ],
  },
  nodes: buildNodes(),
};

/**
 * Candidate-specific context policy.  The campaign bridge remains generic;
 * this adapter is the only place where story decisions tune a battle snapshot.
 */
export function applyCandidate01BattleContext(request: BattleRequest, state: CampaignState): BattleRequest {
  const result = structuredClone(request);
  const player = result.level.players.find((entry) => entry.id === 1);
  if (player) {
    const supplies = Math.max(0, state.resources.supplies ?? 0);
    const treasury = Math.max(0, state.resources.treasury ?? 0);
    const account = player.resources.funds;
    if (account) account.current = 180 + supplies * 30 + treasury * 20;
  }

  if (result.levelId === 'c01-01' && state.flags.includes('twin_hills_rushed')) {
    const torren = result.level.units.find((unit) => unit.key === 'campaign-torren');
    if (torren) Object.assign(torren, { x: 4, y: 0, hp: Math.min(torren.hp ?? 98, 72) });
  }
  if (result.levelId === 'c01-02' && state.flags.includes('bridge_no_promise') && result.level.scenario) {
    result.level.scenario.engagementRules = [];
    const enemy = result.level.players.find((entry) => entry.id === 2);
    if (enemy?.ai) enemy.ai.aggression = 0.68;
  }
  if (result.levelId === 'c01-09' && state.flags.includes('ivra_freed_by_ritual')) {
    for (const structure of result.level.structures ?? []) structure.hp = 72;
  }
  if (result.levelId === 'c01-12' && state.flags.includes('bell_tower_joint_control')) {
    const cain = result.level.units.find((unit) => unit.key === 'imperial-cain');
    if (cain) cain.owner = 1;
  }
  if (result.levelId === 'c01-15' && state.flags.includes('silverwood_compromise')) {
    const druid = result.level.units.find((unit) => unit.key === 'controlled-druid');
    if (druid) druid.owner = 3;
  }
  result.level.extra = { ...(result.level.extra ?? {}), campaignContextApplied: true };
  return result;
}

/**
 * Story-one treats named protagonists as wounded/forced off the field on a
 * victorious route.  Generic retainers still use true permanent disposition,
 * so the campaign preserves consequence without invalidating later authored
 * scenes that require their viewpoint character.
 */
export function applyCandidate01BattleResultPolicy(result: BattleResult, state: CampaignState): BattleResult {
  const projected = structuredClone(result);
  for (const unit of projected.units) {
    const campaign = state.roster[unit.campaignUnit];
    if (!campaign?.tags?.includes('hero') || unit.disposition === 'available') continue;
    unit.disposition = 'available';
    unit.hpRatio = Math.max(0.3, unit.hpRatio);
    unit.moraleRatio = Math.max(0.4, unit.moraleRatio);
    projected.signals.push(`hero_wounded:${unit.campaignUnit}`);
  }
  return projected;
}
