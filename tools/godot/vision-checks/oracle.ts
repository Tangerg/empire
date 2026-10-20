import {readFileSync,writeFileSync} from 'node:fs';
import {ContentPackInstaller,createContentCatalog,createBattleEngine,type LevelData} from '../../../packages/battle-engine/src/index';
import {activeCasts} from '../../../packages/battle-engine/src/casting';
import {SpellCastEntity} from '../../../packages/battle-engine/src/domain/spell-cast';
import {visibleTiles,visibleUnits,isUnitVisible} from '../../../packages/battle-engine/src/vision';
const packs=JSON.parse(readFileSync('godot/data/content-packs.json','utf8'));
const level:LevelData=JSON.parse(readFileSync('godot/data/grayflag-level.json','utf8'));
const content=createContentCatalog();new ContentPackInstaller(content).install(...packs);
const engine=createBattleEngine({content});const cases=[];
for(const grid of ['square4','square8','hex'])for(const fog of [false,true])for(const variation of [0,1,2,3]) {
 const state=engine.createState({...level,units:level.units.map(unit=>({...unit,facing:engine.rules.grids.get(grid).restingFacing})),rules:{...level.rules,grid,fog}});
 if(variation===1)state.players[1].team=state.players[0].team;
 if(variation===2)state.units=state.units.filter(u=>u.owner===2);
 if(variation===3){state.map.tiles.fill('forest');state.map.owners[0]=1;}
 for(const viewer of [0,1,2])cases.push({state,viewer,tiles:[...visibleTiles(engine.rules,state,viewer)],units:visibleUnits(engine.rules,state,viewer).map(u=>u.id),individual:state.units.map(u=>isUnitVisible(engine.rules,state,viewer,u))});
}
const casts=[];
for(const grid of ['square4','square8','hex'])for(const tick of [0,1,3,8]) {
 const state=engine.createState({...level,units:level.units.map(unit=>({...unit,facing:engine.rules.grids.get(grid).restingFacing})),rules:{...level.rules,grid}});state.actorTurns=tick;
 const weapons=content.weapons.all().filter((weapon,index,list)=>list.findIndex(other=>other.area===weapon.area)===index);
 state.pendingCasts=weapons.map((weapon,index)=>({caster:state.units[index%state.units.length].id,owner:1,ability:'attack',weapon:weapon.id,origin:{x:2,y:2},target:{x:4,y:4},declaredAt:index,resolveAt:index+3}));
 state.pendingCasts.push({...state.pendingCasts[0],caster:99999});
 const incoming=new Map<number,number>();const board=engine.rules.space.board(state);
 for(const cast of activeCasts(state))for(const cell of engine.rules.areaShapes.coverage(board,cast.origin,cast.target,content.weapons.get(cast.weapon).area))incoming.set(board.indexOf(cell),new SpellCastEntity(cast).remainingAt(state.actorTurns));
 casts.push({state,incoming:[...incoming].map(([index,remaining])=>({...board.coordOf(index),remaining}))});
}
writeFileSync(process.argv[2],JSON.stringify({packs,cases,casts}));
