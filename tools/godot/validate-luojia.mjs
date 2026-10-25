import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const read = path => JSON.parse(readFileSync(resolve(root, path), 'utf8'));
const hash = value => createHash('sha256').update(value).digest('hex');
const key = cell => cell.join(',');
const keys = cells => new Set(cells.map(key));
const position = unit => [unit.x, unit.y];
function clip(polygon, axis, boundary, sign) {
  const output = [];
  for (let i = 0; i < polygon.length; i++) {
    const a = polygon[i], b = polygon[(i + 1) % polygon.length];
    const aIn = (a[axis] - boundary) * sign >= 0, bIn = (b[axis] - boundary) * sign >= 0;
    if (aIn) output.push(a);
    if (aIn !== bIn) { const t = (boundary - a[axis]) / (b[axis] - a[axis]); output.push([a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])]); }
  }
  return output;
}
const area = polygon => Math.abs(polygon.reduce((sum, a, i) => { const b = polygon[(i + 1) % polygon.length]; return sum + a[0] * b[1] - b[0] * a[1]; }, 0)) / 2;
function overlaps(polygon, [x, y], basis) {
  const [ox, oy] = basis.originPixels, dx = basis.cellXPixels[0], dy = basis.cellYPixels[1];
  let p = polygon;
  for (const [axis, boundary, sign] of [[0, ox + x * dx, 1], [0, ox + (x + 1) * dx, -1], [1, oy + y * dy, 1], [1, oy + (y + 1) * dy, -1]]) p = clip(p, axis, boundary, sign);
  return area(p) > 0.01;
}
function facts(source) {
  const level = structuredClone(source);
  for (const k of ['width', 'height', 'terrain', 'elevation', 'owners']) delete level[k];
  for (const unit of level.units) { delete unit.x; delete unit.y; }
  for (const zone of level.scenario.zones) delete zone.cells;
  return level;
}
function validate({ level, retired, scene, painted, audit, packs }) {
  assert.deepEqual(facts(level), facts(retired), 'preserved nonspatial C07 mission, actors, morale and deadline');
  assert(level.width > 23 && level.height > 15);
  assert.equal(level.terrain.length, level.height);
  assert(level.terrain.every(row => row.length === level.width));
  assert.equal(level.elevation.length, level.width * level.height);
  assert(level.elevation.every(value => value === 0));
  const chars = Object.assign({}, ...packs.map(pack => pack.terrainCharacters ?? {}));
  const terrains = new Map(packs.flatMap(pack => pack.terrains ?? []).map(t => [t.id, t]));
  const cells = level.terrain.flatMap((row, y) => [...row].map((symbol, x) => ({ cell: [x, y], symbol })));
  const at = ([x, y]) => level.terrain[y]?.[x];
  assert(cells.every(entry => terrains.has(chars[entry.symbol])));
  const passable = keys(cells.filter(entry => terrains.get(chars[entry.symbol]).cost.foot !== null).map(entry => entry.cell));
  assert.equal(audit.sites.length, 16);
  for (const [symbol, count] of [['v', 8], ['B', 4], ['q', 4]]) assert.equal(cells.filter(e => e.symbol === symbol).length, count, 'original site types and counts');
  assert.deepEqual(keys(cells.filter(e => terrains.get(chars[e.symbol]).capturable).map(e => e.cell)), keys(audit.sites.map(s => s.cell)));
  assert.deepEqual(audit.sites.map(s => [s.terrain, s.owner]).sort(), [['q', 1], ['q', 2], ['q', 0], ['q', 0], ...Array.from({ length: 8 }, () => ['v', 0]), ...Array.from({ length: 4 }, () => ['B', 0])].sort(), 'original site owners');
  for (const [id, owner] of Object.entries({ 'northwest-gate': 1, 'northeast-gate': 2, 'southwest-gate': 0, 'southeast-gate': 0 })) {
    const sites = audit.sites.filter(site => site.id === id);
    assert.equal(sites.length, 1, 'canonical gate identity must be unique');
    assert.deepEqual([sites[0].terrain, sites[0].owner], ['q', owner], `${id} canonical gate ownership`);
  }
  assert.deepEqual(level.owners, audit.sites.filter(s => s.terrain !== 'v').map(s => ({ x: s.cell[0], y: s.cell[1], owner: s.owner })), 'canonical explicit owners');
  for (const site of audit.sites) {
    assert.equal(at(site.cell), site.terrain);
    assert(passable.has(key(site.cell)) && passable.has(key(site.approach)), `${site.id} real entrance and approach`);
    assert.equal(Math.abs(site.cell[0] - site.approach[0]) + Math.abs(site.cell[1] - site.approach[1]), 1);
  }
  const walls = new Set();
  for (const polygon of audit.blockedPolygons) {
    assert(area(polygon.polygonPixels) > 0);
    const covered = cells.filter(e => overlaps(polygon.polygonPixels, e.cell, audit.basis)).map(e => e.cell);
    assert(covered.length > 0, `${polygon.id} must have a physical footprint`);
    assert.deepEqual(keys(polygon.blockedCells), keys(covered), 'complete positive-area foundation coverage');
    for (const cell of covered) { walls.add(key(cell)); assert.equal(at(cell), '#', `${polygon.id} physical foundation cannot be walkable`); }
  }
  assert.deepEqual(keys(cells.filter(e => e.symbol === '#').map(e => e.cell)), walls, 'all walls have reviewed geometry');
  assert.deepEqual(keys(cells.filter(e => e.symbol === '_').map(e => e.cell)), keys(audit.outsideCells), 'explicit reviewed nonplayable projection pockets');
  assert(audit.outsideCells.length < cells.length * 0.05, 'projection pockets cannot become empty map enlargement');
  const queue = [audit.openingFocus], reached = keys(queue);
  for (let i = 0; i < queue.length; i++) {
    const [x, y] = queue[i];
    for (const n of [[x-1,y],[x+1,y],[x,y-1],[x,y+1]]) if (passable.has(key(n)) && !reached.has(key(n))) { reached.add(key(n)); queue.push(n); }
  }
  assert.equal(reached.size, passable.size, 'one connected street network');
  assert.equal(keys(level.units.map(position)).size, 23);
  for (const unit of level.units) assert(passable.has(key(position(unit))), `actual ground for ${unit.key}`);
  for (const zone of level.scenario.zones) {
    assert.equal(zone.cells.length, 1);
    const site = audit.sites.find(s => s.id === zone.id);
    assert(site && site.terrain === 'B');
    assert.deepEqual(position(zone.cells[0]), site.cell, 'mission control target is actual civic entrance');
  }
  const arches = ['northwest-arch', 'northeast-arch', 'southwest-arch', 'southeast-arch'];
  assert.deepEqual(audit.ingressPoints.map(ingress => ingress.id).sort(), [...arches, 'north-street', 'south-street', 'west-street', 'east-street'].sort(),
    'exactly eight distinct authored ingress identities');
  assert.equal(new Set(audit.routes.map(route => route.id)).size, audit.routes.length, 'route identities must be unique');
  const nearPixel = (cell, pixel) => {
    const [ox, oy] = audit.basis.originPixels, dx = audit.basis.cellXPixels[0], dy = audit.basis.cellYPixels[1];
    // Reviewed openings may snap to the neighboring clear cell when their pixel lies on a blocked edge.
    return Math.abs(ox + (cell[0] + 0.5) * dx - pixel[0]) <= 1.5 * dx
      && Math.abs(oy + (cell[1] + 0.5) * dy - pixel[1]) <= 1.5 * dy;
  };
  for (const ingress of audit.ingressPoints) {
    assert(passable.has(key(ingress.cell)), `${ingress.id} visibly open entry must stay open`);
    assert(nearPixel(ingress.cell, ingress.pixel), `${ingress.id} entry remains at its reviewed visible opening`);
    const toCenter = audit.routes.find(route => route.id === ingress.id);
    assert(toCenter && key(toCenter.cells[0]) === key(ingress.cell)
      && key(toCenter.cells.at(-1)) === key(audit.openingFocus), `${ingress.id} route must connect its entry to the civic center`);
    if (!arches.includes(ingress.id)) continue;
    const through = audit.routes.find(route => route.id === `through-${ingress.id}`);
    assert(through && through.cells.some(cell => key(cell) === key(ingress.cell)), `${ingress.id} through-route must cross its actual arch`);
    assert(nearPixel(through.cells[0], ingress.outsidePixel) && nearPixel(through.cells.at(-1), ingress.insidePixel),
      `${ingress.id} through-route endpoints must match the reviewed exterior and interior approaches`);
  }
  for (const route of audit.routes) {
    assert(route.cells.length > 1);
    for (const [i, cell] of route.cells.entries()) {
      assert(passable.has(key(cell)), `${route.id} real street`);
      if (i) assert.equal(Math.abs(cell[0] - route.cells[i-1][0]) + Math.abs(cell[1] - route.cells[i-1][1]), 1);
    }
  }
  assert.deepEqual([scene.levelId, scene.mapSize, scene.runtimeReady, scene.paintedGroundPath], ['c01-07', [level.width, level.height], true, 'res://data/scenes/c01-07-painted-ground.json']);
  for (const field of ['placements', 'framePlacements', 'zones']) assert.deepEqual(scene[field], []);
  assert.equal(painted.resourcePath, 'res://assets/native/scenes/luojia.png');
  assert.deepEqual(painted.pixelSize, [1536, 1024]);
  for (const f of ['worldScale', 'originPixels', 'cellXPixels', 'cellYPixels']) assert.deepEqual(painted[f], audit.basis[f]);
  assert.deepEqual(keys(painted.occluders.filter(o => o.selectionCell).map(o => o.selectionCell)), keys(audit.sites.map(s => s.cell)));
  for (const site of audit.sites) {
    const o = painted.occluders.find(o => o.id === site.id);
    assert(o?.ownsMapStructureBody && o.parts.some(p => p.pointer.kind === 'target'), 'every site owns a real facade target');
    assert.deepEqual(o.selectionCell, site.cell);
  }
  for (const o of painted.occluders) for (const p of o.parts.filter(p => p.id.includes('roof'))) assert.equal(p.pointer.kind, 'pass-through', 'roof must pass through');
  assert.equal(painted.levelSourceSha256, hash(JSON.stringify(level)), 'canonical source checksum');
  return { walkable: passable.size, walls: walls.size };
}
const data = {
  level: read('godot/data/grayflag-campaign-levels.json').find(l => l.id === 'c01-07'),
  retired: read('godot/tests/native/luojia-retired-level.json'),
  scene: read('godot/data/scenes/c01-07.json'),
  painted: read('godot/data/scenes/c01-07-painted-ground.json'),
  audit: read('godot/docs/art-provenance/luojia-geometry.json'),
  packs: read('godot/data/content-packs.json'),
};
const counts = validate(data);
const image = readFileSync(resolve(root, 'godot/assets/native/scenes/luojia.png'));
assert.equal(hash(image), data.audit.sourceSha256);
assert.deepEqual([image.readUInt32BE(16), image.readUInt32BE(20)], data.painted.pixelSize);
if (process.argv.includes('--self-test')) {
  const setCell = (d, [x,y], symbol) => { const row=d.level.terrain[y]; d.level.terrain[y]=row.slice(0,x)+symbol+row.slice(x+1); };
  const mutations = [
    ['foundation hole', d => setCell(d,d.audit.blockedPolygons[0].blockedCells[0],'p'), /physical foundation/],
    ['shorter deadline', d => d.level.rules.turnLimit=10, /nonspatial C07/],
    ['changed objective', d => d.level.players[0].objectives[0].objective.type='any', /nonspatial C07/],
    ['extra site', d => setCell(d,d.audit.openingFocus,'v'), /site types and counts/],
    ['changed site owner', d => d.audit.sites[0].owner=9, /site owners/],
    ['swapped gate owners', d => {
      for (const id of ['northwest-gate', 'northeast-gate']) {
        const site = d.audit.sites.find(site => site.id === id);
        site.owner = site.owner === 1 ? 2 : 1;
        d.level.owners.find(owner => key(position(owner)) === key(site.cell)).owner = site.owner;
      }
    }, /canonical gate ownership/],
    ['missing side ingress', d => d.audit.ingressPoints.splice(d.audit.ingressPoints.findIndex(i => i.id === 'west-street'), 1), /eight distinct authored ingress/],
    ['duplicate ingress identity', d => d.audit.ingressPoints[1].id=d.audit.ingressPoints[0].id, /eight distinct authored ingress/],
    ['route skips its arch', d => {
      const ingress = d.audit.ingressPoints.find(i => i.id === 'northwest-arch');
      const route = d.audit.routes.find(r => r.id === 'through-northwest-arch');
      route.cells = route.cells.filter(cell => key(cell) !== key(ingress.cell));
    }, /must cross its actual arch/],
    ['reversed gate approaches', d => d.audit.routes.find(r => r.id === 'through-northwest-arch').cells.reverse(), /through-route endpoints/],
    ['roof steals pick', d => d.painted.occluders.flatMap(o=>o.parts).find(p=>p.id.includes('roof')).pointer.kind='target', /roof must pass through/],
    ['stale digest', d => d.painted.levelSourceSha256='0'.repeat(64), /canonical source checksum/],
  ];
  for (const [name, mutate, pattern] of mutations) { const d=structuredClone(data); mutate(d); assert.throws(()=>validate(d),pattern,name); }
  console.log(`PASS: ${mutations.length} semantic negative controls`);
}
console.log(`PASS: C07 ${data.level.width}x${data.level.height}; 16 canonical sites, ${counts.walkable} connected foot cells, ${counts.walls} reviewed solid cells. Native movement and normal-AI/GUI gates remain separate.`);
