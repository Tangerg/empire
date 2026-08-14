import type {
  CareerId,
  LevelData,
  PlayerId,
  ResourceAccounts,
  ScenarioValue,
  UnitRank,
  UnitTypeId,
} from '@empire/battle-engine';

export type CampaignNodeId = string;
export type CampaignUnitId = string;
export type CampaignOutcome = 'victory' | 'defeat' | 'retreat';
export type CampaignStatus = 'active' | 'completed' | 'failed';
export type RosterDisposition = 'available' | 'fallen' | 'routed' | 'surrendered' | 'missing';

/** Open campaign predicate algebra; feature packages may declaration-merge it. */
export interface CampaignConditionKindMap {
  flag: { type: 'flag'; flag: string; present?: boolean };
  variable: { type: 'variable'; key: string; op: 'eq' | 'neq' | 'gte' | 'lte'; value: ScenarioValue };
  resource: { type: 'resource'; resource: string; op: 'gte' | 'lte'; value: number };
  relation: { type: 'relation'; faction: string; op: 'gte' | 'lte'; value: number };
  roster: { type: 'roster'; unit: CampaignUnitId; disposition?: RosterDisposition };
  all: { type: 'all'; conditions: CampaignCondition[] };
  any: { type: 'any'; conditions: CampaignCondition[] };
  not: { type: 'not'; condition: CampaignCondition };
}

export type CampaignCondition = CampaignConditionKindMap[keyof CampaignConditionKindMap];

/** Open state-transition algebra, deliberately free of story-specific effect names. */
export interface CampaignEffectKindMap {
  setVariable: { type: 'setVariable'; key: string; value: ScenarioValue };
  addVariable: { type: 'addVariable'; key: string; amount: number };
  setFlag: { type: 'setFlag'; flag: string };
  clearFlag: { type: 'clearFlag'; flag: string };
  addResource: { type: 'addResource'; resource: string; amount: number };
  changeRelation: { type: 'changeRelation'; faction: string; amount: number };
  setFeature: { type: 'setFeature'; feature: string; enabled: boolean };
  setRosterDisposition: { type: 'setRosterDisposition'; unit: CampaignUnitId; disposition: RosterDisposition };
}

export type CampaignEffect = CampaignEffectKindMap[keyof CampaignEffectKindMap];

export interface CampaignChoice {
  id: string;
  next: CampaignNodeId;
  condition?: CampaignCondition;
  effects?: CampaignEffect[];
}

interface CampaignNodeBase {
  id: CampaignNodeId;
  effects?: CampaignEffect[];
  /** Opaque presentation locator. The campaign engine never parses prose. */
  presentation?: string;
  tags?: string[];
}

/** A node that simply leads somewhere once its effects have landed. */
interface CampaignPassage extends CampaignNodeBase {
  next: CampaignNodeId;
}

/**
 * Open node algebra, like the campaign's predicates and effects already were.
 *
 * Node kinds were the one closed vocabulary left in this layer, and their
 * behaviour was spread over the definition validator and four runtime methods —
 * so a story pack could add a predicate and an effect, but not a shop, a
 * barracks, or a dialogue with a skill check. A pack declaration-merges its kind
 * here and registers the handler that runs it.
 */
export interface CampaignNodeKindMap {
  story: CampaignPassage & { type: 'story' };
  hub: CampaignPassage & { type: 'hub' };
  travel: CampaignPassage & { type: 'travel' };
  choice: CampaignNodeBase & { type: 'choice'; choices: CampaignChoice[] };
  battle: CampaignNodeBase & {
    type: 'battle';
    level: string;
    /** Player whose team defines campaign victory/defeat. Defaults to 1. */
    perspectivePlayer?: PlayerId;
    rosterBindings?: Array<{ campaignUnit: CampaignUnitId; levelUnitKey: string }>;
    next: Partial<Record<CampaignOutcome, CampaignNodeId>>;
    outcomeEffects?: Partial<Record<CampaignOutcome, CampaignEffect[]>>;
  };
  ending: CampaignNodeBase & { type: 'ending'; outcome: 'completed' | 'failed' };
}

export type CampaignNodeKind = Extract<keyof CampaignNodeKindMap, string>;
export type CampaignNode = CampaignNodeKindMap[CampaignNodeKind];

export interface CampaignRosterSeed {
  id: CampaignUnitId;
  unitType: UnitTypeId;
  owner: PlayerId;
  rank?: UnitRank;
  rankProgress?: number;
  resources?: ResourceAccounts;
  career?: CareerId | null;
  unlockedCareers?: CareerId[];
  careerMastery?: Record<CareerId, number>;
  learnedAbilities?: string[];
  tags?: string[];
  meta?: Record<string, ScenarioValue>;
}

export interface CampaignDefinition {
  schema: 1;
  id: string;
  version: number;
  start: CampaignNodeId;
  contentPacks: Record<string, number>;
  nodes: CampaignNode[];
  initial?: {
    flags?: string[];
    variables?: Record<string, ScenarioValue>;
    resources?: Record<string, number>;
    relations?: Record<string, number>;
    features?: string[];
    roster?: CampaignRosterSeed[];
  };
}

export interface CampaignUnitState extends CampaignRosterSeed {
  disposition: RosterDisposition;
  hpRatio: number;
  moraleRatio: number;
}

export interface CampaignBattleRecord {
  requestId: string;
  node: CampaignNodeId;
  level: string;
  outcome: CampaignOutcome;
  turns: number;
  signals: string[];
}

export interface PendingBattle {
  requestId: string;
  node: CampaignNodeId;
  level: string;
}

export interface CampaignState {
  definitionId: string;
  definitionVersion: number;
  currentNode: CampaignNodeId;
  status: CampaignStatus;
  flags: string[];
  variables: Record<string, ScenarioValue>;
  resources: Record<string, number>;
  relations: Record<string, number>;
  features: string[];
  roster: Record<CampaignUnitId, CampaignUnitState>;
  completedNodes: CampaignNodeId[];
  battleHistory: CampaignBattleRecord[];
  pendingBattle: PendingBattle | null;
  battleSequence: number;
}

export interface BattleRequest {
  id: string;
  campaignId: string;
  node: CampaignNodeId;
  levelId: string;
  perspectivePlayer: PlayerId;
  /** Ready-to-run immutable-by-contract level snapshot. */
  level: LevelData;
  rosterBindings: Array<{ campaignUnit: CampaignUnitId; levelUnitKey: string }>;
  context: {
    flags: string[];
    variables: Record<string, ScenarioValue>;
    features: string[];
  };
}

export interface BattleUnitResult {
  campaignUnit: CampaignUnitId;
  disposition: RosterDisposition;
  hpRatio: number;
  moraleRatio: number;
  unitType: UnitTypeId;
  rank: UnitRank;
  rankProgress: number;
  resources: ResourceAccounts;
  career: CareerId | null;
  unlockedCareers: CareerId[];
  careerMastery: Record<CareerId, number>;
  learnedAbilities: string[];
}

export interface BattleResult {
  requestId: string;
  outcome: CampaignOutcome;
  winnerTeam: number | null;
  reason: string;
  turns: number;
  units: BattleUnitResult[];
  signals: string[];
  eventCounts: Record<string, number>;
}

export type CampaignLevelResolver = (levelId: string) => LevelData;
