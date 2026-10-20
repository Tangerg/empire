import { readFileSync, writeFileSync } from 'node:fs';
import {
  ContentPackInstaller,
  createBattleEngine,
  createContentCatalog,
  type Action,
  type ContentPack,
  type GameState,
  type LevelData,
} from '../../../packages/battle-engine/src/index';
import { createState } from '../../../packages/battle-engine/src/state';
import {
  attackTargetCoords,
  attackTilesFrom,
  computeMoveField,
  hasDirectLineOfSight,
  healTargetsFrom,
  moveCostOf,
  pathTo,
  threatTiles,
} from '../../../packages/battle-engine/src/movement';

const root = process.cwd();
const read = <T>(path: string): T => JSON.parse(readFileSync(`${root}/${path}`, 'utf8')) as T;
const packs = read<ContentPack[]>('godot/data/content-packs.json');
const content = createContentCatalog();
new ContentPackInstaller(content).install(...packs);
const engine = createBattleEngine({ content });
const raw = read<{ level: LevelData; state: GameState }>('godot/tests/oracle/grayflag-initial-raw.json');
const cases = read<{ action: Action; before: GameState }[]>('godot/tests/oracle/action-cases.json');
const selected = raw.state.units.map((unit) => ({
  name: `grayflag-${unit.id}`,
  state: raw.state,
  unit: unit.id,
}));
for (let index = 0; index < cases.length; index += 12) {
  const entry = cases[index];
  if (entry.action.kind === 'command') {
    selected.push({ name: `midgame-${index}`, state: entry.before, unit: entry.action.unit });
  }
}

const level: LevelData = {
  schema: 2,
  id: 'movement-fixture',
  name: 'Movement fixture',
  width: 6,
  height: 4,
  terrain: ['......', '.T#h..', '..~^..', '......'],
  owners: [],
  units: [
    { key: 'a', unit: 'soldier', owner: 1, x: 0, y: 0 },
    { key: 'b', unit: 'archer', owner: 1, x: 1, y: 0, hp: 50 },
    { key: 'c', unit: 'soldier', owner: 2, x: 3, y: 0 },
  ],
  players: raw.level.players,
  rules: {},
  victory: [],
};
const base = createState(engine.rules, level);
selected.push({ name: 'terrain-and-occupancy', state: base, unit: 1 });
const variants: [string, (state: GameState) => void][] = [
  ['no-allied-pass', (state) => { state.rules.friendlyPassThrough = false; }],
  ['enemy-pass', (state) => { state.rules.enemiesBlockMovement = false; }],
  ['zone-of-control', (state) => { state.rules.zoneOfControl = true; }],
  ['square8', (state) => { state.rules.grid = 'square8'; }],
  ['hex', (state) => { state.rules.grid = 'hex'; }],
  ['layers', (state) => {
    state.map.elevation[1] = 1;
    state.map.cliffs = [{ from: { x: 0, y: 0 }, to: { x: 0, y: 1 } }];
    state.structures = [{
      id: 'gate-a', type: 'gate', x: 2, y: 0, owner: 0,
      hp: 100, disabled: false, statuses: [],
    }];
    state.scenario.overlays = [{
      id: 'flood-a', type: 'flooded', cells: [{ x: 0, y: 1 }], remainingRounds: null,
    }];
  }],
  ['modifiers', (state) => {
    state.units[0].statuses = [{ id: 'shaken', stacks: 2, remaining: 2 }];
    state.units[0].formation = 'formation-defensive';
    state.units[0].commanderId = 'cmd';
    state.commanders = [{
      id: 'cmd', unitId: 2, owner: 1, radius: 2,
      aura: { attackMultiplier: 1, defenseDelta: 0, movementDelta: 2 },
      turnGrants: [], tactics: [], usedTactics: [],
    }];
  }],
];
for (const [name, edit] of variants) {
  const state = structuredClone(base);
  edit(state);
  selected.push({ name, state, unit: 1 });
}

const output = selected.map(({ name, state, unit: id }) => {
  const unit = state.units.find((candidate) => candidate.id === id);
  if (!unit) throw new Error(`Missing fixture actor ${id}`);
  const field = computeMoveField(engine.rules, state, unit);
  const tiles = [...field.tiles.values()];
  return {
    name,
    state,
    unit: id,
    tiles,
    stops: [...field.stops],
    paths: tiles.map((tile) => ({
      to: { x: tile.x, y: tile.y },
      cost: moveCostOf(field, state.map, tile),
      path: pathTo(field, state.map, tile),
    })),
    attackTiles: attackTilesFrom(engine.rules, state, unit, unit),
    attackTargets: attackTargetCoords(engine.rules, state, unit, unit),
    healTargets: healTargetsFrom(engine.rules, state, unit, unit).map((ally) => ally.id),
    threat: [...threatTiles(engine.rules, state, unit, field)],
    lineOfSight: [...field.stops].slice(0, 8).map((index) => {
      const at = { x: index % state.map.width, y: Math.floor(index / state.map.width) };
      return { at, visible: hasDirectLineOfSight(engine.rules, state, unit, at) };
    }),
  };
});
writeFileSync(`${root}/godot/tests/oracle/movement-vectors.json`, `${JSON.stringify(output)}\n`);
console.log(`Generated ${output.length} movement-query vectors`);
