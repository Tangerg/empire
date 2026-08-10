import { Terrains } from './data/terrain';
import { idx } from './grid';
import { areEnemies, hqTilesOf, productionTilesOf, unitsOf } from './state';
import type { GameState, Objective, PlayerId } from './types';

export interface VictoryResult {
  team: number | null;
  reason: string;
}

/** A player is out when they have neither units nor any way to make more. */
export function isDefeated(s: GameState, id: PlayerId): boolean {
  return unitsOf(s, id).length === 0 && productionTilesOf(s, id).length === 0;
}

/**
 * True if the player started with a keep and no longer holds one. Players who
 * never had a keep are excluded, so "capture the enemy castle" cannot be won by
 * default on maps where the enemy has none.
 */
function lostHQ(s: GameState, id: PlayerId): boolean {
  const p = s.players.find((x) => x.id === id);
  if (!p?.startedWithHQ) return false;
  return hqTilesOf(s, id).length === 0;
}

function objectiveMet(s: GameState, id: PlayerId, o: Objective): boolean {
  switch (o.type) {
    case 'routEnemies':
      return s.players
        .filter((p) => areEnemies(s, p.id, id))
        .every((p) => unitsOf(s, p.id).length === 0);
    case 'captureHQ': {
      const contenders = s.players.filter((p) => areEnemies(s, p.id, id) && p.startedWithHQ);
      return contenders.length > 0 && contenders.every((p) => lostHQ(s, p.id));
    }
    case 'holdAllVillages': {
      let total = 0;
      let mine = 0;
      for (let i = 0; i < s.map.tiles.length; i++) {
        if (!Terrains.get(s.map.tiles[i]).capturable) continue;
        total++;
        if (s.map.owners[i] === id) mine++;
      }
      return total > 0 && mine === total;
    }
    case 'surviveTurns':
      return s.turn > o.turns;
    default:
      return false;
  }
}

const OBJECTIVE_LABEL: Record<Objective['type'], string> = {
  routEnemies: '歼灭所有敌军',
  captureHQ: '攻占敌方城堡',
  holdAllVillages: '控制全部据点',
  surviveTurns: '坚守回合',
};

export function describeObjective(o: Objective): string {
  if (o.type === 'surviveTurns') return `坚守 ${o.turns} 回合`;
  return OBJECTIVE_LABEL[o.type];
}

/**
 * Evaluates the board. Elimination is checked first (it also flips `alive`),
 * then each living player's own objective list.
 */
export function evaluateVictory(s: GameState): VictoryResult {
  for (const p of s.players) {
    if (!p.alive) continue;
    if (isDefeated(s, p.id)) p.alive = false;
  }

  const living = s.players.filter((p) => p.alive);
  const teams = new Set(living.map((p) => p.team));
  if (living.length === 0) return { team: null, reason: '全员覆灭' };
  if (teams.size === 1) {
    return { team: living[0].team, reason: '敌军已被全歼' };
  }

  for (const p of living) {
    for (const o of p.objectives) {
      if (o.type === 'surviveTurns') continue; // resolved by the turn limit
      if (objectiveMet(s, p.id, o)) {
        return { team: p.team, reason: `${p.name} 完成目标：${describeObjective(o)}` };
      }
    }
  }

  const limit = s.rules.turnLimit;
  if (limit !== null && s.turn > limit) {
    const survivors = living.filter((p) =>
      p.objectives.some((o) => o.type === 'surviveTurns' && s.turn > o.turns),
    );
    if (survivors.length > 0) {
      return { team: survivors[0].team, reason: `${survivors[0].name} 坚守到了最后` };
    }
    return { team: null, reason: '回合数耗尽，平局' };
  }

  return { team: null, reason: '' };
}

/** Progress string for the HUD, e.g. "3/5 据点". */
export function objectiveProgress(s: GameState, id: PlayerId, o: Objective): string {
  switch (o.type) {
    case 'routEnemies': {
      const left = s.players
        .filter((p) => areEnemies(s, p.id, id))
        .reduce((n, p) => n + unitsOf(s, p.id).length, 0);
      return `剩余敌军 ${left}`;
    }
    case 'captureHQ': {
      const left = s.players
        .filter((p) => areEnemies(s, p.id, id))
        .reduce((n, p) => n + hqTilesOf(s, p.id).length, 0);
      return `敌方城堡 ${left}`;
    }
    case 'holdAllVillages': {
      let total = 0;
      let mine = 0;
      for (let i = 0; i < s.map.tiles.length; i++) {
        if (!Terrains.get(s.map.tiles[i]).capturable) continue;
        total++;
        if (s.map.owners[i] === id) mine++;
      }
      return `${mine}/${total} 据点`;
    }
    case 'surviveTurns':
      return `${Math.min(s.turn, o.turns)}/${o.turns} 回合`;
    default:
      return '';
  }
}

/** Income a player collects at the start of their turn. */
export function incomeFor(s: GameState, id: PlayerId): number {
  let total = s.rules.baseIncome;
  for (let i = 0; i < s.map.owners.length; i++) {
    if (s.map.owners[i] !== id) continue;
    const t = Terrains.get(s.map.tiles[i]);
    total += s.rules.incomeOverride ?? t.income;
  }
  return total;
}

export function healRateAt(s: GameState, x: number, y: number, owner: PlayerId): number {
  const i = idx(s.map, x, y);
  if (!s.rules.healOnOwnedBuilding) return 0;
  if (s.map.owners[i] !== owner) return 0;
  return Terrains.get(s.map.tiles[i]).heal;
}
