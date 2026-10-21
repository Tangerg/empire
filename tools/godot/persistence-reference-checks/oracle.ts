import { readFileSync, writeFileSync } from 'node:fs';
import {oldSaveCases,oldCatalogCases,oldLevelCases} from './legacy-cases';
import { gzipSync } from 'node:zlib';
import { ContentPackInstaller, createContentCatalog, createBattleEngine, hashState } from '../../../packages/battle-engine/src/index';
import type { GameState, BattleSave, Action } from '../../../packages/battle-engine/src/index';
const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const engine = createBattleEngine({content});
const initial: GameState = JSON.parse(readFileSync(process.argv[2], 'utf8')).initialState;
const fixed = '2026-01-01T00:00:00.000Z';
const cases: unknown[] = [];
const wait = (state:GameState): Action => { const unit=state.units.find(u=>u.owner===state.currentPlayer && !u.done)!; return {kind:'command',unit:unit.id,path:[{x:unit.x,y:unit.y}],command:{ability:'wait'}}; };
function record(name:string,edit:(state:any)=>void,action?:Action|((state:GameState)=>Action),legacyAccepted?:boolean) {
 const raw:BattleSave=engine.saveBattle(structuredClone(initial),fixed);edit(raw.state);
 const untouched=JSON.stringify(raw);let loadError:string|null=null;let roundtripHash:string|null=null;let actionError:string|null=null;
 let after:GameState|null=null;let postActionSaveError:string|null=null;let events:unknown=null;let attempted:Action|null=null;let atomic=true;
 try { const state=engine.loadBattle(raw); const roundtrip=engine.saveBattle(state,fixed); roundtripHash=hashState(roundtrip.state);
   if (JSON.stringify(roundtrip.state)!==JSON.stringify(raw.state)) throw new Error('roundtrip changed state');
   if(action){attempted=typeof action==='function'?action(state):action;const before=JSON.stringify(state);try{events=engine.dispatch(state,attempted);}catch(e){actionError=(e as Error).message;atomic=JSON.stringify(state)===before;}after=state;try{engine.saveBattle(state,fixed);}catch(e){postActionSaveError=(e as Error).message;}}
 }catch(e){loadError=(e as Error).message;}
 if(JSON.stringify(raw)!==untouched)throw new Error(name+' mutated source input');
 cases.push({name,save:raw,loadError,roundtripHash,action:attempted,actionError,postActionSaveError,events,after,atomic,legacyAccepted,category:loadError?"source-rejects-now":(actionError||postActionSaveError)?"source-fails-when-used":"source-accepts-inert"});
}
const account={current:7,capacity:11};
record('ordinary load roundtrip and wait',()=>{},wait);
record('unknown player account is inert',s=>s.players[0].resources.unknown=account,wait);
record('unknown unit account is inert',s=>s.units[0].resources.unknown=account,wait);
record('unknown weapon account is inert',s=>Object.values<any>(s.units[0].weaponState)[0].resources.unknown=account,wait);
record('unused site upkeep override is inert',s=>s.rules.siteResourceOverrides.unknown=13, {kind:'endTurn'});
record('unknown base upkeep admitted then resolved',s=>s.rules.baseResourceGrants.push({resource:'unknown',amount:2}), {kind:'endTurn'});
record('unknown commander upkeep admitted then resolved',s=>{const unit=s.units.find((u:any)=>u.owner!==s.currentPlayer);Object.assign(s.commanders[0],{owner:unit.owner,unitId:unit.id});s.commanders[0].turnGrants.push({resource:'unknown',amount:2});}, {kind:'endTurn'});
function trigger(effect:any,active=false) { return {id:'reference-reader-probe',timing:'afterAction',once:true,condition:{type:'turnAtLeast',turn:active?1:999},effects:[effect]}; }
record('future unknown directive remains dormant',s=>s.scenario.triggers.push(trigger({type:'setUnitDirective',selector:{},directive:{mode:'unknown',waypoints:[],cursor:0}})),wait);
record('future unknown status remains dormant',s=>s.scenario.triggers.push(trigger({type:'addStatus',selector:{},status:'unknown',duration:1})),wait);
record('future unknown status resolves at execution',s=>s.scenario.triggers.push(trigger({type:'addStatus',selector:{},status:'unknown',duration:1},true)),wait);
record('future unknown spawn remains dormant',s=>s.scenario.triggers.push(trigger({type:'spawnUnits',units:[{unit:'unknown',owner:1,x:0,y:0}]})),wait);
record('unknown current learned ability rejected',s=>s.units[0].learnedAbilities.push('unknown'));
record('unknown current reaction rejected',s=>s.units[0].reaction='unknown');
record('unknown current directive rejected',s=>s.units[0].directive.mode='unknown');
record('unknown objective rejected',s=>s.players[0].objectives.push({type:'unknown',id:'extra'}));
record('unknown nested condition rejected',s=>s.scenario.triggers.push({...trigger({type:'emitSignal',signal:'noop'}),condition:{type:'all',conditions:[{type:'not',condition:{type:'unknown'}}]}}));
record('unknown effect rejected',s=>s.scenario.triggers.push(trigger({type:'unknown',status:'unknown'})));
record('reference phase order and first seen dedupe',s=>{s.units[0].learnedAbilities.push('z','z','a');s.units[0].reaction='missing';s.units[0].directive.mode='missing';});
record('unknown failOn condition admitted then evaluated',s=>s.players[0].objectives[0].condition={type:'unknown'},wait);
record('future unknown directive becomes current after execution',s=>s.scenario.triggers.push(trigger({type:'setUnitDirective',selector:{ids:[s.units[0].id]},directive:{mode:'unknown',waypoints:[],cursor:0}},true)),wait);
oldSaveCases(record,initial);
record('active unknown scenario resource resolves atomically',s=>s.scenario.triggers.push(trigger({type:'changeUnitResource',selector:{ids:[s.units[0].id]},resource:'unknown',amount:1},true)),wait);
record('active unknown spawn resolves atomically',s=>s.scenario.triggers.push(trigger({type:'spawnUnits',units:[{unit:'unknown',owner:1,x:0,y:0}]},true)),wait);
record('active unknown directive zone resolves atomically',s=>s.scenario.triggers.push(trigger({type:'setUnitDirective',selector:{},directive:{mode:'guard',zone:'unknown-zone'}},true)),wait);
record('active unknown spawn owner resolves atomically',s=>s.scenario.triggers.push(trigger({type:'spawnUnits',units:[{unit:'soldier',owner:999,x:0,y:0}]},true)),wait);
record('active offboard spawn resolves atomically',s=>s.scenario.triggers.push(trigger({type:'spawnUnits',units:[{unit:'soldier',owner:1,x:-1,y:0}]},true)),wait);
const catalogCases:any[]=[];
oldCatalogCases((name:string,packs:any[],legacyAccepted:boolean)=>{let error:string|null=null;let errorName:string|null=null;let saved:any=null;
 try{const content=createContentCatalog();new ContentPackInstaller(content).install(...packs);const engine=createBattleEngine({content});saved=engine.saveBattle(initial,fixed);}catch(e){error=(e as Error).message;errorName=(e as Error).name;}
 catalogCases.push({name,packs,error,errorName,saved,legacyAccepted,category:error?'source-rejects-now':'source-accepts-inert'});
},packs);
const levelCases:any[]=[];
oldLevelCases((name:string,level:any,legacyAccepted:boolean)=>{let error:string|null=null;let errorName:string|null=null;let state:any=null;
 try{state=engine.createState(level,{seed:99});}catch(e){error=(e as Error).message;errorName=(e as Error).name;}
 levelCases.push({name,level,error,errorName,state,legacyAccepted,category:errorName==='DomainInvariantError'?'source-fails-when-used':error?'source-rejects-now':'source-accepts-inert'});
},JSON.parse(readFileSync('godot/tests/oracle/source-v1/grayflag-level.json','utf8')));
const result={cases,catalogCases,levelCases};writeFileSync(process.argv[3],gzipSync(JSON.stringify(result)));
for(const entry of cases as any[]) console.log(JSON.stringify({name:entry.name,loadError:entry.loadError,actionError:entry.actionError,atomic:entry.atomic}));

const map=[...cases.map((entry:any)=>({phase:'save',name:entry.name,legacyAccepted:entry.legacyAccepted,category:entry.category,error:entry.loadError??entry.actionError??entry.postActionSaveError})),...catalogCases.map(({name,legacyAccepted,category,error}:any)=>({phase:'catalog',name,legacyAccepted,category,error})),...levelCases.map(({name,legacyAccepted,category,error}:any)=>({phase:'level',name,legacyAccepted,category,error}))];
writeFileSync(process.argv[3]+'.map.json',JSON.stringify(map,null,2));
console.log(JSON.stringify({saveCases:cases.length,catalogCases:catalogCases.length,levelCases:levelCases.length}));
