import type {
  BattleResult,
  CampaignDefinition,
  CampaignEffect,
  CampaignNode,
  CampaignState,
  CampaignUnitState,
} from './types';
import type { CampaignRuleServices } from './rules';

function unique(values: readonly string[] = []): string[] {
  return [...new Set(values)];
}

export function validateCampaignDefinition(definition: CampaignDefinition): void {
  if (definition.schema !== 1) throw new Error(`unsupported campaign schema ${definition.schema}`);
  if (!definition.id.trim()) throw new Error('campaign id cannot be empty');
  if (!Number.isInteger(definition.version) || definition.version < 1) throw new Error('campaign version must be positive');
  const nodes = new Map<string, CampaignNode>();
  for (const node of definition.nodes) {
    if (!node.id.trim()) throw new Error('campaign node id cannot be empty');
    if (nodes.has(node.id)) throw new Error(`duplicate campaign node "${node.id}"`);
    nodes.set(node.id, node);
  }
  if (!nodes.has(definition.start)) throw new Error(`unknown campaign start node "${definition.start}"`);
  const requireNode = (id: string, owner: string) => {
    if (!nodes.has(id)) throw new Error(`${owner} references unknown campaign node "${id}"`);
  };
  for (const node of definition.nodes) {
    if (node.type === 'story' || node.type === 'hub' || node.type === 'travel') requireNode(node.next, node.id);
    if (node.type === 'choice') {
      if (node.choices.length === 0) throw new Error(`choice node "${node.id}" has no choices`);
      const ids = new Set<string>();
      for (const choice of node.choices) {
        if (ids.has(choice.id)) throw new Error(`duplicate choice "${node.id}:${choice.id}"`);
        ids.add(choice.id);
        requireNode(choice.next, `${node.id}:${choice.id}`);
      }
    }
    if (node.type === 'battle') {
      if (!node.level.trim()) throw new Error(`battle node "${node.id}" has no level`);
      for (const next of Object.values(node.next)) if (next) requireNode(next, node.id);
      const rosterIds = new Set<string>();
      const keys = new Set<string>();
      for (const binding of node.rosterBindings ?? []) {
        if (rosterIds.has(binding.campaignUnit) || keys.has(binding.levelUnitKey)) {
          throw new Error(`battle node "${node.id}" has duplicate roster binding`);
        }
        rosterIds.add(binding.campaignUnit);
        keys.add(binding.levelUnitKey);
      }
    }
  }
  const roster = new Set<string>();
  for (const unit of definition.initial?.roster ?? []) {
    if (!unit.id.trim() || roster.has(unit.id)) throw new Error(`duplicate or empty roster unit "${unit.id}"`);
    roster.add(unit.id);
  }
  for (const node of definition.nodes) if (node.type === 'battle') {
    for (const binding of node.rosterBindings ?? []) if (!roster.has(binding.campaignUnit)) {
      throw new Error(`battle node "${node.id}" binds unknown roster unit "${binding.campaignUnit}"`);
    }
  }
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
    throw new Error('campaign state does not match definition identity/version');
  }
  if (!definition.nodes.some((node) => node.id === state.currentNode)) {
    throw new Error(`campaign state references unknown node "${state.currentNode}"`);
  }
  if (!['active', 'completed', 'failed'].includes(state.status)) throw new Error('campaign state has invalid status');
  if (!Number.isInteger(state.battleSequence) || state.battleSequence < 0) throw new Error('campaign battle sequence is invalid');
  for (const [id, unit] of Object.entries(state.roster)) {
    if (unit.id !== id) throw new Error(`campaign roster key mismatch for "${id}"`);
    if (!['available', 'fallen', 'routed', 'surrendered', 'missing'].includes(unit.disposition)) {
      throw new Error(`campaign roster unit "${id}" has invalid disposition`);
    }
    if (!Number.isFinite(unit.hpRatio) || unit.hpRatio < 0 || unit.hpRatio > 1 ||
      !Number.isFinite(unit.moraleRatio) || unit.moraleRatio < 0 || unit.moraleRatio > 1) {
      throw new Error(`campaign roster unit "${id}" has invalid battle ratios`);
    }
  }
  if (state.pendingBattle && (state.pendingBattle.node !== state.currentNode ||
    definition.nodes.find((node) => node.id === state.currentNode)?.type !== 'battle')) {
    throw new Error('campaign pending battle does not match current node');
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
    validateCampaignState(definition, state);
    this.nodes = new Map(definition.nodes.map((node) => [node.id, node]));
  }

  node(): CampaignNode {
    const node = this.nodes.get(this.state.currentNode);
    if (!node) throw new Error(`unknown current campaign node "${this.state.currentNode}"`);
    return node;
  }

  apply(effects: readonly CampaignEffect[] = []): void {
    for (const effect of effects) this.rules.effects.apply(this.state, effect);
  }

  moveTo(next: string, completed = this.state.currentNode): void {
    if (!this.nodes.has(next)) throw new Error(`unknown campaign node "${next}"`);
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
