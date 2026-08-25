import { CampaignAggregate, createCampaignState } from './aggregate';
import { CampaignBattleBridge } from './battle-bridge';
import { createCampaignRuleServices, type CampaignRuleServices } from './rules';
import type { CampaignNodeAdvance } from './nodes';
import { CampaignActionError } from './errors';
import type {
  BattleRequest,
  BattleResult,
  CampaignDefinition,
  CampaignNode,
  CampaignState,
} from './types';

/** Application-facing deterministic campaign state machine. */
export class CampaignRuntime {
  readonly state: CampaignState;
  readonly definition: CampaignDefinition;
  private readonly aggregate: CampaignAggregate;
  private readonly campaignRules: CampaignRuleServices;

  constructor(
    definition: CampaignDefinition,
    state: CampaignState = createCampaignState(definition),
    rules: CampaignRuleServices = createCampaignRuleServices(),
  ) {
    this.definition = deepFreeze(structuredClone(definition));
    this.state = structuredClone(state);
    this.campaignRules = {
      conditions: rules.conditions.clone().seal(),
      effects: rules.effects.clone().seal(),
      nodes: rules.nodes.clone().seal(),
    };
    this.aggregate = new CampaignAggregate(this.definition, this.state, this.campaignRules);
  }

  node(): CampaignNode {
    return this.aggregate.node();
  }

  /** Advances non-interactive nodes; a kind that needs input says which API. */
  advance(): CampaignNode {
    return this.transaction(() => {
      this.requireActive();
      const node = this.node();
      this.campaignRules.nodes.advance(this.leaving(node), node);
      return this.node();
    });
  }

  /** What leaving one node is allowed to do, handed to that node's handler. */
  private leaving(node: CampaignNode): CampaignNodeAdvance {
    return {
      apply: (effects) => this.aggregate.apply(effects),
      moveTo: (next) => this.aggregate.moveTo(next),
      settle: (status) => {
        this.completeNode(node.id);
        this.state.status = status;
      },
      needs: (api) => { throw new CampaignActionError(`${node.type} node requires ${api}`); },
    };
  }

  choices(): Array<{ id: string; next: string }> {
    const node = this.node();
    if (node.type !== 'choice') return [];
    return node.choices
      .filter((choice) => !choice.condition || this.campaignRules.conditions.evaluate(this.state, choice.condition))
      .map((choice) => ({ id: choice.id, next: choice.next }));
  }

  choose(id: string): CampaignNode {
    return this.transaction(() => {
      this.requireActive();
      const node = this.node();
      if (node.type !== 'choice') throw new CampaignActionError('current campaign node is not a choice');
      const selected = node.choices.find((choice) => choice.id === id);
      if (!selected) throw new CampaignActionError(`unknown choice "${id}"`);
      if (selected.condition && !this.campaignRules.conditions.evaluate(this.state, selected.condition)) {
        throw new CampaignActionError(`choice "${id}" is not currently available`);
      }
      this.aggregate.apply(node.effects);
      this.aggregate.apply(selected.effects);
      this.aggregate.moveTo(selected.next);
      return this.node();
    });
  }

  beginBattle(bridge: CampaignBattleBridge): BattleRequest {
    return this.transaction(() => {
      this.requireActive();
      const node = this.node();
      if (node.type !== 'battle') throw new CampaignActionError('current campaign node is not a battle');
      this.aggregate.apply(node.effects);
      const request = bridge.prepare(this.definition, this.state);
      this.state.battleSequence++;
      this.state.pendingBattle = { requestId: request.id, node: node.id, level: node.level };
      return request;
    });
  }

  completeBattle(result: BattleResult): CampaignNode | null {
    return this.transaction(() => {
      this.requireActive();
      const pending = this.state.pendingBattle;
      const node = this.node();
      if (!pending || node.type !== 'battle') throw new CampaignActionError('campaign has no pending battle');
      if (pending.requestId !== result.requestId || pending.node !== node.id) throw new CampaignActionError('battle result does not match pending request');
      this.aggregate.projectBattleResult(result);
      this.aggregate.apply(node.outcomeEffects?.[result.outcome]);
      this.state.battleHistory.push({
        requestId: result.requestId,
        node: node.id,
        level: node.level,
        outcome: result.outcome,
        turns: result.turns,
        signals: result.signals.slice(),
      });
      this.state.pendingBattle = null;
      const next = node.next[result.outcome];
      if (!next) {
        this.completeNode(node.id);
        this.state.status = result.outcome === 'victory' ? 'completed' : 'failed';
        return null;
      }
      this.aggregate.moveTo(next);
      return this.node();
    });
  }

  snapshot(): CampaignState {
    return structuredClone(this.state);
  }

  private requireActive(): void {
    if (this.state.status !== 'active') throw new CampaignActionError(`campaign is ${this.state.status}`);
  }

  private completeNode(id: string): void {
    if (!this.state.completedNodes.includes(id)) this.state.completedNodes.push(id);
  }

  private transaction<T>(operation: () => T): T {
    const before = structuredClone(this.state);
    try {
      return operation();
    } catch (error) {
      for (const key of Object.keys(this.state) as Array<keyof CampaignState>) delete this.state[key];
      Object.assign(this.state, before);
      throw error;
    }
  }
}

function deepFreeze<T>(value: T, seen = new Set<object>()): T {
  if (typeof value !== 'object' || value === null || seen.has(value)) return value;
  seen.add(value);
  for (const nested of Object.values(value)) deepFreeze(nested, seen);
  return Object.freeze(value);
}
