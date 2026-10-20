/** Tactical contracts and reproducible AI outcomes for the authored river valley. */
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { Battlefield, computeMoveField, validateLevel, type Coord, type MovementClass } from '@empire/battle-engine';
import { GROUND_TAGS, taggedGround } from '@empire/game-ui';
import { hashState } from '../../packages/battle-engine/src/replay';
import { content, engine, shippedLevel } from '../board-harness';

const output = resolve(process.env.RIVER_LAYOUT_OUTPUT ?? '/tmp/empire-river-layout-contract');
mkdirSync(output, { recursive: true });
const level = shippedLevel('c01-02');
const initial = engine.createState(level);
const battlefield = new Battlefield(initial, content);
const grid = engine.rules.grids.get('square4');
const key = ({ x, y }: Coord) => `${x},${y}`;
function distances(start: Coord, movement: MovementClass, keepBridgeRow?: number) {
  const distance = new Map([[key(start), 0]]), queue: Coord[] = [start];
  for (const from of queue) for (const to of grid.adjacent(from)) {
    if (!battlefield.contains(to) || battlefield.traversalCost(from, to, movement) === null
      || (keepBridgeRow !== undefined && battlefield.cell(to).terrain.id === 'bridge' && to.y !== keepBridgeRow)
      || distance.has(key(to))) continue;
    distance.set(key(to), distance.get(key(from))! + 1); queue.push(to);
  }
  return distance;
}
const bridges = initial.map.tiles.flatMap((id, index) => id === 'bridge' ? [{ x: index % level.width, y: Math.floor(index / level.width) }] : []);
const bridgeRows = [...new Set(bridges.map(p => p.y))];
const buildings = initial.map.tiles.flatMap((id, index) => taggedGround(content.terrains.get(id), GROUND_TAGS.settlement)
  ? [{ x: index % level.width, y: Math.floor(index / level.width), id }] : []);
if (buildings.length !== 9) throw new Error('river scene must retain nine buildings');
const safety = level.scenario!.zones!.find(zone => zone.id === 'east-safety')!;
const actorReachability = initial.units.map(unit => {
  const movement = content.units.get(unit.type).movementClass;
  const found = distances(unit, movement);
  const stops = computeMoveField(engine.rules, initial, unit).stops.size;
  if (!battlefield.cell(unit).admits(movement) || stops < 2) throw new Error(`blocked deployment ${unit.key}`);
  const missing = [...initial.units, ...buildings, ...safety.cells].filter(p => !found.has(key(p)));
  if (missing.length) throw new Error(`unreachable contract from ${unit.key}: ${missing.map(key)}`);
  return { key: unit.key, cell: [unit.x, unit.y], movement, openingStops: stops,
    shortestSafetyCellSteps: Math.min(...safety.cells.map(p => found.get(key(p))!)) };
});
const independentCrossings = bridgeRows.map(row => {
  const found = distances({ x: 1, y: 9 }, 'foot', row);
  if (!found.has('20,1')) throw new Error(`bridge row ${row} does not independently join both banks`);
  return { row, westToEastCellSteps: found.get('20,1') };
});
const errors = validateLevel(engine.rules, level);
if (errors.length) throw new Error(JSON.stringify(errors));
const replay = [];
for (const aggression of [0.35, 0.58, 0.8]) {
  const state = engine.createState(level);
  for (const player of state.players) { player.controller = 'ai'; player.ai = { aggression }; }
  const events: string[] = [];
  let actions = 0;
  let firstCombat: unknown;
  while (state.phase !== 'over' && actions < 800) {
    const action = engine.chooseAiAction(state);
    const emitted = engine.dispatch(state, action);
    events.push(...emitted.map(event => JSON.stringify(event))); actions++;
    if (!firstCombat && emitted.some(event => event.type === 'attack')) firstCombat = { turn: state.turn, units: state.units.map(({ key, x, y, hp }) => ({ key, x, y, hp })) };
  }
  if (state.phase !== 'over') throw new Error(`river replay exceeded bound at ${aggression}`);
  writeFileSync(resolve(output, `replay-${aggression}.jsonl`), events.join('\n') + '\n');
  replay.push({ firstCombat, survivors: state.units.map(({ key, x, y, hp }) => ({ key, x, y, hp })), aggression, actions, turn: state.turn, winner: state.winnerTeam, reason: state.endReason,
    eventCount: events.length, eventSha256: createHash('sha256').update(events.join('\n')).digest('hex'), stateSha256: hashState(state) });
}
writeFileSync(resolve(output, 'level.json'), JSON.stringify(level, null, 2) + '\n');
writeFileSync(resolve(output, 'validation.json'), JSON.stringify({ levelId: level.id, bridges, buildings,
  actorReachability, independentCrossings, validateLevelErrors: errors, replay }, null, 2) + '\n');
console.log(output);
