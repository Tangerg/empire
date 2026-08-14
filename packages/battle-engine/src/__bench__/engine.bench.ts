import { bench, describe } from 'vitest';
import { createDefaultBattleEngine } from '../plugins/default';
import { createTestCatalog } from '@empire/test-content';

const BENCH_CONTENT = createTestCatalog();
import { Battlefield } from '../domain/battlefield';
import type { Coord, LevelStructure, LevelUnit } from '../types';
import { makeLevel, u } from '../__tests__/fixtures';

const width = 24;
const height = 24;
const rows = Array.from({ length: height }, (_, y) =>
  Array.from({ length: width }, (_, x) => (x + y) % 7 === 0 ? 'T' : '.').join(''),
);

const occupied = new Set<string>();
const units: LevelUnit[] = [];
for (let y = 1; y < height - 1; y += 3) {
  for (let x = 1; x < width - 1; x += 4) {
    const owner = x < width / 2 ? 1 : 2;
    units.push(u(x, y, owner === 1 ? 'soldier' : 'rogue', owner));
    occupied.add(`${x},${y}`);
  }
}

const structures: LevelStructure[] = [];
for (let y = 2; y < height - 2; y += 5) {
  for (let x = 2; x < width - 2; x += 6) {
    if (occupied.has(`${x},${y}`)) continue;
    structures.push({ id: `wall-${x}-${y}`, type: 'gate', owner: 0, x, y });
  }
}

const hazardCells: Coord[] = [];
for (let y = 0; y < height; y++) {
  for (let x = 0; x < width; x++) {
    if ((x * 3 + y * 5) % 11 === 0) hazardCells.push({ x, y });
  }
}

const engine = createDefaultBattleEngine(BENCH_CONTENT);
const state = engine.createState(makeLevel(rows, {
  units,
  structures,
  scenario: {
    zones: [{ id: 'hazards', cells: hazardCells }],
    overlays: [{ id: 'fire', type: 'fire_field', zone: 'hazards' }],
  },
  directionalCover: hazardCells.map((at) => ({ at, sides: { north: 'half', west: 'full' } })),
  cliffs: hazardCells
    .filter((at) => at.x + 1 < width)
    .map((at) => ({ from: at, to: { x: at.x + 1, y: at.y } })),
}));
const mover = state.units.find((unit) => unit.owner === 1)!;
const attacker = state.units.find((unit) => unit.owner === 1)!;
const defender = state.units.find((unit) => unit.owner === 2)!;
let sink: unknown;

describe('SRPG core hot paths', () => {
  bench('project every battlefield cell', () => {
    const battlefield = new Battlefield(state, engine.rules.content);
    let checksum = 0;
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        const cell = battlefield.cellAt(x, y);
        checksum += (cell.movementCost('foot') ?? 0) + cell.defense + cell.elevation;
        checksum += cell.directionalCoverFrom(attacker) === 'full' ? 1 : 0;
      }
    }
    sink = checksum;
  });

  bench('compute one movement field', () => {
    const field = engine.space.moveField(state, mover);
    sink = field.stops.size;
  });

  bench('forecast deterministic combat', () => {
    sink = engine.forecast(state, attacker, defender).strike.damage;
  });

  bench('choose one AI action', () => {
    const aiState = engine.cloneState(state);
    aiState.currentPlayer = 2;
    sink = engine.chooseAiAction(aiState).kind;
  }, { time: 600, warmupTime: 100 });
});

void sink;
