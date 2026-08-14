import {
  normaliseLevel,
  teamOf,
  type ContentCatalog,
  type GameEvent,
  type GameState,
  type Unit,
} from '@empire/battle-engine';
import type {
  BattleRequest,
  BattleResult,
  BattleUnitResult,
  CampaignDefinition,
  CampaignLevelResolver,
  CampaignNode,
  CampaignState,
  CampaignUnitState,
  RosterDisposition,
} from './types';

function requireBattleNode(definition: CampaignDefinition, id: string): Extract<CampaignNode, { type: 'battle' }> {
  const node = definition.nodes.find((candidate) => candidate.id === id);
  if (!node || node.type !== 'battle') throw new Error(`campaign node "${id}" is not a battle`);
  return node;
}

function seedLevelUnit(
  level: ReturnType<typeof normaliseLevel>,
  levelUnitKey: string,
  campaign: CampaignUnitState,
  content: ContentCatalog,
): void {
  const index = level.units.findIndex((unit) => unit.key === levelUnitKey);
  if (index < 0) throw new Error(`level "${level.id}" has no unit key "${levelUnitKey}"`);
  if (campaign.disposition !== 'available') {
    level.units.splice(index, 1);
    return;
  }
  const unit = level.units[index];
  unit.unit = campaign.unitType;
  unit.owner = campaign.owner;
  unit.hp = Math.max(1, Math.round(content.units.get(campaign.unitType).maxHp * campaign.hpRatio));
  unit.morale = Math.max(1, Math.round((content.units.get(campaign.unitType).morale?.maximum ?? 100) * campaign.moraleRatio));
  unit.rank = campaign.rank ?? 0;
  unit.rankProgress = campaign.rankProgress ?? 0;
  unit.resources = structuredClone(campaign.resources ?? {});
  unit.career = campaign.career ?? undefined;
  unit.unlockedCareers = campaign.unlockedCareers?.slice();
  unit.careerMastery = { ...(campaign.careerMastery ?? {}) };
  unit.learnedAbilities = campaign.learnedAbilities?.slice();
}

/**
 * What one way of leaving the battlefield means to a roster.
 *
 * A marker kind is an open string in the battle engine, and this translation
 * was a four-arm ternary whose fallthrough reported every kind it did not
 * recognise as `fallen` — permanently dead. That is the most destructive answer
 * available, handed out by default to `transport-loss` and to any marker kind a
 * story pack invents. An unrecognised departure is `missing`: something happened
 * to that unit and the campaign does not know what.
 */
export const DEFAULT_MARKER_DISPOSITIONS: Readonly<Record<string, RosterDisposition>> = {
  corpse: 'fallen',
  'transport-loss': 'fallen',
  routed: 'routed',
  surrendered: 'surrendered',
  withdrawn: 'missing',
};

/** Anti-corruption layer from persistent campaign state to a battle snapshot. */
export class CampaignBattleBridge {
  constructor(
    private readonly resolveLevel: CampaignLevelResolver,
    /** Catalog the campaign is played against; never an ambient default. */
    private readonly content: ContentCatalog,
    /** How this campaign reads each way off the field; extend for a pack's own. */
    private readonly dispositions: Readonly<Record<string, RosterDisposition>> =
      DEFAULT_MARKER_DISPOSITIONS,
  ) {}

  prepare(definition: CampaignDefinition, state: CampaignState): BattleRequest {
    const node = requireBattleNode(definition, state.currentNode);
    if (state.pendingBattle) throw new Error(`campaign already has pending battle "${state.pendingBattle.requestId}"`);
    const level = normaliseLevel(structuredClone(this.resolveLevel(node.level)));
    const declaredBindings = (node.rosterBindings ?? []).map((binding) => ({ ...binding }));
    for (const binding of declaredBindings) {
      const campaign = state.roster[binding.campaignUnit];
      if (!campaign) throw new Error(`unknown campaign roster unit "${binding.campaignUnit}"`);
      seedLevelUnit(level, binding.levelUnitKey, campaign, this.content);
    }
    const bindings = declaredBindings.filter((binding) => state.roster[binding.campaignUnit].disposition === 'available');
    const id = `${definition.id}:${node.id}:${state.battleSequence + 1}`;
    return {
      id,
      campaignId: definition.id,
      node: node.id,
      levelId: node.level,
      perspectivePlayer: node.perspectivePlayer ?? 1,
      level,
      rosterBindings: bindings,
      context: {
        flags: state.flags.slice(),
        variables: { ...state.variables },
        features: state.features.slice(),
      },
    };
  }

  result(
    request: BattleRequest,
    state: GameState,
    events: readonly GameEvent[],
    outcome?: BattleResult['outcome'],
  ): BattleResult {
    const resolvedOutcome = outcome ?? (state.phase !== 'over'
      ? 'retreat'
      : state.winnerTeam !== null && state.winnerTeam === teamOf(state, request.perspectivePlayer)
        ? 'victory' : 'defeat');
    return {
      requestId: request.id,
      outcome: resolvedOutcome,
      winnerTeam: state.winnerTeam,
      reason: state.endReason,
      turns: state.turn,
      units: request.rosterBindings.map((binding) => this.projectUnit(state, binding.campaignUnit, binding.levelUnitKey)),
      signals: events.filter((event) => event.type === 'scenarioSignal').map((event) => event.signal),
      eventCounts: { ...state.scenario.eventCounts },
    };
  }

  private projectUnit(
    state: GameState,
    campaignUnit: string,
    levelUnitKey: string,
  ): BattleUnitResult {
    const active = state.units.find((unit) => unit.key === levelUnitKey) ??
      state.embarkedUnits.find((entry) => entry.unit.key === levelUnitKey)?.unit;
    const marker = state.markers.find((candidate) => candidate.fallenUnit?.key === levelUnitKey);
    const snapshot = active ?? marker?.fallenUnit;
    if (!snapshot) throw new Error(`battle result cannot resolve roster unit key "${levelUnitKey}"`);
    const disposition: RosterDisposition = active
      ? 'available'
      : (marker && this.dispositions[marker.kind]) ?? 'missing';
    return this.unitResult(campaignUnit, disposition, snapshot);
  }

  private unitResult(campaignUnit: string, disposition: RosterDisposition, unit: Unit): BattleUnitResult {
    const definition = this.content.units.get(unit.type);
    return {
      campaignUnit,
      disposition,
      hpRatio: Math.max(0, Math.min(1, unit.hp / definition.maxHp)),
      moraleRatio: Math.max(0, Math.min(1, unit.morale.current / unit.morale.maximum)),
      unitType: unit.type,
      rank: unit.rank,
      rankProgress: unit.rankProgress,
      resources: structuredClone(unit.resources),
      career: unit.career.current,
      unlockedCareers: unit.career.unlocked.slice(),
      careerMastery: { ...unit.career.mastery },
      learnedAbilities: unit.learnedAbilities.slice(),
    };
  }
}
