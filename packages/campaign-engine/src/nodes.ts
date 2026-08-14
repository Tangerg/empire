import { KeyedRegistry } from '@empire/battle-engine';
import type {
  CampaignDefinition,
  CampaignEffect,
  CampaignNode,
  CampaignNodeId,
  CampaignNodeKind,
  CampaignNodeKindMap,
  CampaignStatus,
  CampaignUnitId,
} from './types';

/** Leaving a node, and the only three things leaving one can mean. */
export interface CampaignNodeAdvance {
  apply(effects?: readonly CampaignEffect[]): void;
  moveTo(next: CampaignNodeId): void;
  /** The campaign is over, one way or the other. */
  settle(status: Exclude<CampaignStatus, 'active'>): void;
  /**
   * This kind cannot be left without something from the player. Naming the API
   * is the whole message: `advance()` used to know that a choice needs
   * `choose()` and a battle needs `beginBattle()`.
   */
  needs(api: string): never;
}

/** One campaign definition under inspection, for a node kind to check itself against. */
export interface CampaignNodeInspection {
  readonly definition: CampaignDefinition;
  hasNode(id: CampaignNodeId): boolean;
  hasRosterUnit(id: CampaignUnitId): boolean;
  reject(message: string): never;
}

export interface CampaignNodeHandler<K extends CampaignNodeKind = CampaignNodeKind> {
  readonly kind: K;
  /** What happens when the campaign leaves this node under its own power. */
  advance(context: CampaignNodeAdvance, node: CampaignNodeKindMap[K]): void;
  /** What this node's declaration has to satisfy. */
  validate?(inspection: CampaignNodeInspection, node: CampaignNodeKindMap[K]): void;
}

export class CampaignNodeRegistry extends KeyedRegistry<CampaignNodeKind, CampaignNodeHandler> {
  constructor() {
    super('campaign node handler');
  }

  protected keyOf(handler: CampaignNodeHandler): CampaignNodeKind {
    return handler.kind;
  }

  override register<K extends CampaignNodeKind>(handler: CampaignNodeHandler<K>): this {
    return super.register(handler as CampaignNodeHandler);
  }

  override replace<K extends CampaignNodeKind>(handler: CampaignNodeHandler<K>): this {
    return super.replace(handler as CampaignNodeHandler);
  }

  advance(context: CampaignNodeAdvance, node: CampaignNode): void {
    this.get(node.type).advance(context, node as never);
  }

  validate(inspection: CampaignNodeInspection, node: CampaignNode): void {
    const handler = this.tryGet(node.type);
    if (!handler) inspection.reject(`campaign node "${node.id}" has unknown type "${node.type}"`);
    handler.validate?.(inspection, node as never);
  }

  clone(): CampaignNodeRegistry {
    return this.copyInto(new CampaignNodeRegistry());
  }
}

/** Effects land, then the campaign moves on. Three kinds differ only in name. */
const passage = <K extends 'story' | 'hub' | 'travel'>(kind: K): CampaignNodeHandler<K> => ({
  kind,
  advance: (context, node) => {
    context.apply(node.effects);
    context.moveTo(node.next);
  },
  validate: (inspection, node) => {
    if (!inspection.hasNode(node.next)) {
      inspection.reject(`${node.id} references unknown campaign node "${node.next}"`);
    }
  },
});

export const DefaultCampaignNodes = new CampaignNodeRegistry()
  .register(passage('story'))
  .register(passage('hub'))
  .register(passage('travel'))
  .register<'choice'>({
    kind: 'choice',
    advance: (context) => context.needs('choose()'),
    validate: (inspection, node) => {
      if (node.choices.length === 0) inspection.reject(`choice node "${node.id}" has no choices`);
      const seen = new Set<string>();
      for (const choice of node.choices) {
        if (seen.has(choice.id)) inspection.reject(`duplicate choice "${node.id}:${choice.id}"`);
        seen.add(choice.id);
        if (!inspection.hasNode(choice.next)) {
          inspection.reject(`${node.id}:${choice.id} references unknown campaign node "${choice.next}"`);
        }
      }
    },
  })
  .register<'battle'>({
    kind: 'battle',
    advance: (context) => context.needs('beginBattle()'),
    validate: (inspection, node) => {
      if (!node.level.trim()) inspection.reject(`battle node "${node.id}" has no level`);
      for (const next of Object.values(node.next)) {
        if (next && !inspection.hasNode(next)) {
          inspection.reject(`${node.id} references unknown campaign node "${next}"`);
        }
      }
      const rosterIds = new Set<string>();
      const keys = new Set<string>();
      for (const binding of node.rosterBindings ?? []) {
        if (rosterIds.has(binding.campaignUnit) || keys.has(binding.levelUnitKey)) {
          inspection.reject(`battle node "${node.id}" has duplicate roster binding`);
        }
        rosterIds.add(binding.campaignUnit);
        keys.add(binding.levelUnitKey);
        if (!inspection.hasRosterUnit(binding.campaignUnit)) {
          inspection.reject(`battle node "${node.id}" binds unknown roster unit "${binding.campaignUnit}"`);
        }
      }
    },
  })
  .register<'ending'>({
    kind: 'ending',
    advance: (context, node) => {
      context.apply(node.effects);
      context.settle(node.outcome);
    },
  });
