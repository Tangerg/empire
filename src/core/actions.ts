import { abilityDef, availableAbilities, clearCaptureAt } from './abilities';
import { Terrains } from './data/terrain';
import { unitDef } from './data/units';
import { idx, sameCoord } from './grid';
import { computeMoveField, pathTo } from './movement';
import { player, removeUnit, requireUnit, spawnUnit, unitAt, unitsOf } from './state';
import { evaluateVictory, healRateAt, incomeFor } from './victory';
import type { Action, Coord, GameEvent, GameState, Unit } from './types';

export class IllegalActionError extends Error {}

function fail(msg: string): never {
  throw new IllegalActionError(msg);
}

/* --------------------------------------------------------------- enumeration */

export interface CommandOption {
  ability: string;
  name: string;
  hint: string;
  selfTargeted: boolean;
  targets: Coord[];
}

/** Command menu for a unit that has moved to `at`. */
export function commandOptions(s: GameState, unit: Unit, at: Coord): CommandOption[] {
  const moved = !(unit.x === at.x && unit.y === at.y);
  const q = { state: s, unit, at, moved };
  return availableAbilities(q).map((a) => ({
    ability: a.id,
    name: a.name,
    hint: a.hint,
    selfTargeted: a.selfTargeted,
    targets: a.targets(q),
  }));
}

export const canAct = (s: GameState, u: Unit): boolean =>
  s.phase === 'playing' && u.owner === s.currentPlayer && !u.done;

/* ------------------------------------------------------------------- helpers */

function validatePath(s: GameState, unit: Unit, path: Coord[]): Coord {
  if (path.length === 0) fail('路径为空');
  if (!sameCoord(path[0], { x: unit.x, y: unit.y })) fail('路径起点与单位位置不符');
  const dest = path[path.length - 1];
  if (path.length === 1) return dest;

  const field = computeMoveField(s, unit);
  const canonical = pathTo(field, s.map, dest);
  if (!canonical) fail(`目标格 ${dest.x},${dest.y} 不在移动范围内`);
  if (!field.stops.has(idx(s.map, dest.x, dest.y))) fail('目标格已被占据');
  // Trust the engine's own cheapest path rather than the client's route.
  return dest;
}

function moveUnit(s: GameState, unit: Unit, dest: Coord, emit: (e: GameEvent) => void): void {
  if (unit.x === dest.x && unit.y === dest.y) return;
  const field = computeMoveField(s, unit);
  const path = pathTo(field, s.map, dest) ?? [{ x: unit.x, y: unit.y }, dest];
  clearCaptureAt(s, { x: unit.x, y: unit.y });
  unit.x = dest.x;
  unit.y = dest.y;
  unit.capture = 0;
  emit({ type: 'move', unit: unit.id, path });
}

/* --------------------------------------------------------------------- apply */

/** Mutates `s` and returns the event stream the UI animates. */
export function applyAction(s: GameState, action: Action): GameEvent[] {
  const events: GameEvent[] = [];
  const emit = (e: GameEvent) => events.push(e);

  if (s.phase !== 'playing') fail('对局已结束');

  switch (action.kind) {
    case 'command': {
      const unit = requireUnit(s, action.unit);
      if (unit.owner !== s.currentPlayer) fail('不是你的单位');
      if (unit.done) fail('该单位本回合已行动');

      const dest = validatePath(s, unit, action.path);
      const ability = abilityDef(action.command.ability);
      const moved = !(unit.x === dest.x && unit.y === dest.y);

      // Legality is judged from the destination, before the unit is there.
      const q = { state: s, unit, at: dest, moved };
      if (!unitDef(unit.type).abilities.includes(ability.id)) {
        fail(`${unitDef(unit.type).name} 没有「${ability.name}」`);
      }
      if (!ability.usable(q)) fail(`此处无法使用「${ability.name}」`);

      const target = 'target' in action.command ? action.command.target ?? null : null;
      if (!ability.selfTargeted) {
        if (!target) fail(`「${ability.name}」需要指定目标`);
        if (!ability.targets(q).some((t) => sameCoord(t, target))) fail('目标不合法');
      }

      moveUnit(s, unit, dest, emit);
      ability.execute({ state: s, unit, at: dest, moved }, target, emit);

      // The unit may have died to a counterattack.
      if (s.units.some((u) => u.id === unit.id)) unit.done = true;
      break;
    }

    case 'recruit': {
      const i = idx(s.map, action.at.x, action.at.y);
      const terrain = Terrains.get(s.map.tiles[i]);
      if (!terrain.produces.includes(action.unit)) fail(`${terrain.name} 无法生产该兵种`);
      if (s.map.owners[i] !== s.currentPlayer) fail('该建筑不属于你');
      if (unitAt(s, action.at.x, action.at.y)) fail('建筑上已有单位');

      const p = player(s, s.currentPlayer);
      const def = unitDef(action.unit);
      if (p.funds < def.cost) fail(`资金不足（需要 ${def.cost}）`);
      const cap = s.rules.maxUnitsPerPlayer;
      if (cap !== null && unitsOf(s, p.id).length >= cap) fail(`单位数量已达上限 ${cap}`);

      p.funds -= def.cost;
      const u = spawnUnit(s, action.unit, p.id, action.at, {
        done: !s.rules.recruitsActImmediately,
      });
      emit({ type: 'recruit', unit: u.id, at: action.at });
      break;
    }

    case 'endTurn':
      advanceTurn(s, emit);
      break;
  }

  checkGameOver(s, emit);
  return events;
}

/* ---------------------------------------------------------------- turn cycle */

function advanceTurn(s: GameState, emit: (e: GameEvent) => void): void {
  emit({ type: 'turnEnd', player: s.currentPlayer });

  const order = s.players.map((p) => p.id);
  let cursor = order.indexOf(s.currentPlayer);

  // At most one full lap, so `turn` can only advance once per call.
  for (let step = 0; step < order.length; step++) {
    cursor++;
    if (cursor >= order.length) {
      cursor = 0;
      s.turn++;
    }
    const p = player(s, order[cursor]);
    if (!p.alive) continue;
    s.currentPlayer = p.id;
    beginTurn(s, emit);
    return;
  }
  // Nobody left alive.
  s.phase = 'over';
}

function beginTurn(s: GameState, emit: (e: GameEvent) => void): void {
  const p = player(s, s.currentPlayer);
  emit({ type: 'turnStart', player: p.id, turn: s.turn });

  const gold = incomeFor(s, p.id);
  if (gold > 0) {
    p.funds += gold;
    emit({ type: 'income', player: p.id, amount: gold });
  }

  for (const u of unitsOf(s, p.id)) {
    u.done = false;
    const heal = healRateAt(s, u.x, u.y, p.id);
    if (heal > 0) {
      const max = unitDef(u.type).maxHp;
      const amount = Math.min(heal, max - u.hp);
      if (amount > 0) {
        u.hp += amount;
        emit({ type: 'regen', unit: u.id, amount });
      }
    }
  }
}

function checkGameOver(s: GameState, emit: (e: GameEvent) => void): void {
  const before = s.players.filter((p) => p.alive).map((p) => p.id);
  const result = evaluateVictory(s);
  for (const id of before) {
    if (!player(s, id).alive) emit({ type: 'defeat', player: id });
  }
  if (result.team !== null || result.reason) {
    s.phase = 'over';
    s.winnerTeam = result.team;
    s.endReason = result.reason;
    emit({ type: 'gameOver', team: result.team, reason: result.reason });
  }
}

/** Marks every remaining unit done — used by "end turn" confirmation UI. */
export function idleUnits(s: GameState, id = s.currentPlayer): Unit[] {
  return unitsOf(s, id).filter((u) => !u.done);
}

/** Removes a unit outright (used by editor previews and scripted effects). */
export function killUnit(s: GameState, id: number): void {
  removeUnit(s, id);
}
