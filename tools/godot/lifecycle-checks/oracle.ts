import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { ContentPackInstaller, createBattleEngine, createContentCatalog, type GameEvent, type GameState } from '../../../packages/battle-engine/src/index';
import { createState } from '../../../packages/battle-engine/src/state';
import { BattleLifecycle } from '../../../packages/battle-engine/src/turn-cycle';
import { runScenarioTriggers } from '../../../packages/battle-engine/src/scenario';
const root=resolve('godot');
const packs=JSON.parse(readFileSync(root+'/data/content-packs.json','utf8'));
const content=createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const engine=createBattleEngine({content}); const level=JSON.parse(readFileSync(root+'/data/grayflag-level.json','utf8'));
type Operation='start'|'advanceTurn'|'afterAction'|'beginPlaying';
interface LifecycleCase {
 name:string;
 operation:Operation;
 before:GameState;
 events:GameEvent[];
 after:GameState;
}
const results:LifecycleCase[]=[];
function record(name:string,state:GameState,operation:Operation) {
 const before=structuredClone(state),events:GameEvent[]=[];
 const emit=(event:GameEvent)=>{events.push(event);state.scenario.eventCounts[event.type]=(state.scenario.eventCounts[event.type]??0)+1;};
 const lifecycle=new BattleLifecycle(state,engine.rules,emit);
 if(operation==='afterAction'){runScenarioTriggers(engine.rules,state,'afterAction',emit);lifecycle.concludeIfDecided();}
 else lifecycle[operation]();
 results.push({name,operation,before,events,after:structuredClone(state)});
}
record('initial side',createState(engine.rules,level),'start');
const initiativeLevel=structuredClone(level); initiativeLevel.rules.turnOrder='initiative'; const initiative=createState(engine.rules,initiativeLevel); record('initial initiative',initiative,'start');
for(let i=0;i<30;i++) record('initiative actor '+i,initiative,'advanceTurn');
const dead=engine.createState(level); dead.units=dead.units.filter(u=>u.key!=='campaign-laiya'); dead.scenario.eventCounts.death=1; record('critical survivor lost',dead,'afterAction');
const victory=engine.createState(level); victory.units=victory.units.filter(u=>u.owner!==2); victory.scenario.eventCounts.death=8; record('enemy routed',victory,'afterAction');
const ongoing=engine.createState(level); ongoing.scenario.eventCounts.death=1; record('one shot signal',ongoing,'afterAction'); record('one shot stays spent',ongoing,'afterAction');
const deploying=createState(engine.rules,level); deploying.phase='deployment'; deploying.deployment={order:[],currentIndex:0,assignments:[]}; for(const unit of deploying.units)unit.done=true;record('begin playing',deploying,'beginPlaying');
const repeating=engine.createState(level); repeating.scenario.triggers=[{id:'repeat',timing:'afterAction',repeat:{everyRounds:2,startTurn:1,maxFirings:2},condition:{type:'turnAtLeast',turn:1},effects:[{type:'emitSignal',signal:'repeat'}]}]; record('repeating first',repeating,'afterAction');record('repeating same occurrence',repeating,'afterAction'); repeating.turn=3;record('repeating second',repeating,'afterAction');repeating.turn=5;record('repeating exhausted',repeating,'afterAction');
writeFileSync(process.argv[2] ?? '/tmp/empire-lifecycle-oracle.json',JSON.stringify(results));console.log(`Generated ${results.length} actual TS lifecycle cases`);
