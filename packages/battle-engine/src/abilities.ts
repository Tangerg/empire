import {
  availableWeapon,
  healAmount,
  primaryWeapon,
} from './combat';
import { executeCombatPlan, forecastCombatPlan } from './combat-plan';
import type { CombatModifierPipeline } from './combat-modifiers';
import type { WeaponHitEffectHandlerRegistry } from './hit-effects';
import { awardRankProgress, type RankProgressionPolicy } from './progression';
import { unitAbilityIds } from './careers';
import { idx } from './grid';
import { Registry } from './registry';
import { unitAtCoord } from './state';
import { player } from './state';
import { blockedAbilityStatus, combinedStatusModifiers } from './statuses';
import { BattleAggregate, UnitEntity } from './domain/index';
import type { Coord, GameEvent, GameState, Unit, WeaponDef, WeaponId } from './types';
import { type BattleResourceSystem } from './resources';
import { type TacticalSpace } from './tactical-space';
import { type ContentCatalog } from './content-pack';
import { hostileActionAllowed } from './engagement';

/**
 * The question: which unit is trying to act, from where, with what.
 *
 * Deliberately free of services. Conflating "what is being asked" with "which
 * ruleset answers it" is what forced ambient singletons into this module.
 */
export interface AbilityQuery {
  state: GameState;
  unit: Unit;
  /** Tile the unit acts from (i.e. the end of its move). */
  at: Coord;
  /** Did the unit actually change tiles this turn? */
  moved: boolean;
  /** Selected attack profile. Omitted for non-attack abilities. */
  weaponId?: WeaponId;
}

/**
 * The ruleset that answers the question. Declared here as a consumer port; the
 * composition-level `BattleRuleServices` satisfies it structurally.
 */
export interface AbilityRules {
  readonly content: ContentCatalog;
  readonly resources: BattleResourceSystem;
  readonly combatModifiers: CombatModifierPipeline;
  readonly hitEffects: WeaponHitEffectHandlerRegistry;
  readonly progression: RankProgressionPolicy;
  /** Shared spatial legality used by menus, AI and action execution. */
  readonly space: TacticalSpace;
  readonly abilities: Registry<AbilityDef>;
}

export type Emit = (e: GameEvent) => void;

export interface AbilityDef {
  id: string;
  name: string;
  hint: string;
  /** Needs no target tile (capture, wait, ...). */
  selfTargeted: boolean;
  /** Sort order in the command menu; lower comes first. */
  priority: number;
  /** Semantic tags used by statuses and content rules. */
  tags: string[];
  targets(rules: AbilityRules, q: AbilityQuery): Coord[];
  usable(rules: AbilityRules, q: AbilityQuery): boolean;
  execute(rules: AbilityRules, q: AbilityQuery, target: Coord | null, emit: Emit): void;
}

export const Abilities = new Registry<AbilityDef>('ability');

function ability(def: Partial<AbilityDef> & { id: string; name: string }): AbilityDef {
  return {
    hint: '',
    selfTargeted: true,
    priority: 50,
    tags: [],
    targets: () => [],
    usable: () => true,
    execute: () => {},
    ...def,
  };
}

/* ------------------------------------------------------------------- attack */

export function clearCaptureAt(content: ContentCatalog, s: GameState, c: Coord): void {
  new BattleAggregate(s, content).clearCaptureAt(c);
}

function selectedWeapon(rules: AbilityRules, q: AbilityQuery): WeaponDef {
  return availableWeapon(
    rules,
    q.unit,
    q.weaponId ?? primaryWeapon(q.unit, rules.content).id,
    player(q.state, q.unit.owner),
  );
}

Abilities.defineAll([
  ability({
    id: 'attack',
    name: '攻击',
    hint: '对射程内的敌人造成伤害；若对方也能打到你，会遭到反击。',
    selfTargeted: false,
    priority: 10,
    tags: ['attack'],
    targets: (rules, q) => {
      const weapon = selectedWeapon(rules, q);
      return rules.space.attackTargets(q.state, q.unit, q.at, weapon)
        .filter((target) => hostileActionAllowed(q.state, q.unit.owner, q.at, target, 'attack'));
    },
    usable: (rules, q) => {
      let weapon: WeaponDef;
      try {
        weapon = selectedWeapon(rules, q);
      } catch {
        return false;
      }
      if (q.moved && !weapon.moveAndAttack) return false;
      return true;
    },
    execute: (rules, q, target, emit) => {
      if (!target) throw new Error('attack requires a target');
      const { state, unit, at } = q;
      const weapon = selectedWeapon(rules, q);
      const plan = forecastCombatPlan(rules, state, unit, target, { from: at, weapon: weapon.id });
      executeCombatPlan(rules, state, plan, emit);
    },
  }),

  ability({
    id: 'heal',
    name: '治疗',
    hint: '恢复相邻友军的生命值。',
    selfTargeted: false,
    priority: 20,
    tags: ['healing'],
    targets: (rules, q) => rules.space
      .healTargets(q.state, q.unit, q.at)
      .map((u) => ({ x: u.x, y: u.y })),
    usable: () => true,
    execute: (rules, { state, unit }, target, emit) => {
      if (!target) throw new Error('heal requires a target');
      const content = rules.content;
      const ally = unitAtCoord(state, target);
      if (!ally) throw new Error('no unit to heal');
      const amount = healAmount(unit, ally, content);
      const healed = new UnitEntity(ally).heal(amount, content.units.get(ally.type).maxHp);
      emit({ type: 'heal', source: unit.id, target: ally.id, amount: healed });
      awardRankProgress(unit, Math.max(5, healed), emit, rules.progression, content);
    },
  }),

  ability({
    id: 'capture',
    name: '占领',
    hint: '占下城镇、兵营或城堡；只有人类步兵可以占领。',
    priority: 5,
    usable: (rules, { state, unit, at }) => {
      const content = rules.content;
      if (combinedStatusModifiers(unit, content).cannotCapture) return false;
      const i = idx(state.map, at.x, at.y);
      const terrain = content.terrains.get(state.map.tiles[i]);
      if (!terrain.capturable) return false;
      return state.map.owners[i] !== unit.owner;
    },
    execute: (rules, { state, unit, at }, _t, emit) => {
      const content = rules.content;
      const progression = rules.progression;
      const i = idx(state.map, at.x, at.y);
      const battleRules = state.rules;
      if (battleRules.captureMode === 'instant') {
        state.map.owners[i] = unit.owner;
        state.map.captureProgress[i] = 0;
        emit({ type: 'capture', at, player: unit.owner, progress: 1, captured: true });
        awardRankProgress(unit, 60, emit, progression, content);
        return;
      }
      const def = content.units.get(unit.type);
      const contribution = Math.max(
        1,
        Math.round(battleRules.captureThreshold * (unit.hp / def.maxHp)),
      );
      const next = state.map.captureProgress[i] + contribution;
      if (next >= battleRules.captureThreshold) {
        state.map.owners[i] = unit.owner;
        state.map.captureProgress[i] = 0;
        emit({ type: 'capture', at, player: unit.owner, progress: 1, captured: true });
        awardRankProgress(unit, 60, emit, progression, content);
      } else {
        state.map.captureProgress[i] = next;
        emit({
          type: 'capture',
          at,
          player: unit.owner,
          progress: next / battleRules.captureThreshold,
          captured: false,
        });
        awardRankProgress(unit, 20, emit, progression, content);
      }
    },
  }),

  ability({
    id: 'wait',
    name: '待机',
    hint: '结束该单位的行动。',
    priority: 90,
  }),
]);

export const abilityDef = (rules: AbilityRules, id: string): AbilityDef => rules.abilities.get(id);

export function canUseAbility(rules: AbilityRules, ability: AbilityDef, query: AbilityQuery): boolean {
  const tags = [...ability.tags];
  if (ability.id === 'attack') {
    try {
      tags.push(...selectedWeapon(rules, query).tags);
    } catch {
      return false;
    }
  }
  return blockedAbilityStatus(query.unit, tags, rules.content) === null && ability.usable(rules, query);
}

/** Shared target-policy projection used by menus, AI and authoritative actions. */
export function abilityTargets(
  rules: AbilityRules,
  ability: AbilityDef,
  query: AbilityQuery,
): Coord[] {
  const targets = ability.targets(rules, query);
  const engagementKind = ability.tags.includes('attack')
    ? 'attack'
    : ability.tags.includes('hostile') || ability.tags.includes('hostile-action')
      ? 'hostile-action' : null;
  if (!engagementKind) return targets;
  return targets.filter((target) =>
    hostileActionAllowed(query.state, query.unit.owner, query.at, target, engagementKind));
}

/** Abilities this unit can use right now, in menu order. */
export function availableAbilities(rules: AbilityRules, q: AbilityQuery): AbilityDef[] {
  return unitAbilityIds(q.unit, rules.content)
    .map((id) => abilityDef(rules, id))
    .filter((a) => canUseAbility(rules, a, q))
    .filter((a) => a.selfTargeted || a.targets(rules, q).length > 0)
    .sort((a, b) => a.priority - b.priority);
}
