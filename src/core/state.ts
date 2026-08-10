import { Terrains } from './data/terrain';
import { unitDef } from './data/units';
import { idx } from './grid';
import { DEFAULT_VICTORY, mapFromLevel, resolveRules } from './mapio';
import type {
  Coord,
  GameState,
  LevelData,
  PlayerId,
  PlayerState,
  Unit,
  UnitTypeId,
} from './types';

export function createState(level: LevelData): GameState {
  const map = mapFromLevel(level);
  const rules = resolveRules(level);

  const ownsHQ = (id: PlayerId) =>
    map.owners.some((owner, i) => owner === id && Terrains.get(map.tiles[i]).hq);

  const players: PlayerState[] = level.players.map((p) => ({
    id: p.id,
    name: p.name,
    team: p.team ?? p.id,
    color: p.color,
    controller: p.controller,
    funds: p.funds ?? 0,
    alive: true,
    startedWithHQ: ownsHQ(p.id),
    objectives: p.objectives?.length ? p.objectives : (level.victory ?? DEFAULT_VICTORY),
    ai: { aggression: p.ai?.aggression ?? 0.5 },
  }));
  players.sort((a, b) => a.id - b.id);

  let nextUnitId = 1;
  const units: Unit[] = level.units.map((u) => {
    const def = unitDef(u.unit);
    return {
      id: nextUnitId++,
      type: u.unit,
      owner: u.owner,
      x: u.x,
      y: u.y,
      hp: clampHp(u.hp ?? def.maxHp, def.maxHp),
      done: false,
      capture: 0,
      meta: {},
    };
  });

  const state: GameState = {
    levelId: level.id,
    levelName: level.name,
    map,
    units,
    players,
    rules,
    turn: 1,
    currentPlayer: players[0]?.id ?? 1,
    phase: 'playing',
    winnerTeam: null,
    endReason: '',
    nextUnitId,
  };
  return state;
}

const clampHp = (hp: number, max: number) => Math.max(1, Math.min(max, Math.round(hp)));

/** Structural clone. Used by undo and by the AI to simulate. */
export function cloneState(s: GameState): GameState {
  return {
    ...s,
    map: {
      width: s.map.width,
      height: s.map.height,
      tiles: s.map.tiles.slice(),
      owners: s.map.owners.slice(),
      captureProgress: s.map.captureProgress.slice(),
    },
    units: s.units.map((u) => ({ ...u, meta: { ...u.meta } })),
    players: s.players.map((p) => ({ ...p, ai: { ...p.ai }, objectives: p.objectives.slice() })),
    rules: { ...s.rules },
  };
}

/* ---------------------------------------------------------------- accessors */

export function unitAt(s: GameState, x: number, y: number): Unit | undefined {
  return s.units.find((u) => u.x === x && u.y === y);
}

export const unitAtCoord = (s: GameState, c: Coord): Unit | undefined => unitAt(s, c.x, c.y);

export function unitById(s: GameState, id: number): Unit | undefined {
  return s.units.find((u) => u.id === id);
}

export function requireUnit(s: GameState, id: number): Unit {
  const u = unitById(s, id);
  if (!u) throw new Error(`no unit with id ${id}`);
  return u;
}

export function player(s: GameState, id: PlayerId): PlayerState {
  const p = s.players.find((x) => x.id === id);
  if (!p) throw new Error(`no player with id ${id}`);
  return p;
}

export const teamOf = (s: GameState, id: PlayerId): number =>
  s.players.find((p) => p.id === id)?.team ?? -id;

export const areAllies = (s: GameState, a: PlayerId, b: PlayerId): boolean =>
  a !== 0 && b !== 0 && teamOf(s, a) === teamOf(s, b);

export const areEnemies = (s: GameState, a: PlayerId, b: PlayerId): boolean =>
  a !== 0 && b !== 0 && teamOf(s, a) !== teamOf(s, b);

export const unitsOf = (s: GameState, id: PlayerId): Unit[] => s.units.filter((u) => u.owner === id);

export const enemyUnitsOf = (s: GameState, id: PlayerId): Unit[] =>
  s.units.filter((u) => areEnemies(s, u.owner, id));

export const currentPlayerState = (s: GameState): PlayerState => player(s, s.currentPlayer);

export function tilesOwnedBy(s: GameState, id: PlayerId): Coord[] {
  const out: Coord[] = [];
  for (let i = 0; i < s.map.owners.length; i++) {
    if (s.map.owners[i] === id) out.push({ x: i % s.map.width, y: Math.floor(i / s.map.width) });
  }
  return out;
}

export function hqTilesOf(s: GameState, id: PlayerId): Coord[] {
  return tilesOwnedBy(s, id).filter((c) => Terrains.get(s.map.tiles[idx(s.map, c.x, c.y)]).hq);
}

export function productionTilesOf(s: GameState, id: PlayerId): Coord[] {
  return tilesOwnedBy(s, id).filter(
    (c) => Terrains.get(s.map.tiles[idx(s.map, c.x, c.y)]).produces.length > 0,
  );
}

/** Recruitable list for a tile, filtered by what the owner can afford. */
export function recruitOptions(s: GameState, c: Coord): { unit: UnitTypeId; cost: number; affordable: boolean }[] {
  const i = idx(s.map, c.x, c.y);
  const terrain = Terrains.get(s.map.tiles[i]);
  const owner = s.map.owners[i];
  if (owner !== s.currentPlayer) return [];
  const funds = player(s, owner).funds;
  return terrain.produces.map((id) => {
    const def = unitDef(id);
    return { unit: id, cost: def.cost, affordable: def.cost <= funds };
  });
}

export function spawnUnit(
  s: GameState,
  type: UnitTypeId,
  owner: PlayerId,
  at: Coord,
  opts: { hp?: number; done?: boolean } = {},
): Unit {
  const def = unitDef(type);
  const u: Unit = {
    id: s.nextUnitId++,
    type,
    owner,
    x: at.x,
    y: at.y,
    hp: clampHp(opts.hp ?? def.maxHp, def.maxHp),
    done: opts.done ?? false,
    capture: 0,
    meta: {},
  };
  s.units.push(u);
  return u;
}

export function removeUnit(s: GameState, id: number): void {
  const i = s.units.findIndex((u) => u.id === id);
  if (i >= 0) s.units.splice(i, 1);
}
