import { readFileSync, writeFileSync } from 'node:fs';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { StatusBehaviors, resolveTurnStartStatuses, addStatus, removeStatus } from '../../../packages/battle-engine/src/statuses';
import { hashState } from '../../../packages/battle-engine/src/replay';
const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const initial = JSON.parse(readFileSync('godot/tests/oracle/grayflag-initial-raw.json', 'utf8')).state;
const defs = packs.flatMap((p: any) => p.statuses ?? []);
const template = structuredClone(defs[0]);
for (const id of ['test-hook', 'test-next']) packs[packs.length-1].statuses.push({ ...template, id, name:id, periodic:id==='test-hook'?{timing:'ownerTurnStart',maxHpFraction:0.01,nonlethal:false}:null, stackMode:'refresh' });
const cases: any[] = [];
for (const mode of ['emit','damage','nonlethal','zero','negative','remove','replace-status','append-status','remove-later','kill','replace-unit','unknown','periodic-kill']) {
 for (const lifecycle of [false,true]) {
  const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
  const hooks = StatusBehaviors.clone().register({ id:'test-hook', onOwnerTurnStart(context:any) {
    context.state.scenario.variables.sameContent=context.content===content;
    context.emit({type:'scenarioSignal',signal:`hook:${context.status.remaining}`});
    if (['damage','nonlethal','zero','negative','kill'].includes(mode)) {
      const amount = context.damage(mode==='kill'||mode==='nonlethal' ? 100000 : mode==='zero' ? 0 : mode==='negative' ? -3 : 3, mode==='nonlethal');
      context.state.scenario.variables.damageResult = amount;
    }
    if (mode==='remove') removeStatus(context.unit,'test-hook',context.emit);
    if (mode==='replace-status') { removeStatus(context.unit,'test-hook',context.emit); addStatus(content,context.unit,{id:'test-hook',remaining:7},context.emit); }
    if (mode==='append-status') addStatus(content,context.unit,{id:'test-next',remaining:4},context.emit);
    if (mode==='remove-later') removeStatus(context.unit,'test-next',context.emit);
    if (mode==='replace-unit') { const index=context.state.units.findIndex((u:any)=>u.id===context.unit.id); context.state.units[index]=structuredClone(context.unit); context.state.units[index].hp=20; }
  }}).register({ id:'test-next', onOwnerTurnStart(context:any) { context.emit({type:'scenarioSignal',signal:`next:${context.status.remaining}`}); }});
  const engine = createBattleEngine({ content, statusBehaviors: hooks });
  const state=structuredClone(initial); state.scenario.triggers=[];
  const unit=state.units.find((u:any)=>u.owner===(lifecycle?2:1));
  if(mode==='periodic-kill') unit.hp=1;
  unit.statuses=[{id:mode==='unknown'?defs[0].id:'test-hook',remaining:1,stacks:1},{id:'test-next',remaining:3,stacks:1}];
  const before=structuredClone(state); const events:any[]=[]; let error:string|null=null;
  try { if(lifecycle) events.push(...engine.dispatch(state,{kind:'endTurn'})); else resolveTurnStartStatuses(engine.rules,state,[unit],event=>events.push(event)); }
  catch(caught) {error=(caught as Error).message;}
  cases.push({mode,lifecycle,unit:unit.id,before,events,error,after:state,hash:hashState(state)});
 }
}
writeFileSync(process.argv[2],JSON.stringify({packs,cases}));
