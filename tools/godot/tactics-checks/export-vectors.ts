import {readFileSync, writeFileSync} from 'node:fs';
import {createBattleEngine,createContentCatalog,ContentPackInstaller} from '../../../packages/battle-engine/src/index';
import {createState} from '../../../packages/battle-engine/src/state';
import {computeMoveField,pathTo,moveCostOf,attackTilesFrom,attackTargetCoords,healTargetsFrom,threatTiles,hasDirectLineOfSight} from '../../../packages/battle-engine/src/movement';
const root=process.cwd();const read=(p:string)=>JSON.parse(readFileSync(root+'/'+p,'utf8'));
const packs=read('godot/data/content-packs.json');const content=createContentCatalog();new ContentPackInstaller(content).install(...packs);const engine=createBattleEngine({content});
const raw=read('godot/tests/oracle/grayflag-initial-raw.json');const cases=read('godot/tests/oracle/action-cases.json');
const selected:{name:string,state:any,unit:number}[]=raw.state.units.map((u:any)=>({name:'grayflag-'+u.id,state:raw.state,unit:u.id}));
for(let i=0;i<cases.length;i+=12){const c=cases[i];if(c.action.unit)selected.push({name:'midgame-'+i,state:c.before,unit:c.action.unit});}
const level={schema:2,id:'movement-fixture',name:'Movement fixture',width:6,height:4,terrain:['......','.T#h..','..~^..','......'],units:[{key:'a',unit:'soldier',owner:1,x:0,y:0},{key:'b',unit:'archer',owner:1,x:1,y:0,hp:50},{key:'c',unit:'soldier',owner:2,x:3,y:0}],players:raw.level.players};
const base=createState(engine.rules,level as any);selected.push({name:'terrain-and-occupancy',state:base,unit:1});
for(const [name,edit] of [
 ['no-allied-pass',(s:any)=>s.rules.friendlyPassThrough=false],
 ['enemy-pass',(s:any)=>s.rules.enemiesBlockMovement=false],
 ['zone-of-control',(s:any)=>s.rules.zoneOfControl=true],
 ['square8',(s:any)=>s.rules.grid='square8'],
 ['hex',(s:any)=>s.rules.grid='hex'],
 ['layers',(s:any)=>{s.map.elevation[1]=1;s.map.cliffs=[{from:{x:0,y:0},to:{x:0,y:1}}];s.structures=[{id:'gate-a',type:'gate',x:2,y:0,owner:0,hp:100,disabled:false,statuses:[]}];s.scenario.overlays=[{id:'flood-a',type:'flooded',cells:[{x:0,y:1}],remainingRounds:null}];}],
 ['modifiers',(s:any)=>{s.units[0].statuses=[{id:'shaken',stacks:2,remaining:2}];s.units[0].formation='formation-defensive';s.units[0].commanderId='cmd';s.commanders=[{id:'cmd',unitId:2,owner:1,radius:2,aura:{attackMultiplier:1,defenseDelta:0,movementDelta:2},turnGrants:[],tactics:[],usedTactics:[]}];}],
] as const){const state=structuredClone(base);edit(state);selected.push({name,state,unit:1});}
const output=selected.map(({name,state,unit:id})=>{const unit=state.units.find((u:any)=>u.id===id);const field=computeMoveField(engine.rules,state,unit);const tiles=[...field.tiles.values()];return{name,state,unit:id,tiles,stops:[...field.stops],paths:tiles.map(t=>({to:{x:t.x,y:t.y},cost:moveCostOf(field,state.map,t),path:pathTo(field,state.map,t)})),attackTiles:attackTilesFrom(engine.rules,state,unit,unit),attackTargets:attackTargetCoords(engine.rules,state,unit,unit),healTargets:healTargetsFrom(engine.rules,state,unit,unit).map(u=>u.id),threat:[...threatTiles(engine.rules,state,unit,field)],lineOfSight:[...field.stops].slice(0,8).map(index=>{const at={x:index%state.map.width,y:Math.floor(index/state.map.width)};return{at,visible:hasDirectLineOfSight(engine.rules,state,unit,at)}})};});
writeFileSync(root+'/godot/tests/oracle/movement-vectors.json',JSON.stringify(output));console.log('Generated',output.length,'movement-query vectors');
