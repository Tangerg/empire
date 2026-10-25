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
  for (const [axis, boundary, sign] of [[0, 20 + x * 32, 1], [0, 52 + x * 32, -1], [1, 30 + y * 28, 1], [1, 58 + y * 28, -1]])
    intersection = clip(intersection, axis, boundary, sign);
  return area(intersection) > 0.01;
}
function missionFacts(source) {
  const level = structuredClone(source);
  for (const field of ['width', 'height', 'terrain', 'elevation', 'owners']) delete level[field];
  for (const unit of level.units) { delete unit.x; delete unit.y; }
  for (const zone of level.scenario.zones) delete zone.cells;
  for (const trigger of level.scenario.triggers)
    for (const effect of trigger.effects)
      for (const unit of effect.units ?? []) { delete unit.x; delete unit.y; }
  return level;
}
function validate({ level, retired, scene, painted, audit, packs }) {
  assert.deepEqual([level.id, level.schema, level.width, level.height], ['c01-06', 3, 50, 32]);
  assert.equal(level.terrain.length, 32);
  for (const row of level.terrain) assert.equal(row.length, 50);
  assert.equal(level.elevation.length, 1600);
  assert(level.elevation.every(value => value === 0));
  assert.deepEqual([retired.id, retired.width, retired.height], ['c01-06', 24, 14]);
  assert.deepEqual(missionFacts(level), missionFacts(retired), 'preserved nonspatial escort, actors, T4/T6 triggers and thirteen-turn limit');
  assert.deepEqual(level.owners, retired.owners, 'all ten neutral sites stay neutral');
  const characters = Object.assign({}, ...packs.map(pack => pack.terrainCharacters ?? {}));
  const terrains = new Map(packs.flatMap(pack => pack.terrains ?? []).map(terrain => [terrain.id, terrain]));
  const at = ([x, y]) => level.terrain[y]?.[x];
  const cells = level.terrain.flatMap((row, y) => [...row].map((symbol, x) => ({ cell: [x, y], symbol })));
  for (const { symbol } of cells) assert(terrains.has(characters[symbol]), `known terrain ${symbol}`);
  const passable = keys(cells.filter(({ symbol }) => terrains.get(characters[symbol]).cost.foot !== null).map(entry => entry.cell));
  assert.equal(audit.schema, 'white-river-ground-geometry-v1');
  assert.equal(audit.sites.length, 10);
  assert.equal(audit.houses.length, 5);
  assert.equal(audit.sites.filter(site => site.terrain === 'v').length, 5);
  assert.equal(audit.sites.filter(site => site.terrain === 'q').length, 5);
  assert.deepEqual(keys(cells.filter(({ symbol }) => terrains.get(characters[symbol]).capturable).map(entry => entry.cell)), keys(audit.sites.map(site => site.cell)), 'all and only ten sites');
  for (const site of audit.sites) {
    assert.equal(at(site.cell), site.terrain, `${site.id} terrain`);
    assert.equal(site.owner, 0);
    assert(passable.has(key(site.cell)) && passable.has(key(site.approach)), `${site.id} entrance and approach`);
    assert.equal(Math.abs(site.cell[0] - site.approach[0]) + Math.abs(site.cell[1] - site.approach[1]), 1);
  }
  const expectedWalls = new Set();
  for (const physical of [...audit.houses, ...audit.rocks]) {
    const covered = cells.filter(entry => entry.symbol !== '_' && overlaps(physical.groundPolygonPixels, entry.cell)).map(entry => entry.cell);
    assert.deepEqual(keys(physical.blockedCells), keys(covered), `${physical.id} complete physical foundation mask`);
    for (const cell of covered) {
      assert.equal(at(cell), '#', `${physical.id} physical foundation ${key(cell)}`);
      expectedWalls.add(key(cell));
    }
    if (physical.cell) assert(!keys(covered).has(key(physical.cell)), `${physical.id} entrance cannot puncture a closed foundation`);
  }
  assert.deepEqual(keys(cells.filter(entry => entry.symbol === '#').map(entry => entry.cell)), expectedWalls, 'no unexplained collision');
  assert.equal(audit.bridges.length, 3);
  const bridgeCells = audit.bridges.flatMap(bridge => bridge.cells);
  assert.deepEqual(keys(cells.filter(entry => entry.symbol === '=').map(entry => entry.cell)), keys(bridgeCells), 'all and only actual deck cells');
  for (const bridge of audit.bridges) {
    assert.deepEqual(keys(cells.filter(entry => inside([36 + entry.cell[0] * 32, 44 + entry.cell[1] * 28], bridge.groundPolygonPixels)).map(entry => entry.cell)), keys(bridge.cells), `${bridge.id} centers on actual deck`);
    assert(bridge.route.some(cell => at(cell) === '='), 'bank-to-bank route must actually cross deck');
    assert.deepEqual([bridge.route[0], bridge.route.at(-1)], bridge.bankApproaches);
    for (const [i, cell] of bridge.route.entries()) {
      assert(passable.has(key(cell)), 'bridge bank-to-bank route');
      if (i) assert.equal(Math.abs(cell[0] - bridge.route[i - 1][0]) + Math.abs(cell[1] - bridge.route[i - 1][1]), 1);
    }
  }
  assert.deepEqual(keys(audit.waterCells), keys(cells.filter(entry => entry.symbol === '~').map(entry => entry.cell)));
  assert(audit.waterCells.length > 0 && terrains.get(characters['~']).cost.foot === null);
  const queue = [cells.find(entry => passable.has(key(entry.cell))).cell], reached = keys(queue);
  for (let i = 0; i < queue.length; i++) {
    const [x, y] = queue[i];
    for (const next of [[x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]])
      if (passable.has(key(next)) && !reached.has(key(next))) { reached.add(key(next)); queue.push(next); }
  }
  assert.equal(reached.size, passable.size, 'one connected static foot region');
  const spawns = [...level.units, ...level.scenario.triggers.flatMap(trigger => trigger.effects.flatMap(effect => effect.units ?? []))];
  assert.equal(keys(level.units.map(position)).size, level.units.length, 'distinct opening positions');
  for (const unit of spawns) assert(passable.has(key(position(unit))), `passable spawn ${unit.key}`);
  const zone = level.scenario.zones.find(zone => zone.id === 'north-bank');
  assert.equal(zone.cells.length, 6);
  for (const cell of zone.cells) assert(passable.has(key(position(cell))), 'north-bank arrival is real ground');
  for (const [i, cell] of audit.escortRoute.entries()) {
    assert(passable.has(key(cell)), 'escort route floor');
    if (i) assert.equal(Math.abs(cell[0] - audit.escortRoute[i - 1][0]) + Math.abs(cell[1] - audit.escortRoute[i - 1][1]), 1);
  }
  assert.deepEqual(audit.escortRoute[0], position(level.units.find(unit => unit.key === 'refugee-1')));
  assert(keys(zone.cells.map(position)).has(key(audit.escortRoute.at(-1))));
  assert.deepEqual([scene.levelId, scene.mapSize, scene.runtimeReady, scene.paintedGroundPath], ['c01-06', [50, 32], true, 'res://data/scenes/c01-06-painted-ground.json']);
  for (const field of ['placements', 'framePlacements', 'zones']) assert.deepEqual(scene[field], []);
  assert.equal(painted.levelId, level.id);
  assert.deepEqual([painted.pixelSize, painted.worldScale, painted.originPixels, painted.cellXPixels, painted.cellYPixels], [[1643, 957], 1.25, [20, 30], [32, 0], [0, 28]]);
  assert.equal(painted.resourcePath, 'res://assets/native/scenes/white-river.png');
  assert.equal(new Set(painted.occluders.map(object => object.id)).size, painted.occluders.length);
  assert.deepEqual(keys(painted.occluders.filter(object => object.selectionCell).map(object => object.selectionCell)), keys(audit.sites.map(site => site.cell)));
  for (const site of audit.sites) {
    const object = painted.occluders.find(candidate => candidate.id === site.id);
    assert.deepEqual(object.selectionCell, site.cell);
    assert.equal(object.ownsMapStructureBody, true);
    assert(object.parts.some(part => part.pointer.kind === 'target'), `${site.id} requires selectable facade or post material`);
    for (const part of object.parts.filter(part => part.id.includes('roof')))
      assert.equal(part.pointer.kind, 'pass-through', `${site.id} roof must pass through`);
  }
  assert.equal(painted.levelSourceSha256, sha256(JSON.stringify(level)), 'canonical source checksum');
  return { walkable: passable.size, walls: expectedWalls.size, water: audit.waterCells.length, deck: bridgeCells.length };
}
const data = {
  level: readJson('godot/data/grayflag-campaign-levels.json').find(level => level.id === 'c01-06'),
  retired: readJson('godot/tests/native/white-river-retired-level.json'),
  scene: readJson('godot/data/scenes/c01-06.json'),
  painted: readJson('godot/data/scenes/c01-06-painted-ground.json'),
  audit: readJson('godot/docs/art-provenance/white-river-geometry.json'),
  packs: readJson('godot/data/content-packs.json'),
};
const counts = validate(data);
const image = readFileSync(resolve(root, 'godot/assets/native/scenes/white-river.png'));
assert.equal(sha256(image), data.audit.sourceSha256);
assert.deepEqual([image.readUInt32BE(16), image.readUInt32BE(20)], data.painted.pixelSize);
if (process.argv.includes('--self-test')) {
  const setCell = (copy, [x, y], symbol) => { const row = copy.level.terrain[y]; copy.level.terrain[y] = row.slice(0, x) + symbol + row.slice(x + 1); };
  const mutations = [
    ['foundation hole', copy => setCell(copy, copy.audit.houses[0].blockedCells[0], '.'), /physical foundation/],
    ['bridge on bank', copy => setCell(copy, [24, 18], '='), /actual deck/],
    ['weakened escort', copy => copy.level.players[0].objectives[0].objective.count = 3, /nonspatial escort/],
    ['early wave', copy => copy.level.scenario.triggers[0].condition.turn = 3, /nonspatial escort/],
    ['shortened deadline', copy => copy.level.rules.turnLimit = 8, /nonspatial escort/],
    ['roof steals click', copy => copy.painted.occluders[0].parts[0].pointer.kind = 'target', /roof must pass through/],
    ['stale registration', copy => copy.painted.levelSourceSha256 = '0'.repeat(64), /canonical source checksum/],
  ];
  for (const [name, mutate, expected] of mutations) {
    const copy = structuredClone(data); mutate(copy);
    assert.throws(() => validate(copy), expected, `must reject ${name}`);
  }
  console.log(`PASS: ${mutations.length} semantic mutations rejected`);
}
console.log(`PASS: C06 50x32, 5 villages + 5 outposts; ${counts.walkable} connected foot cells, ${counts.walls} walls, ${counts.water} water, ${counts.deck} actual deck cells`);
console.log(`PASS: retired mission parity, image bytes and canonical registration ${data.painted.levelSourceSha256}`);
console.log('Static routes do not establish movement costs, escort balance or normal-AI gameplay. WhiteRiverChecks and GUI acceptance are separate.');
