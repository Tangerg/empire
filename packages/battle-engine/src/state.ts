import {
  type BattleResourceSystem,
  playerResource,
} from './resources';
import { idx } from './grid';
import { createRandomState } from './random';
import { DEFAULT_VICTORY, mapFromLevel, resolveRules } from './level/index';
import { assignObjectiveIds, createObjectiveStates } from './objective-model';
import type {
  Coord,
  GameState,
  LevelData,
  PlayerId,
  PlayerState,
  Unit,
  UnitTypeId,
  ResourceAccounts,
  ResourceAmount,
  LevelUnit,
} from './types';
import { type ContentCatalog } from './content-pack';
import { cloneUnitState } from './unit-state';

const cloneResources = (resources: ResourceAccounts = {}): ResourceAccounts =>
  Object.fromEntries(Object.entries(resources).map(([id, account]) => [id, { ...account }]));

/**
 * The career a placed unit starts in: the one it names, or the lowest tier its
 * type has. Was written as an immediately-invoked function inside the unit
 * literal, which is a function that has refused to be named.
 */
function initialCareer(source: LevelUnit, content: ContentCatalog): Unit['career'] {
  const requested = source.career
    ? content.careers.tryGet(source.career)
    : content.careers.all()
        .filter((career) => career.unitType === source.unit)
        .sort((left, right) => left.tier - right.tier || left.id.localeCompare(right.id))[0];
  if (requested && requested.unitType !== source.unit) {
    throw new Error(`career ${requested.id} does not use unit type "${source.unit}"`);
  }
  const current = requested?.id ?? null;
  return {
    current,
    unlocked: [...new Set([...(source.unlockedCareers ?? []), ...(current ? [current] : [])])],
    mastery: {
      ...(current ? { [current]: Math.max(0, Math.round(source.rankProgress ?? 0)) } : {}),
      ...(source.careerMastery ?? {}),
    },
  };
}

function createUnitState(
  source: LevelUnit,
  id: number,
  done: boolean,
  content: ContentCatalog,
): Unit {
  const def = content.units.get(source.unit);
  return {
    id,
    key: source.key,
    type: source.unit,
    owner: source.owner,
    x: source.x,
    y: source.y,
    hp: clampHp(source.hp ?? def.maxHp, def.maxHp),
    done,
    capture: 0,
    statuses: [],
    weaponState: Object.fromEntries(
      def.weapons.map((weaponId) => {
        const weapon = content.weapons.get(weaponId);
        return [weaponId, { cooldownRemaining: 0, resources: cloneResources(weapon.resources) }];
      }),
    ),
    commanderId: source.commander ?? null,
    rank: source.rank ?? 0,
    rankProgress: Math.max(0, Math.round(source.rankProgress ?? 0)),
    resources: cloneResources({ ...def.resources, ...(source.resources ?? {}) }),
    reaction: source.reaction ?? def.defaultReaction,
    reactionUsedRound: -1,
    facing: source.facing ?? 'south',
    morale: {
      current: Math.max(1, Math.min(def.morale?.maximum ?? 100, Math.round(source.morale ?? def.morale?.maximum ?? 100))),
      maximum: def.morale?.maximum ?? 100,
      resilience: Math.max(0, Math.min(0.9, def.morale?.resilience ?? 0)),
    },
    formation: source.formation ?? null,
    directive: {
      mode: source.directive?.mode ?? 'assault',
      zone: source.directive?.zone,
      waypoints: source.directive?.waypoints?.map((point) => ({ ...point })) ?? [],
      cursor: Math.max(0, Math.round(source.directive?.cursor ?? 0)),
    },
    career: initialCareer(source, content),
    learnedAbilities: [...new Set(source.learnedAbilities ?? [])],
    meta: {},
  };
}

/** Construction knobs that are not part of the level document. */
export interface CreateStateOptions {
  /** Seeds the battle's random stream; a stable default keeps runs repeatable. */
  seed?: number;
}

/** Stable per-level default so an unseeded battle is still reproducible. */
function defaultSeed(level: LevelData): number {
  let hash = 0x811c9dc5;
  for (const character of level.id) {
    hash ^= character.charCodeAt(0);
    hash = Math.imul(hash, 0x01000193);
  }
  return hash >>> 0;
}

/**
 * The pre-battle deployment phase, resolved against the units that exist.
 *
 * Everything it throws is an invariant of the *level*, not of play: a zone that
 * names nobody, a unit placed in two zones, a side with no zone at all. Better
 * to refuse the level than to open a deployment phase nobody can finish.
 */
function createDeployment(
  level: LevelData,
  players: readonly PlayerState[],
  units: readonly Unit[],
  zones: Record<string, Coord[]>,
): GameState['deployment'] {
  if (!level.deployment) return null;
  const order = [...new Set(level.deployment.order ?? level.deployment.zones.map((entry) => entry.player))];
  if (order.length === 0) throw new Error('deployment must include at least one player');
  for (const owner of order) {
    if (!players.some((candidate) => candidate.id === owner)) {
      throw new Error(`deployment references unknown player ${owner}`);
    }
  }

  const assignments = level.deployment.zones.map((entry) => {
    if (!zones[entry.zone]) throw new Error(`deployment references unknown zone "${entry.zone}"`);
    if (!players.some((candidate) => candidate.id === entry.player)) {
      throw new Error(`deployment references unknown player ${entry.player}`);
    }
    const unitIds = units
      .filter((unit) => unit.owner === entry.player && (!entry.unitKeys || (unit.key && entry.unitKeys.includes(unit.key))))
      .map((unit) => unit.id);
    if (entry.unitKeys && unitIds.length !== entry.unitKeys.length) {
      throw new Error(`deployment zone "${entry.zone}" references an unknown or wrong-owner unit key`);
    }
    return { player: entry.player, zone: entry.zone, unitIds };
  });

  const assigned = new Set<number>();
  for (const assignment of assignments) {
    for (const unitId of assignment.unitIds) {
      if (assigned.has(unitId)) throw new Error(`unit ${unitId} belongs to multiple deployment zones`);
      assigned.add(unitId);
    }
  }
  for (const owner of order) {
    if (!assignments.some((entry) => entry.player === owner)) {
      throw new Error(`deployment player ${owner} has no deployment zone`);
    }
  }
  return { order, currentIndex: 0, assignments };
}

export function createState(
  level: LevelData,
  content: ContentCatalog,
  options: CreateStateOptions = {},
): GameState {
  const map = mapFromLevel(level, content);
  const rules = resolveRules(level);

  const ownsHQ = (id: PlayerId) =>
    map.owners.some((owner, i) => owner === id && content.terrains.get(map.tiles[i]).hq);

  const players: PlayerState[] = level.players.map((p) => {
    const objectives = assignObjectiveIds(
      p.objectives?.length ? p.objectives : (level.victory ?? DEFAULT_VICTORY),
      `player-${p.id}`,
    );
    return {
      id: p.id,
      name: p.name,
      team: p.team ?? p.id,
      color: p.color,
      controller: p.controller,
      resources: cloneResources(p.resources),
      alive: true,
      startedWithHQ: ownsHQ(p.id),
      objectives,
      objectiveStates: createObjectiveStates(objectives),
      ai: { aggression: p.ai?.aggression ?? 0.5 },
    };
  });
  players.sort((a, b) => a.id - b.id);

  let nextUnitId = 1;
  const units: Unit[] = level.units.map((unit) => createUnitState(unit, nextUnitId++, false, content));

  const commanders = (level.commanders ?? []).map((entry) => {
    const leader = units.find((unit) => unit.key === entry.unitKey);
    if (!leader) throw new Error(`commander ${entry.id} references unknown unit key "${entry.unitKey}"`);
    if (leader.commanderId === null) leader.commanderId = entry.id;
    return {
      id: entry.id,
      unitId: leader.id,
      owner: leader.owner,
      radius: Math.max(0, entry.radius),
      aura: {
        attackMultiplier: entry.aura?.attackMultiplier ?? 1,
        defenseDelta: entry.aura?.defenseDelta ?? 0,
        movementDelta: entry.aura?.movementDelta ?? 0,
      },
      turnGrants: (entry.turnGrants ?? []).map((grant) => ({ ...grant })),
      tactics: entry.tactics ?? [],
      usedTactics: [],
    };
  });

  const structures = (level.structures ?? []).map((entry) => {
    const def = content.structures.get(entry.type);
    return {
      id: entry.id,
      type: entry.type,
      owner: entry.owner ?? 0,
      x: entry.x,
      y: entry.y,
      hp: Math.max(0, Math.min(def.maxHp, Math.round(entry.hp ?? def.maxHp))),
      disabled: entry.disabled ?? false,
      statuses: [],
    };
  });
  const structureIds = new Set(structures.map((structure) => structure.id));
  const composites = (level.composites ?? []).map((entry) => {
    const parts = [...new Set(entry.parts)];
    for (const part of parts) {
      if (!structureIds.has(part)) throw new Error(`composite ${entry.id} references unknown structure "${part}"`);
    }
    if (parts.length === 0) throw new Error(`composite ${entry.id} must include at least one structure`);
    return {
      id: entry.id,
      parts,
      minimumNeutralized: Math.max(1, Math.min(parts.length, Math.round(entry.minimumNeutralized ?? parts.length))),
      tags: [...new Set(entry.tags ?? [])],
    };
  });

  const zones = Object.fromEntries(
    (level.scenario?.zones ?? []).map((zone) => [zone.id, zone.cells.map((cell) => ({ ...cell }))]),
  );
  const overlays = (level.scenario?.overlays ?? []).map((overlay) => ({
    id: overlay.id,
    type: overlay.type,
    cells: (zones[overlay.zone] ?? []).map((cell) => ({ ...cell })),
    remainingRounds: overlay.remainingRounds ?? null,
  }));

  const deployment = createDeployment(level, players, units, zones);

  const state: GameState = {
    levelId: level.id,
    levelName: level.name,
    map,
    units,
    structures,
    composites,
    embarkedUnits: [],
    markers: [],
    commanders,
    players,
    rules,
    turn: 1,
    currentPlayer: deployment?.order[0] ?? players[0]?.id ?? 1,
    phase: deployment ? 'deployment' : 'playing',
    winnerTeam: null,
    endReason: '',
    nextUnitId,
    nextMarkerId: 1,
    deployment,
    turnOrder: { policy: rules.turnOrder, activeUnit: null, data: {} },
    actorTurns: 0,
    pendingCasts: [],
    random: createRandomState(options.seed ?? defaultSeed(level)),
    scenario: {
      variables: { ...(level.scenario?.variables ?? {}) },
      zones,
      overlays,
      triggers: level.scenario?.triggers ?? [],
      firedTriggerIds: [],
      triggerRuntime: {},
      eventCounts: {},
      zoneTags: Object.fromEntries(
        (level.scenario?.zones ?? []).map((zone) => [zone.id, [...new Set(zone.tags ?? [])]]),
      ),
      engagementRules: (level.scenario?.engagementRules ?? []).map((rule) => ({
        ...rule,
        players: rule.players?.slice(),
      })),
    },
  };
  return state;
}

const clampHp = (hp: number, max: number) => Math.max(1, Math.min(max, Math.round(hp)));

/** Structural clone. Used by undo and by the AI to simulate. */
export function cloneState(state: GameState): GameState {
  return {
    ...state,
    map: {
      width: state.map.width,
      height: state.map.height,
      tiles: state.map.tiles.slice(),
      owners: state.map.owners.slice(),
      captureProgress: state.map.captureProgress.slice(),
      elevation: state.map.elevation.slice(),
      cliffs: state.map.cliffs.map((edge) => ({ from: { ...edge.from }, to: { ...edge.to } })),
      directionalCover: state.map.directionalCover.map((cover) => ({ at: { ...cover.at }, sides: { ...cover.sides } })),
    },
    units: state.units.map(cloneUnitState),
    turnOrder: { ...state.turnOrder, data: { ...state.turnOrder.data } },
    pendingCasts: state.pendingCasts.map((cast) => ({
      ...cast,
      target: { ...cast.target },
      origin: { ...cast.origin },
    })),
    random: { seed: state.random.seed, counters: { ...state.random.counters } },
    composites: state.composites.map((composite) => ({
      ...composite,
      parts: composite.parts.slice(),
      tags: composite.tags.slice(),
    })),
    embarkedUnits: state.embarkedUnits.map((entry) => ({
      carrier: entry.carrier,
      unit: cloneUnitState(entry.unit),
    })),
    structures: state.structures.map((structure) => ({
      ...structure,
      statuses: structure.statuses.map((status) => ({ ...status })),
    })),
    markers: state.markers.map((marker) => ({
      ...marker,
      at: { ...marker.at },
      fallenUnit: marker.fallenUnit ? cloneUnitState(marker.fallenUnit) : undefined,
      meta: { ...marker.meta },
    })),
    commanders: state.commanders.map((commander) => ({
      ...commander,
      aura: { ...commander.aura },
      turnGrants: commander.turnGrants.map((grant) => ({ ...grant })),
      tactics: commander.tactics.slice(),
      usedTactics: commander.usedTactics.slice(),
    })),
    players: state.players.map((p) => ({
      ...p,
      resources: cloneResources(p.resources),
      ai: { ...p.ai },
      objectives: p.objectives.slice(),
      objectiveStates: Object.fromEntries(
        Object.entries(p.objectiveStates).map(([id, runtime]) => [id, { ...runtime }]),
      ),
    })),
    rules: { ...state.rules },
    deployment: state.deployment
      ? {
          order: state.deployment.order.slice(),
          currentIndex: state.deployment.currentIndex,
          assignments: state.deployment.assignments.map((entry) => ({ ...entry, unitIds: entry.unitIds.slice() })),
        }
      : null,
    scenario: {
      variables: { ...state.scenario.variables },
      zones: Object.fromEntries(
        Object.entries(state.scenario.zones).map(([id, cells]) => [
          id,
          cells.map((cell) => ({ ...cell })),
        ]),
      ),
      overlays: state.scenario.overlays.map((overlay) => ({
        ...overlay,
        cells: overlay.cells.map((cell) => ({ ...cell })),
      })),
      triggers: state.scenario.triggers,
      firedTriggerIds: state.scenario.firedTriggerIds.slice(),
      triggerRuntime: Object.fromEntries(
        Object.entries(state.scenario.triggerRuntime).map(([id, runtime]) => [id, { ...runtime }]),
      ),
      eventCounts: { ...state.scenario.eventCounts },
      zoneTags: Object.fromEntries(
        Object.entries(state.scenario.zoneTags).map(([id, tags]) => [id, tags.slice()]),
      ),
      engagementRules: state.scenario.engagementRules.map((rule) => ({
        ...rule,
        players: rule.players?.slice(),
      })),
    },
  };
}

/** Restores the aggregate root in place so external owners keep its identity. */
export function restoreState(target: GameState, snapshot: GameState): void {
  Object.assign(target, snapshot);
}

/* ---------------------------------------------------------------- accessors */

export function unitAt(state: GameState, x: number, y: number): Unit | undefined {
  return state.units.find((u) => u.x === x && u.y === y);
}

export const unitAtCoord = (state: GameState, c: Coord): Unit | undefined => unitAt(state, c.x, c.y);

export function unitById(state: GameState, id: number): Unit | undefined {
  return state.units.find((u) => u.id === id);
}

export function requireUnit(state: GameState, id: number): Unit {
  const u = unitById(state, id);
  if (!u) throw new Error(`no unit with id ${id}`);
  return u;
}

export function player(state: GameState, id: PlayerId): PlayerState {
  const p = state.players.find((x) => x.id === id);
  if (!p) throw new Error(`no player with id ${id}`);
  return p;
}

export const teamOf = (state: GameState, id: PlayerId): number =>
  state.players.find((p) => p.id === id)?.team ?? -id;

export const areAllies = (state: GameState, a: PlayerId, b: PlayerId): boolean =>
  a !== 0 && b !== 0 && teamOf(state, a) === teamOf(state, b);

export const areEnemies = (state: GameState, a: PlayerId, b: PlayerId): boolean =>
  a !== 0 && b !== 0 && teamOf(state, a) !== teamOf(state, b);

export const unitsOf = (state: GameState, id: PlayerId): Unit[] => state.units.filter((u) => u.owner === id);

export const enemyUnitsOf = (state: GameState, id: PlayerId): Unit[] =>
  state.units.filter((u) => areEnemies(state, u.owner, id));

export const currentPlayerState = (state: GameState): PlayerState => player(state, state.currentPlayer);

export function tilesOwnedBy(state: GameState, id: PlayerId): Coord[] {
  const out: Coord[] = [];
  for (let i = 0; i < state.map.owners.length; i++) {
    if (state.map.owners[i] === id) out.push({ x: i % state.map.width, y: Math.floor(i / state.map.width) });
  }
  return out;
}

export function hqTilesOf(state: GameState, id: PlayerId, content: ContentCatalog): Coord[] {
  return tilesOwnedBy(state, id).filter((c) => content.terrains.get(state.map.tiles[idx(state.map, c.x, c.y)]).hq);
}

export function productionTilesOf(state: GameState, id: PlayerId, content: ContentCatalog): Coord[] {
  return tilesOwnedBy(state, id).filter(
    (c) => content.terrains.get(state.map.tiles[idx(state.map, c.x, c.y)]).produces.length > 0,
  );
}

/** Recruitable list for a tile, filtered by what the owner can afford. */
/** One line of the recruitment menu: what it is, what it costs, can we pay. */
export interface RecruitOption {
  readonly unit: UnitTypeId;
  readonly costs: ResourceAmount[];
  readonly affordable: boolean;
}

export function recruitOptions(
  state: GameState,
  at: Coord,
  resources: BattleResourceSystem,
  content: ContentCatalog,
): RecruitOption[] {
  const tile = idx(state.map, at.x, at.y);
  const terrain = content.terrains.get(state.map.tiles[tile]);
  const owner = state.map.owners[tile];
  if (owner !== state.currentPlayer) return [];
  const account = playerResource(player(state, owner));
  return terrain.produces.map((id) => {
    const definition = content.units.get(id);
    return {
      unit: id,
      costs: definition.recruitCosts.map((cost) => ({ ...cost })),
      affordable: definition.recruitCosts.every((cost) => resources.canSpend(cost.resource, account, cost.amount)),
    };
  });
}

export function spawnUnit(
  content: ContentCatalog,
  state: GameState,
  type: UnitTypeId,
  owner: PlayerId,
  at: Coord,
  opts: { hp?: number; done?: boolean; source?: Partial<LevelUnit> } = {},
): Unit {
  const source: LevelUnit = {
    x: at.x,
    y: at.y,
    unit: type,
    owner,
    ...opts.source,
    hp: opts.hp ?? opts.source?.hp,
  };
  const u = createUnitState(source, state.nextUnitId++, opts.done ?? false, content);
  state.units.push(u);
  return u;
}

export function removeUnit(state: GameState, id: number): void {
  const i = state.units.findIndex((u) => u.id === id);
  if (i < 0) return;
  const unit = state.units[i];
  state.map.captureProgress[idx(state.map, unit.x, unit.y)] = 0;
  state.units.splice(i, 1);
}
