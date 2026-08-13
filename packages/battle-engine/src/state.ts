import {
  type BattleResourceSystem,
  DefaultBattleResources,
  playerResource,
} from './resources';
import { idx } from './grid';
import { DEFAULT_VICTORY, mapFromLevel, resolveRules } from './mapio';
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
import { GlobalContentCatalog, type ContentCatalog } from './content-pack';
import { cloneUnitState } from './unit-state';

const cloneResources = (resources: ResourceAccounts = {}): ResourceAccounts =>
  Object.fromEntries(Object.entries(resources).map(([id, account]) => [id, { ...account }]));

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
    career: (() => {
      const requested = source.career
        ? content.careers.tryGet(source.career)
        : content.careers.all()
            .filter((career) => career.unitType === source.unit)
            .sort((left, right) => left.tier - right.tier || left.id.localeCompare(right.id))[0];
      if (requested && requested.unitType !== source.unit) {
        throw new Error(`career ${requested.id} does not use unit type "${source.unit}"`);
      }
      const current = requested?.id ?? null;
      const unlocked = [...new Set([...(source.unlockedCareers ?? []), ...(current ? [current] : [])])];
      return {
        current,
        unlocked,
        mastery: {
          ...(current ? { [current]: Math.max(0, Math.round(source.rankProgress ?? 0)) } : {}),
          ...(source.careerMastery ?? {}),
        },
      };
    })(),
    learnedAbilities: [...new Set(source.learnedAbilities ?? [])],
    meta: {},
  };
}

export function createState(level: LevelData, content: ContentCatalog = GlobalContentCatalog): GameState {
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

  const deployment = (() => {
    if (!level.deployment) return null;
    const order = [...new Set(level.deployment.order ?? level.deployment.zones.map((entry) => entry.player))];
    if (order.length === 0) throw new Error('deployment must include at least one player');
    for (const owner of order) {
      if (!players.some((candidate) => candidate.id === owner)) throw new Error(`deployment references unknown player ${owner}`);
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
  })();

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
export function cloneState(s: GameState): GameState {
  return {
    ...s,
    map: {
      width: s.map.width,
      height: s.map.height,
      tiles: s.map.tiles.slice(),
      owners: s.map.owners.slice(),
      captureProgress: s.map.captureProgress.slice(),
      elevation: s.map.elevation.slice(),
      cliffs: s.map.cliffs.map((edge) => ({ from: { ...edge.from }, to: { ...edge.to } })),
      directionalCover: s.map.directionalCover.map((cover) => ({ at: { ...cover.at }, sides: { ...cover.sides } })),
    },
    units: s.units.map(cloneUnitState),
    composites: s.composites.map((composite) => ({
      ...composite,
      parts: composite.parts.slice(),
      tags: composite.tags.slice(),
    })),
    embarkedUnits: s.embarkedUnits.map((entry) => ({
      carrier: entry.carrier,
      unit: cloneUnitState(entry.unit),
    })),
    structures: s.structures.map((structure) => ({
      ...structure,
      statuses: structure.statuses.map((status) => ({ ...status })),
    })),
    markers: s.markers.map((marker) => ({
      ...marker,
      at: { ...marker.at },
      fallenUnit: marker.fallenUnit ? cloneUnitState(marker.fallenUnit) : undefined,
      meta: { ...marker.meta },
    })),
    commanders: s.commanders.map((commander) => ({
      ...commander,
      aura: { ...commander.aura },
      turnGrants: commander.turnGrants.map((grant) => ({ ...grant })),
      tactics: commander.tactics.slice(),
      usedTactics: commander.usedTactics.slice(),
    })),
    players: s.players.map((p) => ({
      ...p,
      resources: cloneResources(p.resources),
      ai: { ...p.ai },
      objectives: p.objectives.slice(),
      objectiveStates: Object.fromEntries(
        Object.entries(p.objectiveStates).map(([id, runtime]) => [id, { ...runtime }]),
      ),
    })),
    rules: { ...s.rules },
    deployment: s.deployment
      ? {
          order: s.deployment.order.slice(),
          currentIndex: s.deployment.currentIndex,
          assignments: s.deployment.assignments.map((entry) => ({ ...entry, unitIds: entry.unitIds.slice() })),
        }
      : null,
    scenario: {
      variables: { ...s.scenario.variables },
      zones: Object.fromEntries(
        Object.entries(s.scenario.zones).map(([id, cells]) => [
          id,
          cells.map((cell) => ({ ...cell })),
        ]),
      ),
      overlays: s.scenario.overlays.map((overlay) => ({
        ...overlay,
        cells: overlay.cells.map((cell) => ({ ...cell })),
      })),
      triggers: s.scenario.triggers,
      firedTriggerIds: s.scenario.firedTriggerIds.slice(),
      triggerRuntime: Object.fromEntries(
        Object.entries(s.scenario.triggerRuntime).map(([id, runtime]) => [id, { ...runtime }]),
      ),
      eventCounts: { ...s.scenario.eventCounts },
      zoneTags: Object.fromEntries(
        Object.entries(s.scenario.zoneTags).map(([id, tags]) => [id, tags.slice()]),
      ),
      engagementRules: s.scenario.engagementRules.map((rule) => ({
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

export function hqTilesOf(s: GameState, id: PlayerId, content: ContentCatalog = GlobalContentCatalog): Coord[] {
  return tilesOwnedBy(s, id).filter((c) => content.terrains.get(s.map.tiles[idx(s.map, c.x, c.y)]).hq);
}

export function productionTilesOf(s: GameState, id: PlayerId, content: ContentCatalog = GlobalContentCatalog): Coord[] {
  return tilesOwnedBy(s, id).filter(
    (c) => content.terrains.get(s.map.tiles[idx(s.map, c.x, c.y)]).produces.length > 0,
  );
}

/** Recruitable list for a tile, filtered by what the owner can afford. */
export function recruitOptions(
  s: GameState,
  c: Coord,
  resources: BattleResourceSystem = DefaultBattleResources,
  content: ContentCatalog = GlobalContentCatalog,
): { unit: UnitTypeId; costs: ResourceAmount[]; affordable: boolean }[] {
  const i = idx(s.map, c.x, c.y);
  const terrain = content.terrains.get(s.map.tiles[i]);
  const owner = s.map.owners[i];
  if (owner !== s.currentPlayer) return [];
  const subject = playerResource(player(s, owner));
  return terrain.produces.map((id) => {
    const def = content.units.get(id);
    return {
      unit: id,
      costs: def.recruitCosts.map((cost) => ({ ...cost })),
      affordable: def.recruitCosts.every((cost) => resources.canSpend(cost.resource, subject, cost.amount)),
    };
  });
}

export function spawnUnit(
  s: GameState,
  type: UnitTypeId,
  owner: PlayerId,
  at: Coord,
  opts: { hp?: number; done?: boolean; source?: Partial<LevelUnit> } = {},
  content: ContentCatalog = GlobalContentCatalog,
): Unit {
  const source: LevelUnit = {
    x: at.x,
    y: at.y,
    unit: type,
    owner,
    ...opts.source,
    hp: opts.hp ?? opts.source?.hp,
  };
  const u = createUnitState(source, s.nextUnitId++, opts.done ?? false, content);
  s.units.push(u);
  return u;
}

export function removeUnit(s: GameState, id: number): void {
  const i = s.units.findIndex((u) => u.id === id);
  if (i < 0) return;
  const unit = s.units[i];
  s.map.captureProgress[idx(s.map, unit.x, unit.y)] = 0;
  s.units.splice(i, 1);
}
