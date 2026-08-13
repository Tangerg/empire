import type { LevelData, Objective } from '../types';
import type { LevelIssueLog } from './issues';

/**
 * Every child a composite objective declares.
 *
 * A structural walk over the document, deliberately not the objective handler
 * registry's `children`: this lints a level *file*, and the editor that shows
 * the result has a content catalog but no composed ruleset. The cost is that a
 * content-defined composite kind is not traversed here — its own children go
 * unchecked until the battle composes the rules that know about them.
 */
export function declaredChildObjectives(objective: Objective): Objective[] {
  if (objective.type === 'all' || objective.type === 'any' || objective.type === 'sequence') {
    return objective.objectives;
  }
  if (objective.type === 'optional' || objective.type === 'failOn') return [objective.objective];
  return [];
}

/** The objectives a player actually plays under: their own, or the shared ones. */
export function objectivesOf(level: LevelData, player: LevelData['players'][number]): Objective[] {
  return player.objectives?.length ? player.objectives : (level.victory ?? []);
}

/**
 * Every name a level declares, gathered before a single reference is checked.
 *
 * This is the half of validation that used to make the other half impossible to
 * break up. Each section built its own `Set` of ids as a local variable, so the
 * order of four hundred statements was load-bearing: the trigger checks worked
 * only because the zone loop happened to run earlier and leave `zoneIds` behind.
 * Nothing could be moved, tested, or read in isolation.
 *
 * Gathering is also where declaring the same name twice is caught, because that
 * is a fact about the declaration and not about any one reference to it.
 */
export class LevelDeclarations {
  readonly players: ReadonlySet<number>;
  readonly unitKeys: ReadonlySet<string>;
  readonly commanders: ReadonlySet<string>;
  readonly structures: ReadonlySet<string>;
  readonly composites: ReadonlySet<string>;
  readonly zones: ReadonlySet<string>;
  readonly engagementRules: ReadonlySet<string>;
  readonly overlays: ReadonlySet<string>;
  readonly triggers: ReadonlySet<string>;
  /** Objective ids per player, including those nested inside composites. */
  readonly objectives: ReadonlyMap<number, ReadonlySet<string>>;

  constructor(level: LevelData, private readonly log: LevelIssueLog) {
    this.players = this.collectPlayerIds(level);
    this.unitKeys = this.collectUnitKeys(level);
    this.commanders = this.collectIds(level.commanders ?? [], '指挥官');
    this.structures = this.collectIds(level.structures ?? [], '结构');
    this.composites = this.collectIds(level.composites ?? [], '复合目标');
    this.zones = this.collectIds(level.scenario?.zones ?? [], '区域');
    this.engagementRules = this.collectIds(level.scenario?.engagementRules ?? [], '交战规则');
    this.overlays = this.collectIds(level.scenario?.overlays ?? [], '地形覆盖');
    this.triggers = this.collectIds(level.scenario?.triggers ?? [], '触发器');
    this.objectives = this.collectObjectiveIds(level);
  }

  /** Objective ids one player may be referred to by, empty for a stranger. */
  objectivesOfPlayer(player: number): ReadonlySet<string> {
    return this.objectives.get(player) ?? new Set();
  }

  /** One naming rule for every family of named things a level declares. */
  private collectIds(entries: readonly { id: string }[], subject: string): ReadonlySet<string> {
    const ids = new Set<string>();
    for (const entry of entries) {
      if (!entry.id.trim()) this.log.error(`${subject}缺少 id`);
      if (ids.has(entry.id)) this.log.error(`${subject} id 重复：${entry.id}`);
      ids.add(entry.id);
    }
    return ids;
  }

  private collectPlayerIds(level: LevelData): ReadonlySet<number> {
    const ids = new Set<number>();
    for (const player of level.players) {
      if (player.id < 1) this.log.error(`玩家 id 必须 >= 1（发现 ${player.id}）`);
      if (ids.has(player.id)) this.log.error(`玩家 id 重复：${player.id}`);
      ids.add(player.id);
    }
    return ids;
  }

  /** A unit key is optional; only the ones that exist have to be unique. */
  private collectUnitKeys(level: LevelData): ReadonlySet<string> {
    const keys = new Set<string>();
    for (const unit of level.units) {
      if (!unit.key) continue;
      if (keys.has(unit.key)) this.log.error(`单位 key 重复：${unit.key}`);
      keys.add(unit.key);
    }
    return keys;
  }

  private collectObjectiveIds(level: LevelData): ReadonlyMap<number, ReadonlySet<string>> {
    const byPlayer = new Map<number, ReadonlySet<string>>();
    for (const player of level.players) {
      const ids = new Set<string>();
      byPlayer.set(player.id, ids);
      const visit = (objective: Objective): void => {
        if (objective.id) {
          if (ids.has(objective.id)) this.log.error(`玩家 ${player.id} 的目标 id 重复：${objective.id}`);
          ids.add(objective.id);
        }
        for (const child of declaredChildObjectives(objective)) visit(child);
      };
      objectivesOf(level, player).forEach(visit);
    }
    return byPlayer;
  }
}
