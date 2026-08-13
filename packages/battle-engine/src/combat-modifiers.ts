import { commanderAuraFor } from './commanders';
import { Battlefield } from './domain/battlefield';
import { combinedStatusModifiers } from './statuses';
import { dist } from './grid';
import { hasOpposedFlanker, relativeAttackSide } from './spatial';
import type { Coord, GameState, Unit, WeaponDef } from './types';
import { type ContentCatalog } from './content-pack';
import { activeFormation } from './formations';

export const MAX_MITIGATION = 0.6;

export type ModifierStage = 'power' | 'mitigation' | 'final';
export type ModifierOperation = 'add' | 'multiply';

/** One explainable contribution to a numeric combat result. */
export interface CombatModifier {
  id: string;
  label: string;
  source: 'weapon' | 'matchup' | 'unit' | 'status' | 'commander' | 'terrain' | 'reaction' | 'elevation' | 'position' | 'cover' | 'extension';
  stage: ModifierStage;
  operation: ModifierOperation;
  value: number;
  details?: Record<string, number | string | boolean>;
}

export interface UnitDamageContext {
  state: GameState;
  attacker: Unit;
  attackerAt: Coord;
  defender: Unit;
  defenderAt: Coord;
  weapon: WeaponDef;
  readonly content: ContentCatalog;
  /** Shared spatial projection for every provider in one damage evaluation. */
  readonly battlefield: Battlefield;
}

export interface CombatModifierProvider {
  id: string;
  priority: number;
  provide(context: UnitDamageContext): CombatModifier[];
}

/** Extensible Strategy collection for combat-rule contributions. */
export class CombatModifierProviderRegistry {
  private readonly providers = new Map<string, CombatModifierProvider>();
  private orderedCache: readonly CombatModifierProvider[] | null = null;

  register(provider: CombatModifierProvider): this {
    if (this.providers.has(provider.id)) {
      throw new Error(`combat modifier provider already registered: "${provider.id}"`);
    }
    this.providers.set(provider.id, provider);
    this.orderedCache = null;
    return this;
  }

  replace(provider: CombatModifierProvider): this {
    this.providers.set(provider.id, provider);
    this.orderedCache = null;
    return this;
  }

  ordered(): readonly CombatModifierProvider[] {
    if (!this.orderedCache) {
      this.orderedCache = Object.freeze([...this.providers.values()].sort(
        (left, right) => left.priority - right.priority || left.id.localeCompare(right.id),
      ));
    }
    return this.orderedCache;
  }

  clone(): CombatModifierProviderRegistry {
    const copy = new CombatModifierProviderRegistry();
    for (const provider of this.providers.values()) copy.register(provider);
    return copy;
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
      value: 0.5 + 0.5 * (attacker.hp / content.units.get(attacker.type).maxHp),
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
  provide: ({ state, attacker }) => [
    {
      id: 'commander.attack',
      label: '攻击方指挥光环',
      source: 'commander',
      stage: 'power',
      operation: 'multiply',
      value: commanderAuraFor(state, attacker).attackMultiplier,
    },
  ],
};

const formationProvider: CombatModifierProvider = {
  id: 'core.formation-attack',
  priority: 510,
  provide: ({ state, attacker, content }) => {
    const formation = activeFormation(state, attacker, content);
    if (!formation || formation.attackMultiplier === 1) return [];
    return [{
      id: `formation.attack.${formation.id}`,
      label: formation.name,
      source: 'extension',
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
  provide: ({ state, attacker, attackerAt, defender, weapon }) => {
    if (dist(attackerAt, defender) !== 1 || (!weapon.tags.includes('melee') && weapon.maxRange > 1)) return [];
    const side = relativeAttackSide(defender, attackerAt);
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
    if (hasOpposedFlanker(state, attacker, defender, attackerAt)) {
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
  provide: ({ state, attackerAt, defenderAt, weapon, content, battlefield = new Battlefield(state, content) }) => {
    if (dist(attackerAt, defenderAt) <= 1 || weapon.lineOfSight === 'arc' || weapon.tags.includes('ignores-cover')) return [];
    const cell = battlefield.cell(defenderAt);
    const score = (level: 'none' | 'half' | 'full') => level === 'full' ? 2 : level === 'half' ? 1 : 0;
    let level = score(cell.directionalCoverFrom(attackerAt)) > score(cell.cover)
      ? cell.directionalCoverFrom(attackerAt)
      : cell.cover;
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
  provide: ({ state, defender, defenderAt, content, battlefield = new Battlefield(state, content) }) => {
    const definition = content.units.get(defender.type);
    const status = combinedStatusModifiers(defender, content);
    const command = commanderAuraFor(state, defender);
    const formation = activeFormation(state, defender, content);
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

export const DefaultCombatModifierPipeline = new CombatModifierPipeline(CombatModifierProviders);
