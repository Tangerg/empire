import { ContentRegistry } from './registry';
import { player, unitsOf } from './state';
import type { ContentCatalog } from './content-pack';
import type { GameEvent, GameState, PlayerId, TurnOrderState, Unit } from './types';

/**
 * Turn order is a *policy*, not a hard-coded loop.
 *
 * Advance Wars / Fire Emblem / Ancient Empires give a whole side one turn;
 * Tactics Ogre and Final Fantasy Tactics interleave individual units by speed.
 * Both answer the same question — "who may act, and when does a round end" — so
 * the engine asks a policy instead of assuming an answer.
 *
 * Two vocabularies must stay distinct for either family to work:
 *  - **round**: one lap of the battle clock. Income, terrain-overlay decay and
 *    scenario `everyRounds` triggers key off this; `state.turn` counts it.
 *  - **actor turn**: one entitlement to act. Status ticks, building healing,
 *    weapon cooldowns and reaction budgets key off this.
 *
 * Under a side-based policy the two coincide per player, which is precisely why
 * the distinction was invisible while that was the only policy.
 */
export interface TurnHandoff {
  /** Player who owns the new actor turn. */
  player: PlayerId;
  /** Single entitled unit, or null when the whole side may act. */
  activeUnit: number | null;
  /** True when the battle clock advanced to a new round. */
  roundAdvanced: boolean;
  /** No living participant remains; the caller ends the battle. */
  exhausted?: boolean;
}

/** Dependencies a policy may use. Nothing ambient, same rule as the rest. */
export interface TurnOrderContext {
  readonly content: ContentCatalog;
  readonly emit: (event: GameEvent) => void;
}

export interface TurnOrderPolicy {
  readonly id: string;
  readonly name: string;
  /** Fresh policy state for a starting battle. */
  initialState(state: GameState, content: ContentCatalog): TurnOrderState;
  /** First actor turn of the battle. */
  begin(state: GameState, context: TurnOrderContext): TurnHandoff;
  /** The current actor turn is over; hand off to the next one. */
  advance(state: GameState, context: TurnOrderContext): TurnHandoff;
  /** May this unit act right now? */
  canAct(state: GameState, unit: Unit): boolean;
  /** Units entitled to act in the current actor turn, in display order. */
  actors(state: GameState): Unit[];
  /** Presentation-only look-ahead. Deterministic, and never mutates state. */
  preview(state: GameState, content: ContentCatalog, count: number): number[];
}

export const TurnOrders = new ContentRegistry<TurnOrderPolicy>('turn order');

/**
 * Port declared by this module. The composition-level `BattleRuleServices`
 * satisfies it structurally, so neither side needs to import the other.
 */
export interface TurnOrderRules {
  readonly content: ContentCatalog;
  readonly turnOrders: ContentRegistry<TurnOrderPolicy>;
}

/** The policy this battle is running under, as recorded in its own state. */
export function activeTurnOrder(rules: TurnOrderRules, state: GameState): TurnOrderPolicy {
  return rules.turnOrders.get(state.turnOrder.policy);
}

/**
 * The single answer to "may this unit act right now".
 *
 * Phase and entitlement together: a policy only speaks about entitlement, and a
 * battle that is deploying or over entitles nobody. Every caller — the engine
 * façade, the action pipeline, the AI — must ask this one question, because
 * three copies of it drifted apart once a second policy existed.
 */
export function mayAct(rules: TurnOrderRules, state: GameState, unit: Unit): boolean {
  return state.phase === 'playing' && activeTurnOrder(rules, state).canAct(state, unit);
}

const livingPlayers = (state: GameState): PlayerId[] =>
  state.players.filter((candidate) => candidate.alive).map((candidate) => candidate.id);

/* ------------------------------------------------------------- side turns */

/**
 * Classic side turns: every unit a player owns acts once, then the next living
 * player takes over. A round is one lap of the player list.
 */
export const SideTurnOrder: TurnOrderPolicy = {
  id: 'side',
  name: '阵营回合',

  initialState: () => ({ policy: 'side', activeUnit: null, data: {} }),

  begin: (state) => ({
    player: livingPlayers(state)[0] ?? state.players[0]?.id ?? 1,
    activeUnit: null,
    roundAdvanced: false,
  }),

  advance: (state) => {
    const order = state.players.map((candidate) => candidate.id);
    let cursor = order.indexOf(state.currentPlayer);
    let roundAdvanced = false;

    // At most one full lap, so the round counter can only move once per call.
    for (let step = 0; step < order.length; step++) {
      cursor++;
      if (cursor >= order.length) {
        cursor = 0;
        roundAdvanced = true;
      }
      const candidate = player(state, order[cursor]);
      if (!candidate.alive) continue;
      return { player: candidate.id, activeUnit: null, roundAdvanced };
    }
    return { player: state.currentPlayer, activeUnit: null, roundAdvanced, exhausted: true };
  },

  canAct: (state, unit) => unit.owner === state.currentPlayer && !unit.done,

  actors: (state) => unitsOf(state, state.currentPlayer).filter((unit) => !unit.done),

  preview: (state) => unitsOf(state, state.currentPlayer).filter((unit) => !unit.done).map((unit) => unit.id),
};

/* -------------------------------------------------------------- initiative */

/** Charge a unit must accumulate before it earns an actor turn. */
export const INITIATIVE_THRESHOLD = 100;

/**
 * Tempo stat derived from the one mobility value every unit declares. A separate
 * initiative field should be added only when authored content needs that second
 * balancing axis; an untyped runtime metadata key is not a rules contract.
 */
function initiativeSpeed(content: ContentCatalog, unit: Unit): number {
  return Math.max(1, content.units.get(unit.type).movement * 10);
}

const chargeKey = (unitId: number): string => `charge.${unitId}`;
const chargeOf = (state: GameState, unitId: number): number =>
  state.turnOrder.data[chargeKey(unitId)] ?? 0;

interface Ticker {
  charge: Map<number, number>;
  speed: Map<number, number>;
}

function readyOrder(ticker: Ticker): number[] {
  return [...ticker.charge.entries()]
    .filter(([, value]) => value >= INITIATIVE_THRESHOLD)
    .sort((left, right) =>
      right[1] - left[1] ||
      (ticker.speed.get(right[0]) ?? 0) - (ticker.speed.get(left[0]) ?? 0) ||
      left[0] - right[0])
    .map(([id]) => id);
}

function tickerFor(content: ContentCatalog, state: GameState): Ticker {
  const charge = new Map<number, number>();
  const speed = new Map<number, number>();
  for (const unit of state.units) {
    if (!player(state, unit.owner).alive) continue;
    charge.set(unit.id, chargeOf(state, unit.id));
    speed.set(unit.id, initiativeSpeed(content, unit));
  }
  return { charge, speed };
}

/**
 * Tactics Ogre / FFT style ordering: every unit accumulates charge each tick and
 * acts alone once it crosses the threshold. Ties break by charge, then speed,
 * then unit id — fully deterministic, therefore replayable.
 */
export const InitiativeTurnOrder: TurnOrderPolicy = {
  id: 'initiative',
  name: '个体行动序',

  initialState: (state, content) => {
    const data: Record<string, number> = {};
    // A deterministic fraction of speed so the opening round is not a flat tie,
    // without consuming any randomness.
    for (const unit of state.units) {
      data[chargeKey(unit.id)] = initiativeSpeed(content, unit) % 37;
    }
    return { policy: 'initiative', activeUnit: null, data };
  },

  begin: (state, context) => advanceInitiative(state, context, { spendActive: false }),

  advance: (state, context) => advanceInitiative(state, context, { spendActive: true }),

  canAct: (state, unit) => state.turnOrder.activeUnit === unit.id && !unit.done,

  actors: (state) => {
    const active = state.turnOrder.activeUnit;
    return state.units.filter((unit) => unit.id === active && !unit.done);
  },

  preview: (state, content, count) => {
    const ticker = tickerFor(content, state);
    const out: number[] = [];
    let guard = 0;
    while (out.length < count && guard++ < 10_000) {
      const ready = readyOrder(ticker);
      if (ready.length === 0) {
        for (const [id, value] of ticker.charge) {
          ticker.charge.set(id, value + (ticker.speed.get(id) ?? 1));
        }
        continue;
      }
      out.push(ready[0]);
      ticker.charge.set(ready[0], (ticker.charge.get(ready[0]) ?? 0) - INITIATIVE_THRESHOLD);
    }
    return out;
  },
};

/**
 * Hands the initiative to whoever is next.
 *
 * `spendActive` is named because the call sites are `begin` and `advance`, and
 * `advanceInitiative(state, context, true)` said nothing about which of them was
 * which: opening a round does not charge anybody for a turn they have not taken.
 */
function advanceInitiative(
  state: GameState,
  context: TurnOrderContext,
  { spendActive }: { spendActive: boolean },
): TurnHandoff {
  pruneDepartedUnits(state);
  const active = state.turnOrder.activeUnit;
  if (spendActive && active !== null) {
    state.turnOrder.data[chargeKey(active)] = chargeOf(state, active) - INITIATIVE_THRESHOLD;
  }

  let roundAdvanced = false;
  // Threshold is finite and every speed is >= 1, so this terminates; the guard
  // only protects against a pathological content pack.
  for (let guard = 0; guard <= INITIATIVE_THRESHOLD * 4; guard++) {
    const ticker = tickerFor(context.content, state);
    if (ticker.charge.size === 0) {
      state.turnOrder.activeUnit = null;
      return { player: state.currentPlayer, activeUnit: null, roundAdvanced, exhausted: true };
    }
    // The ticker only charges units that are on the field, so the first ready id
    // names one. Said as a lookup rather than an assertion: a ticker that ever
    // disagreed would keep charging instead of crashing the turn.
    const first = readyOrder(ticker)[0];
    const actor = first === undefined ? undefined : state.units.find((unit) => unit.id === first);
    if (actor) {
      state.turnOrder.activeUnit = actor.id;
      return { player: actor.owner, activeUnit: actor.id, roundAdvanced };
    }
    for (const [id, value] of ticker.charge) {
      state.turnOrder.data[chargeKey(id)] = value + (ticker.speed.get(id) ?? 1);
    }
    // One clock lap without an actor is one round of the battle clock.
    roundAdvanced = true;
  }
  state.turnOrder.activeUnit = null;
  return { player: state.currentPlayer, activeUnit: null, roundAdvanced, exhausted: true };
}

TurnOrders.defineAll([SideTurnOrder, InitiativeTurnOrder]);
TurnOrders.seal();

/**
 * Prunes charge for units that left the battlefield.
 *
 * Deliberately self-maintained rather than hooked into unit spawn/removal: a
 * lifecycle hook would make `state` depend on turn order and close a dependency
 * cycle, and stale charge is inert anyway (only live units are ever ticked).
 * A unit that joins mid-battle simply starts at zero charge, which is also the
 * fair reading of "it just arrived".
 */
function pruneDepartedUnits(state: GameState): void {
  const live = new Set(state.units.map((unit) => `charge.${unit.id}`));
  for (const key of Object.keys(state.turnOrder.data)) {
    if (key.startsWith('charge.') && !live.has(key)) delete state.turnOrder.data[key];
  }
  if (state.turnOrder.activeUnit !== null &&
    !state.units.some((unit) => unit.id === state.turnOrder.activeUnit)) {
    state.turnOrder.activeUnit = null;
  }
}
