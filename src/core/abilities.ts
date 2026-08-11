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
import { type BattleResourceSystem, DefaultBattleResources } from './resources';
import { CoreTacticalSpace, type TacticalSpace } from './tactical-space';
import { GlobalContentCatalog, type ContentCatalog } from './content-pack';
import { hostileActionAllowed } from './engagement';

/** Everything an ability needs to decide legality. */
export interface AbilityQuery {
  state: GameState;
  unit: Unit;
  /** Tile the unit acts from (i.e. the end of its move). */
  at: Coord;
  /** Did the unit actually change tiles this turn? */
  moved: boolean;
  /** Selected attack profile. Omitted for non-attack abilities. */
  weaponId?: WeaponId;
  /** Injected ruleset used by execution; queries may omit it. */
  combatModifiers?: CombatModifierPipeline;
  hitEffects?: WeaponHitEffectHandlerRegistry;
  progression?: RankProgressionPolicy;
  resources?: BattleResourceSystem;
  /** Shared spatial legality used by menus, AI and action execution. */
  space?: TacticalSpace;
  content?: ContentCatalog;
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
  targets(q: AbilityQuery): Coord[];
  usable(q: AbilityQuery): boolean;
  execute(q: AbilityQuery, target: Coord | null, emit: Emit): void;
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

export function clearCaptureAt(s: GameState, c: Coord): void {
  new BattleAggregate(s).clearCaptureAt(c);
}

function selectedWeapon(q: AbilityQuery): WeaponDef {
  return availableWeapon(
    q.unit,
    q.weaponId ?? primaryWeapon(q.unit, q.content ?? GlobalContentCatalog).id,
    q.resources ?? DefaultBattleResources,
    player(q.state, q.unit.owner),
    q.content ?? GlobalContentCatalog,
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
    targets: (q) => {
      const weapon = selectedWeapon(q);
      return (q.space ?? CoreTacticalSpace).attackTargets(q.state, q.unit, q.at, weapon)
        .filter((target) => hostileActionAllowed(q.state, q.unit.owner, q.at, target, 'attack'));
    },
    usable: (q) => {
      let weapon: WeaponDef;
      try {
        weapon = selectedWeapon(q);
      } catch {
        return false;
      }
      if (q.moved && !weapon.moveAndAttack) return false;
      return true;
    },
    execute: (q, target, emit) => {
      if (!target) throw new Error('attack requires a target');
      const { state, unit, at } = q;
      const weapon = selectedWeapon(q);
      const resources = q.resources ?? DefaultBattleResources;
      const content = q.content ?? GlobalContentCatalog;
      const plan = forecastCombatPlan(state, unit, target, at, weapon.id, q.combatModifiers, resources, content);
      executeCombatPlan(state, plan, emit, q.hitEffects, q.progression, resources, content);
    },
  }),

  ability({
    id: 'heal',
    name: '治疗',
    hint: '恢复相邻友军的生命值。',
    selfTargeted: false,
    priority: 20,
    tags: ['healing'],
    targets: (q) => (q.space ?? CoreTacticalSpace)
      .healTargets(q.state, q.unit, q.at)
      .map((u) => ({ x: u.x, y: u.y })),
    usable: () => true,
    execute: ({ state, unit, progression, content = GlobalContentCatalog }, target, emit) => {
      if (!target) throw new Error('heal requires a target');
      const ally = unitAtCoord(state, target);
      if (!ally) throw new Error('no unit to heal');
      const amount = healAmount(unit, ally, content);
      const healed = new UnitEntity(ally).heal(amount, content.units.get(ally.type).maxHp);
      emit({ type: 'heal', source: unit.id, target: ally.id, amount: healed });
      awardRankProgress(unit, Math.max(5, healed), emit, progression, content);
    },
  }),

  ability({
    id: 'capture',
    name: '占领',
    hint: '占下城镇、兵营或城堡；只有人类步兵可以占领。',
    priority: 5,
    usable: ({ state, unit, at, content = GlobalContentCatalog }) => {
      if (combinedStatusModifiers(unit, content).cannotCapture) return false;
      const i = idx(state.map, at.x, at.y);
      const terrain = content.terrains.get(state.map.tiles[i]);
      if (!terrain.capturable) return false;
      return state.map.owners[i] !== unit.owner;
    },
    execute: ({ state, unit, at, progression, content = GlobalContentCatalog }, _t, emit) => {
      const i = idx(state.map, at.x, at.y);
      const rules = state.rules;
      if (rules.captureMode === 'instant') {
        state.map.owners[i] = unit.owner;
        state.map.captureProgress[i] = 0;
        emit({ type: 'capture', at, player: unit.owner, progress: 1, captured: true });
        awardRankProgress(unit, 60, emit, progression, content);
        return;
      }
      const def = content.units.get(unit.type);
      const contribution = Math.max(
        1,
        Math.round(rules.captureThreshold * (unit.hp / def.maxHp)),
      );
      const next = state.map.captureProgress[i] + contribution;
      if (next >= rules.captureThreshold) {
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
          progress: next / rules.captureThreshold,
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

export const abilityDef = (id: string, abilities: Registry<AbilityDef> = Abilities): AbilityDef => abilities.get(id);

export function canUseAbility(ability: AbilityDef, query: AbilityQuery): boolean {
  const tags = [...ability.tags];
  if (ability.id === 'attack') {
    try {
      tags.push(...selectedWeapon(query).tags);
    } catch {
      return false;
    }
  }
  return blockedAbilityStatus(query.unit, tags, query.content ?? GlobalContentCatalog) === null && ability.usable(query);
}

/** Abilities this unit can use right now, in menu order. */
export function availableAbilities(
  q: AbilityQuery,
  abilities: Registry<AbilityDef> = Abilities,
): AbilityDef[] {
  return unitAbilityIds(q.unit, q.content ?? GlobalContentCatalog)
    .map((id) => abilityDef(id, abilities))
    .filter((a) => canUseAbility(a, q))
    .filter((a) => a.selfTargeted || a.targets(q).length > 0)
    .sort((a, b) => a.priority - b.priority);
}
