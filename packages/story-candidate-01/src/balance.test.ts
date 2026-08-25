import { describe, expect, it } from 'vitest';
import {
  GameSession,
  scenarioSignalsOf,
  type GameEvent,
  type LevelData,
  createBattleEngine,
  errorMessage,
} from '@empire/battle-engine';
import { CANDIDATE_01_LEVELS } from './levels';

import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from './index';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const TEST_ENGINE = createBattleEngine({ content: TEST_CATALOG });

export interface BalanceResult {
  id: string;
  winner: number | null;
  turns: number;
  actions: number;
  allies: number;
  enemies: number;
  allyHp: number;
  attacks: number;
  skills: number;
  signals: number;
  reason: string;
  fallen: string;
}

export function simulateCandidate01(level: LevelData, aggression = 0.58, actionLimit = 800): BalanceResult {
  const snapshot = structuredClone(level);
  for (const player of snapshot.players) {
    if (player.id === 1) {
      player.controller = 'ai';
      player.ai = { aggression };
    }
  }
  const session = new GameSession(snapshot, TEST_ENGINE);
  const events: GameEvent[] = [];
  let actions = 0;
  while (session.state.phase === 'playing' && actions < actionLimit) {
    let action;
    try {
      action = session.chooseAiAction();
    } catch (error) {
      const detail = errorMessage(error);
      throw new Error(`${level.id} turn ${session.state.turn} player ${session.state.currentPlayer}: ${detail}`, { cause: error });
    }
    let emitted: GameEvent[];
    try {
      emitted = session.tryDispatch(action) ?? session.tryDispatch({ kind: 'endTurn' }) ?? [];
    } catch (error) {
      const detail = errorMessage(error);
      throw new Error(`${level.id} turn ${session.state.turn} player ${session.state.currentPlayer} action ${JSON.stringify(action)}: ${detail}`, { cause: error });
    }
    events.push(...emitted);
    actions++;
  }
  const teamOf = (owner: number) => session.state.players.find((player) => player.id === owner)?.team;
  const allies = session.state.units.filter((unit) => teamOf(unit.owner) === 1);
  const enemies = session.state.units.filter((unit) => teamOf(unit.owner) !== 1);
  const attackEvents = events.filter((event) => ['attack', 'counter', 'areaAttack', 'supportAttack', 'attackStructure', 'areaAttackStructure'].includes(event.type));
  const skills = attackEvents.filter((event) => 'weapon' in event && (
    event.weapon.includes('gray-oath') || event.weapon.includes('charge') || event.weapon.includes('satchel') ||
    event.weapon.includes('dispel') || event.weapon.includes('forge-burst') || event.weapon.includes('dragon-breath')
  ));
  return {
    id: level.id,
    winner: session.state.winnerTeam,
    turns: session.state.turn,
    actions,
    allies: allies.length,
    enemies: enemies.length,
    allyHp: allies.length === 0 ? 0 : Math.round(allies.reduce((sum, unit) => sum + unit.hp / TEST_CATALOG.units.get(unit.type).maxHp, 0) / allies.length * 100),
    attacks: attackEvents.length,
    skills: skills.length,
    signals: scenarioSignalsOf(events).length,
    reason: session.state.endReason,
    fallen: session.state.markers.map((marker) => marker.fallenUnit?.key ?? marker.kind).join(','),
  };
}

describe('candidate-01 balance envelope', () => {
  it('lets objective-aware AI finish every chapter battle inside a hard action bound', () => {
    const results = CANDIDATE_01_LEVELS.map((level) => simulateCandidate01(level));
    console.table(results);
    expect(results.filter((result) => result.actions >= 800)).toEqual([]);
    expect(results.filter((result) => result.winner === null)).toEqual([]);
    const wins = results.filter((result) => result.winner === 1);
    expect(wins.length).toBeGreaterThanOrEqual(11);
    expect(wins.length).toBeLessThanOrEqual(15);
    expect(Math.max(...results.map((result) => result.turns))).toBeLessThanOrEqual(31);
    expect(results.every((result) => result.attacks >= 7 && result.skills >= 1)).toBe(true);
    expect(results.reduce((sum, result) => sum + result.skills, 0)).toBeGreaterThanOrEqual(80);
    expect(results.find((result) => result.id === 'c01-15')?.fallen).not.toContain('silverwood-witness');
    for (const chapter of [[0, 5], [5, 10], [10, 16]] as const) {
      expect(results.slice(...chapter).filter((result) => result.winner !== 1).length).toBeLessThanOrEqual(2);
    }
  });

  it('stays resolvable for cautious and aggressive allied AI profiles', () => {
    for (const aggression of [0.35, 0.78]) {
      const results = CANDIDATE_01_LEVELS.map((level) => simulateCandidate01(level, aggression));
      expect(results.filter((result) => result.actions >= 800 || result.winner === null).map((result) => ({ aggression, id: result.id, actions: result.actions, turns: result.turns, reason: result.reason }))).toEqual([]);
      expect(results.filter((result) => result.winner === 1).length).toBeGreaterThanOrEqual(8);
      expect(results.reduce((sum, result) => sum + result.skills, 0)).toBeGreaterThanOrEqual(60);
    }
  });
});
