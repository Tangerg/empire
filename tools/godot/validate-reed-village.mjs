import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const readJson = path => JSON.parse(readFileSync(resolve(root, path), 'utf8'));
const sha256 = value => createHash('sha256').update(value).digest('hex');
const key = ([x, y]) => `${x},${y}`;
const position = ({ x, y }) => [x, y];
const keys = cells => new Set(cells.map(key));
const sites = [
  ['northwest-slate-cottage', [13, 8], 'v', 0], ['north-red-cottage', [23, 9], 'v', 0],
  ['northeast-thatch-cottage', [34, 10], 'v', 0], ['west-red-cottage', [12, 15], 'v', 0],
  ['east-slate-cottage', [39, 17], 'v', 0], ['southwest-red-cottage', [21, 23], 'v', 0],
  ['southeast-slate-cottage', [34, 23], 'v', 0], ['northwest-watch-shelter', [5, 6], 'q', 1],
  ['northeast-watch-shelter', [48, 13], 'q', 2], ['southwest-bridge-shelter', [5, 22], 'q', 1],
  ['southeast-watch-shelter', [46, 25], 'q', 0], ['central-supply-cart', [31, 14], 'q', 1],
];
const bridgeCells = [[11, 18], [9, 19], [10, 19], [11, 19], [7, 20], [8, 20], [9, 20]];

function inside([x, y], polygon) {
  let result = false;
  for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
    const [ax, ay] = polygon[j], [bx, by] = polygon[i];
    if ((ay > y) !== (by > y) && x < (bx - ax) * (y - ay) / (by - ay) + ax) result = !result;
  }
  return result;
}
function clip(polygon, axis, boundary, sign) {
  const output = [];
  for (let i = 0; i < polygon.length; i++) {
    const a = polygon[i], b = polygon[(i + 1) % polygon.length];
    const aIn = (a[axis] - boundary) * sign >= 0, bIn = (b[axis] - boundary) * sign >= 0;
    if (aIn) output.push(a);
    if (aIn !== bIn) {
      const t = (boundary - a[axis]) / (b[axis] - a[axis]);
      output.push([a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])]);
    }
  }
  return output;
}
const area = polygon => Math.abs(polygon.reduce((sum, a, i) => {
  const b = polygon[(i + 1) % polygon.length];
  return sum + a[0] * b[1] - b[0] * a[1];
}, 0)) / 2;
function overlaps(polygon, [x, y]) {
  let intersection = polygon;
  for (const [axis, boundary, sign] of [[0, 20 + x * 32, 1], [0, 52 + x * 32, -1], [1, 32 + y * 28, 1], [1, 60 + y * 28, -1]])
    intersection = clip(intersection, axis, boundary, sign);
  return area(intersection) > 0.01;
}
function missionFacts(source) {
  const level = structuredClone(source);
  for (const field of ['width', 'height', 'terrain', 'elevation', 'owners']) delete level[field];
  for (const unit of level.units) { delete unit.x; delete unit.y; }
  for (const trigger of level.scenario.triggers)
    for (const effect of trigger.effects)
      for (const unit of effect.units ?? []) { delete unit.x; delete unit.y; }
  return level;
}

function validate({ level, retired, scene, painted, audit, packs }) {
  assert.deepEqual([level.id, level.schema, level.width, level.height], ['c01-05', 3, 50, 32]);
  assert.equal(level.terrain.length, 32);
  for (const row of level.terrain) assert.equal(row.length, 50);
  assert.equal(level.elevation.length, 1600);
  assert(level.elevation.every(value => value === 0));
  assert.deepEqual([retired.id, retired.width, retired.height], ['c01-05', 21, 15]);
  assert.deepEqual(missionFacts(level), missionFacts(retired), 'preserved nonspatial mission, actors and T4 wave');
  const characters = Object.assign({}, ...packs.map(pack => pack.terrainCharacters ?? {}));
  const terrains = new Map(packs.flatMap(pack => pack.terrains ?? []).map(terrain => [terrain.id, terrain]));
  const at = ([x, y]) => level.terrain[y]?.[x];
  const cells = level.terrain.flatMap((row, y) => [...row].map((symbol, x) => ({ cell: [x, y], symbol })));
  for (const { symbol } of cells) assert(terrains.has(characters[symbol]), `known terrain ${symbol}`);
  const passable = keys(cells.filter(({ symbol }) => terrains.get(characters[symbol]).cost.foot !== null).map(entry => entry.cell));
  assert.equal(audit.schema, 'reed-village-ground-geometry-v1');
  assert.deepEqual(audit.sites.map(site => [site.id, site.cell, site.terrain, site.owner]), sites);
  assert.deepEqual(keys(cells.filter(({ symbol }) => terrains.get(characters[symbol]).capturable).map(entry => entry.cell)), keys(sites.map(site => site[1])), 'all and only twelve sites');
  const owners = new Map(level.owners.map(owner => [key(position(owner)), owner.owner]));
  assert.equal(owners.size, level.owners.length, 'unique owners');
  assert.equal(owners.size, 5, 'five outpost ownership entries');
  for (const site of audit.sites) {
    assert.equal(at(site.cell), site.terrain, `${site.id} terrain`);
    assert.equal(owners.get(key(site.cell)) ?? 0, site.owner, `${site.id} owner`);
    assert(passable.has(key(site.cell)) && passable.has(key(site.approach)), `${site.id} entrance and approach`);
    assert.equal(Math.abs(site.cell[0] - site.approach[0]) + Math.abs(site.cell[1] - site.approach[1]), 1);
  }
  for (const owned of owners.keys()) assert(audit.sites.some(site => key(site.cell) === owned));

  const expectedWalls = new Set();
  assert.equal(audit.houses.length, 7, 'seven complete cottage foundations');
  for (const physical of [...audit.houses, ...audit.rocks, ...audit.furniture]) {
    const polygon = physical.foundationConservative ?? physical.groundPolygonPixels;
    const covered = cells.filter(entry => entry.symbol !== '_' && overlaps(polygon, entry.cell)).map(entry => entry.cell);
    assert.deepEqual(keys(physical.blockedCells), keys(covered), `${physical.id} complete physical foundation mask`);
    for (const cell of covered) {
      assert.equal(at(cell), '#', `${physical.id} physical foundation ${key(cell)}`);
      expectedWalls.add(key(cell));
    }
    if (physical.entrance) assert(!keys(covered).has(key(physical.entrance)), `${physical.id} entrance cannot puncture a closed foundation`);
  }
  assert.deepEqual(keys(cells.filter(entry => entry.symbol === '#').map(entry => entry.cell)), expectedWalls, 'no unexplained collision');
  assert.deepEqual(keys(cells.filter(entry => entry.symbol === '=').map(entry => entry.cell)), keys(bridgeCells), 'seven actual plank bridge cells');
  assert.deepEqual(keys(audit.bridge.cells), keys(bridgeCells));
  assert.deepEqual(keys(cells.filter(entry => inside([36 + entry.cell[0] * 32, 46 + entry.cell[1] * 28], audit.bridge.groundPolygonPixels)).map(entry => entry.cell)), keys(bridgeCells), 'bridge centers lie on visible upper planks');
  assert.deepEqual(keys(audit.waterCells), keys(cells.filter(entry => entry.symbol === '~').map(entry => entry.cell)));
  assert(audit.waterCells.length > 0 && terrains.get(characters['~']).cost.foot === null, 'river is genuinely impassable to foot');
  assert.deepEqual(audit.bridge.bankApproaches, [[7, 21], [12, 18]]);
  for (const cell of audit.bridge.bankCells) assert(passable.has(key(cell)) && at(cell) !== '=', 'banks must not enlarge bridge planks');
  for (const [i, cell] of audit.bridge.route.entries()) {
    assert(passable.has(key(cell)), 'bridge bank-to-bank route');
    if (i) assert.equal(Math.abs(cell[0] - audit.bridge.route[i - 1][0]) + Math.abs(cell[1] - audit.bridge.route[i - 1][1]), 1);
  }
  const queue = [cells.find(entry => passable.has(key(entry.cell))).cell], reached = keys(queue);
  for (let i = 0; i < queue.length; i++) {
    const [x, y] = queue[i];
    for (const next of [[x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]])
      if (passable.has(key(next)) && !reached.has(key(next))) { reached.add(key(next)); queue.push(next); }
  }
  assert.equal(reached.size, passable.size, 'one connected static foot region, not a movement-cost forecast');
  const spawns = [...level.units, ...level.scenario.triggers.flatMap(trigger => trigger.effects.flatMap(effect => effect.units ?? []))];
  assert.equal(keys(level.units.map(position)).size, level.units.length, 'distinct opening positions');
  for (const unit of spawns) assert(passable.has(key(position(unit))), `passable spawn ${unit.key}`);
  assert.deepEqual(position(level.units.find(unit => unit.key === 'purifier-5')), [43, 24], 'final fair southern knight');
  assert.deepEqual(position(level.units.find(unit => unit.key === 'villager-3')), [36, 23]);
  assert.deepEqual(audit.civilianEscape.destination, [33, 23]);
  for (const cell of audit.civilianEscape.route) assert(passable.has(key(cell)), 'civilian escape floor');

  assert.deepEqual([scene.levelId, scene.mapSize, scene.runtimeReady, scene.paintedGroundPath], ['c01-05', [50, 32], true, 'res://data/scenes/c01-05-painted-ground.json']);
  for (const field of ['placements', 'framePlacements', 'zones']) assert.deepEqual(scene[field], []);
  assert.equal(painted.levelId, level.id);
  assert.deepEqual([painted.pixelSize, painted.worldScale, painted.originPixels, painted.cellXPixels, painted.cellYPixels], [[1651, 953], 1.25, [20, 32], [32, 0], [0, 28]]);
  assert.equal(painted.resourcePath, 'res://assets/native/scenes/reed-village.png');
  assert.equal(new Set(painted.occluders.map(object => object.id)).size, painted.occluders.length, 'unique logical objects');
  assert.deepEqual(keys(painted.occluders.filter(object => object.selectionCell).map(object => object.selectionCell)), keys(sites.map(site => site[1])));
  for (const [id, cell] of sites) {
    const object = painted.occluders.find(candidate => candidate.id === id);
    assert.deepEqual(object.selectionCell, cell);
    assert.equal(object.ownsMapStructureBody, true);
    assert(object.parts.some(part => part.pointer.kind === 'target'), `${id} requires selectable facade or rail material`);
    for (const part of object.parts.filter(part => part.id.includes('roof')))
      assert.equal(part.pointer.kind, 'pass-through', `${id} roof must pass through, not steal site/road picks`);
  }
  assert.deepEqual(audit.roofRoadProbe, { objectId: 'west-red-cottage', partId: 'roof', pixel: [410, 357], cell: [12, 11] });
  assert(passable.has('12,11') && !keys(sites.map(site => site[1])).has('12,11'), 'roof-overlapped road probe is independent ground');
  assert.equal(painted.levelSourceSha256, sha256(JSON.stringify(level)), 'canonical source checksum');
  return { walkable: passable.size, walls: expectedWalls.size, foundations: keys(audit.houses.flatMap(house => house.blockedCells)).size, water: audit.waterCells.length };
}

const data = {
  level: readJson('godot/data/grayflag-campaign-levels.json').find(level => level.id === 'c01-05'),
  retired: readJson('godot/tests/native/reed-village-retired-level.json'),
  scene: readJson('godot/data/scenes/c01-05.json'),
  painted: readJson('godot/data/scenes/c01-05-painted-ground.json'),
  audit: readJson('godot/docs/art-provenance/reed-village-geometry.json'),
  packs: readJson('godot/data/content-packs.json'),
};
const counts = validate(data);
const image = readFileSync(resolve(root, 'godot/assets/native/scenes/reed-village.png'));
assert.equal(sha256(image), data.audit.sourceSha256, 'audited mother painting bytes');
assert.deepEqual([image.readUInt32BE(16), image.readUInt32BE(20)], data.painted.pixelSize, 'PNG dimensions');
if (process.argv.includes('--self-test')) {
  const setCell = (copy, [x, y], symbol) => { const row = copy.level.terrain[y]; copy.level.terrain[y] = row.slice(0, x) + symbol + row.slice(x + 1); };
  const mutations = [
    ['foundation hole', copy => setCell(copy, copy.audit.houses[0].blockedCells[0], '.'), /physical foundation/],
    ['bridge painted onto dirt bank', copy => setCell(copy, [7, 21], '='), /plank bridge/],
    ['weakened civilian objective', copy => copy.level.players[0].objectives[0].objective.objectives[0].minimumAlive = 1, /nonspatial mission/],
    ['early western wave', copy => copy.level.scenario.triggers[0].condition.turn = 3, /nonspatial mission/],
    ['roof steals click', copy => copy.painted.occluders[0].parts[0].pointer.kind = 'target', /roof must pass through/],
    ['stale registration', copy => copy.painted.levelSourceSha256 = '0'.repeat(64), /canonical source checksum/],
  ];
  for (const [name, mutate, expected] of mutations) {
    const copy = structuredClone(data);
    mutate(copy);
    assert.throws(() => validate(copy), expected, `must reject ${name} at its semantic guard`);
  }
  console.log(`PASS: ${mutations.length} focused semantic mutations rejected`);
}
console.log(`PASS: C05 50x32, 7 village + 5 outpost sites; ${counts.walkable} connected foot cells, ${counts.walls} walls (${counts.foundations} cottage cells), ${counts.water} river cells, 7 actual plank cells`);
console.log(`PASS: retired nonspatial mission parity, painting bytes and canonical registration ${data.painted.levelSourceSha256}`);
console.log('Production movement, picking, threat, objective timing and save admission require ReedVillageChecks; natural play and GUI acceptance are separate.');
