import {writeFileSync} from 'node:fs';
import {ContentPackInstaller,createBattleEngine,createContentCatalog,type GameState,type Action} from '../../../packages/battle-engine/src/index';
import {COMMON_CONTENT_PACK} from '../../../packages/content-common/src/index';
import {ANCIENT_EMPIRES_CONTENT_PACK} from '../../../packages/content-ancient-empires/src/index';
import {CANDIDATE_01_CONTENT_PACK,candidate01Level} from '../../../packages/story-candidate-01/src/index';
const content=createContentCatalog();new ContentPackInstaller(content).install(COMMON_CONTENT_PACK,ANCIENT_EMPIRES_CONTENT_PACK,CANDIDATE_01_CONTENT_PACK);const engine=createBattleEngine({content});
const cases:unknown[]=[];
for(const type of ['soldier','archer','rogue','cleric','mage','knight','ogre','ballista','dragon'])for(const immediate of [true,false])test(type+' '+immediate,s=>{s.rules.recruitsActImmediately=immediate;}, {kind:'recruit',at:{x:0,y:0},unit:type});
test('off board',()=>{}, {kind:'recruit',at:{x:-1,y:0},unit:'soldier'});
test('wrong terrain',s=>{s.map.tiles[0]='plain';},{kind:'recruit',at:{x:0,y:0},unit:'soldier'});
test('enemy owner',s=>{s.map.owners[0]=2;},{kind:'recruit',at:{x:0,y:0},unit:'soldier'});
test('no owner',s=>{s.map.owners[0]=0;},{kind:'recruit',at:{x:0,y:0},unit:'soldier'});
test('insufficient',s=>{s.players[0].resources.funds.current=0;},{kind:'recruit',at:{x:0,y:0},unit:'soldier'});
test('cap',s=>{s.rules.maxUnitsPerPlayer=1;},{kind:'recruit',at:{x:0,y:0},unit:'soldier'});
test('occupied',s=>{s.units[0].x=0;s.units[0].y=0;},{kind:'recruit',at:{x:0,y:0},unit:'soldier'});
function test(name:string,mutate:(s:GameState)=>void,action:Action){const state=engine.createState(candidate01Level('c01-01'));state.map.tiles[0]='castle';state.map.owners[0]=1;state.players[0].resources.funds.current=9999;mutate(state);const before=structuredClone(state);let events:readonly unknown[]=[];let error:string|null=null;try{events=engine.dispatch(state,action);}catch(e){error=(e as Error).message;}cases.push({name,before,action,events,error,after:state});}
writeFileSync(process.argv[2],JSON.stringify(cases));
