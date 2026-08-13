import type { GameState, RandomState } from './types';

/**
 * Deterministic randomness.
 *
 * Combat resolution stays deterministic by default — the HUD forecast is the
 * truth — but a general SRPG engine has to be able to host rulesets that do want
 * variance (hit chance, criticals, random events, random growth). The
 * requirement is not "no randomness", it is **reproducible** randomness: the
 * stream lives on `GameState`, so a clone, a save file and a replay all draw the
 * same numbers, and the AI can simulate without perturbing the live battle.
 *
 * The generator is counter-based rather than a mutable stream: each draw is a
 * pure function of (seed, stream name, counter). That makes streams independent,
 * so adding a new consumer cannot shift the numbers an existing one receives —
 * the usual way seeded systems lose reproducibility between versions.
 */
export interface RandomSource {
  readonly id: string;
  /** Uniform value in [0, 1). */
  next(state: GameState, stream: string): number;
  /** Uniform integer in [0, bound). */
  int(state: GameState, stream: string, bound: number): number;
  /** True with the given probability, expressed in percent. */
  chance(state: GameState, stream: string, percent: number): boolean;
  /** Uniform element of a non-empty list. */
  pick<T>(state: GameState, stream: string, items: readonly T[]): T;
  /** Draws without advancing the stream; for previews and AI estimation. */
  peek(state: GameState, stream: string): number;
}

export const createRandomState = (seed: number): RandomState => ({
  seed: Math.trunc(seed) >>> 0,
  counters: {},
});

/** 32-bit string hash so stream names participate in the mix. */
function hashString(value: string): number {
  let hash = 0x811c9dc5;
  for (let index = 0; index < value.length; index++) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }
  return hash >>> 0;
}

/** SplitMix32 finaliser: good avalanche, tiny, and identical across engines. */
function mix32(value: number): number {
  let z = value >>> 0;
  z = (z + 0x9e3779b9) >>> 0;
  z ^= z >>> 16;
  z = Math.imul(z, 0x21f0aaad) >>> 0;
  z ^= z >>> 15;
  z = Math.imul(z, 0x735a2d97) >>> 0;
  z ^= z >>> 15;
  return z >>> 0;
}

function draw(state: GameState, stream: string, counter: number): number {
  const mixed = mix32((state.random.seed ^ hashString(stream) ^ Math.imul(counter, 0x85ebca6b)) >>> 0);
  return mixed / 0x1_0000_0000;
}

export const SplitMixRandom: RandomSource = {
  id: 'splitmix32',

  next(state, stream) {
    const counter = state.random.counters[stream] ?? 0;
    state.random.counters[stream] = counter + 1;
    return draw(state, stream, counter);
  },

  peek(state, stream) {
    return draw(state, stream, state.random.counters[stream] ?? 0);
  },

  int(state, stream, bound) {
    if (!Number.isInteger(bound) || bound < 1) throw new Error('random bound must be a positive integer');
    return Math.floor(this.next(state, stream) * bound);
  },

  chance(state, stream, percent) {
    if (percent <= 0) return false;
    if (percent >= 100) return true;
    return this.next(state, stream) * 100 < percent;
  },

  pick(state, stream, items) {
    if (items.length === 0) throw new Error('cannot pick from an empty list');
    return items[this.int(state, stream, items.length)];
  },
};

/**
 * A source that refuses to produce randomness.
 *
 * Installed by rulesets that promise exact forecasts: any content that reaches
 * for a die roll fails loudly at development time instead of quietly making the
 * HUD lie.
 */
export const DeterministicOnlyRandom: RandomSource = {
  id: 'deterministic-only',
  next: () => refuse(),
  peek: () => refuse(),
  int: () => refuse(),
  chance: () => refuse(),
  pick: () => refuse(),
};

function refuse(): never {
  throw new Error(
    'this ruleset is configured for exact forecasts; install a seeded RandomSource to use variance',
  );
}
