import { spawnUnit } from '../../../packages/battle-engine/src/state';
import { embarkUnit } from '../../../packages/battle-engine/src/transports';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { hashState } from '../../../packages/battle-engine/src/replay';

export function appendScenarioCases({ base, clone, run, rules, original }) {
  const one = { ids: [1] };
  const statuses = rules.content.statuses.all().map(value => value.id);
  for (const status of statuses) {
    for (const duration of [0, 1, 1.5, 3]) run(`status ${status} duration=${duration}`, base(), [
      { type: 'addStatus', selector: one, status, duration }, { type: 'addStatus', selector: one, status, duration: 2 },
      { type: 'removeStatus', selector: one, status }, { type: 'removeStatus', selector: one, status },
    ]);
  }
  run('missing status references fail when selected', base(), [{ type: 'addStatus', selector: one, status: 'absent', duration: 2 }]);
  run('empty status selection does not look up content', base(), [{ type: 'addStatus', selector: { ids: [] }, status: 'absent', duration: 2 }]);

  for (const current of [0, 85, 100, null, 'absent']) for (const amount of [-999, -0.5, 0, 0.49, 0.5, 999]) {
    const state = base(); state.units[0].resources = current === 'absent' ? {} : { momentum: { current, capacity: 100 } };
    run(`unit resource current=${current} amount=${amount}`, state, [{ type: 'changeUnitResource', selector: one, resource: 'momentum', amount }]);
  }
  for (const resource of ['funds', 'missing']) run(`unit resource wrong adapter ${resource}`, base(), [{ type: 'changeUnitResource', selector: one, resource, amount: 1 }]);

  for (const type of ['activateObjective', 'cancelObjective', 'completeObjective', 'revealObjective']) {
    for (const status of ['active', 'inactive', 'completed', 'cancelled', 'failed']) {
      const state = base(); state.players[0].objectiveStates.goal = { id: 'goal', status, hidden: true };
      run(`${type} from ${status} including repeated event`, state, [{ type, player: 1, id: 'goal' }, { type, player: 1, id: 'goal' }]);
    }
    run(`${type} missing objective`, base(), [{ type, player: 1, id: 'missing' }]);
    run(`${type} missing player`, base(), [{ type, player: 99, id: 'missing' }]);
  }
  for (const player of [1, 2, 99]) for (const team of [1, 2, 99]) run(`team ${player}->${team}`, base(), [{ type: 'setPlayerTeam', player, team }]);
  for (const players of [undefined, [], [1], [1, 2]]) run(`engagement players ${players}`, base(), [
    { type: 'addEngagementRule', rule: { id: 'test', zone: 'near', mode: 'no-hostile', players } },
    { type: 'removeEngagementRule', id: 'test' },
  ]);
  run('duplicate engagement refuses without overwrite', base(), [
    { type: 'addEngagementRule', rule: { id: 'test', zone: 'near', mode: 'no-attacks' } },
    { type: 'addEngagementRule', rule: { id: 'test', zone: 'near', mode: 'no-hostile' } },
  ]);
  run('missing engagement zone', base(), [{ type: 'addEngagementRule', rule: { id: 'test', zone: 'missing', mode: 'no-attacks' } }]);

  for (const overlay of rules.content.terrainOverlays.all()) for (const rounds of [undefined, null, -1, 0, 1.5, 2])
    run(`overlay ${overlay.id} rounds=${rounds}`, base(), [
      { type: 'addOverlay', id: 'environment', overlay: overlay.id, zone: 'near', rounds },
      { type: 'removeOverlay', id: 'missing' }, { type: 'removeOverlay', id: 'environment' },
    ]);
  run('overlay duplicate before unknown definition', base(), [
    { type: 'addOverlay', id: 'same', overlay: 'flooded', zone: 'near' },
    { type: 'addOverlay', id: 'same', overlay: 'missing', zone: 'near' },
  ]);
  run('overlay unknown type', base(), [{ type: 'addOverlay', id: 'new', overlay: 'missing', zone: 'near' }]);
  run('overlay missing zone precedes type check', base(), [{ type: 'addOverlay', id: 'new', overlay: 'missing', zone: 'missing' }]);
  for (const rounds of [0, 1, 2, 3]) run(`overlay round-clock ${rounds}`, base(), [
    { type: 'addOverlay', id: 'forever', overlay: 'flooded', zone: 'near' },
    { type: 'addOverlay', id: 'short', overlay: 'fire_field', zone: 'near', rounds: 1 },
    { type: 'addOverlay', id: 'long', overlay: 'vacuum', zone: 'near', rounds: 2 },
    { type: 'addOverlay', id: 'fraction', overlay: 'low_gravity', zone: 'near', rounds: 0.5 },
  ], null, null, null, false, rounds);

  for (const terrain of ['plain', 'forest', 'water', 'missing']) run(`terrain ${terrain}`, base(), [{ type: 'replaceTerrain', zone: 'near', terrain }]);
  for (const type of ['setElevation', 'addElevation']) for (const value of [-1.5, -0.5, 0, 0.5, 1.5]) {
    const state = base(); state.map.elevation[8] = 2; state.scenario.zones.near.push({ x: 1, y: 1 });
    run(`${type} rounding and duplicate zone ${value}`, state, [{ type, zone: 'near', value, amount: value }]);
  }
  for (const type of ['replaceTerrain', 'setElevation', 'addElevation']) {
    const state = base(); state.scenario.zones.bad = [{ x: 1, y: 1 }, { x: -1, y: 0 }];
    run(`${type} partial before bad zone cell`, state, [{ type, zone: 'bad', terrain: 'forest', value: 3, amount: 2 }]);
  }
  const edge = { from: { x: 1, y: 1 }, to: { x: 2, y: 1 } };
  for (const grid of ['square4', 'square8', 'hex']) {
    const state = base(); state.rules.grid = grid;
    run(`cliffs use orthogonal storage edges ${grid}`, state, [
      { type: 'setCliffs', edges: [edge, { from: edge.to, to: edge.from }], blocked: true },
      { type: 'setCliffs', edges: [{ from: edge.to, to: edge.from }, edge], blocked: false },
    ]);
  }
  for (const invalid of [{ from: { x: 0, y: 0 }, to: { x: 1, y: 1 } }, { from: { x: 0, y: 0 }, to: { x: -1, y: 0 } }])
    run(`invalid cliff ${JSON.stringify(invalid)}`, base(), [{ type: 'setCliffs', edges: [edge, invalid], blocked: true }]);
  {
    const state = base(); state.map.directionalCover = [{ at: { x: 6, y: 6 }, sides: {} }];
    run('cover same value still emits and empty entries prune globally', state, [{ type: 'setDirectionalCover', covers: [
      { at: { x: 1, y: 1 }, sides: { north: 'full', east: 'half' } },
      { at: { x: 1, y: 1 }, sides: { north: 'full', east: 'half' } }, { at: { x: 1, y: 1 }, sides: {} },
    ] }]);
    run('cover partial then out of bounds', base(), [{ type: 'setDirectionalCover', covers: [
      { at: { x: 1, y: 1 }, sides: { north: 'half' } }, { at: { x: 7, y: 0 }, sides: {} },
    ] }]);
  }

  for (const type of rules.content.structures.all()) for (const hp of [0, 50, type.maxHp, type.maxHp + 10]) for (const amount of [-1, 0.1, 1.5, 999]) {
    const state = base(); state.structures = [{ id: 'repair', type: type.id, hp, disabled: true, x: 5, y: 5, owner: 1, statuses: [] }];
    run(`repair ${type.id} hp=${hp} amount=${amount}`, state, [{ type: 'repairStructure', id: 'repair', amount }]);
  }
  run('repair missing structure', base(), [{ type: 'repairStructure', id: 'missing', amount: 2 }]);

  for (const grid of ['square4', 'square8', 'hex']) for (const mode of ['push', 'pull']) for (const distance of [-1, 0, 0.5, 2.5]) {
    const state = base(); state.rules.grid = grid; state.units = [state.units[0]]; state.units[0].x = 3; state.units[0].y = 3;
    state.units[0].capture = 12; state.map.captureProgress[24] = 12;
    run(`force ${grid} ${mode} ${distance}`, state, [{ type: 'forceMove', selector: one, source: { x: 2, y: 2 }, mode, distance, collisionDamage: 10 }]);
  }
  for (const amount of [0, 20, 999]) {
    const state = base(); state.units[0].capture = 30; state.map.captureProgress[8] = 30;
    run(`force collision damage ${amount}`, state, [{ type: 'forceMove', selector: one, source: { x: 0, y: 1 }, mode: 'push', distance: 2, collisionDamage: amount }]);
  }
  {
    const state = base(); state.scenario.zones.arrival = [{ x: 5, y: 5 }, { x: 0, y: 4 }, { x: 4, y: 4 }, { x: 3, y: 1 }];
    state.map.tiles[28] = 'water'; state.map.cliffs = [edge]; state.map.elevation[32] = 9;
    run('teleport reading order dynamic occupancy terrain and cliff bypass', state, [{ type: 'teleportUnits', selector: {}, zone: 'arrival' }]);
    run('teleport full zone silently keeps everyone', base(), [{ type: 'teleportUnits', selector: {}, zone: 'near' }]);
    run('teleport missing zone empty selector is no-op', base(), [{ type: 'teleportUnits', selector: { ids: [] }, zone: 'missing' }]);
    run('teleport missing zone selected unit throws', base(), [{ type: 'teleportUnits', selector: one, zone: 'missing' }]);
  }
  function markers() {
    const state = base();
    state.markers = state.units.map((unit, index) => ({ id: index + 1, kind: ['corpse', 'routed', 'withdrawn'][index], owner: index === 1 ? 2 : 1,
      at: { x: index + 1, y: 3 }, fallenUnit: { ...clone(unit), hp: index === 0 ? 0 : 30, statuses: [{ id: 'poisoned', remaining: 2, stacks: 1 }],
        morale: { ...unit.morale, current: 0 }, reactionUsedRound: state.turn } }));
    state.units = []; state.nextMarkerId = 4;
    return state;
  }
  for (const hpPercent of [undefined, -1, 0, 0.25, 1, 2]) run(`revive clamped percentage ${hpPercent}`, markers(), [{ type: 'reviveMarkers', selector: {}, hpPercent, owner: 2 }]);
  for (const selector of [{ ids: [2, 3] }, { kind: 'corpse' }, { owner: 1 }, { ids: [] }, { zone: 'near' }, { zone: 'missing' }]) {
    run(`remove markers ${JSON.stringify(selector)}`, markers(), [{ type: 'removeMarkers', selector }]);
    run(`markerCount ${JSON.stringify(selector)}`, markers(), [], { type: 'markerCount', selector, op: 'gte', value: 1 });
  }
  {
    const state = markers(); state.markers.unshift({ id: 99, kind: 'corpse', owner: 1, at: { x: 0, y: 0 } });
    state.units.push({ ...clone(state.markers[1].fallenUnit), hp: 20, x: 1, y: 3 });
    run('revive no fallen snapshot and occupied duplicate identity skip', state, [{ type: 'reviveMarkers', selector: {} }]);
    const restore = markers(); restore.scenario.zones.rally = [{ x: 6, y: 5 }, { x: 1, y: 4 }];
    run('restore stops at full reading-order zone and resets snapshots', restore, [{ type: 'restoreWithdrawnUnits', selector: {}, zone: 'rally', owner: 2 }]);
    const livingRestore = clone(restore); livingRestore.markers[0].fallenUnit.hp = 25;
    run('restore living snapshots in reading-order zone and reset state', livingRestore, [{ type: 'restoreWithdrawnUnits', selector: {}, zone: 'rally', owner: 2 }]);
    const blocked = markers(); blocked.map.tiles[8] = 'water'; blocked.map.tiles[9] = 'water';
    run('restore first unlandable marker stops later markers', blocked, [{ type: 'restoreWithdrawnUnits', selector: {}, zone: 'near' }]);
    run('restore missing zone ignored with no markers', base(), [{ type: 'restoreWithdrawnUnits', selector: {}, zone: 'missing' }]);
  }
  function commanderState() {
    const state = base(); state.rules.moraleEnabled = true; const commander = clone(original.commanders[0]); commander.unitId = 1;
    state.commanders = [commander]; state.units[1].commanderId = commander.id; state.units[1].morale.current = 1;
    state.pendingCasts = [{ caster: 2, owner: 1, ability: 'attack', weapon: 'c01.border-blade', target: { x: 6, y: 6 }, origin: { x: 2, y: 1 }, declaredAt: 0, resolveAt: 2 }];
    return state;
  }
  run('withdraw captured selection continues after commander routes follower', commanderState(), [{ type: 'withdrawUnits', selector: {} }]);
  run('surrender standing selection skips commander-routed follower', commanderState(), [{ type: 'surrenderUnits', selector: {}, to: 2 }]);
  for (const type of ['withdrawUnits', 'surrenderUnits']) {
    const state = base(); state.units = []; const carrier = spawnUnit(rules, state, 'c01.supply-wagon', 1, { x: 3, y: 3 });
    const passenger = spawnUnit(rules, state, 'c01.swordsman', 1, { x: 2, y: 3 }); embarkUnit(rules, state, passenger.id, carrier.id, () => {});
    run(`${type} canonical passenger departure`, state, [{ type, selector: {}, leaveCorpse: true, to: 2 }]);
  }

  for (const percent of [-1, 0, 0.5, 50, 99, 100, 101]) for (const stream of [undefined, 'script.骰子💥', '\ud800']) {
    run(`chance ${percent} stream=${JSON.stringify(stream)}`, base(), [], Array.from({ length: 8 }, () => ({ type: 'chance', percent, stream })));
  }
  for (const type of ['all', 'any']) for (const first of [0, 100]) run(`chance ${type} short circuit ${first}`, base(), [], Array.from({ length: 5 }, () => ({
    type, conditions: [{ type: 'chance', percent: first, stream: 'gate' }, { type: 'not', condition: { type: 'chance', percent: 50, stream: 'nested' } }],
  })));
  run('chance default and same explicit stream interleave', base(), [], [
    { type: 'chance', percent: 50 }, { type: 'chance', percent: 50, stream: 'scenario.chance' },
    { type: 'chance', percent: 0, stream: 'unused' }, { type: 'chance', percent: 100, stream: 'unused' },
    { type: 'not', condition: { type: 'chance', percent: 50 } },
  ]);

  const integrationCases = [];
  const engine = createBattleEngine({ content: rules.content });
  function playing() {
    const state = base();
    state.scenario.triggers = []; state.scenario.firedTriggerIds = []; state.scenario.triggerRuntime = {}; state.scenario.eventCounts = {};
    for (const player of state.players) {
      player.objectives = [{ id: 'route', type: 'routEnemies', label: 'Defeat enemies' }];
      player.objectiveStates = { route: { id: 'route', status: 'active', hidden: false } };
    }
    spawnUnit(rules, state, 'c01.swordsman', 2, { x: 6, y: 6 });
    return state;
  }
  function dispatch(name, state, actions, success = true) {
    const initial = clone(state), steps = [];
    state = engine.loadBattle(JSON.parse(JSON.stringify(engine.saveBattle(state, '2026-10-20T00:00:00.000Z'))));
    for (const action of actions) {
      const before = clone(state); let events = [], error = null;
      try { events = engine.dispatch(state, action); }
      catch (caught) {
        if (!['DomainInvariantError', 'IllegalActionError'].includes(caught.name)) throw caught;
        error = { kind: caught.name.replace('Error', ''), message: caught.message };
      }
      if (success && error) throw new Error(`Integration fixture ${name} must succeed, got ${error.message}`);
      if (!success && !error) throw new Error(`Integration fixture ${name} must fail to test rollback`);
      const hash = hashState(state);
      steps.push({ action, before, events, error, after: clone(state), hash });
      state = engine.loadBattle(JSON.parse(JSON.stringify(engine.saveBattle(state, '2026-10-20T00:00:00.000Z'))));
      if (hashState(state) !== hash) throw new Error(`Source saved continuation changed ${name}`);
    }
    integrationCases.push({ name, initial, steps });
  }
  const wait = { kind: 'command', unit: 1, path: [{ x: 1, y: 1 }], command: { ability: 'wait' } };
  const always = { type: 'turnAtLeast', turn: 1 };
  {
    const state = playing(); state.scenario.triggers = [{ id: 'layers', timing: 'afterAction', condition: always, effects: [
      { type: 'addOverlay', id: 'weather', overlay: 'flooded', zone: 'near', rounds: 1 },
      { type: 'addStatus', selector: one, status: 'inspired', duration: 2 },
      { type: 'changeUnitResource', selector: one, resource: 'momentum', amount: 3 },
      { type: 'setElevation', zone: 'near', value: 2 },
      { type: 'setDirectionalCover', covers: [{ at: { x: 1, y: 1 }, sides: { north: 'half' } }] },
    ] }];
    dispatch('full engine scenario layers and overlay expiry across round', state, [wait, { kind: 'endTurn' }, { kind: 'endTurn' }]);
  }
  {
    const state = playing(); state.scenario.triggers = [
      { id: 'react-to-marker', timing: 'afterAction', condition: { type: 'markerCount', selector: { kind: 'withdrawn' }, op: 'gte', value: 1 },
        effects: [{ type: 'restoreWithdrawnUnits', selector: { kind: 'withdrawn' }, zone: 'near' }] },
      { id: 'depart', timing: 'afterAction', condition: always, effects: [{ type: 'withdrawUnits', selector: { ids: [2] } }] },
    ];
    dispatch('full engine chained marker condition and return second pass', state, [wait]);
  }
  {
    const state = playing(); state.scenario.triggers = [{ id: 'chance-repeat', timing: 'turnEnd', repeat: { everyRounds: 1, maxFirings: 3 },
      condition: { type: 'chance', percent: 50, stream: 'trigger.骰子💥' }, effects: [{ type: 'addVariable', key: 'rolls', amount: 1 }, { type: 'emitSignal', signal: 'rolled' }] }];
    dispatch('full engine repeated random trigger ledger', state, Array.from({ length: 8 }, () => ({ kind: 'endTurn' })));
  }
  for (const stream of ['\ud800', '\udc00']) {
    const state = playing(); state.scenario.triggers = [{ id: `chance-${stream}`, timing: 'turnEnd', repeat: { everyRounds: 1, maxFirings: 3 },
      condition: { type: 'chance', percent: 50, stream }, effects: [
        { type: 'setVariable', key: '\ud800', value: '\udc00' },
        { type: 'setVariable', key: '\udc00', value: '\ud800' },
        { type: 'setVariable', key: '\ufffd', value: 'distinct replacement character' },
        { type: 'addVariable', key: `rolls-${stream}`, amount: 1 },
        { type: 'emitSignal', signal: `${stream}.signal` },
      ] }];
    dispatch(`full engine exact UTF-16 saved continuation ${JSON.stringify(stream)}`, state, Array.from({ length: 8 }, () => ({ kind: 'endTurn' })));
  }
  {
    const state = playing(); state.scenario.overlays = [{ id: 'duplicate', type: 'flooded', cells: clone(state.scenario.zones.near), remainingRounds: null }];
    state.scenario.triggers = [{ id: 'transaction', timing: 'afterAction', condition: { type: 'chance', percent: 99, stream: 'rollback-stream' }, effects: [
      { type: 'setVariable', key: 'must-rollback', value: true }, { type: 'addStatus', selector: one, status: 'shaken', duration: 2 },
      { type: 'addOverlay', id: 'duplicate', overlay: 'flooded', zone: 'near' },
    ] }];
    dispatch('full engine rollback includes RNG ledger unit status and events', state, [wait], false);
  }
  return integrationCases;
}
