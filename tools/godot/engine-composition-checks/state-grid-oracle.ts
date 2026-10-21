import { readFileSync, writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { BattleRecorder, replayBattle } from '../../../packages/battle-engine/src/replay';
import type { EnginePlugin } from '../../../packages/battle-engine/src/kernel';
import type { TacticalGrid } from '../../../packages/battle-engine/src/tactical-grid';

const packs=JSON.parse(readFileSync('godot/data/content-packs.json','utf8'));
const baseLevel=JSON.parse(readFileSync('godot/tests/oracle/grayflag-0.58.json','utf8')).level;
const clone=<T>(value:T):T=>structuredClone(value);
const cases=['square4','root-north'].map(id=>{
  const catalog=createContentCatalog();new ContentPackInstaller(catalog).install(...packs);
  let resolved:TacticalGrid;
  const plugin:EnginePlugin={id:`test.state-grid.${id}`,version:1,overrides:['grids'],requiresCapabilities:['grids'],install(context){
    const grids=context.require('grids').clone();
    resolved=Object.create(grids.get('square4'),{id:{value:id,enumerable:true},name:{value:'root north',enumerable:true},restingFacing:{value:'north',enumerable:true}}) as TacticalGrid;
    if(grids.has(id))grids.replace(resolved);else grids.register(resolved);
    context.replace('grids',grids);
  }};
  const level=clone(baseLevel);level.rules.grid=id;
  for(const unit of level.units)delete unit.facing;
  level.units[0].facing='east';
  level.scenario={triggers:[{id:'grid-spawn',timing:'afterAction',condition:{type:'turnAtLeast',turn:1},effects:[{type:'spawnUnits',ready:true,units:[
    {key:'grid-default',unit:'c01.swordsman',owner:1,x:0,y:9},
    {key:'grid-authored',unit:'c01.swordsman',owner:1,x:0,y:10,facing:'west'},
  ]}]}]};
  const engine=createBattleEngine({content:catalog,plugins:[plugin]});
  const state=engine.createState(level,{seed:12345});const initial=clone(state);const recorder=new BattleRecorder(engine,state);
  const action={kind:'endTurn'} as const;const events=engine.dispatch(state,action);recorder.record(action);
  const save=engine.saveBattle(state,'2026-10-21T00:00:00.000Z');const replay=recorder.replay(state);
  return {id,level,initial,action,events,state,save,loaded:engine.loadBattle(save),replay,replayed:replayBattle(engine,level,replay).state,
    resolvedIdentity:engine.rules.grids.get(id)===resolved!,facings:state.units.map(unit=>({id:unit.id,key:unit.key,facing:unit.facing})),manifest:engine.rulesetManifest};
});
writeFileSync(process.argv[2],gzipSync(JSON.stringify({cases})));
console.log(`Source state-grid root oracle: ${cases.length} grid registration/replacement cases`);
