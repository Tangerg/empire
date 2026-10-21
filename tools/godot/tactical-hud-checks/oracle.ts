import {readFileSync,writeFileSync} from 'node:fs';
import {JSDOM} from 'jsdom';
import {ContentPackInstaller,createContentCatalog,createBattleEngine,GameSession} from '../../../packages/battle-engine/src/index';
import {embarkUnit} from '../../../packages/battle-engine/src/transports';
import {spawnUnit} from '../../../packages/battle-engine/src/state';
import {GameController} from '../../../packages/game-ui/src/ui/game';
import {BattleLog} from '../../../packages/game-ui/src/ui/battle-log';
import {IDLE,DEPLOYING,UnitSelection,DestinationSelection,TargetSelection,DeploymentSelection,DisembarkSelection,TacticTargetSelection,RecruitSelection} from '../../../packages/game-ui/src/ui/selection';
import {Hud} from '../../../packages/game-ui/src/ui/hud';
import {ArtDirection} from '../../../packages/game-ui/src/art/direction';
const document=new JSDOM('<html></html>').window.document;Object.assign(globalThis,{document});
const packs=JSON.parse(readFileSync('godot/data/content-packs.json','utf8'));
const content=createContentCatalog();new ContentPackInstaller(content).install(...packs);const engine=createBattleEngine({content});
const level=JSON.parse(readFileSync('godot/tests/oracle/source-v1/grayflag-level.json','utf8')); const base=engine.createState(level);const cases:any[]=[];
const actions=['command','tactic','reaction','facing','career','formation','embark','disembark','deploy-pick','recruit','cancel'];
const clean=(s:string)=>s.replace(/\s+/g,' ').trim();
function test(name:string,kind:string,mutate:(s:any)=>void=()=>{},other:any={}){
 const rulesEngine=other.engine??engine;
 const state=other.level?rulesEngine.createState(other.level):structuredClone(base);mutate(state);const unit=other.unitIndex===undefined?state.units.find((u:any)=>u.owner===state.currentPlayer):state.units[other.unitIndex];
 const tile=other.tile??{x:unit.x,y:unit.y};const inspect=other.inspect===false?null:other.inspectIndex===undefined?unit:state.units[other.inspectIndex];
 let selection:any=IDLE;let cursor=Object.hasOwn(other,'cursor')?other.cursor:tile;
 const path=[{x:unit.x,y:unit.y},tile];
 switch(kind){
  case 'Unit':selection=new UnitSelection(unit.id);break;
  case 'Destination':selection=new DestinationSelection(unit.id,tile,path);break;
  case 'DeploymentUnit':selection=new DeploymentSelection(unit.id);break;
  case 'Deploying':selection=DEPLOYING;break;
  case 'Recruit':selection=new RecruitSelection(tile);break;
  case 'Target':{
   const defender=state.units.find((u:any)=>u.owner!==unit.owner);
   other.weapon=Object.hasOwn(other,'weapon')?other.weapon:rulesEngine.content.units.get(unit.type).weapons[0];
   other.target=other.target??{x:defender.x,y:defender.y};
   selection=new TargetSelection(unit.id,tile,path,other.ability??'attack',other.weapon,[other.target]);
   cursor=Object.hasOwn(other,'cursor')?other.cursor:other.target;break;
  }
  case 'Disembark':selection=new DisembarkSelection(unit.id,state.embarkedUnits[0].unit.id,[{x:2,y:2}]);break;
  case 'Tactic':selection=new TacticTargetSelection('test-commander','test-tactic',[tile]);break;
 }
 const session=new GameSession(other.level??level,rulesEngine);session.state=state;
 // Exercise the authored pure composition method with real session/selection/log dependencies.
 // Skip constructor-only renderer/camera/DOM event wiring; no hudView logic is replaced here.
 const controller:any=Object.create(GameController.prototype);
 Object.assign(controller,{session,selection,cursor,inspect,busy:other.busy??false,options:{},log:new BattleLog()});
 const view=controller.hudView();
 const hud=new Hud(new ArtDirection(),{} as any);hud.render(view);
 const buttons=[...hud.el.querySelectorAll('button')].filter((b:any)=>actions.includes(b.dataset.act)||b.closest('.cmd-list,.recruit-grid')&&b.hasAttribute('data-arg')).map((b:any)=>{
 const clone=b.cloneNode(true);clone.querySelectorAll('svg,kbd,.rc-art,.rc-stats,.rc-blurb').forEach((e:any)=>e.remove());
 return {action:b.dataset.act??null,arg:b.dataset.arg??null,label:b.classList.contains('recruit-card')?`${clean(b.querySelector('.rc-title').textContent)} · ${clean(b.querySelector('.rc-cost').textContent)}`:clean(clone.textContent),hint:b.title,enabled:!b.disabled&&!!b.dataset.act,selected:b.classList.contains('primary')};});
 const text=(selector:string)=>[...hud.el.querySelectorAll(selector)].map(e=>clean(e.textContent??''));
 const unitName=hud.el.querySelector('.unit-name')?.cloneNode(true) as HTMLElement|undefined;unitName?.querySelector('.team-tag')?.remove();
 cases.push({name,sourceComposition:"GameController.hudView → Hud.render",hint:view.hint,hasTile:view.tile!==null,hasInspection:view.inspect!==null,swap:view.deployment?.swaps?rulesEngine.content.units.get(view.deployment.swaps.type).name:null,cursor,busy:other.busy??false,unitName:unitName?clean(unitName.textContent??''):null,packs:other.packs??null,state,kind,unit:selection.unitId,inspect:inspect?.id??null,tile,ability:selection.targetingLabel,weapon:other.weapon,target:other.target,forecast:text('.forecast .fc-line,.forecast .hint,.forecast .fc-row,.forecast .fc-detail li'),buttons,objectives:text('.obj-list li'),accounts:text('.hud-crown .funds'),terrain:text('.tile-plaque > .fact-row span'),unitText:text('.unit-section .kv,.stat-grid > div'),raw:hud.el.textContent});
}
test('no human objective owner','Idle',s=>{s.currentPlayer=2;for(const p of s.players)p.controller='ai';});
test('idle','Idle');test('selected','Unit');test('destination','Destination');test('busy destination','Destination',()=>{},{busy:true});test('inspect only','Idle');test('selected other inspected','Unit',()=>{},{inspectIndex:1});test('selected no inspected','Unit',()=>{},{inspect:false});test('cursor leaves board','Unit',()=>{},{cursor:null});test('tactic targeting context','Tactic');
test('recruit','Recruit',s=>{s.map.tiles[0]='castle';s.map.owners[0]=s.currentPlayer;},{tile:{x:0,y:0},inspect:false});
test('unaffordable','Recruit',s=>{s.map.tiles[0]='castle';s.map.owners[0]=s.currentPlayer;s.players[0].resources.funds.current=0;},{tile:{x:0,y:0},inspect:false});
test('cap still offers affordable','Recruit',s=>{s.map.tiles[0]='castle';s.map.owners[0]=s.currentPlayer;s.rules.maxUnitsPerPlayer=1;},{tile:{x:0,y:0},inspect:false});
test('hidden','Idle',s=>{for(const p of s.players)for(const o of Object.values(p.objectiveStates) as any[])o.hidden=true;});
for(const [current,capacity] of [[null,null],[null,10],[7,null],[7,10]])test(`account ${current}/${capacity}`,'Unit',s=>{for(const p of s.players)for(const a of Object.values(p.resources) as any[]){a.current=current;a.capacity=capacity;}});
const campaignLevels=JSON.parse(readFileSync('godot/data/grayflag-campaign-levels.json','utf8'));
for(const level of campaignLevels){test('campaign '+level.id,'Unit',()=>{},{level});test('campaign destination '+level.id,'Destination',()=>{},{level});}
const deploy=structuredClone(level);(deploy.scenario.zones??=[]).push({id:'hud-deploy',cells:deploy.units.filter((u:any)=>u.owner===1).map((u:any)=>({x:u.x,y:u.y}))});deploy.deployment={order:[1],zones:[{player:1,zone:'hud-deploy'}]};
test('deployment roster','Deploying',()=>{},{level:deploy});test('deployment picked','DeploymentUnit',()=>{},{level:deploy});test('deployment swap cursor','DeploymentUnit',()=>{},{level:deploy,cursor:{x:deploy.units[1].x,y:deploy.units[1].y}});test('busy deployment','DeploymentUnit',()=>{},{level:deploy,busy:true});
const initiative=structuredClone(level);initiative.rules.turnOrder='initiative';test('initiative','Idle',()=>{},{level:initiative});
const duel=(s:any)=>{s.units=s.units.filter((u:any,i:number)=>i===0||u.owner!==s.currentPlayer).slice(0,2);s.units[0].x=2;s.units[0].y=2;s.units[1].x=3;s.units[1].y=2;s.rules.fog=false;};
test('career blocked branches','Unit',s=>{duel(s);const foot=spawnUnit(engine.rules,s,'soldier',1,{x:2,y:2});s.units[0]=foot;s.units.pop();foot.career={current:'militia',mastery:{militia:0},unlocked:['militia']};});
const passengers=(s:any)=>{duel(s);const passenger=s.units[0];const carrier=spawnUnit(engine.rules,s,'c01.supply-wagon',1,{x:2,y:3});embarkUnit(engine.rules,s,passenger.id,carrier.id,()=>{});};
test('passenger landing choices','Unit',passengers);test('disembark target context','Disembark',passengers);test('passenger no landing','Unit',s=>{passengers(s);for(const at of [{x:2,y:2},{x:1,y:3},{x:3,y:3},{x:2,y:4}])spawnUnit(engine.rules,s,'c01.swordsman',1,at);});
test('carrier eligible','Unit',s=>{duel(s);spawnUnit(engine.rules,s,'c01.supply-wagon',1,{x:2,y:3});});
test('carrier wrong type','Unit',s=>{duel(s);const rider=spawnUnit(engine.rules,s,'c01.ivra',1,{x:2,y:2});s.units[0]=rider;s.units.pop();spawnUnit(engine.rules,s,'c01.supply-wagon',1,{x:2,y:3});});
test('current broken formation','Unit',s=>{duel(s);s.units[0].formation='formation-defensive';});
test('busy holding formation','Unit',s=>{duel(s);s.units[0].formation='formation-line';spawnUnit(engine.rules,s,'c01.swordsman',1,{x:2,y:3});spawnUnit(engine.rules,s,'c01.swordsman',1,{x:1,y:2});},{busy:true});
test('holding formation','Unit',s=>{duel(s);s.units[0].formation='formation-line';spawnUnit(engine.rules,s,'c01.swordsman',1,{x:2,y:3});spawnUnit(engine.rules,s,'c01.swordsman',1,{x:1,y:2});});
test('different inspected formation quirk','Unit',s=>{duel(s);s.units[0].formation='formation-line';s.units[1].formation='formation-defensive';spawnUnit(engine.rules,s,'c01.swordsman',1,{x:2,y:3});spawnUnit(engine.rules,s,'c01.swordsman',1,{x:1,y:2});},{inspectIndex:1});
test('inspection formation quirk','Idle',s=>{duel(s);s.units[0].formation='formation-line';spawnUnit(engine.rules,s,'c01.swordsman',1,{x:2,y:3});spawnUnit(engine.rules,s,'c01.swordsman',1,{x:1,y:2});});
test('forecast interceptor','Target',s=>{duel(s);const guard=spawnUnit(engine.rules,s,'c01.legion-shield',2,{x:3,y:3});guard.reaction='support';guard.hp=7;});
test('forecast collateral','Target',s=>{duel(s);spawnUnit(engine.rules,s,'c01.legion-shield',2,{x:3,y:3});},{weapon:'c01.gray-oath'});
test('forecast support','Target',s=>{duel(s);const ally=spawnUnit(engine.rules,s,'c01.swordsman',1,{x:2,y:3});ally.reaction='support';});
test('raw terrain with structure','Idle',s=>{s.structures.push({id:'hud-gate',type:'gate',x:3,y:9,owner:1,hp:70,disabled:true,statuses:[]});});
test('casts','Idle',s=>{s.pendingCasts=[{caster:s.units[0].id,owner:1,ability:'attack',weapon:'c01.border-blade',origin:{x:2,y:2},target:{x:3,y:3},declaredAt:0,resolveAt:3}];});
test('structure-only target absence','Target',s=>{duel(s);s.structures.push({id:'aimed-gate',type:'gate',x:2,y:3,owner:2,hp:70,disabled:false,statuses:[]});},{target:{x:2,y:3}});
test('forecast no hovered candidate','Target',duel,{cursor:{x:3,y:2},target:{x:0,y:0}});test('forecast cursor left','Target',duel,{cursor:null});test('forecast helpful non-weapon','Target',duel,{weapon:undefined,ability:'heal'});test('forecast non-attack weapon','Target',duel,{ability:'custom-weapon-order'});test('busy forecast','Target',duel,{busy:true});test('forecast counter','Target',duel);test('forecast guard','Target',s=>{duel(s);s.units[1].reaction='guard';});test('forecast conserve','Target',s=>{duel(s);s.units[1].reaction='conserve';});
const unicodePacks=structuredClone(packs);unicodePacks.flatMap((pack:any)=>pack.units??[]).find((unit:any)=>unit.id==='c01.laiya').name='name " \\ \ud800';
const unicodeContent=createContentCatalog();new ContentPackInstaller(unicodeContent).install(...unicodePacks);const unicodeEngine=createBattleEngine({content:unicodeContent});test('lossless UTF16 label','Unit',()=>{},{engine:unicodeEngine,packs:unicodePacks});
writeFileSync(process.argv[2],JSON.stringify({packs,cases}));
