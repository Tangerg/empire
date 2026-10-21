import { readFileSync, writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { createBattleEngine, DEFAULT_RULE_PLUGINS, contentPluginFor } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { ThresholdRankProgressionPolicy, awardRankProgress } from '../../../packages/battle-engine/src/progression';
import { SideTurnOrder } from '../../../packages/battle-engine/src/turn-order';
import { BattleSaveReader } from '../../../packages/battle-engine/src/battle-save';
import { resolveDamage } from '../../../packages/battle-engine/src/damage';
import { DomainInvariantError, StoredDocumentError } from '../../../packages/battle-engine/src/domain/errors';
import { BattleRecorder, replayBattle } from '../../../packages/battle-engine/src/replay';
import type { EnginePlugin } from '../../../packages/battle-engine/src/kernel';

const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const baseLevel = JSON.parse(readFileSync('godot/tests/oracle/grayflag-0.58.json', 'utf8')).level;
const clone = <T>(value: T): T => structuredClone(value);
function error(call: () => unknown): any {
  try { call(); return null; } catch (value) { const error = value as Error; return { name: error.name.replace('Error','Exception'), message: error.message }; }
}
function run(failStatus: boolean, failDeparture: boolean) {
  const catalog = createContentCatalog(); new ContentPackInstaller(catalog).install(...packs);
  const trace: string[] = [];
  const installed: Record<string, any> = {};
  const same = (actual: unknown) => { if (actual !== catalog) throw new Error('root catalog identity'); };
  const reader = new class extends BattleSaveReader {
    override load(raw: any, environment: any) {
      same(environment.rules.content); trace.push('reader');
      const result = super.load(raw,environment);
      if(result.state.scenario.variables.failRead) throw new StoredDocumentError('root reader refused');
      return result;
    }
  }();
  const plugin: EnginePlugin = { id:'test.root-all-capabilities', version:3,
    overrides:['statusBehaviors','progression','turnOrders','reactions','unitDepartures','referenceChecks','saves','scenarioConditions','scenarioEffects','objectives','actionHandlers'],
    requiresCapabilities:['content','statusBehaviors','turnOrders','reactions','unitDepartures','referenceChecks','scenarioConditions','scenarioEffects','objectives','actionHandlers'],
    install(context) {
      const status = context.require('statusBehaviors').clone().register({ id:'inspired', onOwnerTurnStart(context) {
        same(context.content); context.state.scenario.variables.statusCalls = Number(context.state.scenario.variables.statusCalls ?? 0) + 1;
        context.emit({type:'scenarioSignal',signal:'root-status'});
        if(context.state.scenario.variables.failStatus) throw new DomainInvariantError('root status failed');
      }}); context.replace('statusBehaviors',installed.statusBehaviors=status);
      context.replace('progression',installed.progression=new ThresholdRankProgressionPolicy(1,3));
      const turns = context.require('turnOrders').clone().register({ ...SideTurnOrder, id:'root-turn',name:'root turns',
        initialState(state,content) { same(content); state.scenario.variables.turnInitial=1; return {policy:'root-turn',activeUnit:null,data:{}}; },
        begin(state,context) { same(context.content); state.scenario.variables.turnBegin=1; return SideTurnOrder.begin(state,context); },
        advance(state,context) { same(context.content); state.scenario.variables.turnAdvance=Number(state.scenario.variables.turnAdvance??0)+1; return SideTurnOrder.advance(state,context); },
      }); context.replace('turnOrders',installed.turnOrders=turns);
      const reactions=context.require('reactions').clone().register({id:'root-guard',name:'root guard',intercepts:false,incomingMultiplier:0.25,retaliates:false,conservesResources:false,hint:'root'});
      context.replace('reactions',installed.reactions=reactions);
      const departures=context.require('unitDepartures').clone().register({id:'root.departure',handle(context){
        same(context.rules.content); context.state.scenario.variables.departed=context.unit.id;
        context.emit({type:'scenarioSignal',signal:'root-departure'});
        if(context.state.scenario.variables.failDeparture) throw new DomainInvariantError('root departure failed');
      }}); context.replace('unitDepartures',installed.unitDepartures=departures);
      const references=context.require('referenceChecks').clone().register({id:'root-reference',subject:'root marker',known:()=>['allowed'],
        inContent(content) {same(content);trace.push('content');return [];},
        inLevel(level,rules) {same(rules.content);trace.push('level');return [{by:'root-level',name:String(level.scenario?.variables?.rootCheck??'allowed')}];},
        inState(state,rules) {same(rules.content);trace.push('state');return [{by:'root-state',name:String(state.scenario.variables.rootCheck??'allowed')}];},
      });context.replace('referenceChecks',installed.referenceChecks=references);
      context.replace('saves',installed.saves=reader);
      const conditions=context.require('scenarioConditions').clone().register({kind:'rootCondition',evaluate(context:any){same(context.content);return true;}} as any);
      context.replace('scenarioConditions',installed.scenarioConditions=conditions);
      const effects=context.require('scenarioEffects').clone().register({kind:'rootEffect',apply(context:any){same(context.content);context.state.scenario.variables.effectCalls=Number(context.state.scenario.variables.effectCalls??0)+1;context.emit({type:'scenarioSignal',signal:'root-effect'});}} as any);
      context.replace('scenarioEffects',installed.scenarioEffects=effects);
      const objectives=context.require('objectives').clone().register({kind:'rootHold',label:'root hold',outcome(context:any){same(context.content);return 'pending';},progress:()=> 'root progress'} as any);
      context.replace('objectives',installed.objectives=objectives);
      const actions=context.require('actionHandlers').clone().register({kind:'rootPrepare',execute(context:any){
        const unit=context.state.units.find((u:any)=>u.id===2); unit.statuses=[{id:'inspired',remaining:2,stacks:1}];unit.reaction='root-guard';
        awardRankProgress(context.rules,unit,4,context.emit);
        const target=context.state.units.find((u:any)=>u.owner===2);
        resolveDamage(context.rules,context.state,{unit:target.id,amount:999,report:blow=>({type:'statusTick',unit:target.id,status:'inspired',amount:blow.amount,hpAfter:blow.hpAfter})},context.emit);
      }} as any);context.replace('actionHandlers',actions);
    }};
  const level=clone(baseLevel);level.rules.turnOrder='root-turn';
  level.scenario={variables:{rootCheck:'allowed',failStatus,failDeparture},triggers:[{id:'root-trigger',timing:'afterAction',condition:{type:'rootCondition'},effects:[{type:'rootEffect'}],once:false}]};
  for(const player of level.players)player.objectives=[{id:'root-objective',type:'rootHold'}];
  const engine=createBattleEngine({content:catalog,plugins:[plugin]});
  const identity=Object.fromEntries(Object.entries(installed).map(([key,value])=>[key,(engine.rules as any)[key]===value]));
  const state=engine.createState(level);const initial=clone(state);
  const forecastState=clone(initial);const defender=forecastState.units.find(unit=>unit.id===2)!;defender.reaction='root-guard';const attacker=forecastState.units.find(unit=>unit.owner===2)!;
  const forecast=engine.forecast(forecastState,attacker,defender);const ai=engine.chooseAiAction(initial);
  const recorder=new BattleRecorder(engine,state);
  const actions:any[]=[{kind:'rootPrepare'},{kind:'endTurn'},{kind:'endTurn'}];
  const steps=actions.map(action=>{const before=clone(state);let events:unknown=null;const failure=error(()=>{events=engine.dispatch(state,action);recorder.record(action);});return {action,before,events,failure,after:clone(state)};});
  const save=engine.saveBattle(state,'2026-10-20T00:00:00.000Z');const loaded=engine.loadBattle(save);
  const replay=recorder.replay(state);const replayed=replayBattle(engine,level,replay).state;
  const invalidLevel=clone(level);invalidLevel.scenario.variables.rootCheck='missing';const levelFailure=error(()=>engine.createState(invalidLevel));
  const invalidState=clone(state);invalidState.scenario.variables.rootCheck='missing';const stateFailure=error(()=>engine.saveBattle(invalidState));
  const refused=clone(save);refused.state.scenario.variables.failRead=true;const readFailure=error(()=>engine.loadBattle(refused));
  const seals=Object.fromEntries(['statusBehaviors','turnOrders','reactions','unitDepartures','referenceChecks','scenarioConditions','scenarioEffects','objectives'].map(key=>[key,error(()=>{const registry=installed[key];registry.replace(registry.get(registry.keys()[0]));})!==null]));
  return {level,identity,initial,forecast,ai,steps,save,loaded,replay,replayed,levelFailure,stateFailure,readFailure,seals,trace,manifest:engine.rulesetManifest};
}
const recipe=[contentPluginFor(createContentCatalog()),...DEFAULT_RULE_PLUGINS].map(plugin=>({id:plugin.id,version:plugin.version,provides:[...(plugin.provides??[])],requiresCapabilities:[...(plugin.requiresCapabilities??[])]}));
const result={recipe,cases:[[false,false],[true,false],[false,true]].map(([status,departure])=>run(status,departure))};
writeFileSync(process.argv[2],gzipSync(JSON.stringify(result)));
console.log(`Source all-capability root oracle: ${result.cases.length} integrated cases`);
