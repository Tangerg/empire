import { readFileSync, writeFileSync } from 'node:fs';
import { ContentPackInstaller, createContentCatalog, createBattleEngine } from '../../../packages/battle-engine/src/index';
import { recruitOptions, spawnUnit } from '../../../packages/battle-engine/src/state';
import { playerResource, unitResource, weaponResource } from '../../../packages/battle-engine/src/resources';
import { activeCasts } from '../../../packages/battle-engine/src/casting';
import { SpellCastEntity } from '../../../packages/battle-engine/src/domain/spell-cast';
import { commanderUnit, activeCommanderFor, commanderAuraFor, tacticOptions } from '../../../packages/battle-engine/src/commanders';
const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const engine = createBattleEngine({ content });
const base = engine.createState(JSON.parse(readFileSync('godot/tests/oracle/source-v1/grayflag-level.json', 'utf8')));
const cases: unknown[] = [];
const visibleObjectives = (state:any) => state.players.map((player:any) => player.objectives.filter((o:any) => !player.objectiveStates[o.id]?.hidden).map((o:any) => ({id:o.id,description:o.label ?? engine.rules.objectives.describe(o),status:player.objectiveStates[o.id]?.status ?? 'active',progress:engine.rules.objectives.progress(engine.rules,state,player.id,o),role:engine.rules.objectives.role(o)})));
function capture(fn: () => unknown) { try { return { value: fn() }; } catch (error) { return { error: (error as Error).message }; } }
function test(name: string, mutate: (state: any) => void = () => {}) {
 const state = structuredClone(base); mutate(state);
 const resources = engine.rules.resources;
 const accountSubjects = [playerResource(state.players[0]), unitResource(state.units[0]), ...Object.keys(state.units[0].weaponState).map(id => weaponResource(state.units[0], id))];
 const accounts = accountSubjects.map(subject => resources.adapters.keys().filter(id => resources.hasAccount(id, subject)).map(resource => ({ resource, name: resources.adapters.get(resource).name, ...resources.inspect(resource, subject) })));
 const objectives = state.players.flatMap((player: any) => player.objectives.map((objective: any) => ({owner: player.id, objective, value: capture(() => engine.rules.objectives.progress(engine.rules, state, player.id, objective))})));
 const casts = capture(() => activeCasts(state).map(cast => { const clock = new SpellCastEntity(cast); return {caster:cast.caster,owner:cast.owner,weapon:cast.weapon,name:content.weapons.get(cast.weapon).name,origin:cast.origin,target:cast.target,declaredAt:cast.declaredAt,resolveAt:cast.resolveAt,remaining:clock.remainingAt(state.actorTurns),progress:clock.progressAt(state.actorTurns)}; }));
 const incoming = capture(() => { const result = new Map<number, number>(); const board=engine.rules.space.board(state); for(const cast of activeCasts(state)){const clock=new SpellCastEntity(cast); for(const cell of engine.rules.areaShapes.coverage(board,cast.origin,cast.target,content.weapons.get(cast.weapon).area))result.set(board.indexOf(cell),clock.remainingAt(state.actorTurns));} return [...result].map(([index,remaining])=>({...board.coordOf(index),remaining})); });
 cases.push({ name, state, accounts, objectives, casts, incoming,
 visibleObjectives: visibleObjectives(state),
 cards: capture(() => recruitOptions(engine.rules, state, {x:0,y:0})),
 commanders:state.commanders.map((commander:any)=>({leader:commanderUnit(state,commander)??null,tactics:tacticOptions(engine.rules,state,commander.id)})),
 auras:state.units.map((unit:any)=>({commander:activeCommanderFor(engine.rules,state,unit),aura:commanderAuraFor(engine.rules,state,unit)})) });
}
const site=(s:any)=>{s.map.tiles[0]='castle';s.map.owners[0]=s.currentPlayer;};
test('base'); test('hidden objective',s=>{for(const p of s.players)for(const runtime of Object.values(p.objectiveStates) as any[])runtime.hidden=true;}); test('empty label',s=>{for(const p of s.players)for(const o of p.objectives)o.label='';}); test('terminal phase',s=>s.phase='over'); test('owned offers',site); test('unaffordable',s=>{site(s);s.players[0].resources.funds.current=0;});
test('occupied',s=>{site(s);s.units[0].x=0;s.units[0].y=0;}); test('capped',s=>{site(s);s.rules.maxUnitsPerPlayer=1;});
test('foreign site',s=>{site(s);s.map.owners[0]=2;});
for(const [current,capacity] of [[null,null],[null,12],[7,null],[7,7],[0,0]])test(`account ${current}/${capacity}`,s=>{site(s);for(const p of s.players)for(const a of Object.values(p.resources) as any[]){a.current=current;a.capacity=capacity;}for(const u of s.units){for(const a of Object.values(u.resources) as any[]){a.current=current;a.capacity=capacity;}for(const w of Object.values(u.weaponState) as any[])for(const a of Object.values(w.resources) as any[]){a.current=current;a.capacity=capacity;}}});
for(const status of ['active','inactive','completed','failed','cancelled'])test('objective '+status,s=>{for(const p of s.players)for(const runtime of Object.values(p.objectiveStates) as any[])runtime.status=status;});
const objectiveCases:any[]=[{type:'routEnemies'},{type:'captureHQ'},{type:'holdAllVillages'},{type:'surviveTurns',turns:3},{type:'eliminate',selector:{owner:1}},{type:'protect',selector:{owner:1},minimumAlive:2,untilTurn:8},{type:'destroy',structures:['missing']},{type:'escort',selector:{owner:1},count:2,zone:'queryZone'},{type:'control',zone:'queryZone'},{type:'score',variable:'queryScore',atLeast:8},{type:'interact',variable:'queryScore',equals:8},{type:'neutralizeComposite',composite:'queryComposite'}];
for(const objective of objectiveCases)test('objective kind '+objective.type,s=>{s.players[0].objectives=[{...objective,id:'query'}];s.players[0].objectiveStates.query={status:'active',hidden:false};s.scenario.zones.queryZone=[{x:0,y:0},{x:1,y:0}];s.scenario.variables.queryScore='3';s.composites.push({id:'queryComposite',parts:['missing'],minimumNeutralized:2});});
for(const type of ['all','any','sequence','optional','failOn'])test('nested '+type,s=>{const children=[{id:'a',type:'surviveTurns',turns:4},{id:'b',type:'routEnemies'}];s.players[0].objectives=[{id:'outer',type,...(['all','any','sequence'].includes(type)?{objectives:children}:{objective:children[0],condition:{type:'always'}})}];s.players[0].objectiveStates={outer:{status:'active',hidden:false},a:{status:'completed',hidden:true},b:{status:'inactive',hidden:false}};});
for(const value of [null,false,true,'','  ','bad',4,'0xff','0b11','0o10','Infinity','-Infinity','infinity','1e-7','1e21','1e999','-0','+0x1'])test('score coercion '+JSON.stringify(value),s=>{s.players[0].objectives=[{id:'q',type:'score',variable:'q',atLeast:8}];s.players[0].objectiveStates.q={status:'active',hidden:false};s.scenario.variables.q=value;});
for(const tick of [-2,0,1,3,10])test('cast clock '+tick,s=>{s.actorTurns=tick;s.pendingCasts=[0,1].map((v)=>({caster:s.units[v].id,owner:s.units[v].owner,ability:'attack',weapon:content.weapons.all()[0].id,origin:{x:0,y:0},target:{x:1,y:1},declaredAt:0,resolveAt:3+v}));s.pendingCasts.push({...s.pendingCasts[0],caster:99999,resolveAt:0});});
test('malformed live cast',s=>{s.pendingCasts=[{caster:s.units[0].id,owner:1,weapon:content.weapons.all()[0].id,origin:{x:0,y:0},target:{x:1,y:1},declaredAt:2,resolveAt:2}];});
test('neutral aura',s=>{for(const c of s.commanders){c.aura={attackMultiplier:1,defenseDelta:0,movementDelta:0};c.radius=99;}});
test('commander absent',s=>{s.commanders[0].unitId=99999;});test('commander wrong owner',s=>{s.commanders[0].owner=2;});test('commander range',s=>{s.commanders[0].radius=-1;});
const thresholds=[-1,0,1,2,3].map(rank=>({rank,next:engine.rules.progression.nextThreshold(rank as any)}));
const ranks=[-1,0,119,120,319,320,9999].map(progress=>({progress,rank:engine.rules.progression.rankFor(progress)}));
const duplicatePacks=structuredClone(packs);
const duplicateUnit=duplicatePacks.flatMap((pack:any)=>pack.units??[]).find((unit:any)=>unit.id==='soldier');
duplicateUnit.recruitCosts=[{resource:'funds',amount:6},{resource:'funds',amount:6}];
const duplicateContent=createContentCatalog();new ContentPackInstaller(duplicateContent).install(...duplicatePacks);
const duplicateEngine=createBattleEngine({content:duplicateContent});const duplicateState=structuredClone(base);site(duplicateState);duplicateState.units=duplicateState.units.filter((u:any)=>u.x!==0||u.y!==0);duplicateState.players[0].resources.funds.current=10;
const duplicate={packs:duplicatePacks,state:duplicateState,cards:recruitOptions(duplicateEngine.rules,duplicateState,{x:0,y:0}),afford:duplicateEngine.rules.resources.canAfford(duplicateUnit.recruitCosts,playerResource(duplicateState.players[0]))};
const inspectErrors=[['funds','unit'],['unknown','player']].map(([resource,kind])=>({resource,kind,result:capture(()=>engine.rules.resources.inspect(resource,kind==='unit'?unitResource(base.units[0]):playerResource(base.players[0])))}));
const families=['movementProfiles','damageTypes','armorClasses','terrains','weapons','units','statuses','structures','terrainOverlays','tactics','careers','formations'];
const catalog=Object.fromEntries(families.map(family=>[family,(content as any)[family].all()]));
const unicodePacks=structuredClone(packs);const unicodeUnit=unicodePacks.flatMap((pack:any)=>pack.units??[]).find((unit:any)=>unit.id==='soldier');unicodeUnit.name='escaped \" \\ \ud800';unicodeUnit['key\udfff']='value\ud800';
const unicodeContent=createContentCatalog();new ContentPackInstaller(unicodeContent).install(...unicodePacks);
const unicode={packs:unicodePacks,definition:unicodeContent.units.get('soldier')};
const reactions=engine.rules.reactions.all();
const commandLabels=[];
for(const definition of content.units.all()){
 const state=structuredClone(base);state.units=[];
 const actor=spawnUnit(engine.rules,state,definition.id,1,{x:1,y:1});
 spawnUnit(engine.rules,state,'soldier',2,{x:2,y:1});
 const ally=spawnUnit(engine.rules,state,'soldier',1,{x:1,y:2});ally.hp=1;
 state.map.tiles[1+state.map.width]='castle';state.map.owners[1+state.map.width]=2;
 for(const at of [{x:1,y:1},{x:0,y:1}])commandLabels.push({state:structuredClone(state),actor:actor.id,at,options:engine.commandsAt(state,actor,at).map(({key,name,hint})=>({key,name,hint}))});
}
writeFileSync(process.argv[2],JSON.stringify({packs,cases,thresholds,ranks,duplicate,inspectErrors,catalog,unicode,reactions,commandLabels}));
