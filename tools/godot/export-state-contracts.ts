import { mkdirSync, writeFileSync } from 'node:fs';
import { ContentPackInstaller, createBattleEngine, createContentCatalog, normaliseLevel } from '../../packages/battle-engine/src/index';
import { spawnUnit } from '../../packages/battle-engine/src/state';
import { COMMON_CONTENT_PACK } from '../../packages/content-common/src/index';
import { ANCIENT_EMPIRES_CONTENT_PACK } from '../../packages/content-ancient-empires/src/index';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS, candidate01Level } from '../../packages/story-candidate-01/src/index';
const content = createContentCatalog();
new ContentPackInstaller(content).install(COMMON_CONTENT_PACK, ANCIENT_EMPIRES_CONTENT_PACK, CANDIDATE_01_CONTENT_PACK);
const engine = createBattleEngine({ content });
const inputs: unknown[] = [...CANDIDATE_01_LEVELS,
  { schema: 2, terrain: ['..', '..'] },
  { schema: 2, terrain: ['....'], elevation: ['2', null, true, '0x10'] },
];
const levels = inputs.map(input => ({ input, normalized: normaliseLevel(input) }));
const spawns = [];
for (const grid of ['square4', 'square8', 'hex'] as const) {
  const level = candidate01Level('c01-01');
  level.rules = { ...level.rules, grid };
  for (const unit of level.units) unit.facing = engine.rules.grids.get(grid).restingFacing;
  const state = engine.createState(level);
  const source = { key: 'new-arrival', rank: 1 as const, rankProgress: 177, reaction: 'guard' as const, hp: 12 };
  const before = structuredClone(state);
  const unit = spawnUnit(engine.rules, state, 'c01.swordsman', 1, { x: 10, y: 10 }, { hp: 23.5, done: true, source });
  spawns.push({ grid, before, source, type: 'c01.swordsman', owner: 1, at: { x: 10, y: 10 }, hp: 23.5, done: true, unit, after: state });
}
mkdirSync('godot/tests/oracle', { recursive: true });
writeFileSync('godot/tests/oracle/state-contracts.json', JSON.stringify({ levels, spawns }) + '\n');
console.log(`${levels.length} normalization and ${spawns.length} spawn contracts exported`);
