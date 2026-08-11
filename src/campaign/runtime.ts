import { CampaignAggregate, createCampaignState } from './aggregate';
import { CampaignBattleBridge } from './battle-bridge';
import { createCampaignRuleServices, type CampaignRuleServices } from './rules';
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
  private readonly aggregate: CampaignAggregate;
  private readonly campaignRules: CampaignRuleServices;

  constructor(
    readonly definition: CampaignDefinition,
    state: CampaignState = createCampaignState(definition),
    rules: CampaignRuleServices = createCampaignRuleServices(),
  ) {
    this.state = structuredClone(state);
    this.campaignRules = rules;
    this.aggregate = new CampaignAggregate(definition, this.state, this.campaignRules);
  }

  node(): CampaignNode {
    return this.aggregate.node();
  }

  /** Advances non-interactive nodes; choice and battle nodes have explicit APIs. */
  advance(): CampaignNode {
    return this.transaction(() => {
      this.requireActive();
      const node = this.node();
      if (node.type === 'choice') throw new Error('choice node requires choose()');
      if (node.type === 'battle') throw new Error('battle node requires beginBattle()');
      this.aggregate.apply(node.effects);
      if (node.type === 'ending') {
        this.completeNode(node.id);
        this.state.status = node.outcome === 'completed' ? 'completed' : 'failed';
        return node;
      }
      this.aggregate.moveTo(node.next);
      return this.node();
    });
  }

  choices(): Array<{ id: string; next: string }> {
    const node = this.node();
    if (node.type !== 'choice') return [];
    return node.choices
      .filter((choice) => !choice.condition || this.rules().conditions.evaluate(this.state, choice.condition))
      .map((choice) => ({ id: choice.id, next: choice.next }));
  }

  choose(id: string): CampaignNode {
    return this.transaction(() => {
      this.requireActive();
      const node = this.node();
      if (node.type !== 'choice') throw new Error('current campaign node is not a choice');
      const selected = node.choices.find((choice) => choice.id === id);
      if (!selected) throw new Error(`unknown choice "${id}"`);
      if (selected.condition && !this.rules().conditions.evaluate(this.state, selected.condition)) {
        throw new Error(`choice "${id}" is not currently available`);
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
      if (node.type !== 'battle') throw new Error('current campaign node is not a battle');
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
      if (!pending || node.type !== 'battle') throw new Error('campaign has no pending battle');
      if (pending.requestId !== result.requestId || pending.node !== node.id) throw new Error('battle result does not match pending request');
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

  private rules(): CampaignRuleServices {
    return this.campaignRules;
  }

  private requireActive(): void {
    if (this.state.status !== 'active') throw new Error(`campaign is ${this.state.status}`);
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
