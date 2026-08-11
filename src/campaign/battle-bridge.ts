import { normaliseLevel } from '../core/mapio';
import { teamOf } from '../core/state';
import type { ContentCatalog } from '../core/content-pack';
import { GlobalContentCatalog } from '../core/content-pack';
import type { GameEvent, GameState, Unit } from '../core/types';
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

/** Anti-corruption layer from persistent campaign state to a battle snapshot. */
export class CampaignBattleBridge {
  constructor(
    private readonly resolveLevel: CampaignLevelResolver,
    private readonly content: ContentCatalog = GlobalContentCatalog,
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
    const disposition: RosterDisposition = active ? 'available'
      : marker?.kind === 'routed' ? 'routed'
        : marker?.kind === 'surrendered' ? 'surrendered'
          : marker?.kind === 'withdrawn' ? 'missing'
          : marker ? 'fallen' : 'missing';
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
