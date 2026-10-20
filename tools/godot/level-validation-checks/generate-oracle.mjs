import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { validateLevel } from '../../../packages/battle-engine/src/level-validation';
const root = fileURLToPath(new URL('../../../', import.meta.url));
const output = process.env.EMPIRE_LEVEL_VALIDATION_ORACLE_OUTPUT;
if (!output) throw new Error('Set EMPIRE_LEVEL_VALIDATION_ORACLE_OUTPUT.');
const packs = JSON.parse(readFileSync(`${root}/godot/data/content-packs.json`, 'utf8'));
const levels = JSON.parse(readFileSync(`${root}/godot/data/grayflag-campaign-levels.json`, 'utf8'));
const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const rules = createBattleEngine({ content }).rules;
const features = Object.fromEntries(rules.referenceChecks.all().map(check => [check.id, [...check.known(rules)]]));
const clone = value => structuredClone(value);
const cases = [];
const failureCases = [];
const player = id => ({ id, name: `Side ${id}`, team: id, color: '#123456', controller: id === 1 ? 'human' : 'ai', resources: { funds: { current: 0, capacity: null } }, ai: { aggression: 0.5 } });
const unit = (owner = 1, x = 0, y = 0) => ({ key: `unit-${owner}`, unit: 'soldier', owner, x, y });
const point = (x = 0, y = 0) => ({ x, y });
/** @returns {any} Deliberately mutable JSON fixtures, including malformed source inputs. */
function base() {
  return { schema: 2, id: 'validation-probe', name: 'Probe', description: '', width: 6, height: 4,
    terrain: ['......', '......', '......', '......'], players: [player(1), player(2)], units: [unit(), unit(2, 5, 3)], rules: {}, victory: [{ type: 'routEnemies', id: 'rout' }],
    scenario: { zones: [{ id: 'near', cells: [point()] }], triggers: [] } };
}
/** @param {string} name @param {(level: any) => void} [edit] @param {any} [supplied] */
function run(name, edit = () => {}, supplied) {
  const level = supplied ? clone(supplied) : base(); edit(level);
  const before = JSON.stringify(level);
  const issues = validateLevel(rules, level).filter(issue => issue.severity === 'error').map(issue => issue.message);
  if (before !== JSON.stringify(level)) throw new Error(`Source validator mutated ${name}`);
  cases.push({ name, level, issues });
}
function condition(name, value) { run(name, level => level.scenario.triggers.push({ id: 'test', timing: 'afterAction', condition: value, effects: [] })); }
function effect(name, value) { run(name, level => level.scenario.triggers.push({ id: 'test', timing: 'afterAction', condition: { type: 'turnAtLeast', turn: 2 }, effects: [value] })); }
function objective(name, value) { run(name, level => level.victory = [value]); }
run('valid base');
for (const level of levels) run(`shipped ${level.id}`, () => {}, level);
const mutations = /** @type {Array<[string, (level: any) => void]>} */ ([
  ['bad map size', l => l.width = 0], ['fractional map size', l => l.width = 2.5],
  ['missing row', l => l.terrain.pop()], ['short row', l => l.terrain[1] = '.'],
  ['unknown terrain char', l => l.terrain[1] = '.?....'], ['wrong elevation size', l => l.elevation = [0]],
  ['fractional elevation', l => l.elevation = Array(24).fill(0.1)], ['missing identity', l => l.id = '  '],
  ['duplicate invalid players', l => l.players = [player(0), player(0)]], ['single player', l => l.players.pop()],
  ['duplicate unit keys', l => l.units[1].key = l.units[0].key],
  ['duplicate shared objectives', l => l.victory = [{ type: 'routEnemies', id: 'same' }, { type: 'all', objectives: [{ type: 'captureHQ', id: 'same' }] }]],
  ['bad player resources', l => l.players[0].resources = { ' ': { current: -1, capacity: -2 }, funds: { current: 4, capacity: 3 } }],
  ['unknown unit', l => l.units[0].unit = 'missing-unit'], ['unknown unit owner', l => l.units[0].owner = 8],
  ['bad rank', l => l.units[0].rank = 3], ['bad rank progress', l => l.units[0].rankProgress = -0.1],
  ['bad morale', l => l.units[0].morale = -1], ['bad unit resources', l => l.units[0].resources = { '': { current: -1, capacity: -2 } }],
  ['unknown facing', l => l.units[0].facing = 'south-south'], ['hex facing', l => { l.rules.grid = 'hex'; l.units[0].facing = 'hexNortheast'; }],
  ['hex rejects square facing', l => { l.rules.grid = 'hex'; l.units[0].facing = 'north'; }],
  ['square8 diagonal', l => { l.rules.grid = 'square8'; l.units[0].facing = 'northeast'; }],
  ['unknown grid abstains facing', l => { l.rules.grid = 'missing'; l.units[0].facing = 'impossible'; }],
  ['unknown formation', l => l.units[0].formation = 'missing'], ['disallowed formation', l => l.units[0].formation = 'formation-defensive'],
  ['unknown career', l => l.units[0].career = 'missing'], ['mismatched career', l => l.units[0].career = content.careers.all().find(c => c.unitType !== 'soldier').id],
  ['unlocked career and mastery', l => { l.units[0].unlockedCareers = ['missing']; l.units[0].careerMastery = { missing: -2 }; }],
  ['unit off board', l => l.units[0].x = -1], ['occupied cell', l => { l.units[1].x = 0; l.units[1].y = 0; }],
  ['impassable tile', l => l.terrain[0] = '~.....'],
  ['bad patrol', l => l.units[0].directive = { mode: 'patrol', zone: 'missing', waypoints: [point(-1), point(0.5)] }],
  ['bad commander link', l => l.units[0].commander = 'missing'],
  ['bad commander', l => l.commanders = [{ id: 'lead', unitKey: 'missing', radius: -1, tactics: ['missing'] }]],
  ['unknown structure', l => l.structures = [{ id: 'fort', type: 'missing', x: -1, y: 0, owner: 8 }]],
  ['empty composite', l => l.composites = [{ id: 'bridge', parts: [] }]],
  ['unknown composite part', l => l.composites = [{ id: 'bridge', parts: ['missing'], minimumNeutralized: 0.5 }]],
  ['offboard zone', l => l.scenario.zones[0].cells.push(point(99))],
  ['bad engagement rule', l => l.scenario.engagementRules = [{ id: 'rule', zone: 'missing', players: [0, 8], mode: 'no-attacks' }]],
  ['empty deployment', l => l.deployment = { zones: [], order: [] }],
  ['unknown deployment refs', l => l.deployment = { order: [8], zones: [{ player: 8, zone: 'missing', unitKeys: ['missing'] }] }],
  ['wrong deployment owner duplicate key', l => l.deployment = { zones: [{ player: 2, zone: 'near', unitKeys: ['unit-1'] }, { player: 1, zone: 'near', unitKeys: ['unit-1'] }] }],
  ['duplicate catch all', l => l.deployment = { zones: [{ player: 1, zone: 'near' }, { player: 1, zone: 'near' }] }],
  ['explicit empty assignments not catch all', l => l.deployment = { zones: [{ player: 1, zone: 'near', unitKeys: [] }, { player: 1, zone: 'near', unitKeys: [] }] }],
  ['bad overlay', l => l.scenario.overlays = [{ id: 'smoke', type: 'missing', zone: 'missing', remainingRounds: 0 }]],
  ['owner offboard', l => l.owners = [{ x: -1, y: 0, owner: 8 }]],
  ['unownable tile', l => l.owners = [{ x: 0, y: 0, owner: 8 }]],
  ['unknown building owner', l => { const terrain = content.terrains.all().find(t => t.capturable); l.terrain[0] = content.terrainEncoding.character(terrain.id) + '.....'; l.owners = [{ x: 0, y: 0, owner: 8 }]; }],
  ['no opening force', l => l.units = []],
  ['unknown learned abilities', l => l.units[0].learnedAbilities = ['missing', 'missing']],
  ['unknown reaction', l => l.units[0].reaction = 'missing'], ['unknown directive', l => l.units[0].directive = { mode: 'missing' }],
  ['unknown turn order', l => l.rules.turnOrder = 'missing'],
]);
for (const [name, edit] of mutations) run(name, edit);
const declarations = /** @type {Array<[string, string, object, boolean]>} */ ([
  ['commanders', 'commander', { unitKey: 'unit-1', radius: 1 }, false],
  ['structures', 'structure', { type: content.structures.all()[0].id, x: 1, y: 1 }, false],
  ['composites', 'composite', { parts: ['absent'] }, false],
  ['zones', 'zone', { cells: [] }, true], ['engagementRules', 'engagement', { zone: 'near', mode: 'no-attacks' }, true],
  ['overlays', 'overlay', { type: content.terrainOverlays.all()[0].id, zone: 'near' }, true],
  ['triggers', 'trigger', { condition: { type: 'turnAtLeast', turn: 9 }, effects: [], timing: 'afterAction' }, true],
]);
for (const [family, subject, template, scenario] of declarations) run(`declaration ${subject} empty and repeated`, l => { (scenario ? l.scenario : l)[family] = [{ id: '', ...clone(template) }, { id: '', ...clone(template) }]; });
const cliffs = /** @type {Array<[string, object, object]>} */ ([['cliff offboard', point(-1), point()], ['cliff nonadjacent', point(), point(2)], ['cliff fractional', point(0.5), point(1.5)], ['cliff valid', point(), point(1)]]);
for (const [name, from, to] of cliffs)
  run(name, l => l.cliffs = [{ from, to }]);
const covers = /** @type {Array<[string, object, object]>} */ ([['cover offboard', point(-1), { north: 'half' }], ['cover fractional', point(0.5), { north: 'half' }], ['cover invalid strength', point(), { north: 'none' }], ['cover invalid side', point(), { nowhere: 'full' }], ['empty cover warning omitted', point(), {}]]);
for (const [name, at, sides] of covers)
  run(name, l => l.directionalCover = [{ at, sides }]);
const conditions = /** @type {Array<[string, object]>} */ ([
  ['turnCycle', { every: 0 }], ['turnCycle', { every: 1.5 }], ['currentPlayer', { player: 0 }], ['unitInZone', { zone: 'missing', owner: 999 }],
  ['unitCount', { selector: { zone: 'missing', owner: 0 }, op: 'eq', value: 0 }], ['unitHealth', { selector: { zone: 'missing' }, op: 'eq', value: 0, aggregate: 'all' }],
  ['markerCount', { selector: { zone: 'missing' }, op: 'eq', value: 0 }], ['structure', { id: 'missing', state: 'destroyed' }], ['composite', { id: 'missing', state: 'destroyed' }],
  ['objective', { player: 8, id: 'missing', status: 'completed' }], ['all', { conditions: [{ type: 'any', conditions: [{ type: 'not', condition: { type: 'currentPlayer', player: 8 } }] }] }],
  ['missing', { condition: { type: 'currentPlayer', player: 8 } }], ['all', { conditions: [{ type: 'missing' }] }],
]);
for (const [type, payload] of conditions) condition(`condition ${type} ${JSON.stringify(payload)}`, { type, ...payload });
for (const type of ['addStatus', 'removeStatus']) effect(type, { type, status: 'missing-status', selector: { zone: 'missing-zone' }, duration: 2 });
for (const type of ['changeUnitOwner', 'withdrawUnits', 'reviveMarkers', 'removeMarkers', 'changeUnitResource', 'changeMorale', 'surrenderUnits'])
  effect(type, { type, selector: { zone: 'missing' }, owner: 0, to: 0, resource: 'funds', amount: 1 });
effect('spawn multiple refs', { type: 'spawnUnits', units: [{ unit: 'missing', owner: 8, x: -1, y: 0 }, { unit: 'soldier', owner: 0, x: 0.5, y: 0 }] });
effect('set team', { type: 'setPlayerTeam', player: 8, team: 1 });
for (const distance of [-1, 0.5, 0]) effect(`force move ${distance}`, { type: 'forceMove', selector: { zone: 'missing' }, source: point(-1), mode: 'pull', distance });
for (const type of ['teleportUnits', 'restoreWithdrawnUnits']) effect(type, { type, selector: { zone: 'selector-zone' }, zone: 'destination-zone' });
effect('overlay', { type: 'addOverlay', overlay: 'missing', zone: 'missing', id: 'new' });
for (const type of ['activateObjective', 'cancelObjective', 'completeObjective', 'revealObjective']) effect(type, { type, player: 8, id: 'missing' });
effect('directive all refs', { type: 'setUnitDirective', selector: { zone: 'selector-zone' }, directive: { mode: 'missing', zone: 'directive-zone', waypoints: [point(-1)] } });
effect('dynamic engagement', { type: 'addEngagementRule', rule: { id: ' ', zone: 'missing', players: [0], mode: 'no-attacks' } });
effect('dynamic terrain', { type: 'replaceTerrain', zone: 'missing', terrain: 'missing' });
effect('set elevation', { type: 'setElevation', zone: 'missing', value: 0.5 });
effect('add elevation', { type: 'addElevation', zone: 'missing', amount: 0.5 });
effect('cliff refs', { type: 'setCliffs', edges: [{ from: point(), to: point(2) }, { from: point(-1), to: point() }], blocked: true });
effect('cover refs', { type: 'setDirectionalCover', covers: [{ at: point(-1), sides: {} }] });
for (const type of ['damageStructure', 'repairStructure', 'moveComposite']) effect(type, { type, id: 'missing', amount: 1, dx: 1, dy: 1 });
for (const type of ['setVariable', 'addVariable', 'removeOverlay', 'removeEngagementRule', 'emitSignal']) effect(`no references ${type}`, { type, id: 'missing', key: 'flag', value: true, amount: 1, signal: 'ok' });
effect('unknown effect', { type: 'missing', selector: { zone: 'missing' } });
const objectives = /** @type {Array<[string, object]>} */ ([
  ['eliminate', { selector: { zone: 'missing' } }], ['destroy', { structures: ['missing'] }], ['neutralizeComposite', { composite: 'missing', minimumNeutralized: 0.5 }],
  ['protect', { selector: { zone: 'missing' }, minimumAlive: 0, untilTurn: 0 }], ['escort', { zone: 'missing', selector: { zone: 'selector-zone' }, count: 0 }],
  ['control', { zone: 'missing' }], ['all', { objectives: [] }], ['any', { objectives: [{ type: 'missing' }] }], ['missing', { objectives: [{ type: 'control', zone: 'missing' }] }],
  ['failOn', { condition: { type: 'not', condition: { type: 'unitInZone', zone: 'missing' } }, objective: { type: 'routEnemies' } }],
  ['optional', { objective: { type: 'destroy', structures: ['missing'] } }],
]);
for (const [type, payload] of objectives) objective(`objective ${type}`, { type, ...payload });
run('repeat schedule errors', l => l.scenario.triggers.push({ id: 'repeat', timing: 'turnStart', condition: { type: 'turnCycle', every: 0 }, effects: [], repeat: { everyRounds: 0.5, startTurn: 0, endTurn: -1, maxFirings: 0.5 } }));
run('specific objective declarations replace shared', l => { l.players[0].objectives = [{ id: 'own', type: 'routEnemies' }]; l.scenario.triggers.push({ id: 'targets', timing: 'afterAction', condition: { type: 'turnAtLeast', turn: 1 }, effects: [{ type: 'activateObjective', player: 1, id: 'rout' }, { type: 'activateObjective', player: 2, id: 'rout' }] }); });
run('selector neutral absent ids valid', l => { l.victory = [{ type: 'eliminate', selector: { owner: 0, ids: [999], keys: ['not-spawned'] } }]; });
run('playable production without units', l => { const terrain = content.terrains.all().find(t => t.capturable && t.produces.length); const char = content.terrainEncoding.character(terrain.id); l.terrain[0] = char + char + '....'; l.units = []; l.owners = [{ x: 0, y: 0, owner: 1 }, { x: 1, y: 0, owner: 2 }]; });
run('map failure suppresses placement not refs', l => { l.terrain = []; l.units[0].x = -1; l.units[0].unit = 'missing'; l.scenario.zones[0].cells = [point(-1)]; l.victory = [{ type: 'control', zone: 'missing' }]; });
for (const value of [null, 'not-a-number', true, {}, []]) {
  for (const field of ['rank', 'rankProgress', 'morale']) run(`malformed ${field} ${JSON.stringify(value)}`, l => l.units[0][field] = clone(value));
  run(`malformed current resource ${JSON.stringify(value)}`, l => l.players[0].resources.funds.current = clone(value));
  run(`malformed commander radius ${JSON.stringify(value)}`, l => l.commanders = [{ id: 'leader', unitKey: 'unit-1', radius: clone(value) }]);
  run(`malformed mastery ${JSON.stringify(value)}`, l => l.units[0].careerMastery = { militia: clone(value) });
  condition(`malformed cycle ${JSON.stringify(value)}`, { type: 'turnCycle', every: clone(value) });
  effect(`malformed force distance ${JSON.stringify(value)}`, { type: 'forceMove', selector: {}, source: point(), mode: 'pull', distance: clone(value) });
  effect(`malformed dynamic elevation ${JSON.stringify(value)}`, { type: 'setElevation', zone: 'near', value: clone(value) });
  objective(`malformed neutralize threshold ${JSON.stringify(value)}`, { type: 'neutralizeComposite', composite: 'missing', minimumNeutralized: clone(value) });
  run(`malformed repeat cadence ${JSON.stringify(value)}`, l => l.scenario.triggers.push({ id: 'repeat', timing: 'turnStart', condition: { type: 'turnAtLeast', turn: 2 }, effects: [], repeat: { everyRounds: clone(value), maxFirings: clone(value) } }));
}
run('missing resource capacity', l => delete l.players[0].resources.funds.capacity);
run('invalid resource capacity text', l => l.players[0].resources.funds.capacity = 'not-a-number');
run('ECMAScript blank BOM identity', l => l.id = '\ufeff');
run('ECMAScript nonblank next-line identity', l => l.id = '\u0085');
run('ECMAScript blank BOM resource', l => l.players[0].resources = { '\ufeff': { current: 0, capacity: null } });
effect('ECMAScript blank engagement id', { type: 'addEngagementRule', rule: { id: '\ufeff', zone: 'near', mode: 'no-attacks' } });
{
  const level = base(); level.units[0].x = 0.5;
  try { validateLevel(rules, level); throw new Error('Source accepted fractional tile index'); }
  catch (error) {
    if (error.name !== 'DomainInvariantError') throw error;
    failureCases.push({ name: 'fractional tile index must not truncate', level, message: error.message });
  }
}
writeFileSync(output, JSON.stringify({ packs, features, cases, failureCases }));
console.log(`Generated ${cases.length} source level validation cases.`);
