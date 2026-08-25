import type { BattleEngine } from './engine';
import { DomainInvariantError, IllegalActionError } from './domain/errors';
import {
  requirePersistentRuleset,
  rulesetDifferences,
  type BattleRulesetManifest,
} from './ruleset-manifest';
import type { Action, GameState, LevelData } from './types';

/**
 * Replay and determinism verification.
 *
 * The battle core is a pure reducer over a serialisable state with a
 * state-resident random stream, which makes a replay almost free: level id,
 * seed and the action list reconstruct the battle, while the ruleset manifest
 * and boundary hashes prove it was reconstructed by the same rules. That is
 * worth having for its own sake (bug reports, balance analysis, netplay later),
 * but the immediate value is as a guard: a replay that diverges proves something
 * in the engine reached for ambient state — wall-clock time, Math.random, or an
 * iteration order that is not stable.
 */
export interface BattleReplay {
  readonly schema: 1;
  readonly levelId: string;
  readonly seed: number;
  readonly ruleset: BattleRulesetManifest;
  readonly initialStateHash: string;
  readonly actions: readonly Action[];
  readonly finalStateHash: string;
}

/**
 * State that cannot decide anything, and so is left out of the digest.
 *
 * Everything else is in, which is the point of writing the exclusions instead of
 * the inclusions. The digest used to be a hand-written projection of about twenty
 * fields, and the state had grown past it: embarked passengers, a commander's
 * spent tactics, dynamic no-fight zones, blocked edges, directional cover, a
 * repeating trigger's ledger, zone contents, event counts and a unit's patrol
 * route were all invisible to it. Two battles differing in any of them hashed
 * the same, and a replay that diverged in one of them was reported as
 * reproducing exactly. Naming what is *out* fails closed: a new field is hashed
 * until someone argues otherwise, and an unhashable one — a timestamp, a cache —
 * announces itself as a determinism failure the first time it is added.
 */
/** Only these exact domain paths are presentation/session state. */
function ignoredPath(path: readonly string[], key: string): boolean {
  if (path.length === 0 && key === 'levelName') return true;
  // Who is driving a side cannot change what that side's recorded orders do.
  return path.length === 2 && path[0] === 'players' && /^\d+$/.test(path[1]) && key === 'controller';
}

/**
 * Structural normal form: object keys sorted, absent and undefined alike, array
 * order preserved.
 *
 * Order is kept deliberately. A deterministic engine replays a battle into the
 * same order, so sorting buys nothing — and it cannot be applied generically
 * without lying: `map.tiles` and a unit's patrol waypoints are sequences whose
 * order *is* their meaning, and a digest that sorted them would call two
 * different battlefields identical.
 */
function canonicalize(value: unknown, path: readonly string[] = []): unknown {
  if (Array.isArray(value)) {
    return value.map((entry, index) => canonicalize(entry, [...path, String(index)]));
  }
  if (value && typeof value === 'object') {
    return Object.entries(value as Record<string, unknown>)
      .filter(([key, entry]) => entry !== undefined && !ignoredPath(path, key))
      .sort(([left], [right]) => (left < right ? -1 : left > right ? 1 : 0))
      .map(([key, entry]) => [key, canonicalize(entry, [...path, key])]);
  }
  return value;
}

const canonical = (state: GameState): unknown => canonicalize(state);

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
  private readonly levelId: string;
  private readonly seed: number;
  private readonly ruleset: BattleRulesetManifest;
  private readonly initialStateHash: string;

  constructor(
    engine: BattleEngine,
    initialState: GameState,
  ) {
    requirePersistentRuleset(engine.rulesetManifest);
    this.levelId = initialState.levelId;
    this.seed = initialState.random.seed;
    this.ruleset = structuredClone(engine.rulesetManifest);
    this.initialStateHash = hashState(initialState);
  }

  record(action: Action): void {
    this.log.push(structuredClone(action));
  }

  replay(finalState: GameState): BattleReplay {
    if (finalState.levelId !== this.levelId) {
      throw new DomainInvariantError(
        `cannot finish replay for "${this.levelId}" with state from "${finalState.levelId}"`,
      );
    }
    return {
      schema: 1,
      levelId: this.levelId,
      seed: this.seed,
      ruleset: structuredClone(this.ruleset),
      initialStateHash: this.initialStateHash,
      actions: this.log.map((action) => structuredClone(action)),
      finalStateHash: hashState(finalState),
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
  requirePersistentRuleset(engine.rulesetManifest);
  if (replay.schema !== 1) {
    return { state: engine.createState(level, { seed: replay.seed }), divergedAt: 0, reason: `不支持回放 schema ${replay.schema}` };
  }
  if (replay.levelId !== level.id) {
    return { state: engine.createState(level, { seed: replay.seed }), divergedAt: 0, reason: `回放关卡 ${replay.levelId} 与 ${level.id} 不匹配` };
  }
  const ruleset = rulesetDifferences(engine.rulesetManifest, replay.ruleset);
  if (ruleset.length > 0) {
    return { state: engine.createState(level, { seed: replay.seed }), divergedAt: 0, reason: ruleset.join('；') };
  }
  const state = engine.createState(level, { seed: replay.seed });
  if (hashState(state) !== replay.initialStateHash) {
    return { state, divergedAt: 0, reason: '回放初始状态摘要不匹配' };
  }
  for (let index = 0; index < replay.actions.length; index++) {
    try {
      engine.dispatch(state, replay.actions[index]);
    } catch (error) {
      if (error instanceof IllegalActionError) {
        return { state, divergedAt: index, reason: error.message };
      }
      throw error;
    }
    if (state.phase === 'over') {
      const finalHashMatches = hashState(state) === replay.finalStateHash;
      return {
        state,
        divergedAt: index === replay.actions.length - 1
          ? (finalHashMatches ? null : replay.actions.length)
          : index + 1,
        reason: index === replay.actions.length - 1
          ? (finalHashMatches ? '' : '回放最终状态摘要不匹配')
          : '对局提前结束',
      };
    }
  }
  if (hashState(state) !== replay.finalStateHash) {
    return { state, divergedAt: replay.actions.length, reason: '回放最终状态摘要不匹配' };
  }
  return { state, divergedAt: null, reason: '' };
}
