import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, isAbsolute, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const readJson = path => JSON.parse(readFileSync(resolve(root, path), 'utf8'));
const sha256 = value => createHash('sha256').update(value).digest('hex');
const key = ([x, y]) => `${x},${y}`;
const cell = ({ x, y }) => [x, y];
const physicalAudit = readJson('godot/docs/art-provenance/blackflag-camp-geometry.json');

// These are reviewed author expectations, not an alternate movement or mission implementation.
const sites = [
  ['prisoner-shelter', 'B', [21, 12], [21, 13], 0],
  ['archive-tent', 'B', [24, 15], [24, 16], 2],
  ['witness-shelter', 'B', [28, 18], [28, 19], 0],
  ['north-supply-canopy', 'B', [27, 8], [27, 9], 2],
  ['west-aid-post', 'B', [14, 18], [15, 18], 0],
  ['east-watch-post', 'B', [43, 16], [44, 16], 2],
  ['grayflag-approach-outpost', 'q', [7, 21], [8, 21], 1],
  ['enemy-rear-outpost', 'q', [42, 5], [42, 6], 2],
  ['woodkeeper-home', 'v', [16, 15], [17, 15], 0],
  ['south-caretaker-home', 'v', [37, 26], [37, 27], 0],
];
const deployments = {
  'campaign-laiya': [17, 20], 'campaign-torren': [17, 19],
  'campaign-elin': [18, 20], 'campaign-mirelle': [18, 19],
  'scout-1': [19, 21], 'scout-2': [17, 21], 'scout-3': [19, 19],
  'scout-4': [15, 22], 'scout-cleric': [18, 21], 'scout-reserve': [16, 20],
  'camp-guard-1': [21, 13], 'camp-guard-2': [26, 11],
  'camp-inquisitor': [25, 16], 'camp-bow': [22, 17], 'camp-mage': [31, 17],
  'north-patrol': [31, 9], 'east-patrol': [41, 19],
  'south-patrol': [32, 26], 'gate-reserve': [41, 21],
};
const rowCells = rows => rows.flatMap(([y, first, last]) =>
  Array.from({ length: last - first + 1 }, (_, index) => [first + index, y]));
const foundations = [
  rowCells([[18, 5, 6], [19, 4, 7], [20, 4, 7]]),
  rowCells([[3, 43, 45], [4, 42, 46], [5, 43, 44]]),
  rowCells([[13, 13, 16], [14, 13, 16]]),
  rowCells([[24, 34, 36], [25, 34, 37], [26, 35, 36]]),
];
const outsideFoundations = new Set([[44, 3], [45, 3], [45, 4], [46, 4]].map(key));
const zones = [
  { id: 'tent-a', cells: [{ x: 21, y: 12 }] },
  { id: 'tent-b', cells: [{ x: 24, y: 15 }] },
  { id: 'tent-c', cells: [{ x: 28, y: 18 }] },
  { id: 'west-exit', cells: [{ x: 7, y: 21 }, { x: 8, y: 21 }, { x: 9, y: 21 }] },
];

function nonspatialFacts(level) {
  const facts = structuredClone(level);
  for (const field of ['width', 'height', 'terrain', 'elevation', 'owners']) delete facts[field];
  for (const unit of facts.units) { delete unit.x; delete unit.y; }
  for (const zone of facts.scenario.zones) delete zone.cells;
  for (const trigger of facts.scenario.triggers)
    for (const effect of trigger.effects)
      if (effect.type === 'setUnitDirective') delete effect.directive.waypoints;
  delete facts.extra.tacticalTheme;
  return facts;
}

function validateRegistration(document, level, pixelSize, label) {
  assert.equal(document.levelId, level.id, `${label} level identity`);
  assert.equal(document.levelSourceSha256, sha256(JSON.stringify(level)), `${label} canonical source checksum`);
  assert.deepEqual(document.pixelSize, pixelSize, `${label} reviewed canvas`);
  assert.equal(document.worldScale, 1.25, `${label} world scale`);
  for (const [field, value] of Object.entries(physicalAudit.basis))
    assert.deepEqual(document[field], value, `${label} ${field}`);
}

function validateImage(images, path, pixelSize) {
  assert(Object.hasOwn(images, path), `loaded image resource ${path}`);
  const bytes = Buffer.from(images[path]);
  assert(bytes.length >= 24 && bytes.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))
    && bytes.toString('ascii', 12, 16) === 'IHDR', `PNG header ${path}`);
  assert.deepEqual([bytes.readUInt32BE(16), bytes.readUInt32BE(20)], pixelSize, `PNG dimensions ${path}`);
  return bytes;
}

function validatePresentation({ assembled, catalog, images, provenance, historical }, level) {
  const groundPath = 'res://assets/native/c04-layered-v1/ground-candidate.png';
  assert.equal(assembled.resourcePath, groundPath, 'formal independent ground resource');
  assert.equal(assembled.objectCatalogPath, 'res://data/scenes/c01-04-layered-objects.json', 'formal object catalog');
  const groundEvidence = provenance.files.find(record => `res://${record.file}` === groundPath);
  assert(groundEvidence, 'reviewed ground byte provenance');
  validateRegistration(assembled, level, groundEvidence.pixelSize, 'assembled');
  validateImage(images, groundPath, assembled.pixelSize);
  assert.equal(catalog.schema, 1, 'scene object catalog schema');
  for (const asset of Object.values(catalog.objects)) validateImage(images, asset.resourcePath, asset.pixelSize);
  // Provenance status strings describe old checkpoints. The scene selects production art;
  // these records retain source-byte integrity, not a second admission decision.
  for (const path of new Set([groundPath, ...Object.values(catalog.objects).map(asset => asset.resourcePath)])) {
    if (!path.startsWith('res://assets/native/c04-layered-v1/')) continue;
    const record = provenance.files.find(record => `res://${record.file}` === path);
    assert(record, `reviewed independent image ${path}`);
    const bytes = validateImage(images, path, record.pixelSize);
    assert.equal(sha256(bytes), record.sha256, `independent image bytes ${path}`);
  }
  assert.equal(assembled.objects.length, 139, 'reviewed independent composition count');
  assert.equal(new Set(assembled.objects.map(object => object.id)).size, assembled.objects.length, 'unique placement identities');
  for (const object of assembled.objects) {
    assert(typeof object.id === 'string' && object.id.length > 0, 'explicit placement identity');
    assert(Object.hasOwn(catalog.objects, object.assetId), `registered object asset ${object.id}`);
    assert(Array.isArray(object.contact) && object.contact.length === 2 && object.contact.every(Number.isFinite), `finite object contact ${object.id}`);
    assert(Number.isFinite(object.scale) && object.scale > 0, `positive object scale ${object.id}`);
    assert(['target', 'block', 'pass-through'].includes(object.pointer), `declared object pointer ${object.id}`);
    assert.equal(typeof object.ownsMapStructureBody, 'boolean', `explicit body ownership ${object.id}`);
    if (object.selectionCell === null) {
      assert(!object.ownsMapStructureBody && object.pointer !== 'target', `non-site object cannot own or target a site ${object.id}`);
      continue;
    }
    const site = sites.find(site => site[0] === object.id);
    assert(site, `existing canonical site ${object.id}`);
    assert.deepEqual(object.selectionCell, site[2], `canonical site cell ${object.id}`);
    assert(object.ownsMapStructureBody && object.pointer === 'target', `owned site target ${object.id}`);
  }
  assert.deepEqual(assembled.objects.filter(object => object.selectionCell !== null).map(object => object.id).sort(),
    sites.map(site => site[0]).sort(), 'exactly ten canonical site bodies');

  // Preserve the old painting as an explicit historical oracle, never the formal renderer.
  validateRegistration(historical, level, physicalAudit.pixelSize, 'historical');
  assert.equal(historical.resourcePath, `res://${physicalAudit.sourceImage}`, 'historical painting resource');
  const original = validateImage(images, historical.resourcePath, historical.pixelSize);
  assert.equal(sha256(original), physicalAudit.sourceSha256, 'historical painting bytes');
}

function validate(data) {
  const { levels, scene, packs, campaign } = data;
  assert.equal(levels.filter(level => level.id === 'c01-04').length, 1, 'one canonical C04');
  const level = levels.find(level => level.id === 'c01-04');
  assert.equal(level.schema, 3);
  assert.deepEqual([level.width, level.height], [50, 30], 'map size');
  assert.equal(level.terrain.length, 30, 'terrain height');
  for (const row of level.terrain) assert.equal(row.length, 50, 'terrain row width');
  assert.equal(level.elevation.length, 1500, 'elevation shape');
  assert(level.elevation.every(value => value === 0), 'authored level ground');
  const characters = Object.assign({}, ...packs.map(pack => pack.terrainCharacters ?? {}));
  const terrains = new Map(packs.flatMap(pack => pack.terrains ?? []).map(terrain => [terrain.id, terrain]));
  const at = ([x, y]) => level.terrain[y]?.[x];
  const cells = level.terrain.flatMap((row, y) => [...row].map((symbol, x) => ({ at: [x, y], symbol })));
  for (const { symbol } of cells) assert(terrains.has(characters[symbol]), `known terrain ${symbol}`);
  const walkable = cells.filter(({ symbol }) => terrains.get(characters[symbol]).cost.foot !== null);
  const passable = new Set(walkable.map(entry => key(entry.at)));
  assert.equal(cells.filter(entry => entry.symbol === '_').length, 323, 'outside cells');
  assert.equal(cells.filter(entry => entry.symbol !== '_').length, 1177, 'canonical domain');
  assert.equal(new Set(foundations.flat().map(key)).size, 37, 'complete foundation mask');
  for (const footprint of foundations)
    for (const point of footprint)
      assert.equal(at(point), outsideFoundations.has(key(point)) ? '_' : '#', `foundation ${key(point)}`);
  const expectedWalls = new Set(foundations.flat().filter(point => !outsideFoundations.has(key(point))).map(key));
  for (const rock of physicalAudit.rocks)
    for (const point of rock.coveredCells) {
      const outside = rock.outsideCells.some(candidate => key(candidate) === key(point));
      assert.equal(at(point), outside ? '_' : '#', `${rock.id} ground footprint ${key(point)}`);
      if (!outside) expectedWalls.add(key(point));
    }
  for (const prop of physicalAudit.furniture)
    for (const point of prop.blockedStandingCells) {
      assert.equal(at(point), '#', `${prop.id} occupied standing cell ${key(point)}`);
      expectedWalls.add(key(point));
    }
  assert.deepEqual(new Set(cells.filter(entry => entry.symbol === '#').map(entry => key(entry.at))),
    expectedWalls, 'no unexplained collision beyond the reviewed physical masks');
  assert.equal(walkable.length, physicalAudit.counts.footWalkableCells, 'audited final walkable count');

  // Four-neighbor connectivity is a static authoring check, not an engine route/cost forecast.
  const queue = [walkable[0].at];
  const seen = new Set([key(queue[0])]);
  for (let index = 0; index < queue.length; index++) {
    const [x, y] = queue[index];
    for (const next of [[x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]]) {
      const id = key(next);
      if (passable.has(id) && !seen.has(id)) { seen.add(id); queue.push(next); }
    }
  }
  assert.equal(seen.size, passable.size, 'one connected walkable region');
  assert.deepEqual(Object.fromEntries(level.units.map(unit => [unit.key, cell(unit)])), deployments, 'all 19 deployments');
  assert.equal(new Set(level.units.map(unit => key(cell(unit)))).size, 19, 'distinct spawns');
  for (const unit of level.units) assert(passable.has(key(cell(unit))), `walkable spawn ${unit.key}`);
  assert.equal(level.units.filter(unit => unit.owner === 1).length, 10, 'friendly actors');
  assert.equal(level.units.filter(unit => unit.owner === 2).length, 9, 'enemy actors');

  assert.equal(level.owners.length, 5, 'five initially owned sites');
  const owners = new Map(level.owners.map(owner => [key(cell(owner)), owner.owner]));
  assert.equal(owners.size, 5, 'unique site ownership');
  const capturables = cells.filter(({ symbol }) => terrains.get(characters[symbol]).capturable);
  assert.equal(capturables.length, 10, 'ten capturable sites');
  for (const [id, symbol, entrance, approach, owner] of sites) {
    assert.equal(at(entrance), symbol, `${id} terrain`);
    assert.equal(owners.get(key(entrance)) ?? 0, owner, `${id} ownership`);
    assert(passable.has(key(entrance)) && passable.has(key(approach)), `${id} entrance/approach`);
    assert.equal(Math.abs(entrance[0] - approach[0]) + Math.abs(entrance[1] - approach[1]), 1, `${id} approach adjacency`);
  }
  for (const position of owners.keys()) assert(sites.some(site => key(site[2]) === position), `owner on site ${position}`);
  assert.deepEqual(level.scenario.zones, zones, 'investigation/exit cells');
  assert.deepEqual(level.scenario.triggers.find(trigger => trigger.id === 'withdraw-with-evidence')
    .effects[0].directive.waypoints, [{ x: 8, y: 21 }], 'retreat waypoint');
  assert.equal(level.scenario.variables.evidence, 0, 'fresh battle-local evidence');
  assert.equal(level.rules.turnLimit, null, 'no hard turn deadline');
  assert.equal(level.extra.recommendedTurns, 12, 'recommendation is unchanged pending play evidence');
  assert.equal(level.extra.tacticalTheme, '开放林间营区、分兵搜证与西路撤离');
  assert.equal(sha256(JSON.stringify(nonspatialFacts(level))),
    '7f35e62b65a1ed6605dc8da5903aa36923ff63d13c3e72fc5d594f537059d329',
    'unchanged rules, objectives, actors, commanders, players and nonspatial scenario facts');

  assert.deepEqual(campaign.nodes.find(node => node.id === 'choice-black-camp-plan').choices, [
    { id: 'rescue-and-evidence', next: 'battle-04', effects: [
      { type: 'setFlag', flag: 'black_camp_rescue' }, { type: 'addVariable', key: 'witnesses', amount: 2 },
    ] },
    { id: 'split-the-force', next: 'battle-04', effects: [
      { type: 'setFlag', flag: 'black_camp_split' }, { type: 'addVariable', key: 'evidence', amount: 2 },
    ] },
  ], 'campaign-only staging choice effects');
  assert.deepEqual(campaign.nodes.find(node => node.id === 'battle-04').rosterBindings, [
    { campaignUnit: 'laiya', levelUnitKey: 'campaign-laiya' },
    { campaignUnit: 'torren', levelUnitKey: 'campaign-torren' },
    { campaignUnit: 'elin', levelUnitKey: 'campaign-elin' },
    { campaignUnit: 'mirelle', levelUnitKey: 'campaign-mirelle' },
  ], 'existing roster projection');
  assert.equal(scene.schemaVersion, 'environment-scene-v1.1');
  assert.equal(scene.levelId, level.id);
  assert.deepEqual(scene.mapSize, [50, 30], 'scene map size');
  assert.deepEqual(scene.openingFocus, [19, 19]);
  assert.equal(scene.runtimeReady, true, 'formal assembled scene readiness');
  assert.equal(scene.assembledGroundPath, 'res://data/scenes/c01-04-layered-ground.json', 'formal assembled scene binding');
  assert(scene.paintedGroundPath == null, 'one formal ground renderer');
  for (const field of ['placements', 'framePlacements', 'zones']) assert.deepEqual(scene[field], [], `retired tile ${field}`);
  assert(!Object.hasOwn(scene, 'groundOutline'), 'outside terrain owns the irregular domain');
  validatePresentation(data, level);
  return level;
}

const data = {
  levels: readJson('godot/data/grayflag-campaign-levels.json'),
  scene: readJson('godot/data/scenes/c01-04.json'),
  packs: readJson('godot/data/content-packs.json'),
  campaign: readJson('godot/data/grayflag-campaign.json'),
};
const resourceFile = path => {
  assert(typeof path === 'string' && path.startsWith('res://'), 'project resource path');
  const relative = path.slice(6);
  assert(relative.length > 0 && !isAbsolute(relative) && !relative.includes('\\') && !relative.includes(':')
    && !relative.split('/').includes('..'), 'project resource path');
  return resolve(root, 'godot', relative);
};
const readResource = path => readFileSync(resourceFile(path));
data.assembled = JSON.parse(readResource(data.scene.assembledGroundPath));
data.catalog = JSON.parse(readResource(data.assembled.objectCatalogPath));
data.historical = readJson('godot/data/scenes/c01-04-painted-ground.json');
data.provenance = readJson('godot/docs/art-provenance/c04-layered-v1/manifest.json');
data.images = Object.fromEntries([...new Set([
  data.assembled.resourcePath, data.historical.resourcePath,
  ...Object.values(data.catalog.objects).map(asset => asset.resourcePath),
])].map(path => [path, readResource(path)]));
const level = validate(data);
const levelSha256 = sha256(JSON.stringify(level));

if (process.argv.includes('--self-test')) {
  assert.equal(resourceFile(data.scene.assembledGroundPath), resolve(root, 'godot/data/scenes/c01-04-layered-ground.json'));
  const invalidPaths = ['res:///tmp/outside.json', 'res://../outside.json', 'res://C:/outside.json',
    'res://..\\outside.json', 'res://', 'file:///tmp/outside.json'];
  for (const path of invalidPaths) assert.throws(() => resourceFile(path), /project resource path/, path);
  console.log(`PASS: ${invalidPaths.length} invalid project resource paths rejected before file access`);
  const mutations = [
    ['map shape', (copy, c04) => c04.terrain[0] = c04.terrain[0].slice(1), /terrain row width/],
    ['foundation hole', (copy, c04) => c04.terrain[20] = c04.terrain[20].slice(0, 6) + '.' + c04.terrain[20].slice(7), /foundation/],
    ['solid rock hole', (copy, c04) => c04.terrain[17] = c04.terrain[17].slice(0, 38) + '.' + c04.terrain[17].slice(39), /ground footprint/],
    ['cart occupancy hole', (copy, c04) => c04.terrain[16] = c04.terrain[16].slice(0, 41) + '.' + c04.terrain[16].slice(42), /occupied standing cell/],
    ['spawn drift', (copy, c04) => c04.units[0].x++, /all 19 deployments/],
    ['site ownership', (copy, c04) => c04.owners[0].owner = 1, /ownership/],
    ['old retreat coordinate', (copy, c04) => c04.scenario.triggers[3].effects[0].directive.waypoints[0].x = 0, /retreat waypoint/],
    ['battle evidence shortcut', (copy, c04) => c04.scenario.variables.evidence = 2, /fresh battle-local evidence/],
    ['actor stat change', (copy, c04) => c04.units[0].hp = 1, /unchanged rules/],
    ['staging effect change', copy => copy.campaign.nodes.find(node => node.id === 'choice-black-camp-plan').choices[1].effects[1].amount = 3, /campaign-only staging choice effects/],
    ['stale scene size', copy => copy.scene.mapSize = [20, 14], /scene map size/],
    ['missing assembled binding', copy => delete copy.scene.assembledGroundPath, /formal assembled scene binding/],
    ['wrong assembled binding', copy => copy.scene.assembledGroundPath = 'res://data/scenes/c01-03-layered-ground.json', /formal assembled scene binding/],
    ['competing painted binding', copy => copy.scene.paintedGroundPath = 'res://data/scenes/c01-04-painted-ground.json', /one formal ground renderer/],
    ['disabled admission', copy => copy.scene.runtimeReady = false, /formal assembled scene readiness/],
    ['wrong registered level', copy => copy.assembled.levelId = 'c01-03', /assembled level identity/],
    ['stale registered digest', copy => copy.assembled.levelSourceSha256 = '0'.repeat(64), /assembled canonical source checksum/],
    ['wrong canvas', copy => copy.assembled.pixelSize[0]++, /assembled reviewed canvas/],
    ['wrong origin', copy => copy.assembled.originPixels[0]++, /assembled originPixels/],
    ['wrong cell axes', copy => copy.assembled.cellXPixels[0]++, /assembled cellXPixels/],
    ['rescaled map', copy => copy.assembled.worldScale = 1, /assembled world scale/],
    ['historical ground substituted', copy => copy.assembled.resourcePath = copy.historical.resourcePath, /formal independent ground resource/],
    ['wrong object catalog', copy => copy.assembled.objectCatalogPath = 'res://data/scenes/c01-03-layered-objects.json', /formal object catalog/],
    ['duplicate placement identity', copy => copy.assembled.objects[1].id = copy.assembled.objects[0].id, /unique placement identities/],
    ['unknown object asset', copy => copy.assembled.objects[0].assetId = 'absent-asset', /registered object asset/],
    ['nonfinite object contact', copy => copy.assembled.objects[0].contact[0] = Infinity, /finite object contact/],
    ['zero object scale', copy => copy.assembled.objects[0].scale = 0, /positive object scale/],
    ['missing composition object', copy => copy.assembled.objects.pop(), /reviewed independent composition count/],
    ['missing site binding', copy => {
      Object.assign(copy.assembled.objects[0], { selectionCell: null, ownsMapStructureBody: false, pointer: 'pass-through' });
    }, /exactly ten canonical site bodies/],
    ['duplicate site cell', copy => copy.assembled.objects[1].selectionCell = [...copy.assembled.objects[0].selectionCell], /canonical site cell/],
    ['lost site body', copy => copy.assembled.objects[0].ownsMapStructureBody = false, /owned site target/],
    ['non-target site', copy => copy.assembled.objects[0].pointer = 'pass-through', /owned site target/],
    ['unbound object texture', copy => copy.catalog.objects['prisoner-tent'].resourcePath = 'res://absent.png', /loaded image resource/],
    ['wrong object canvas', copy => copy.catalog.objects['prisoner-tent'].pixelSize[0]++, /PNG dimensions/],
    ['missing registered texture', copy => delete copy.images[copy.assembled.resourcePath], /loaded image resource/],
    ['changed ground bytes', copy => {
      const path = copy.assembled.resourcePath;
      copy.images[path] = Buffer.from(copy.images[path]);
      copy.images[path][copy.images[path].length - 1] ^= 1;
    }, /independent image bytes/],
    ['changed object bytes', copy => {
      const path = copy.catalog.objects['prisoner-tent'].resourcePath;
      copy.images[path] = Buffer.from(copy.images[path]);
      copy.images[path][copy.images[path].length - 1] ^= 1;
    }, /independent image bytes/],
    ['historical stale digest', copy => copy.historical.levelSourceSha256 = '0'.repeat(64), /historical canonical source checksum/],
    ['historical image changed', copy => {
      const path = copy.historical.resourcePath;
      copy.images[path] = Buffer.from(copy.images[path]);
      copy.images[path][copy.images[path].length - 1] ^= 1;
    }, /historical painting bytes/],
  ];
  for (const [name, mutate, pattern] of mutations) {
    const copy = structuredClone({ ...data, images: null });
    copy.images = { ...data.images }; // Byte mutations copy only the image they change.
    mutate(copy, copy.levels.find(candidate => candidate.id === 'c01-04'));
    assert.throws(() => validate(copy), pattern, `must reject ${name}`);
  }
  validate(data);
  console.log(`PASS: ${mutations.length} deliberate author-data and presentation mutations rejected`);
}
const characters = Object.assign({}, ...data.packs.map(pack => pack.terrainCharacters ?? {}));
const terrains = new Map(data.packs.flatMap(pack => pack.terrains ?? []).map(terrain => [terrain.id, terrain]));
const footCells = [...level.terrain.join('')].filter(symbol => terrains.get(characters[symbol]).cost.foot !== null).length;
console.log(`PASS: C04 50x30, 1177 domain, ${footCells} connected foot cells, 37 house foundation cells, 19 spawns, 10 sites`);
console.log(`C04 ECMAScript JSON SHA-256: ${levelSha256}`);
console.log('PASS: formal assembled registration, 139 placements, ten site bodies, referenced PNG dimensions and C04 source bytes');
console.log('PASS: historical painting integrity retained separately; native rendering, natural play and GUI acceptance are separate.');
