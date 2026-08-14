import { commanderAuraFor } from './commanders';
import { Battlefield } from './domain/battlefield';
import type { Board } from './domain/board';
import type { FormationRules } from './formations';
import type { GridRules } from './tactical-grid';
import { combinedStatusModifiers } from './statuses';
import { hasOpposedFlanker, relativeAttackSide } from './spatial';
import type { Coord, GameState, Unit, WeaponDef } from './types';
import { type ContentCatalog } from './content-pack';
import { activeFormation } from './formations';
import { PriorityRegistry } from './registry';

export const MAX_MITIGATION = 0.6;

/**
 * How hard a wounded attacker still hits: 0.5 at death's door, 1.0 fresh.
 *
 * Stated once because two callers need the same number — the provider that
 * explains it in a unit strike's modifier chain, and siege damage, which does
 * not run that chain.
 */
export const attackerStrength = (content: ContentCatalog, attacker: Unit): number =>
  0.5 + 0.5 * (attacker.hp / content.units.get(attacker.type).maxHp);

export type ModifierStage = 'power' | 'mitigation' | 'final';
export type ModifierOperation = 'add' | 'multiply';

/**
 * Which rule a contribution came from — the chip in front of it in the forecast.
 *
 * Open, like every other extension key in the engine. As a closed union of
 * eleven names it left a rule plugin no way to name its own category, and the
 * core's own formation provider was already filing itself under `extension`:
 * a shipped mechanic labelled 「扩展」 to the player because the list of names
 * had no room for 「阵形」.
 */
export type CombatModifierSource = string;

/** One explainable contribution to a numeric combat result. */
export interface CombatModifier {
  id: string;
  label: string;
  source: CombatModifierSource;
  stage: ModifierStage;
  operation: ModifierOperation;
  value: number;
  details?: Record<string, number | string | boolean>;
}

export interface UnitDamageContext {
  /** The ruleset this strike is resolved under, for whatever a provider needs. */
  readonly rules: CombatModifierRules;
  state: GameState;
  attacker: Unit;
  attackerAt: Coord;
  defender: Unit;
  defenderAt: Coord;
  weapon: WeaponDef;
  readonly content: ContentCatalog;
  /** Shared spatial projection for every provider in one damage evaluation. */
  readonly battlefield: Battlefield;
  /**
   * The board under its tiling, shared for the same reason.
   *
   * Six providers asked "are these two adjacent" and "which side is this attack
   * coming from" through free functions that assumed a four-way board.
   */
  readonly board: Board;
}

/**
 * What a provider may consult. Open by intent: a rule pack's own provider reads
 * whatever the ruleset offers, and `BattleRuleServices` satisfies this.
 */
export interface CombatModifierRules extends FormationRules, GridRules {}

export interface CombatModifierProvider {
  id: string;
  priority: number;
  provide(context: UnitDamageContext): CombatModifier[];
}

/** Extensible Strategy collection for combat-rule contributions. */
export class CombatModifierProviderRegistry extends PriorityRegistry<CombatModifierProvider> {
  constructor() {
    super('combat modifier provider');
  }

  clone(): CombatModifierProviderRegistry {
    return this.copyInto(new CombatModifierProviderRegistry());
  }
}

export interface ModifierPipelineResult {
  modifiers: CombatModifier[];
  powerBeforeMitigation: number;
  mitigation: number;
  finalMultiplier: number;
  damage: number;
}

/**
 * Pipes modifiers through explicit phases. Additive mitigation is capped once,
 * preventing extension order from accidentally changing armor semantics.
 */
export class CombatModifierPipeline {
  constructor(
    private readonly providers: CombatModifierProviderRegistry,
    private readonly mitigationCap = MAX_MITIGATION,
  ) {}

  register(provider: CombatModifierProvider): this {
    this.providers.register(provider);
    return this;
  }

  replace(provider: CombatModifierProvider): this {
    this.providers.replace(provider);
    return this;
  }

  collect(context: UnitDamageContext): CombatModifier[] {
    return this.providers.ordered().flatMap((provider) => provider.provide(context));
  }

  evaluate(base: number, context: UnitDamageContext): ModifierPipelineResult {
    const modifiers = this.collect(context);
    let power = base;
    let mitigation = 0;
    let finalMultiplier = 1;
    let finalOffset = 0;
    for (const modifier of modifiers) {
      if (modifier.stage === 'power') {
        power = modifier.operation === 'multiply' ? power * modifier.value : power + modifier.value;
      } else if (modifier.stage === 'mitigation') {
        mitigation = modifier.operation === 'multiply'
          ? mitigation * modifier.value
          : mitigation + modifier.value;
      } else if (modifier.operation === 'multiply') {
        finalMultiplier *= modifier.value;
        finalOffset *= modifier.value;
      } else {
        finalOffset += modifier.value;
      }
    }
    mitigation = Math.max(0, Math.min(this.mitigationCap, mitigation));
    const resolved = power * (1 - mitigation) * finalMultiplier + finalOffset;

    return {
      modifiers,
      powerBeforeMitigation: power,
      mitigation,
      finalMultiplier,
      damage: Math.max(1, Math.round(resolved)),
    };
  }
}

const effectivenessProvider: CombatModifierProvider = {
  id: 'core.matchup',
  priority: 100,
  provide: ({ weapon, defender, content }) => [
    {
      id: 'matchup.effectiveness',
      label: '伤害类型与护甲克制',
      source: 'matchup',
      stage: 'power',
      operation: 'multiply',
      value: content.damageMatchups.effectiveness(weapon.damageType, content.units.get(defender.type).armorClass),
    },
  ],
};

const targetTagProvider: CombatModifierProvider = {
  id: 'core.weapon-target-tags',
  priority: 200,
  provide: ({ weapon, defender, content }) => {
    const tags = content.units.get(defender.type).tags;
    return weapon.bonuses
      .filter((bonus) => tags.includes(bonus.targetTag))
      .map((bonus) => ({
        id: `weapon.target-tag.${bonus.targetTag}`,
        label: bonus.reason,
        source: 'weapon' as const,
        stage: 'power' as const,
        operation: 'multiply' as const,
        value: bonus.multiplier,
        details: { targetTag: bonus.targetTag },
      }));
  },
};

const strengthProvider: CombatModifierProvider = {
  id: 'core.attacker-strength',
  priority: 300,
  provide: ({ attacker, content }) => [
    {
      id: 'unit.hp-strength',
      label: '攻击者剩余生命',
      source: 'unit',
      stage: 'power',
      operation: 'multiply',
      value: attackerStrength(content, attacker),
    },
  ],
};

const statusProvider: CombatModifierProvider = {
  id: 'core.status',
  priority: 400,
  provide: ({ attacker, content }) => [
    {
      id: 'status.attack',
      label: '攻击方状态',
      source: 'status',
      stage: 'power',
      operation: 'multiply',
      value: combinedStatusModifiers(attacker, content).attackMultiplier,
    },
  ],
};

const rankProvider: CombatModifierProvider = {
  id: 'core.rank',
  priority: 350,
  provide: ({ attacker }) => [
    {
      id: 'rank.attack',
      label: attacker.rank === 0 ? '新兵军衔' : attacker.rank === 1 ? '老兵军衔' : '精英军衔',
      source: 'unit',
      stage: 'power',
      operation: 'multiply',
      value: 1 + attacker.rank * 0.04,
      details: { rank: attacker.rank },
    },
  ],
};

const commanderProvider: CombatModifierProvider = {
  id: 'core.commander',
  priority: 500,
  provide: ({ rules, state, attacker }) => [
    {
      id: 'commander.attack',
      label: '攻击方指挥光环',
      source: 'commander',
      stage: 'power',
      operation: 'multiply',
      value: commanderAuraFor(rules, state, attacker).attackMultiplier,
    },
  ],
};

const formationProvider: CombatModifierProvider = {
  id: 'core.formation-attack',
  priority: 510,
  provide: ({ rules, state, attacker }) => {
    const formation = activeFormation(rules, state, attacker);
    if (!formation || formation.attackMultiplier === 1) return [];
    return [{
      id: `formation.attack.${formation.id}`,
      label: formation.name,
      source: 'formation',
      stage: 'power',
      operation: 'multiply',
      value: formation.attackMultiplier,
      details: { formation: formation.id },
    }];
  },
};

const elevationProvider: CombatModifierProvider = {
  id: 'core.elevation',
  priority: 520,
  provide: ({ state, attackerAt, defenderAt, content, battlefield = new Battlefield(state, content) }) => {
    const delta = battlefield.cell(attackerAt).elevation - battlefield.cell(defenderAt).elevation;
    if (delta < state.rules.highGroundThreshold) return [];
    return [{
      id: 'elevation.high-ground',
      label: `高地优势（高 ${delta} 级）`,
      source: 'elevation',
      stage: 'power',
      operation: 'multiply',
      value: state.rules.highGroundDamageMultiplier,
      details: { delta },
    }];
  },
};

const positionProvider: CombatModifierProvider = {
  id: 'core.position',
  priority: 530,
  provide: ({ board, state, attacker, attackerAt, defender, weapon }) => {
    if (board.distance(attackerAt, defender) !== 1 ||
      (!weapon.tags.includes('melee') && weapon.maxRange > 1)) return [];
    const side = relativeAttackSide(board.grid, defender, attackerAt);
    const modifiers: CombatModifier[] = [];
    if (side === 'back') {
      modifiers.push({
        id: 'position.back-attack', label: '背刺', source: 'position', stage: 'power', operation: 'multiply',
        value: state.rules.backAttackMultiplier,
      });
    } else if (side === 'side') {
      modifiers.push({
        id: 'position.side-attack', label: '侧击', source: 'position', stage: 'power', operation: 'multiply',
        value: state.rules.sideAttackMultiplier,
      });
    }
    if (hasOpposedFlanker(board, state, attacker, defender, attackerAt)) {
      modifiers.push({
        id: 'position.flank', label: '夹击', source: 'position', stage: 'power', operation: 'multiply',
        value: state.rules.flankAttackMultiplier,
      });
    }
    return modifiers;
  },
};

const coverProvider: CombatModifierProvider = {
  id: 'core.cover',
  priority: 590,
  provide: ({ board, state, attackerAt, defenderAt, weapon, content, battlefield = new Battlefield(state, content) }) => {
    if (board.distance(attackerAt, defenderAt) <= 1 || weapon.lineOfSight === 'arc' ||
      weapon.tags.includes('ignores-cover')) return [];
    const cell = battlefield.cell(defenderAt);
    const score = (level: 'none' | 'half' | 'full') => level === 'full' ? 2 : level === 'half' ? 1 : 0;
    const facing = cell.directionalCoverFrom(board.grid, attackerAt);
    let level = score(facing) > score(cell.cover) ? facing : cell.cover;
    const elevationDelta = battlefield.cell(attackerAt).elevation - cell.elevation;
    if (elevationDelta >= state.rules.highGroundThreshold) {
      level = level === 'full' ? 'half' : 'none';
    }
    const value = level === 'full' ? state.rules.fullCoverDefense : level === 'half' ? state.rules.halfCoverDefense : 0;
    if (value <= 0) return [];
    return [{
      id: `cover.${level}`,
      label: level === 'full' ? '全掩体' : '半掩体',
      source: 'cover',
      stage: 'mitigation',
      operation: 'add',
      value,
      details: { level, elevationDelta },
    }];
  },
};

const defenseProvider: CombatModifierProvider = {
  id: 'core.defense',
  priority: 600,
  provide: ({ rules, state, defender, defenderAt, content, battlefield = new Battlefield(state, content) }) => {
    const definition = content.units.get(defender.type);
    const status = combinedStatusModifiers(defender, content);
    const command = commanderAuraFor(rules, state, defender);
    const formation = activeFormation(rules, state, defender);
    const terrain = battlefield.cell(defenderAt).defense;
    const rankDefense = defender.rank * 0.02;
    const formationDefense = formation?.defenseDelta ?? 0;
    const unit = Math.max(0, definition.defense + rankDefense + status.defenseDelta + command.defenseDelta + formationDefense);
    return [
      {
        id: 'defense.terrain',
        label: '地形防御',
        source: 'terrain',
        stage: 'mitigation',
        operation: 'add',
        value: terrain,
      },
      {
        id: 'defense.unit',
        label: '单位防御',
        source: 'unit',
        stage: 'mitigation',
        operation: 'add',
        value: unit,
        details: {
          base: definition.defense,
          rankDelta: rankDefense,
          statusDelta: status.defenseDelta,
          commanderDelta: command.defenseDelta,
          formationDelta: formationDefense,
        },
      },
    ];
  },
};

export const CombatModifierProviders = new CombatModifierProviderRegistry()
  .register(effectivenessProvider)
  .register(targetTagProvider)
  .register(strengthProvider)
  .register(rankProvider)
  .register(statusProvider)
  .register(commanderProvider)
  .register(formationProvider)
  .register(elevationProvider)
  .register(positionProvider)
  .register(coverProvider)
  .register(defenseProvider);
