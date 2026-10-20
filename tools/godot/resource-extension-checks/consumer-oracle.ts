import { readFileSync } from 'node:fs';
import { DefaultBattleResources } from '../../../packages/battle-engine/src/resources';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { consumeWeapon, isWeaponReady } from '../../../packages/battle-engine/src/combat';
import { CoreActionHandlers, commandOptions } from '../../../packages/battle-engine/src/actions';
import { ActionExecutionContext } from '../../../packages/battle-engine/src/action-system';
import { executeTactic, tacticOptions } from '../../../packages/battle-engine/src/commanders';
import { hashState } from '../../../packages/battle-engine/src/replay';

type Data = Record<string, any>;
const clone = <T>(value: T): T => structuredClone(value);
export function resourceConsumerOracle(): Data {
  const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
  const original = JSON.parse(readFileSync('godot/tests/oracle/grayflag-initial-raw.json', 'utf8')).state;
  const definition = (family: string, id: string, source = packs): any => source.flatMap((pack: any) => pack[family] ?? []).find((value: any) => value.id === id);
  function actor(id: number, owner: number, x: number): any {
    const type = 'soldier', def = definition('units', type);
    return { ...clone(original.units[0]), id, key: `resource-${id}`, type, owner, x, y: 3, hp: def.maxHp,
      done: false, formation: null, commanderId: null, reaction: def.defaultReaction, statuses: [], learnedAbilities: [],
      resources: clone(def.resources), weaponState: Object.fromEntries(def.weapons.map((weapon: string) =>
        [weapon, { cooldownRemaining: 0, resources: clone(definition('weapons', weapon).resources) }])) };
  }
  function stateFor(config: Data): any {
    const state = clone(original);
    state.map = { width: 7, height: 7, tiles: Array(49).fill('plain'), owners: Array(49).fill(0), captureProgress: Array(49).fill(0),
      elevation: Array(49).fill(0), cliffs: [], directionalCover: [] };
    state.units = [actor(1, 1, 2), actor(2, 2, 3)]; state.commanders = []; state.structures = []; state.composites = [];
    state.pendingCasts = []; state.embarkedUnits = []; state.markers = []; state.nextMarkerId = 1;
    state.rules.moraleEnabled = false; state.rules.counterAttack = false;
    state.scenario.engagementRules = []; state.scenario.zones = {}; state.scenario.variables = {}; state.scenario.eventCounts = {};
    for (const player of state.players) {
      player.resources.charge = { current: config.balance ?? 2, capacity: 8 };
      player.resources.favor = { current: config.favor ?? 2, capacity: 8 };
    }
    if (config.missing) delete state.players[0].resources.charge;
    if (config.tactic) {
      state.commanders = [{ ...clone(original.commanders[0]), id: 'resource-commander', unitId: 1, owner: 1, tactics: ['rally'], usedTactics: [] }];
      state.units[0].commanderId = 'resource-commander';
    }
    return state;
  }
  function resources(config: Data) {
    const result = DefaultBattleResources.clone();
    for (const [id, subjectKind] of [['charge', 'squad'], ['favor', 'player']]) result.adapters.register({
      id, name: id, subjectKind, integer: id === 'charge' && config.integer === true, aiWeight: 0.5,
      account: (subject: any) => {
        const state = subject[subjectKind].resources[id];
        return state ? { read: () => ({ current: state.current, capacity: state.capacity }), write: (current: number) => { state.current = current; } } : null;
      },
    } as any);
    result.subjects.register({ kind: 'squad', subjectFor: (context: any) => ({ kind: 'squad', squad: context.player }),
      ref: (subject: any) => ({ kind: 'squad', id: `squad-${subject.squad.id}`, slot: 'reserve' }),
    } as any);
    return result;
  }
  const consumers: Data[] = [];
  function run(name: string, operation: string, config: Data = {}): void {
    const customPacks = clone(packs);
    const weapon = definition('weapons', 'soldier_sword', customPacks);
    weapon.resourceRequirements = [{ subject: 'squad', resource: 'charge', amount: config.requirement ?? 0.5 }];
    weapon.resourceCosts = (config.costs ?? [0.25, 0.5]).map((amount: number) => ({ subject: 'squad', resource: 'charge', amount }));
    weapon.cooldown = 3;
    const tactic = definition('tactics', 'rally', customPacks);
    Object.assign(tactic, { target: 'self', radius: 0, costs: [{ resource: 'favor', amount: 0.25 }, { resource: 'favor', amount: 0.5 }], effects: [] });
    const content = createContentCatalog(); new ContentPackInstaller(content).install(...customPacks);
    const engine = createBattleEngine({ content, resources: resources(config) });
    let state = stateFor(config); const initial = clone(state);
    const actor = state.units[0];
    const menus = operation === 'command' ? commandOptions(engine.rules, state, actor, { x: actor.x, y: actor.y })
      : operation.startsWith('tactic') ? tacticOptions(engine.rules, state, 'resource-commander') : null;
    let result: any = null, error: any = null, events: any[] = [];
    const action = { kind: 'command', unit: 1, path: [{ x: 2, y: 3 }], command: { ability: 'attack', weapon: 'soldier_sword', target: { x: 3, y: 3 } } } as const;
    try {
      if (operation === 'ready') result = isWeaponReady(engine.rules, actor, content.weapons.get('soldier_sword'), state.players[0]);
      else if (operation === 'consume') consumeWeapon(engine.rules, state, actor, 'soldier_sword', event => events.push(event));
      else if (operation === 'command') {
        const context = new ActionExecutionContext(state, engine.rules);
        CoreActionHandlers.dispatch(context, action as any); events = context.events;
      } else if (operation === 'tactic-execute') executeTactic(engine.rules, state, 'resource-commander', 'rally', undefined, event => events.push(event));
      else if (operation !== 'tactic-options') throw new Error(`unknown consumer operation ${operation}`);
    } catch (caught) {
      const fault = caught as Error;
      if (!['DomainInvariantError', 'IllegalActionError'].includes(fault.name)) throw fault;
      error = { kind: fault.name.replace('Error', ''), message: fault.message };
      if (operation === 'command') {
        state = clone(initial); events = [];
        try { engine.dispatch(state, action as any); throw new Error('Expected source command rejection'); }
        catch (failure) { if ((failure as Error).message !== fault.message) throw failure; }
      }
    }
    consumers.push({ name, operation, config, weapon: clone(weapon), tactic: clone(tactic), initial, menus, result, error, events, after: state, hash: hashState(state) });
  }
  for (const operation of ['ready', 'consume', 'command']) {
    run(`custom squad fractional ${operation}`, operation);
    run(`custom squad unaffordable ${operation}`, operation, { balance: 0.5 });
    run(`custom squad unavailable ${operation}`, operation, { missing: true });
    run(`duplicate integer costs round per spend ${operation}`, operation, { balance: 1, integer: true, costs: [0.4, 0.4] });
    run(`duplicate integer spend can fail after preflight ${operation}`, operation, { balance: 1, integer: true, costs: [0.6, 0.6] });
    run(`separate requirement blocks ${operation}`, operation, { balance: 1, requirement: 2 });
  }
  for (const operation of ['tactic-options', 'tactic-execute']) {
    run(`supplied resource tactic ${operation}`, operation, { tactic: true });
    run(`supplied resource insufficient tactic ${operation}`, operation, { tactic: true, favor: 0.5 });
  }
  return { consumerPacks: packs, consumers };
}
