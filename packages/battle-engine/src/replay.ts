import type { BattleEngine } from './engine';
import type { Action, GameState, LevelData } from './types';

/**
 * Replay and determinism verification.
 *
 * The battle core is a pure reducer over a serialisable state with a
 * state-resident random stream, which makes a replay almost free: level id +
 * seed + the action list is enough to reconstruct the battle exactly. That is
 * worth having for its own sake (bug reports, balance analysis, netplay later),
 * but the immediate value is as a guard: a replay that diverges proves something
 * in the engine reached for ambient state — wall-clock time, Math.random, or an
 * iteration order that is not stable.
 */
export interface BattleReplay {
  readonly schema: 1;
  readonly levelId: string;
  readonly seed: number;
  readonly actions: readonly Action[];
}

/** Fields that define battle outcome. Presentation-only state is excluded. */
function canonical(state: GameState): unknown {
  return {
    turn: state.turn,
    currentPlayer: state.currentPlayer,
    phase: state.phase,
    winnerTeam: state.winnerTeam,
    endReason: state.endReason,
    turnOrder: state.turnOrder,
    random: state.random,
    map: {
      owners: state.map.owners,
      tiles: state.map.tiles,
      captureProgress: state.map.captureProgress,
      elevation: state.map.elevation,
    },
    units: [...state.units]
      .sort((left, right) => left.id - right.id)
      .map((unit) => ({
        id: unit.id,
        type: unit.type,
        owner: unit.owner,
        x: unit.x,
        y: unit.y,
        hp: unit.hp,
        done: unit.done,
        rank: unit.rank,
        rankProgress: unit.rankProgress,
        facing: unit.facing,
        morale: unit.morale.current,
        statuses: [...unit.statuses]
          .sort((left, right) => left.id.localeCompare(right.id))
          .map((status) => [status.id, status.remaining, status.stacks]),
        resources: sortedEntries(unit.resources, (account) => account.current),
      })),
    structures: [...state.structures]
      .sort((left, right) => left.id.localeCompare(right.id))
      .map((structure) => [structure.id, structure.owner, structure.hp, structure.disabled]),
    players: [...state.players]
      .sort((left, right) => left.id - right.id)
      .map((entry) => ({
        id: entry.id,
        alive: entry.alive,
        team: entry.team,
        resources: sortedEntries(entry.resources, (account) => account.current),
        objectives: sortedEntries(entry.objectiveStates, (runtime) => runtime.status),
      })),
    scenario: {
      variables: sortedEntries(state.scenario.variables, (value) => value),
      firedTriggerIds: [...state.scenario.firedTriggerIds].sort(),
      overlays: [...state.scenario.overlays]
        .sort((left, right) => left.id.localeCompare(right.id))
        .map((overlay) => [overlay.id, overlay.type, overlay.remainingRounds]),
    },
    markers: [...state.markers]
      .sort((left, right) => left.id - right.id)
      .map((marker) => [marker.id, marker.kind, marker.at.x, marker.at.y, marker.owner]),
  };
}

function sortedEntries<T, R>(record: Record<string, T>, project: (value: T) => R): [string, R][] {
  return Object.keys(record)
    .sort()
    .map((key) => [key, project(record[key])]);
}

/** Stable 32-bit structural digest of everything that defines the outcome. */
export function hashState(state: GameState): string {
  const text = JSON.stringify(canonical(state));
  let hash = 0x811c9dc5;
  for (let index = 0; index < text.length; index++) {
    hash ^= text.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }
  return (hash >>> 0).toString(16).padStart(8, '0');
}

/** Accumulates the action log a replay needs. */
export class BattleRecorder {
  private readonly log: Action[] = [];

  constructor(
    readonly levelId: string,
    readonly seed: number,
  ) {}

  record(action: Action): void {
    this.log.push(structuredClone(action));
  }

  replay(): BattleReplay {
    return {
      schema: 1,
      levelId: this.levelId,
      seed: this.seed,
      actions: this.log.map((action) => structuredClone(action)),
    };
  }
}

export interface ReplayOutcome {
  state: GameState;
  /** Index of the first action the current rules could not apply. */
  divergedAt: number | null;
  reason: string;
}

/**
 * Re-simulates a recorded battle. `divergedAt` is non-null when the recorded
 * action list no longer applies, which is exactly the signal you want after a
 * rules change: it names the first action whose legality moved.
 */
export function replayBattle(
  engine: BattleEngine,
  level: LevelData,
  replay: BattleReplay,
): ReplayOutcome {
  const state = engine.createState(level, { seed: replay.seed });
  for (let index = 0; index < replay.actions.length; index++) {
    try {
      engine.dispatch(state, replay.actions[index]);
    } catch (error) {
      return { state, divergedAt: index, reason: (error as Error).message };
    }
    if (state.phase === 'over') {
      return {
        state,
        divergedAt: index === replay.actions.length - 1 ? null : index + 1,
        reason: index === replay.actions.length - 1 ? '' : '对局提前结束',
      };
    }
  }
  return { state, divergedAt: null, reason: '' };
}
