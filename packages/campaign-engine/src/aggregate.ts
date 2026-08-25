import type {
  BattleResult,
  CampaignDefinition,
  CampaignEffect,
  CampaignNode,
  CampaignRosterSeed,
  CampaignState,
  CampaignUnitState,
} from './types';
import type { CampaignRuleServices } from './rules';
import type { CampaignNodeInspection } from './nodes';
import { CampaignInvariantError } from './errors';

function unique(values: readonly string[] = []): string[] {
  return [...new Set(values)];
}

const scenarioValueIsValid = (value: unknown): boolean =>
  ['string', 'boolean'].includes(typeof value) ||
  (typeof value === 'number' && Number.isFinite(value));

function validateNamedNumbers(values: Readonly<Record<string, number>>, owner: string): void {
  if (Object.entries(values).some(([key, value]) => !key.trim() || !Number.isFinite(value))) {
    throw new CampaignInvariantError(`campaign ${owner} contains an invalid entry`);
  }
}

function validateRosterUnit(unit: CampaignUnitState | CampaignRosterSeed): void {
  if (!unit.id.trim() || !unit.unitType.trim() || !Number.isInteger(unit.owner) || unit.owner < 1) {
    throw new CampaignInvariantError(`campaign roster unit "${unit.id}" has invalid identity`);
  }
  if (unit.rank !== undefined && ![0, 1, 2].includes(unit.rank) ||
    unit.rankProgress !== undefined && (!Number.isFinite(unit.rankProgress) || unit.rankProgress < 0)) {
    throw new CampaignInvariantError(`campaign roster unit "${unit.id}" has invalid rank progress`);
  }
  for (const [id, account] of Object.entries(unit.resources ?? {})) {
    if (!id.trim() || !Number.isFinite(account.current) || account.current < 0 ||
      account.capacity !== null && (!Number.isFinite(account.capacity) || account.capacity < account.current)) {
      throw new CampaignInvariantError(`campaign roster unit "${unit.id}" has invalid resource "${id}"`);
    }
  }
  const namedLists = [unit.unlockedCareers, unit.learnedAbilities, unit.tags].filter(Boolean) as string[][];
  if (namedLists.some((values) => values.some((value) => !value.trim()) || new Set(values).size !== values.length)) {
    throw new CampaignInvariantError(`campaign roster unit "${unit.id}" has duplicate or empty names`);
  }
  validateNamedNumbers(unit.careerMastery ?? {}, `roster unit "${unit.id}" career mastery`);
}

/**
 * What a campaign document has to be, before anyone asks what its nodes mean.
 *
 * Identity, node ids, the start node and the roster: the facts every kind of
 * node is written against. What one *kind* of node must declare is the node
 * handler's business, and `CampaignAggregate` asks it — that half used to be a
 * four-armed ladder here, which is why a pack could not add a node kind.
 */
export function validateCampaignDefinition(definition: CampaignDefinition): void {
  if (definition.schema !== 1) throw new CampaignInvariantError(`unsupported campaign schema ${definition.schema}`);
  if (!definition.id.trim()) throw new CampaignInvariantError('campaign id cannot be empty');
  if (!Number.isInteger(definition.version) || definition.version < 1) throw new CampaignInvariantError('campaign version must be positive');
  for (const [id, version] of Object.entries(definition.contentPacks)) {
    if (!id.trim() || !Number.isInteger(version) || version < 1) {
      throw new CampaignInvariantError(`campaign content pack "${id}" has invalid version`);
    }
  }
  const nodes = new Set<string>();
  for (const node of definition.nodes) {
    if (!node.id.trim()) throw new CampaignInvariantError('campaign node id cannot be empty');
    if (nodes.has(node.id)) throw new CampaignInvariantError(`duplicate campaign node "${node.id}"`);
    nodes.add(node.id);
  }
  if (!nodes.has(definition.start)) throw new CampaignInvariantError(`unknown campaign start node "${definition.start}"`);
  const roster = new Set<string>();
  for (const unit of definition.initial?.roster ?? []) {
    if (!unit.id.trim() || roster.has(unit.id)) throw new CampaignInvariantError(`duplicate or empty roster unit "${unit.id}"`);
    validateRosterUnit(unit);
    roster.add(unit.id);
  }
  for (const [key, value] of Object.entries(definition.initial?.variables ?? {})) {
    if (!key.trim() || !scenarioValueIsValid(value)) {
      throw new CampaignInvariantError(`campaign initial variable "${key}" is invalid`);
    }
  }
  validateNamedNumbers(definition.initial?.resources ?? {}, 'initial resources');
  validateNamedNumbers(definition.initial?.relations ?? {}, 'initial relations');
}

/** The document's own names, for the node kinds to check their references against. */
function inspectionOf(definition: CampaignDefinition): CampaignNodeInspection {
  const nodes = new Set(definition.nodes.map((node) => node.id));
  const roster = new Set((definition.initial?.roster ?? []).map((unit) => unit.id));
  return {
    definition,
    hasNode: (id) => nodes.has(id),
    hasRosterUnit: (id) => roster.has(id),
    reject: (message) => { throw new CampaignInvariantError(message); },
  };
}

export function createCampaignState(definition: CampaignDefinition): CampaignState {
  validateCampaignDefinition(definition);
  const roster: Record<string, CampaignUnitState> = {};
  for (const seed of definition.initial?.roster ?? []) {
    roster[seed.id] = {
      ...structuredClone(seed),
      disposition: 'available',
      hpRatio: 1,
      moraleRatio: 1,
    };
  }
  return {
    definitionId: definition.id,
    definitionVersion: definition.version,
    currentNode: definition.start,
    status: 'active',
    flags: unique(definition.initial?.flags),
    variables: { ...(definition.initial?.variables ?? {}) },
    resources: { ...(definition.initial?.resources ?? {}) },
    relations: { ...(definition.initial?.relations ?? {}) },
    features: unique(definition.initial?.features),
    roster,
    completedNodes: [],
    battleHistory: [],
    pendingBattle: null,
    battleSequence: 0,
  };
}

export function validateCampaignState(definition: CampaignDefinition, state: CampaignState): void {
  if (state.definitionId !== definition.id || state.definitionVersion !== definition.version) {
    throw new CampaignInvariantError('campaign state does not match definition identity/version');
  }
  if (!definition.nodes.some((node) => node.id === state.currentNode)) {
    throw new CampaignInvariantError(`campaign state references unknown node "${state.currentNode}"`);
  }
  if (!['active', 'completed', 'failed'].includes(state.status)) throw new CampaignInvariantError('campaign state has invalid status');
  if (!Number.isInteger(state.battleSequence) || state.battleSequence < 0) throw new CampaignInvariantError('campaign battle sequence is invalid');
  for (const [owner, values] of [
    ['flag', state.flags],
    ['feature', state.features],
    ['completed node', state.completedNodes],
  ] as const) {
    if (values.some((value) => !value.trim()) || new Set(values).size !== values.length) {
      throw new CampaignInvariantError(`campaign state has duplicate or empty ${owner}`);
    }
  }
  const knownNodes = new Set(definition.nodes.map((node) => node.id));
  if (state.completedNodes.some((node) => !knownNodes.has(node))) {
    throw new CampaignInvariantError('campaign state references an unknown completed node');
  }
  for (const [owner, values] of [['resource', state.resources], ['relation', state.relations]] as const) {
    if (Object.entries(values).some(([key, value]) => !key.trim() || !Number.isFinite(value))) {
      throw new CampaignInvariantError(`campaign state has invalid ${owner} value`);
    }
  }
  for (const [key, value] of Object.entries(state.variables)) {
    if (!key.trim() || !scenarioValueIsValid(value)) {
      throw new CampaignInvariantError(`campaign state variable "${key}" is invalid`);
    }
  }
  const declaredRoster = new Set((definition.initial?.roster ?? []).map((unit) => unit.id));
  // Custom effects may recruit a new unit. Initial members, however, are
  // identity-bearing campaign entities and may change disposition, not vanish.
  if ([...declaredRoster].some((id) => !state.roster[id])) {
    throw new CampaignInvariantError('campaign state is missing a roster unit declared by the definition');
  }
  for (const [id, unit] of Object.entries(state.roster)) {
    if (unit.id !== id) throw new CampaignInvariantError(`campaign roster key mismatch for "${id}"`);
    validateRosterUnit(unit);
    if (!['available', 'fallen', 'routed', 'surrendered', 'missing'].includes(unit.disposition)) {
      throw new CampaignInvariantError(`campaign roster unit "${id}" has invalid disposition`);
    }
    if (!Number.isFinite(unit.hpRatio) || unit.hpRatio < 0 || unit.hpRatio > 1 ||
      !Number.isFinite(unit.moraleRatio) || unit.moraleRatio < 0 || unit.moraleRatio > 1) {
      throw new CampaignInvariantError(`campaign roster unit "${id}" has invalid battle ratios`);
    }
  }
  const requestIds = new Set<string>();
  for (const record of state.battleHistory) {
    const node = definition.nodes.find((candidate) => candidate.id === record.node);
    if (!record.requestId.trim() || requestIds.has(record.requestId) || node?.type !== 'battle' ||
      record.level !== node.level || !['victory', 'defeat', 'retreat'].includes(record.outcome) ||
      !Number.isInteger(record.turns) || record.turns < 0 || record.signals.some((signal) => !signal.trim())) {
      throw new CampaignInvariantError(`campaign battle record "${record.requestId}" is invalid`);
    }
    requestIds.add(record.requestId);
  }
  const current = definition.nodes.find((node) => node.id === state.currentNode);
  if (state.pendingBattle && (state.pendingBattle.node !== state.currentNode ||
    current?.type !== 'battle' || state.pendingBattle.level !== current.level ||
    !state.pendingBattle.requestId.trim() || !state.pendingBattle.level.trim() ||
    requestIds.has(state.pendingBattle.requestId))) {
    throw new CampaignInvariantError('campaign pending battle does not match current node');
  }
}

/** Rich aggregate: all cross-node and battle-result invariants live here. */
export class CampaignAggregate {
  private readonly nodes: ReadonlyMap<string, CampaignNode>;

  constructor(
    readonly definition: CampaignDefinition,
    readonly state: CampaignState,
    private readonly rules: CampaignRuleServices,
  ) {
    validateCampaignDefinition(definition);
    const inspection = inspectionOf(definition);
    for (const node of definition.nodes) rules.nodes.validate(inspection, node);
    validateCampaignState(definition, state);
    this.nodes = new Map(definition.nodes.map((node) => [node.id, node]));
  }

  node(): CampaignNode {
    const node = this.nodes.get(this.state.currentNode);
    if (!node) throw new CampaignInvariantError(`unknown current campaign node "${this.state.currentNode}"`);
    return node;
  }

  apply(effects: readonly CampaignEffect[] = []): void {
    for (const effect of effects) this.rules.effects.apply(this.state, effect);
  }

  moveTo(next: string, completed = this.state.currentNode): void {
    if (!this.nodes.has(next)) throw new CampaignInvariantError(`unknown campaign node "${next}"`);
    if (!this.state.completedNodes.includes(completed)) this.state.completedNodes.push(completed);
    this.state.currentNode = next;
  }

  projectBattleResult(result: BattleResult): void {
    for (const value of result.units) {
      const unit = this.state.roster[value.campaignUnit];
      if (!unit) continue;
      Object.assign(unit, {
        disposition: value.disposition,
        hpRatio: value.hpRatio,
        moraleRatio: value.moraleRatio,
        unitType: value.unitType,
        rank: value.rank,
        rankProgress: value.rankProgress,
        resources: structuredClone(value.resources),
        career: value.career,
        unlockedCareers: value.unlockedCareers.slice(),
        careerMastery: { ...value.careerMastery },
        learnedAbilities: value.learnedAbilities.slice(),
      });
    }
  }
}
