import { readFileSync, writeFileSync, appendFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { ContentPackInstaller, createBattleEngine, createContentCatalog, normaliseLevel } from '../../../packages/battle-engine/src/index';
import { hashState } from '../../../packages/battle-engine/src/replay';
const directory=process.argv[2] ?? '/tmp/empire-campaign-battles';
mkdirSync(directory,{recursive:true});
const content=createContentCatalog();new ContentPackInstaller(content).install(...JSON.parse(readFileSync('godot/data/content-packs.json','utf8')));
const engine=createBattleEngine({content});
const levels=JSON.parse(readFileSync('godot/data/grayflag-campaign-levels.json','utf8'));
for (const raw of levels) {
 if(process.argv[3] && raw.id!==process.argv[3])continue;
 const level=normaliseLevel(raw), state=engine.createState(level), file=join(directory,level.id+'.jsonl');
 writeFileSync(file,JSON.stringify({kind:'header',level,initialHash:hashState(state)})+'\n');
 let count=0;
 console.log(`SOURCE ${level.id} started, authored turnLimit=${state.rules.turnLimit}`);
 try {
  while(state.phase!=='over') {
   const action=engine.chooseAiAction(state), events=engine.dispatch(state,action);
   appendFileSync(file,JSON.stringify({kind:'step',index:count,action,events,stateHash:hashState(state)})+'\n');
   count++;
   if(count%100===0)console.log(`SOURCE ${level.id} ${count} actions, turn ${state.turn}`);
  }
  appendFileSync(file,JSON.stringify({kind:'final',steps:count,turn:state.turn,winnerTeam:state.winnerTeam,reason:state.endReason,stateHash:hashState(state)})+'\n');
  writeFileSync(join(directory,level.id+'.complete'),'complete\n');
  console.log(`SOURCE ${level.id} complete: ${count} actions, turn ${state.turn}, winner ${state.winnerTeam}, ${state.endReason}`);
 } catch(error) {
  const message=error instanceof Error?error.message:String(error);
  appendFileSync(file,JSON.stringify({kind:'error',index:count,message})+'\n');
  writeFileSync(join(directory,level.id+'.error'),message+'\n');
  console.error(`SOURCE ${level.id} failed after ${count}: ${message}`);
 }
}
