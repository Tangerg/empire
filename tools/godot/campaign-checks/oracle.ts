import {readFileSync,writeFileSync} from 'node:fs';
import {ContentPackInstaller,createContentCatalog,createBattleEngine,type GameEvent,type LevelData} from '../../../packages/battle-engine/src/index';
import {CampaignRuntime,CampaignBattleBridge,type CampaignDefinition,type CampaignState} from '../../../packages/campaign-engine/src/index';
import {applyCandidate01BattleContext,applyCandidate01BattleResultPolicy} from '../../../packages/story-candidate-01/src/campaign';
const definition:CampaignDefinition=JSON.parse(readFileSync('godot/data/grayflag-campaign.json','utf8'));
const levels:LevelData[]=JSON.parse(readFileSync('godot/data/grayflag-campaign-levels.json','utf8'));
const content=createContentCatalog();new ContentPackInstaller(content).install(...JSON.parse(readFileSync('godot/data/content-packs.json','utf8')));
const engine=createBattleEngine({content});
const bridge=new CampaignBattleBridge(id=>{
 const level=levels.find(candidate=>candidate.id===id);
 if(!level)throw new Error(`Missing oracle level "${id}"`);
 return level;
},content);
type Operation='choose'|'choices'|'advance'|'begin'|'complete';
interface OperationCase {
 name:string;
 operation:Operation;
 args:Record<string,unknown>;
 before:CampaignState;
 after:CampaignState;
 value?:unknown;
 error?:string;
}
const operations:OperationCase[]=[],policies:unknown[]=[],results:unknown[]=[];
function record<T>(runtime:CampaignRuntime,name:string,operation:Operation,args:Record<string,unknown>,call:()=>T):T|undefined {
 const before=runtime.snapshot();
 let value:T|undefined,error:string|undefined;
 try{value=call();}catch(cause){error=cause instanceof Error?cause.message:String(cause);}
 operations.push({name,operation,args,before,after:runtime.snapshot(),...(error!==undefined?{error}:{value})});
 return value;
}
for (const branch of [0,1]) {
 const runtime=new CampaignRuntime(definition); let guard=0;
 while(runtime.state.status==='active' && guard++<150) {
  const node=runtime.node();
  if(node.type==='choice') {
   record(runtime,'bad choice '+node.id,'choose',{id:'missing'},()=>runtime.choose('missing'));
   const choices=runtime.choices();
   record(runtime,'choices '+node.id,'choices',{},()=>runtime.choices());
   const selected=choices[branch?choices.length-1:0];
   record(runtime,'choose '+node.id,'choose',{id:selected.id},()=>runtime.choose(selected.id));
  } else if(node.type==='battle') {
   record(runtime,'advance rejected '+node.id,'advance',{},()=>runtime.advance());
   const request=record(runtime,'begin '+node.id,'begin',{},()=>runtime.beginBattle(bridge));
   if(request===undefined)throw new Error(`Expected battle request for "${node.id}"`);
   record(runtime,'repeat begin rejected '+node.id,'begin',{},()=>runtime.beginBattle(bridge));
   const contextual=applyCandidate01BattleContext(request,runtime.state);
   policies.push({kind:'context',request,state:runtime.snapshot(),expected:contextual});
   const battle=engine.createState(contextual.level);
   const events:GameEvent[]=[{type:'scenarioSignal',signal:'test-signal'},{type:'scenarioSignal',signal:'test-signal'}];
   const result=bridge.result(contextual,battle,events,'victory');
   results.push({request:contextual,state:battle,events,outcome:'victory',expected:result});
   const projected=applyCandidate01BattleResultPolicy(result,runtime.state);
   policies.push({kind:'result',result,state:runtime.snapshot(),expected:projected});
   const wrong=structuredClone(projected);wrong.requestId='wrong';
   record(runtime,'wrong result rejected '+node.id,'complete',{result:wrong},()=>runtime.completeBattle(wrong));
   record(runtime,'complete '+node.id,'complete',{result:projected},()=>runtime.completeBattle(projected));
  } else record(runtime,'advance '+node.id,'advance',{},()=>runtime.advance());
 }
 if(runtime.state.status!=='completed')throw new Error('route did not finish');
 record(runtime,'completed rejects advance','advance',{},()=>runtime.advance());
}
const runtime=new CampaignRuntime(definition);runtime.advance();runtime.advance();runtime.choose(runtime.choices()[0].id);const request=runtime.beginBattle(bridge);const initial=engine.createState(request.level);
for(const kind of ['corpse','transport-loss','routed','surrendered','withdrawn','custom-exit','embarked']) {
 const state=structuredClone(initial);const key=request.rosterBindings[0].levelUnitKey;const unit=state.units.find(unit=>unit.key===key)!;state.units=state.units.filter(u=>u!==unit);
 if(kind==='embarked')state.embarkedUnits.push({unit,carrier:999});else state.markers.push({id:1,kind,at:{x:unit.x,y:unit.y},owner:unit.owner,fallenUnit:unit});
 const result=bridge.result(request,state,[]);
 results.push({request,state,events:[],expected:result});
 policies.push({kind:'result',result,state:runtime.snapshot(),expected:applyCandidate01BattleResultPolicy(result,runtime.state)});
}
const missing=structuredClone(runtime.state);missing.pendingBattle=null;missing.roster.torren.disposition='fallen';missing.roster.laiya.hpRatio=.451;missing.roster.laiya.moraleRatio=.25;
const omitted=bridge.prepare(definition,missing);results.push({prepare:true,state:missing,expected:omitted});
writeFileSync(process.argv[2]??'/tmp/empire-campaign-oracle.json',JSON.stringify({definition,operations,policies,results}));console.log(`Generated ${operations.length} campaign operations, ${policies.length} policy cases, ${results.length} bridge cases`);
