import { readFileSync, writeFileSync } from 'node:fs';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { forecastCombatPlan, executeCombatPlan } from '../../../packages/battle-engine/src/combat-plan';
import { CombatModifierPipeline, CombatModifierProviderRegistry, CombatModifierProviders } from '../../../packages/battle-engine/src/combat-modifiers';
import { WeaponHitEffectHandlers, WeaponHitEffectHandlerRegistry } from '../../../packages/battle-engine/src/hit-effects';
import { Battlefield } from '../../../packages/battle-engine/src/domain/battlefield';
import { boardOf } from '../../../packages/battle-engine/src/domain/board';
import { addStatus } from '../../../packages/battle-engine/src/statuses';
import { resolveDamage } from '../../../packages/battle-engine/src/damage';
import { hashState } from '../../../packages/battle-engine/src/replay';

const clone = <T>(value: T): T => structuredClone(value);
const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const original = JSON.parse(readFileSync('godot/tests/oracle/grayflag-initial-raw.json', 'utf8')).state;
const definition = (family: string, id: string, source = packs) => source.flatMap((pack: any) => pack[family] ?? []).find((entry: any) => entry.id === id);
function unit(id: number, owner: number, x: number, y: number, patch: any = {}) {
  const type = 'c01.swordsman', def = definition('units', type);
  return { ...clone(original.units[1]), id, key: `test-${id}`, owner, x, y, type,
    hp: def.maxHp, commanderId: null, formation: null, reaction: def.defaultReaction,
    resources: { ...clone(def.resources), momentum: { current: 30, capacity: 100 } }, statuses: [],
    weaponState: Object.fromEntries(def.weapons.map((id: string) => [id, { cooldownRemaining: 0, resources: clone(definition('weapons', id).resources) }])), ...patch };
}
function base() {
  const state = clone(original);
  state.map = { width: 7, height: 7, tiles: Array(49).fill('plain'), owners: Array(49).fill(0), captureProgress: Array(49).fill(0), elevation: Array(49).fill(0), cliffs: [], directionalCover: [] };
  state.units = [unit(1, 1, 2, 3), unit(2, 2, 3, 3)];
  state.structures = []; state.composites = []; state.commanders = []; state.pendingCasts = []; state.embarkedUnits = []; state.markers = []; state.nextMarkerId = 1;
  state.scenario.engagementRules = []; state.scenario.terrainOverlays = []; state.scenario.zones = {};
  state.rules.moraleEnabled = false; state.rules.counterAttack = false;
  return state;
}
const numeric = (v: any) => typeof v === 'string' ? Number(v) : v;
const provider = (spec: any) => ({ id: spec.id, priority: numeric(spec.priority), provide: (context: any) => spec.context ? [{
  id: spec.id, label: 'shared projection', source: 'plugin.context', stage: 'power', operation: 'add',
  value: context.board.grid.distance(context.attackerAt, context.defenderAt) + context.battlefield.cell(context.defenderAt).elevation
    + context.content.units.get(context.attacker.type).maxHp / 100,
}] : clone(spec.modifiers) });
const mod = (id: string, stage: string, operation: string, value: number) => ({ id, label: id, source: 'plugin.custom', stage, operation, value });
function pipeline(config: any) {
  const registry = config.defaults ? CombatModifierProviders.clone() : new CombatModifierProviderRegistry();
  for (const spec of config.providers ?? []) config.replace ? registry.replace(provider(spec) as any) : registry.register(provider(spec) as any);
  return new CombatModifierPipeline(registry, config.cap ?? 0.6);
}
const catalog = createContentCatalog(); new ContentPackInstaller(catalog).install(...packs);
const defaultRules = createBattleEngine({ content: catalog }).rules;
const state = base();
const context: any = { rules: defaultRules, content: catalog, state, attacker: state.units[0], attackerAt: state.units[0], defender: state.units[1], defenderAt: state.units[1], weapon: catalog.weapons.get('c01.border-blade'), battlefield: new Battlefield(state, catalog), board: boardOf(defaultRules, state) };
const pipelineCases: any[] = [];
function evaluate(name: string, config: any, power = 20) { pipelineCases.push({ name, config, power, result: pipeline(config).evaluate(power, context) }); }
for (const cap of [-0.1, 0, 0.2, 0.6, 1, 1.5]) for (const power of [-5, 0, 0.5, 11, 33.5]) {
  evaluate(`affine ordered cap ${cap} base ${power}`, { cap, providers: [
    { id: 'last', priority: 8, modifiers: [mod('final.add', 'final', 'add', 2.5), mod('final.double', 'final', 'multiply', 2), mod('final.tail', 'final', 'add', -1)] },
    { id: 'mitigation', priority: 2, modifiers: [mod('armor', 'mitigation', 'add', 0.75), mod('armor.mult', 'mitigation', 'multiply', 2), mod('armor.offset', 'mitigation', 'add', -0.1)] },
    { id: 'power', priority: 1, modifiers: [mod('power.add', 'power', 'add', 3), mod('power.mult', 'power', 'multiply', 1.5)] },
  ] }, power);
}
for (const priority of [0, -1.25, 'NaN', 'Infinity', '-Infinity']) evaluate(`UTF16 ties ${priority}`, { providers: ['\uE000', '𐀀', 'a', 'A'].map((id, i) => ({ id, priority, modifiers: [mod(id, 'power', i % 2 ? 'multiply' : 'add', i + 1)] })) });
evaluate('empty minimum', { providers: [] }, 0.49);
evaluate('shared projections', { providers: [{ id: 'context', priority: 1, context: true }] });
evaluate('default providers', { defaults: true });
const registryChecks: any = {};
const a = provider({ id: 'a', priority: 10, modifiers: [] });
const registry = new CombatModifierProviderRegistry().register(a).register(provider({ id: 'b', priority: 2, modifiers: [] }));
registryChecks.insertion = registry.keys(); registryChecks.ordered = registry.ordered().map(x => x.id);
const oldOrder = registry.ordered(); const copy = registry.seal().clone();
copy.replace(provider({ id: 'a', priority: -1, modifiers: [] })); copy.replace(provider({ id: '', priority: 0, modifiers: [] }));
registryChecks.oldSnapshot = oldOrder.map(x => x.id); registryChecks.original = registry.ordered().map(x => x.id); registryChecks.copy = copy.ordered().map(x => x.id);
registryChecks.shallow = registry.clone().get('a') === a; registryChecks.cloneMutable = !registry.clone().isSealed;
const fault = (action: () => void) => { try { action(); return null; } catch (e) { return (e as Error).message; } };
registryChecks.duplicate = fault(() => copy.register(copy.get('a'))); registryChecks.sealed = fault(() => registry.replace(a)); registryChecks.unknown = fault(() => registry.get('absent')); registryChecks.pipelineSealed = fault(() => new CombatModifierPipeline(copy).seal().register(a));
const hits = WeaponHitEffectHandlers.clone(); const hitSnapshot = hits.keys(); const hitClone = hits.seal().clone();
hitClone.replace({ kind: 'custom', apply: () => {}, describe: () => 'custom' } as any);
registryChecks.hitKeys = hitSnapshot; registryChecks.hitClone = hitClone.keys(); registryChecks.hitShallow = hits.get('addStatus') === hitClone.get('addStatus'); registryChecks.hitCloneMutable = !hitClone.isSealed;
registryChecks.hitSealed = fault(() => hits.register(hitClone.get('custom'))); registryChecks.hitUnknown = fault(() => hitClone.describe({ type: 'absent' } as any));
function hitRegistry(config: any) {
  const result = WeaponHitEffectHandlers.clone(); let lastContext: any = null;
  result.register({ kind: 'observe', apply: (context: any, effect: any) => {
    context.target.capture += effect.amount; context.emit({ type: 'extensionEffect', unit: context.target.id, hp: context.target.hp, capture: context.target.capture, sameContext: lastContext === context, maxHp: context.content.units.get(context.target.type).maxHp, distance: context.rules.grids.get(context.state.rules.grid).distance(context.attacker, context.target) }); lastContext = context;
  }, describe: (effect: any) => `Observe ${effect.amount}` } as any);
  result.register({ kind: 'pulse', apply: ({ rules, state, target, emit }: any, effect: any) => resolveDamage(rules, state, { unit: target.id, amount: effect.amount, nonlethal: effect.nonlethal ?? false, report: blow => ({ type: 'extensionDamage', unit: target.id, amount: blow.amount, hpAfter: blow.hpAfter, killed: blow.killed }) as any }, emit), describe: (effect: any) => `Pulse ${effect.amount}` } as any);
  if (config.replaceHit) result.replace({ kind: 'addStatus', apply: ({ content, target, attacker, emit }: any, effect: any) => addStatus(content, target, { id: 'armor_down', remaining: effect.duration + 1, sourceUnitId: attacker.id }, emit), describe: () => 'Replacement status' } as any);
  return result;
}
const consumers: any[] = [];
function run(name: string, config: any = {}) {
  const customPacks = clone(packs), weapon = definition('weapons', 'c01.border-blade', customPacks);
  const effects = config.effects ?? []; weapon.hitEffects = config.direct ? [] : effects; if (config.area) weapon.area = 'cross1'; if (config.support) weapon.maxRange = 2;
  const content = createContentCatalog(); new ContentPackInstaller(content).install(...customPacks);
  const combatModifiers = pipeline(config.pipeline ?? { defaults: true }), hitEffects = hitRegistry(config);
  const rules = createBattleEngine({ content, combatModifiers, hitEffects }).rules;
  const state = base(); if (config.hp !== undefined) state.units[1].hp = config.hp;
  if (config.counter) state.rules.counterAttack = true;
  if (config.rout) { state.rules.moraleEnabled = true; state.rules.moraleDamageFactor = 40; }
  if (config.support) state.units.push(unit(3, 1, 2, 2, { reaction: 'support' }));
  if (config.area) state.units.push(unit(3, 2, 3, 2), unit(4, 2, 4, 3));
  if (config.wall) state.units.push(unit(3, 2, 4, 3));
  if (config.structure) { state.units.splice(1, 1); state.structures = [{ id: 'gate', type: 'gate', owner: 2, x: 3, y: 3, hp: 140, disabled: false }]; }
  const initial = clone(state), events: any[] = [], hashes: string[] = [];
  const emit = (event: any) => { events.push(event); hashes.push(hashState(state)); };
  let result: any = null, error: any = null;
  try {
    if (config.direct) hitEffects.apply(rules, state, state.units[0], state.units[1], effects, emit);
    else { const plan = forecastCombatPlan(rules, state, state.units[0], { x: 3, y: 3 }, { weapon: weapon.id }); result = clone(plan); if (config.staleHp) state.units[1].hp = 1; executeCombatPlan(rules, state, plan, emit); }
  } catch (caught) { const e = caught as Error; if (!['DomainInvariantError', 'IllegalActionError'].includes(e.name)) throw e; error = { kind: e.name.replace('Error', ''), message: e.message }; }
  const descriptions = effects.map((effect: any) => { try { return hitEffects.describe(effect); } catch { return null; } });
  consumers.push({ name, config, initial, weapon, result, error, descriptions, events, hashes, after: state });
}
run('default actual plan');
const doubled = { providers: [{ id: 'mode.double', priority: 1, modifiers: [mod('mode.double','power','multiply',2)] }] };
run('replacement pipeline executes', { pipeline: doubled });
run('structure intentionally bypasses injected pipeline', { pipeline: doubled, structure: true });
run('context provider reaches execution', { pipeline: { providers: [{ id: 'context', priority: 1, context: true }] } });
run('replace default provider', { pipeline: { defaults: true, replace: true, providers: [{ id: 'core.rank', priority: 350, modifiers: [mod('rank.custom','power','add',15)] }] } });
run('append custom final', { pipeline: { defaults: true, providers: [{ id: 'final.custom', priority: 1000, modifiers: [mod('final.custom','final','add',9)] }] } });
const observed = [{ type: 'observe', amount: 1 }, { type: 'observe', amount: 2 }];
for (const config of [{}, { direct: true }, { area: true }, { counter: true }, { support: true }, { hp: 1 }]) run(`custom observer ${JSON.stringify(config)}`, { ...config, effects: observed });
run('morale departure suppresses injected effect', { rout: true, effects: observed });
run('stale surviving forecast now dies before effect', { staleHp: true, effects: observed });
run('counter injected secondary lethal settlement', { counter: true, effects: [{ type: 'pulse', amount: 20 }] });
run('custom secondary damage canonical settlement', { effects: [{ type: 'pulse', amount: 7.5 }] });
run('custom secondary damage kills', { effects: [{ type: 'pulse', amount: 1000 }] });
run('custom secondary nonlethal damage', { effects: [{ type: 'pulse', amount: 1000, nonlethal: true }] });
run('default add remove status', { effects: [{ type: 'addStatus', status: 'armor_down', duration: 2 }, { type: 'removeStatus', status: 'armor_down' }] });
run('replacement addStatus used', { replaceHit: true, effects: [{ type: 'addStatus', status: 'poisoned', duration: 2 }] });
run('default push', { effects: [{ type: 'forcedMove', mode: 'push', distance: 1, collisionDamage: 0 }] });
run('default pull', { effects: [{ type: 'forcedMove', mode: 'pull', distance: 1, collisionDamage: 5 }] });
run('default collision lethal suppresses momentum', { wall: true, effects: [{ type: 'forcedMove', mode: 'push', distance: 2, collisionDamage: 1000 }] });
run('effects continue with same context after death', { direct: true, effects: [{ type: 'pulse', amount: 1000 }, ...observed] });
run('unknown handler rejects after earlier effect', { direct: true, effects: [observed[0], { type: 'absent' }] });
writeFileSync(process.argv[2], JSON.stringify({ packs, initial: base(), pipelineCases, registryChecks, consumers }));
console.log(`Source oracle: ${pipelineCases.length} pipeline, ${Object.keys(registryChecks).length} registry, ${consumers.length} canonical consumer cases`);
