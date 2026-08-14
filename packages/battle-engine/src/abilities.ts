import {
  readyWeapon,
  requireReadyWeapon,
  healAmount,
  primaryWeapon,
} from './combat';
import { beginCast } from './casting';
import { executeCombatPlan, forecastCombatPlan } from './combat-plan';
import type { CombatModifierPipeline } from './combat-modifiers';
import type { WeaponHitEffectHandlerRegistry } from './hit-effects';
import { awardRankProgress, type RankProgressionPolicy } from './progression';
import { unitAbilityIds } from './careers';
import { MapLayers } from './domain/map-layers';
import type { GridRules } from './tactical-grid';
import { ContentRegistry } from './registry';
import { type ReactionBehavior } from './reactions';
import { type UnitDepartureHandlerRegistry } from './unit-departure';
import { unitAtCoord } from './state';
import { player } from './state';
import { blockedAbilityStatus, combinedStatusModifiers } from './statuses';
import { BattleAggregate, UnitEntity } from './domain/index';
import type { Coord, GameEvent, GameState, Unit, WeaponDef, WeaponId } from './types';
import { type BattleResourceSystem } from './resources';
import { type TacticalSpace } from './tactical-space';
import { type ContentCatalog } from './content-pack';
import { type WeaponAreaRules } from './weapon-area';
import { hostileActionAllowed, type EngagementKind } from './engagement';

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
export interface AbilityRules extends WeaponAreaRules, GridRules {
  readonly content: ContentCatalog;
  readonly resources: BattleResourceSystem;
  readonly combatModifiers: CombatModifierPipeline;
  readonly hitEffects: WeaponHitEffectHandlerRegistry;
  readonly progression: RankProgressionPolicy;
  /** Shared spatial legality used by menus, AI and action execution. */
  readonly space: TacticalSpace;
  readonly abilities: ContentRegistry<AbilityDef>;
  readonly reactions: ContentRegistry<ReactionBehavior>;
  readonly unitDepartures: UnitDepartureHandlerRegistry;
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
  /**
   * Whether a zone truce may forbid this ability, and how harshly.
   *
   * The core used to sniff it out of the tag list with a three-branch ladder
   * whose last two branches nothing in the world ever matched, and the attack
   * applied the policy a second time inside its own target list. An ability
   * says what it is; the policy is applied once, centrally.
   */
  engagement: EngagementKind | null;
  /**
   * The weapon this ability fires under this query, if it fires one.
   *
   * Declared rather than guessed: `canUseAbility` branched on `id === 'attack'`
   * to fold the weapon's tags into the status check, so a content pack's second
   * weapon-using ability — a volley, a channelled beam — skipped that check
   * entirely, and a status that seals arcane weapons would not have sealed it.
   */
  weaponFor(rules: AbilityRules, q: AbilityQuery): WeaponDef | null;
  targets(rules: AbilityRules, q: AbilityQuery): Coord[];
  usable(rules: AbilityRules, q: AbilityQuery): boolean;
  execute(rules: AbilityRules, q: AbilityQuery, target: Coord | null, emit: Emit): void;
}

export const Abilities = new ContentRegistry<AbilityDef>('ability');

/**
 * Authoring helper, alongside the eight in `content-builders`.
 *
 * It was private, so the core's four abilities had defaults and everyone
 * else's had to spell out all nine fields — which is also why adding one
 * broke every ability declared outside this file.
 */
export function defineAbility(def: Partial<AbilityDef> & Pick<AbilityDef, 'id' | 'name'>): AbilityDef {
  return {
    hint: '',
    selfTargeted: true,
    priority: 50,
    tags: [],
    engagement: null,
    weaponFor: () => null,
    targets: () => [],
    usable: () => true,
    execute: () => {},
    ...def,
  };
}

/* ------------------------------------------------------------------- attack */

export function clearCaptureAt(content: ContentCatalog, state: GameState, c: Coord): void {
  new BattleAggregate(state, content).clearCaptureAt(c);
}

/**
 * The weapon this query is about, or null when there is none to speak of —
 * the unit no longer carries it, cannot fire it yet, or has no weapons at all.
 */
function selectedWeapon(rules: AbilityRules, q: AbilityQuery): WeaponDef | null {
  const id = q.weaponId ?? rules.content.units.get(q.unit.type).weapons[0];
  if (!id) return null;
  return readyWeapon(rules, q.unit, id, player(q.state, q.unit.owner));
}

Abilities.defineAll([
  defineAbility({
    id: 'attack',
    name: '攻击',
    hint: '对射程内的敌人造成伤害；若对方也能打到你，会遭到反击。',
    selfTargeted: false,
    priority: 10,
    tags: ['attack'],
    engagement: 'attack',
    weaponFor: selectedWeapon,
    targets: (rules, q) => {
      const weapon = selectedWeapon(rules, q);
      return weapon ? rules.space.attackTargets(q.state, q.unit, q.at, weapon) : [];
    },
    usable: (rules, q) => {
      const weapon = selectedWeapon(rules, q);
      return weapon !== null && (!q.moved || weapon.moveAndAttack);
    },
    execute: (rules, q, target, emit) => {
      if (!target) throw new Error('attack requires a target');
      const { state, unit, at } = q;
      // Execution: legality was settled before the order was accepted, so a
      // missing weapon here is a defect, not a refusal.
      const weapon = requireReadyWeapon(
        rules,
        unit,
        q.weaponId ?? primaryWeapon(unit, rules.content).id,
        player(state, unit.owner),
      );
      // A charged weapon locks the tile now and strikes it later; everything
      // else about the strike is identical, which is why charge time is a
      // weapon property rather than a separate ability.
      if (weapon.castTurns > 0) {
        // Cost is paid when the strike lands, not when it is committed: a cast
        // that never goes off never spent its ammunition.
        beginCast(state, { caster: unit, ability: 'attack', weapon, target, origin: at }, emit);
        return;
      }
      const plan = forecastCombatPlan(rules, state, unit, target, { from: at, weapon: weapon.id });
      executeCombatPlan(rules, state, plan, emit);
    },
  }),

  defineAbility({
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
      awardRankProgress(rules, unit, Math.max(5, healed), emit);
    },
  }),

  defineAbility({
    id: 'capture',
    name: '占领',
    hint: '占下城镇、兵营或城堡；只有人类步兵可以占领。',
    priority: 5,
    usable: (rules, { state, unit, at }) => {
      const content = rules.content;
      if (combinedStatusModifiers(unit, content).cannotCapture) return false;
      const layers = new MapLayers(state.map);
      if (!content.terrains.get(layers.terrainAt(at)).capturable) return false;
      return layers.owner(at) !== unit.owner;
    },
    execute: (rules, { state, unit, at }, _t, emit) => {
      const layers = new MapLayers(state.map);
      const battleRules = state.rules;
      // The tile falling to this unit was written out twice, once for each way
      // of getting there, and the two copies had to agree on four lines.
      const claim = (): void => {
        layers.changeOwner(at, unit.owner);
        layers.changeCaptureProgress(at, 0);
        emit({ type: 'capture', at, player: unit.owner, progress: 1, captured: true });
        awardRankProgress(rules, unit, 60, emit);
      };
      if (battleRules.captureMode === 'instant') return claim();

      const def = rules.content.units.get(unit.type);
      const contribution = Math.max(
        1,
        Math.round(battleRules.captureThreshold * (unit.hp / def.maxHp)),
      );
      const next = layers.captureProgressAt(at) + contribution;
      if (next >= battleRules.captureThreshold) return claim();
      layers.changeCaptureProgress(at, next);
      emit({
        type: 'capture',
        at,
        player: unit.owner,
        progress: next / battleRules.captureThreshold,
        captured: false,
      });
      awardRankProgress(rules, unit, 20, emit);
    },
  }),

  defineAbility({
    id: 'wait',
    name: '待机',
    hint: '结束该单位的行动。',
    priority: 90,
  }),
]);

export const abilityDef = (rules: AbilityRules, id: string): AbilityDef => rules.abilities.get(id);

export function canUseAbility(rules: AbilityRules, ability: AbilityDef, query: AbilityQuery): boolean {
  const weapon = ability.weaponFor(rules, query);
  const tags = weapon ? [...ability.tags, ...weapon.tags] : ability.tags;
  return blockedAbilityStatus(query.unit, tags, rules.content) === null && ability.usable(rules, query);
}

/** Shared target-policy projection used by menus, AI and authoritative actions. */
export function abilityTargets(
  rules: AbilityRules,
  ability: AbilityDef,
  query: AbilityQuery,
): Coord[] {
  const targets = ability.targets(rules, query);
  const kind = ability.engagement;
  if (!kind) return targets;
  return targets.filter((target) =>
    hostileActionAllowed(query.state, query.unit.owner, query.at, target, kind));
}

/** Abilities this unit can use right now, in menu order. */
export function availableAbilities(rules: AbilityRules, q: AbilityQuery): AbilityDef[] {
  return unitAbilityIds(q.unit, rules.content)
    .map((id) => abilityDef(rules, id))
    .filter((a) => canUseAbility(rules, a, q))
    .filter((a) => a.selfTargeted || abilityTargets(rules, a, q).length > 0)
    .sort((a, b) => a.priority - b.priority);
}
