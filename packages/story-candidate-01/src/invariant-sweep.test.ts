import { describe, expect, it } from 'vitest';
import {
  GameSession,
  type GameEvent,
  type GameState,
  type LevelData,
  hashState,
  createBattleEngine,
} from '@empire/battle-engine';
import { CANDIDATE_01_LEVELS } from './levels';
import { ANCIENT_EMPIRES_LEVELS } from '@empire/content-ancient-empires';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from './index';

const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const TEST_ENGINE = createBattleEngine({ content: TEST_CATALOG });
const COUNTS = { states: 0, saves: 0, undos: 0, events: 0 };

function invariants(state: GameState, where: string, seen: Set<number>): string[] {
  const bad: string[] = [];
  const say = (message: string) => bad.push(`${where}: ${message}`);

  const occupied = new Map<string, number>();
  const ids = new Set<number>();
  for (const unit of state.units) {
    const at = `${unit.x},${unit.y}`;
    if (occupied.has(at)) say(`two units on ${at} (${occupied.get(at)}, ${unit.id})`);
    occupied.set(at, unit.id);
    if (ids.has(unit.id)) say(`duplicate unit id ${unit.id}`);
    ids.add(unit.id);
    seen.add(unit.id);
    const def = TEST_CATALOG.units.get(unit.type);
    if (unit.hp < 1 || unit.hp > def.maxHp) say(`unit ${unit.id} hp ${unit.hp} outside 1..${def.maxHp}`);
    if (unit.x < 0 || unit.y < 0 || unit.x >= state.map.width || unit.y >= state.map.height) {
      say(`unit ${unit.id} at ${at} is off the map`);
    }
    if (unit.morale.current < 0 || unit.morale.current > unit.morale.maximum) {
      say(`unit ${unit.id} morale ${unit.morale.current}/${unit.morale.maximum}`);
    }
    for (const [id, account] of Object.entries(unit.resources)) {
      if (account.current < 0) say(`unit ${unit.id} resource ${id} is negative`);
      if (account.capacity !== null && account.current > account.capacity) {
        say(`unit ${unit.id} resource ${id} ${account.current} over capacity ${account.capacity}`);
      }
    }
    for (const [id, weapon] of Object.entries(unit.weaponState)) {
      if (weapon.cooldownRemaining < 0) say(`unit ${unit.id} weapon ${id} cooldown negative`);
      for (const [resource, account] of Object.entries(weapon.resources)) {
        if (account.current < 0) say(`unit ${unit.id} weapon ${id} ${resource} negative`);
      }
    }
    for (const status of unit.statuses) {
      if (status.remaining === 0) say(`unit ${unit.id} keeps an expired status ${status.id}`);
      if (status.stacks < 1) say(`unit ${unit.id} status ${status.id} has ${status.stacks} stacks`);
    }
  }
  if (state.nextUnitId <= Math.max(0, ...ids)) say(`nextUnitId ${state.nextUnitId} collides with a live unit`);

  for (const entry of state.embarkedUnits) {
    if (ids.has(entry.unit.id)) say(`passenger ${entry.unit.id} is also on the field`);
    if (!ids.has(entry.carrier)) say(`passenger ${entry.unit.id} rides a carrier that is gone`);
  }

  const markerIds = new Set<number>();
  for (const marker of state.markers) {
    if (markerIds.has(marker.id)) say(`duplicate marker id ${marker.id}`);
    markerIds.add(marker.id);
    if (marker.at.x < 0 || marker.at.y < 0 || marker.at.x >= state.map.width || marker.at.y >= state.map.height) {
      say(`marker ${marker.id} is off the map`);
    }
  }
  if (state.nextMarkerId <= Math.max(0, ...markerIds)) say(`nextMarkerId collides with a live marker`);

  for (const cast of state.pendingCasts) {
    if (!ids.has(cast.caster)) say(`pending cast by a unit that is gone (${cast.caster})`);
  }
  for (const commander of state.commanders) {
    if (commander.unitId !== null && !ids.has(commander.unitId) && !state.markers.some((m) => m.fallenUnit?.id === commander.unitId)) {
      // A dead commander is expected; one pointing at nothing at all is not.
      if (state.units.length > 0 && commander.unitId > 0 && commander.unitId >= state.nextUnitId) {
        say(`commander ${commander.id} points at unit ${commander.unitId}`);
      }
    }
  }

  for (const player of state.players) {
    for (const [id, account] of Object.entries(player.resources)) {
      if (account.current < 0) say(`player ${player.id} resource ${id} is negative`);
      if (account.capacity !== null && account.current > account.capacity) {
        say(`player ${player.id} resource ${id} over capacity`);
      }
    }
    if (state.rules.maxUnitsPerPlayer !== null) {
      const owned = state.units.filter((unit) => unit.owner === player.id).length;
      if (owned > state.rules.maxUnitsPerPlayer) say(`player ${player.id} fields ${owned} units over the cap`);
    }
  }

  for (let tile = 0; tile < state.map.tiles.length; tile++) {
    if (!TEST_CATALOG.terrains.has(state.map.tiles[tile])) say(`tile ${tile} has unknown terrain`);
    const owner = state.map.owners[tile];
    if (owner !== 0 && !state.players.some((player) => player.id === owner)) {
      say(`tile ${tile} is owned by nobody (${owner})`);
    }
    const progress = state.map.captureProgress[tile];
    if (progress < 0 || progress > state.rules.captureThreshold) say(`tile ${tile} capture progress ${progress}`);
  }

  if (state.turnOrder.activeUnit !== null && !ids.has(state.turnOrder.activeUnit)) {
    say(`the active unit ${state.turnOrder.activeUnit} is not on the field`);
  }
  if (!state.players.some((player) => player.id === state.currentPlayer)) {
    say(`currentPlayer ${state.currentPlayer} is nobody`);
  }
  if (state.phase === 'over' && state.endReason === '') say('the battle is over for no stated reason');
  return bad;
}

/** An event nobody could act on: a subject that never existed at all. */
function eventSanity(events: readonly GameEvent[], seen: Set<number>, where: string): string[] {
  const bad: string[] = [];
  for (const event of events) {
    const record = event as unknown as Record<string, unknown>;
    for (const key of ['unit', 'source', 'target', 'attacker', 'defender', 'carrier']) {
      const value = record[key];
      if (typeof value === 'number' && value > 0 && !seen.has(value)) {
        bad.push(`${where}: ${event.type}.${key} names unit ${value}, which never existed`);
      }
    }
  }
  return bad;
}

function sweep(level: LevelData, aggression: number): string[] {
  const snapshot = structuredClone(level);
  for (const player of snapshot.players) {
    player.controller = 'ai';
    player.ai = { aggression };
  }
  const session = new GameSession(snapshot, TEST_ENGINE);
  const seen = new Set<number>(session.state.units.map((unit) => unit.id));
  const found: string[] = [];
  const where = `${level.id}@${aggression}`;

  COUNTS.states++;
  found.push(...invariants(session.state, `${where} start`, seen));
  let actions = 0;
  while (session.state.phase === 'playing' && actions < 400 && found.length === 0) {
    let events: GameEvent[] = [];
    try {
      events = session.tryDispatch(session.chooseAiAction()) ?? session.tryDispatch({ kind: 'endTurn' }) ?? [];
    } catch (error) {
      found.push(`${where} #${actions}: THREW ${(error as Error).message}`);
      break;
    }
    actions++;
    COUNTS.states++;
    COUNTS.events += events.length;
    found.push(...invariants(session.state, `${where} #${actions}`, seen));
    found.push(...eventSanity(events, seen, `${where} #${actions}`));

    // A save is the one document a running battle writes; check it every so
    // often against the whole shipped catalogue rather than one fixture.
    if (actions % 40 === 0) {
      COUNTS.saves++;
      const digest = hashState(session.state);
      try {
        const resumed = TEST_ENGINE.loadBattle(JSON.parse(JSON.stringify(session.save())));
        if (hashState(resumed) !== digest) found.push(`${where} #${actions}: save round-trip changed the battle`);
      } catch (error) {
        found.push(`${where} #${actions}: own save refused: ${(error as Error).message}`);
      }
    }

    // Undo must restore the battle exactly, the random stream included, so the
    // same choice made again from the same point must land in the same place.
    if (actions % 37 === 0 && session.canUndo) {
      COUNTS.undos++;
      const before = hashState(session.state);
      session.undo();
      session.tryDispatch(session.chooseAiAction());
      if (hashState(session.state) !== before) {
        found.push(`${where} #${actions}: undo then the same choice diverged`);
      }
    }
  }
  return found;
}

/**
 * Every shipped level, played out three ways, checked after every single order.
 *
 * Unit tests state what one rule does; this states what the whole state may
 * never look like — two units on a tile, a passenger riding a carrier that is
 * gone, an account below zero, a marked cast by a dead caster, an event naming a
 * unit that never existed — and it says it about the levels that actually ship.
 * The save round-trip and the undo replay ride along, because both are claims
 * about *any* battle rather than about a fixture.
 */
describe('invariant sweep over every shipped level', () => {
  it('finds nothing wrong with any state any shipped level reaches', () => {
    const found: string[] = [];
    for (const level of [...CANDIDATE_01_LEVELS, ...ANCIENT_EMPIRES_LEVELS]) {
      for (const aggression of [0.35, 0.58, 0.8]) {
        found.push(...sweep(level, aggression));
      }
    }
    expect(found.slice(0, 25)).toEqual([]);
    // A sweep that stopped covering anything would pass silently, so the
    // coverage itself is asserted: every state, event, save and undo below.
    expect({
      states: COUNTS.states > 10_000,
      events: COUNTS.events > 30_000,
      saves: COUNTS.saves > 200,
      undos: COUNTS.undos > 200,
    }).toEqual({ states: true, events: true, saves: true, undos: true });
  }, 600_000);
});
