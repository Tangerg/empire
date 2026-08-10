import { commandOptions } from './actions';
import { forecast } from './combat';
import { Terrains } from './data/terrain';
import { EFFECTIVENESS, unitDef } from './data/units';
import { dist, idx } from './grid';
import { computeMoveField, threatTiles } from './movement';
import {
  areEnemies,
  enemyUnitsOf,
  player,
  productionTilesOf,
  unitAt,
  unitsOf,
} from './state';
import type { Action, Coord, GameState, Unit, UnitTypeId } from './types';

/**
 * One-ply utility AI. It scores every (destination, ability, target) triple a
 * unit can legally produce this turn and commits to the single best action in
 * the whole army, one action per call — which lets the UI animate each step and
 * keeps the search shallow enough to stay instant.
 *
 * Fog of war is intentionally ignored by the AI (documented, not accidental).
 */

export interface AiOptions {
  /** 0 = turtle on defensible ground, 1 = charge the enemy keep. */
  aggression: number;
}

const hpRatio = (u: Unit) => u.hp / unitDef(u.type).maxHp;
const unitValue = (u: Unit) => unitDef(u.type).cost * (0.4 + 0.6 * hpRatio(u));

/** Total damage enemies could land on each tile if they all attacked it. */
function dangerMap(s: GameState, me: number): Map<number, number> {
  const danger = new Map<number, number>();
  for (const foe of enemyUnitsOf(s, me)) {
    const def = unitDef(foe.type);
    const weight = def.attack * (0.5 + 0.5 * hpRatio(foe));
    for (const i of threatTiles(s, foe)) {
      danger.set(i, (danger.get(i) ?? 0) + weight);
    }
  }
  return danger;
}

interface Objectives {
  /** Tiles worth walking to: enemy/neutral capturables, weighted. */
  captureTargets: { at: Coord; weight: number }[];
  enemyHqs: Coord[];
  myHqs: Coord[];
}

function objectivesFor(s: GameState, me: number): Objectives {
  const captureTargets: { at: Coord; weight: number }[] = [];
  const enemyHqs: Coord[] = [];
  const myHqs: Coord[] = [];
  for (let i = 0; i < s.map.tiles.length; i++) {
    const t = Terrains.get(s.map.tiles[i]);
    if (!t.capturable) continue;
    const at = { x: i % s.map.width, y: Math.floor(i / s.map.width) };
    const owner = s.map.owners[i];
    if (t.hq) {
      if (owner === me) myHqs.push(at);
      else if (areEnemies(s, owner, me)) enemyHqs.push(at);
    }
    if (owner !== me) {
      const weight = t.hq ? 6 : t.produces.length > 0 ? 3 : 2;
      captureTargets.push({ at, weight });
    }
  }
  return { captureTargets, enemyHqs, myHqs };
}

function nearest(from: Coord, list: Coord[]): number {
  let best = Infinity;
  for (const c of list) best = Math.min(best, dist(from, c));
  return best;
}

/** Static desirability of standing on `at`, ignoring what we do from there. */
function positionScore(
  s: GameState,
  unit: Unit,
  at: Coord,
  obj: Objectives,
  danger: Map<number, number>,
  opts: AiOptions,
): number {
  const def = unitDef(unit.type);
  const terrain = Terrains.get(s.map.tiles[idx(s.map, at.x, at.y)]);
  let score = 0;

  score += terrain.defense * 90;

  const canCapture = def.abilities.includes('capture');
  if (canCapture && obj.captureTargets.length > 0) {
    let best = 0;
    for (const t of obj.captureTargets) {
      const d = dist(at, t.at);
      best = Math.max(best, (t.weight * 120) / (1 + d));
    }
    score += best * (0.6 + 0.4 * opts.aggression);
  }

  const foes = enemyUnitsOf(s, unit.owner);
  if (foes.length > 0) {
    const dFoe = nearest(at, foes.map((f) => ({ x: f.x, y: f.y })));
    // Ranged units want to be near-but-not-adjacent; melee wants contact.
    const ideal = def.minRange > 1 ? def.maxRange : 1;
    score -= Math.abs(dFoe - ideal) * 16 * (0.5 + opts.aggression);
  }

  if (obj.enemyHqs.length > 0) {
    score -= nearest(at, obj.enemyHqs) * 8 * opts.aggression;
  }
  if (obj.myHqs.length > 0) {
    // Defensive pull: stay useful to our own keep when it is threatened.
    const threatToHome = obj.myHqs.some((h) =>
      foes.some((f) => dist({ x: f.x, y: f.y }, h) <= 5),
    );
    if (threatToHome) score -= nearest(at, obj.myHqs) * 14 * (1 - opts.aggression * 0.5);
  }

  const risk = danger.get(idx(s.map, at.x, at.y)) ?? 0;
  score -= risk * (0.55 - 0.25 * opts.aggression) * (1 - def.defense);

  const healRate =
    s.map.owners[idx(s.map, at.x, at.y)] === unit.owner ? terrain.heal : 0;
  if (healRate > 0 && hpRatio(unit) < 0.6) score += healRate * 4;

  return score;
}

interface Candidate {
  action: Action;
  score: number;
}

function evaluateUnit(
  s: GameState,
  unit: Unit,
  obj: Objectives,
  danger: Map<number, number>,
  opts: AiOptions,
): Candidate | null {
  const field = computeMoveField(s, unit);
  let best: Candidate | null = null;

  const consider = (c: Candidate) => {
    if (!best || c.score > best.score) best = c;
  };

  for (const i of field.stops) {
    const at = { x: i % s.map.width, y: Math.floor(i / s.map.width) };
    const pos = positionScore(s, unit, at, obj, danger, opts);
    const path = pathFrom(field, s, at);
    if (!path) continue;

    for (const opt of commandOptions(s, unit, at)) {
      if (opt.ability === 'wait') {
        consider({ action: { kind: 'command', unit: unit.id, path, command: { ability: 'wait' } }, score: pos });
        continue;
      }

      if (opt.ability === 'capture') {
        const tile = Terrains.get(s.map.tiles[i]);
        const prize = tile.hq ? 4000 : tile.produces.length > 0 ? 900 : 600;
        consider({
          action: { kind: 'command', unit: unit.id, path, command: { ability: 'capture' } },
          score: pos * 0.3 + prize,
        });
        continue;
      }

      if (opt.ability === 'attack') {
        for (const t of opt.targets) {
          const foe = unitAt(s, t.x, t.y);
          if (!foe) continue;
          const fc = forecast(s, unit, foe, at);
          const dealt = Math.min(fc.strike.damage, foe.hp) / unitDef(foe.type).maxHp;
          let score = dealt * unitDef(foe.type).cost * 1.15;
          if (fc.defenderDies) score += unitValue(foe) * 0.65;
          if (fc.counter) {
            const taken = Math.min(fc.counter.damage, unit.hp) / unitDef(unit.type).maxHp;
            score -= taken * unitDef(unit.type).cost * 1.0;
            if (fc.attackerDies) score -= unitValue(unit) * 0.9;
          }
          // Softly prefer finishing off units that threaten our objectives.
          if (foe.type === 'cleric' || unitDef(foe.type).minRange > 1) score *= 1.1;
          consider({
            action: {
              kind: 'command',
              unit: unit.id,
              path,
              command: { ability: 'attack', target: t },
            },
            score: pos * 0.25 + score,
          });
        }
        continue;
      }

      if (opt.ability === 'heal') {
        for (const t of opt.targets) {
          const ally = unitAt(s, t.x, t.y);
          if (!ally) continue;
          const def = unitDef(ally.type);
          const missing = def.maxHp - ally.hp;
          const healed = Math.min(30, missing);
          consider({
            action: {
              kind: 'command',
              unit: unit.id,
              path,
              command: { ability: 'heal', target: t },
            },
            score: pos * 0.3 + (healed / def.maxHp) * def.cost * 0.9,
          });
        }
        continue;
      }

      // Unknown (mod-added) ability: try it on every target with a mild bonus
      // so new content is at least exercised instead of silently ignored.
      if (opt.selfTargeted) {
        consider({
          action: { kind: 'command', unit: unit.id, path, command: { ability: opt.ability } },
          score: pos * 0.5 + 10,
        });
      } else {
        for (const t of opt.targets) {
          consider({
            action: {
              kind: 'command',
              unit: unit.id,
              path,
              command: { ability: opt.ability, target: t },
            },
            score: pos * 0.5 + 10,
          });
        }
      }
    }
  }

  return best;
}

function pathFrom(
  field: ReturnType<typeof computeMoveField>,
  s: GameState,
  to: Coord,
): Coord[] | null {
  const target = idx(s.map, to.x, to.y);
  const out: Coord[] = [];
  let cur = target;
  for (let guard = 0; guard <= field.tiles.size + 1; guard++) {
    const node = field.tiles.get(cur);
    if (!node) return null;
    out.push({ x: node.x, y: node.y });
    if (node.from === -1) {
      out.reverse();
      return out;
    }
    cur = node.from;
  }
  return null;
}

/* --------------------------------------------------------------- recruitment */

function scoreRecruit(s: GameState, me: number, type: UnitTypeId): number {
  const def = unitDef(type);
  const foes = enemyUnitsOf(s, me);
  const mine = unitsOf(s, me);

  let matchup = 1;
  if (foes.length > 0) {
    let total = 0;
    for (const f of foes) total += EFFECTIVENESS[def.damageType][unitDef(f.type).armorClass];
    matchup = total / foes.length;
  }

  // Value per gold, nudged by how well it answers what we are facing.
  let score = (def.attack * matchup + def.maxHp * 0.35 + def.movement * 12) / (def.cost / 100);

  const infantry = mine.filter((u) => unitDef(u.type).abilities.includes('capture')).length;
  if (def.abilities.includes('capture') && infantry < 2) score *= 1.6;

  const composition = mine.filter((u) => u.type === type).length;
  score *= 1 - Math.min(0.4, composition * 0.08); // discourage one-note armies

  return score;
}

function recruitAction(s: GameState, me: number): Action | null {
  const funds = player(s, me).funds;
  const cap = s.rules.maxUnitsPerPlayer;
  if (cap !== null && unitsOf(s, me).length >= cap) return null;

  for (const at of productionTilesOf(s, me)) {
    if (unitAt(s, at.x, at.y)) continue;
    const terrain = Terrains.get(s.map.tiles[idx(s.map, at.x, at.y)]);
    const affordable = terrain.produces.filter((id) => unitDef(id).cost <= funds);
    if (affordable.length === 0) continue;

    const bestCost = Math.max(...terrain.produces.map((id) => unitDef(id).cost));
    const ranked = affordable
      .map((id) => ({ id, score: scoreRecruit(s, me, id) }))
      .sort((a, b) => b.score - a.score);
    const pick = ranked[0];

    // Save up for something better when we are close and not under pressure.
    const pressure = enemyUnitsOf(s, me).some((f) =>
      productionTilesOf(s, me).some((p) => dist({ x: f.x, y: f.y }, p) <= 4),
    );
    if (!pressure && funds < bestCost && funds >= bestCost * 0.7 && unitsOf(s, me).length >= 3) {
      continue;
    }
    return { kind: 'recruit', at, unit: pick.id };
  }
  return null;
}

/* -------------------------------------------------------------------- driver */

/**
 * The next single action for the current (AI) player. Returns `endTurn` when
 * there is nothing left worth doing, so callers can simply loop.
 */
export function chooseAction(s: GameState, opts?: Partial<AiOptions>): Action {
  const me = s.currentPlayer;
  const aggression = opts?.aggression ?? player(s, me).ai.aggression;
  const options: AiOptions = { aggression };

  const recruit = recruitAction(s, me);
  if (recruit) return recruit;

  const obj = objectivesFor(s, me);
  const danger = dangerMap(s, me);

  let best: Candidate | null = null;
  for (const u of unitsOf(s, me)) {
    if (u.done) continue;
    const c = evaluateUnit(s, u, obj, danger, options);
    if (c && (!best || c.score > best.score)) best = c;
  }

  return best ? best.action : { kind: 'endTurn' };
}

/** Convenience: the whole turn as a list of actions (used in tests). */
export function planTurn(s: GameState, apply: (a: Action) => void, maxActions = 200): void {
  for (let n = 0; n < maxActions; n++) {
    const action = chooseAction(s);
    apply(action);
    if (action.kind === 'endTurn') return;
    if (s.phase !== 'playing') return;
  }
}
