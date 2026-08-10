import { forecast, healAmount } from './combat';
import { Terrains } from './data/terrain';
import { unitDef } from './data/units';
import { idx } from './grid';
import { Registry } from './registry';
import { healTargetsFrom, targetsFrom } from './movement';
import { removeUnit, unitAtCoord } from './state';
import type { Coord, GameEvent, GameState, Unit } from './types';

/** Everything an ability needs to decide legality. */
export interface AbilityQuery {
  state: GameState;
  unit: Unit;
  /** Tile the unit acts from (i.e. the end of its move). */
  at: Coord;
  /** Did the unit actually change tiles this turn? */
  moved: boolean;
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
    targets: () => [],
    usable: () => true,
    execute: () => {},
    ...def,
  };
}

/* ------------------------------------------------------------------- attack */

export function applyDamage(s: GameState, target: Unit, damage: number, emit: Emit): boolean {
  target.hp = Math.max(0, target.hp - damage);
  if (target.hp <= 0) {
    emit({ type: 'death', unit: target.id, at: { x: target.x, y: target.y } });
    clearCaptureAt(s, { x: target.x, y: target.y });
    removeUnit(s, target.id);
    return true;
  }
  return false;
}

export function clearCaptureAt(s: GameState, c: Coord): void {
  const i = idx(s.map, c.x, c.y);
  if (s.map.captureProgress[i] !== 0) s.map.captureProgress[i] = 0;
}

Abilities.defineAll([
  ability({
    id: 'attack',
    name: '攻击',
    hint: '对射程内的敌人造成伤害；若对方也能打到你，会遭到反击。',
    selfTargeted: false,
    priority: 10,
    targets: ({ state, unit, at }) =>
      targetsFrom(state, unit, at).map((u) => ({ x: u.x, y: u.y })),
    usable: (q) => {
      const def = unitDef(q.unit.type);
      if (q.moved && !def.attackAfterMove) return false;
      return targetsFrom(q.state, q.unit, q.at).length > 0;
    },
    execute: ({ state, unit, at }, target, emit) => {
      if (!target) throw new Error('attack requires a target');
      const defender = unitAtCoord(state, target);
      if (!defender) throw new Error('no unit to attack');
      const fc = forecast(state, unit, defender, at);

      const killed = applyDamage(state, defender, fc.strike.damage, emit);
      emit({
        type: 'attack',
        attacker: unit.id,
        defender: defender.id,
        damage: fc.strike.damage,
        killed,
      });

      if (!killed && fc.counter) {
        const attackerKilled = applyDamage(state, unit, fc.counter.damage, emit);
        emit({
          type: 'counter',
          attacker: defender.id,
          defender: unit.id,
          damage: fc.counter.damage,
          killed: attackerKilled,
        });
      }
    },
  }),

  ability({
    id: 'heal',
    name: '治疗',
    hint: '恢复相邻友军的生命值。',
    selfTargeted: false,
    priority: 20,
    targets: ({ state, unit, at }) => healTargetsFrom(state, unit, at).map((u) => ({ x: u.x, y: u.y })),
    usable: (q) => healTargetsFrom(q.state, q.unit, q.at).length > 0,
    execute: ({ state, unit }, target, emit) => {
      if (!target) throw new Error('heal requires a target');
      const ally = unitAtCoord(state, target);
      if (!ally) throw new Error('no unit to heal');
      const amount = healAmount(unit, ally);
      ally.hp += amount;
      emit({ type: 'heal', source: unit.id, target: ally.id, amount });
    },
  }),

  ability({
    id: 'capture',
    name: '占领',
    hint: '占下城镇、兵营或城堡；只有人类步兵可以占领。',
    priority: 5,
    usable: ({ state, unit, at }) => {
      const i = idx(state.map, at.x, at.y);
      const terrain = Terrains.get(state.map.tiles[i]);
      if (!terrain.capturable) return false;
      return state.map.owners[i] !== unit.owner;
    },
    execute: ({ state, unit, at }, _t, emit) => {
      const i = idx(state.map, at.x, at.y);
      const rules = state.rules;
      if (rules.captureMode === 'instant') {
        state.map.owners[i] = unit.owner;
        state.map.captureProgress[i] = 0;
        emit({ type: 'capture', at, player: unit.owner, progress: 1, captured: true });
        return;
      }
      const def = unitDef(unit.type);
      const contribution = Math.max(
        1,
        Math.round(rules.captureThreshold * (unit.hp / def.maxHp)),
      );
      const next = state.map.captureProgress[i] + contribution;
      if (next >= rules.captureThreshold) {
        state.map.owners[i] = unit.owner;
        state.map.captureProgress[i] = 0;
        emit({ type: 'capture', at, player: unit.owner, progress: 1, captured: true });
      } else {
        state.map.captureProgress[i] = next;
        emit({
          type: 'capture',
          at,
          player: unit.owner,
          progress: next / rules.captureThreshold,
          captured: false,
        });
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

export const abilityDef = (id: string): AbilityDef => Abilities.get(id);

/** Abilities this unit can use right now, in menu order. */
export function availableAbilities(q: AbilityQuery): AbilityDef[] {
  return unitDef(q.unit.type)
    .abilities.map(abilityDef)
    .filter((a) => a.usable(q))
    .sort((a, b) => a.priority - b.priority);
}
