import { readFileSync, writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { beginCast, resolveDueCasts } from '../../../packages/battle-engine/src/casting';
import { announceUnitDeparture } from '../../../packages/battle-engine/src/unit-departure';
import { hashState } from '../../../packages/battle-engine/src/replay';
import { makeLevel, u } from '../../../packages/test-content/src/levels';
import type { GameState, GameEvent, WeaponDef } from '../../../packages/battle-engine/src/types';

const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const engine = createBattleEngine({ content });
const clone = <T>(value: T): T => structuredClone(value);
const base = (): GameState => {
  const state = engine.createState(makeLevel(Array(7).fill('.......'), {
  units: [u(1, 3, 'mage', 1), u(4, 3, 'soldier', 2), u(1, 5, 'mage', 1), u(5, 5, 'soldier', 2)],
  rules: { moraleEnabled: false }, victory: [{ type: 'routEnemies' }],
  }));
  state.actorTurns = 0;
  return state;
};
const declaration = (ability = 'test.unregistered', weapon = 'mage_meteor', patch = {}, caster = 1): any => ({
  type: 'declare', caster, ability, weapon: { ...clone(content.weapons.get(weapon)), ...patch },
  target: { x: 4, y: 3 }, origin: { x: 1, y: 3 },
});
const clock = (at: number) => ({ type: 'clock', at });
const cases: any[] = [];
function run(name: string, operations: any[], prepare: (state: GameState) => void = () => {}) {
  let state = base(); prepare(state); const initial = clone(state); const stages: any[] = [];
  for (const operation of operations) {
    const events: GameEvent[] = []; const eventHashes: string[] = [];
    const emit = (event: GameEvent) => { events.push(event); eventHashes.push(hashState(state)); };
    let error: any = null; let returned: any = null;
    let sameStoredObject: boolean | null = null; let savedState: GameState | null = null;
    try {
      switch (operation.type) {
        case 'declare': {
          const target = clone(operation.target), origin = clone(operation.origin);
          const cast = beginCast(state, { caster: state.units.find(unit => unit.id === operation.caster)!,
            ability: operation.ability, weapon: clone(operation.weapon) as WeaponDef, target, origin }, emit);
          sameStoredObject = cast === state.pendingCasts[state.pendingCasts.length - 1];
          returned = clone(cast);
          target.x = -999; origin.y = -999;
          if (operation.mutateEvent) (events[events.length - 1] as any).at.x = -888;
          break;
        }
        case 'clock': state.actorTurns = operation.at; break;
        case 'resolve': resolveDueCasts(engine.rules, state, emit); break;
        case 'saveLoad': {
          const before = JSON.stringify(state); const save = engine.saveBattle(state, '2026-10-20T00:00:00.000Z');
          if (JSON.stringify(state) !== before) throw new Error('source save changed live state');
          savedState = clone(save.state); const serialized = JSON.stringify(save);
          state = engine.loadBattle(JSON.parse(serialized));
          if (JSON.stringify(save) !== serialized) throw new Error('source load changed document');
          break;
        }
        case 'cooldown': state.units[0].weaponState.mage_meteor.cooldownRemaining = 3; break;
        case 'vacate': state.units[1].x = 6; state.units[1].y = 0; break;
        case 'moveCaster': state.units[0].x = 0; state.units[0].y = 0; break;
        case 'protect':
          state.scenario.zones.sanctuary = [clone(operation.at)];
          state.scenario.engagementRules.push({ id: 'truce', zone: 'sanctuary', mode: 'no-attacks', players: [1] }); break;
        case 'depart': {
          const caster = state.units[0]; state.units = state.units.filter(unit => unit !== caster);
          if (operation.announce) announceUnitDeparture(engine.rules, state, caster, emit);
          break;
        }
        case 'reverse': state.pendingCasts.reverse(); break;
        default: throw new Error(`unknown operation ${operation.type}`);
      }
    } catch (caught) {
      const fault = caught as Error;
      if (!['DomainInvariantError', 'IllegalActionError', 'StoredDocumentError'].includes(fault.name)) throw fault;
      error = { kind: fault.name.replace('Error', ''), message: fault.message };
    }
    stages.push({ operation, returned, sameStoredObject, savedState, error, events, eventHashes, after: clone(state), hash: hashState(state) });
  }
  cases.push({ name, initial, stages });
}
for (const ability of ['attack', 'heal', 'test.unregistered', '', '  not-normalized  ', '自定义😀\ue000'])
  run(`metadata ${JSON.stringify(ability)} declaration and persisted resolution`, [declaration(ability), { type: 'saveLoad' }, clock(2), { type: 'resolve' }]);
for (const delay of [-3, 0, 0.1, 0.49, 0.5, 1.49, 1.5, 2.5])
  run(`duration ${delay}`, [declaration('metadata', 'mage_meteor', { castTurns: delay })]);
run('duplicate cast is illegal and unchanged', [declaration(), declaration('second')]);
run('instant weapon defect precedes duplicate refusal', [declaration(), declaration('second', 'mage_bolt')]);
for (const [name, weapon, patch] of [
  ['unknown weapon', 'mage_meteor', { id: 'test.unknown-weapon' }],
  ['non-equipped weapon', 'soldier_sword', { castTurns: 2 }],
] as const) run(`${name} survives save load then cancels`, [declaration('unknown-ability', weapon, patch), { type: 'saveLoad' }, clock(2), { type: 'resolve' }]);
run('not due stays queued then resolves once', [declaration(), clock(1), { type: 'resolve' }, clock(2), { type: 'resolve' }, { type: 'resolve' }]);
run('cooldown cancellation does not spend weapon cost', [declaration(), { type: 'cooldown' }, { type: 'saveLoad' }, clock(2), { type: 'resolve' }]);
run('orphaned caster cancellation', [declaration(), { type: 'depart', announce: false }, { type: 'saveLoad' }, clock(2), { type: 'resolve' }]);
run('departure cancels immediately once', [declaration(), { type: 'depart', announce: true }, clock(2), { type: 'resolve' }]);
for (const at of [{ x: 1, y: 3 }, { x: 4, y: 3 }])
  run(`frozen engagement geometry ${at.x},${at.y}`, [declaration('heal'), { type: 'moveCaster' }, { type: 'protect', at }, clock(2), { type: 'resolve' }]);
run('frozen origin survives caster relocation', [declaration(), { type: 'moveCaster' }, { type: 'saveLoad' }, clock(2), { type: 'resolve' }]);
run('area resolves on vacated tile', [declaration(), { type: 'vacate' }, clock(2), { type: 'resolve' }]);
run('single target cancels on vacated tile', [declaration('heal', 'mage_bolt', { castTurns: 2 }), { type: 'vacate' }, clock(2), { type: 'resolve' }]);
run('due casts resolve oldest first even when stored reversed', [declaration(), clock(1), declaration('heal', 'mage_meteor', {}, 3), { type: 'reverse' }, { type: 'saveLoad' }, clock(3), { type: 'resolve' }]);
run('declaration does not charge cooldown or resources or finish actor', [declaration()], state => { state.units[0].done = false; state.units[0].weaponState.mage_meteor.cooldownRemaining = 8; });
run('unlearned ability metadata is not a unit ability reference', [declaration('custom.not-learned'), { type: 'saveLoad' }]);
run('event target does not alias stored cast target', [{ ...declaration(), mutateEvent: true }]);
run('declaration stores supplied geometry without legality normalization', [{ ...declaration(), target: { x: 99, y: -4 }, origin: { x: -2, y: 37 } }]);
run('actual unit ability references remain validated', [{ type: 'saveLoad' }], state => { state.units[0].learnedAbilities.push('unknown.unit-ability'); });
run('actual unit weapon references remain validated', [{ type: 'saveLoad' }], state => { state.units[0].weaponState['unknown.unit-weapon'] = { cooldownRemaining: 0, resources: {} }; });
// Guard the oracle scenarios themselves: a fixture-clock offset must not turn
// purported resolution/cancellation vectors into two implementations both waiting.
for (const item of cases) {
  const events = item.stages.flatMap((stage: any) => stage.events);
  if (item.name.startsWith('metadata ') && !events.some((event: any) => event.type === 'attack'))
    throw new Error(`${item.name}: source fixture never executed its combat strike`);
  if (item.name.includes('weapon survives save load then cancels') &&
      !events.some((event: any) => event.type === 'castCancelled' && event.reason === 'weaponUnavailable'))
    throw new Error(`${item.name}: source fixture never reached weapon refusal`);
}
const ordered = cases.find(item => item.name === 'due casts resolve oldest first even when stored reversed');
const resolvedIds = ordered.stages.flatMap((stage: any) => stage.events)
  .filter((event: any) => event.type === 'castResolved').map((event: any) => event.unit);
if (JSON.stringify(resolvedIds) !== '[1,3]') throw new Error('source due-order fixture did not resolve both casts in age order');
const data = JSON.stringify({ packs, cases });
writeFileSync(process.argv[2], process.argv[2].endsWith('.gz') ? gzipSync(data) : data);
console.log(`Source casting oracle: ${cases.length} cases, ${cases.reduce((sum, value) => sum + value.stages.length, 0)} stages`);
