import { readFileSync, writeFileSync } from 'node:fs';
import {
  ContentPackInstaller, createBattleEngine, createContentCatalog, normaliseLevel, defineCareer,
  careerOptions, careerMastery, unitAbilityIds, changeCareer, awardCareerProgress, awardRankProgress,
  formationOptions, formationInEffect, validateFormationChange, formationMovementDelta,
  computeMoveField, computeDamage, ActionExecutionContext,
  type ContentPack, type GameState, type GameEvent, type Action, type Unit,
} from '../../../packages/battle-engine/src/index';

const original: ContentPack[] = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const catalogs: ContentPack[][] = [original, structuredClone(original), structuredClone(original), structuredClone(original)];
const findUnit = (packs: ContentPack[], id: string) => packs.flatMap(pack => pack.units ?? []).find(unit => unit.id === id)!;
const findCareer = (packs: ContentPack[], id: string) => packs.flatMap(pack => pack.careers ?? []).find(career => career.id === id)!;
findUnit(catalogs[1], 'soldier').formations = ['formation-line','formation-defensive','formation-loose'];
findUnit(catalogs[1], 'archer').formations = ['formation-loose','formation-line'];
const ranger = findCareer(catalogs[1], 'ranger');
ranger.costs = [{resource:'funds',amount:30},{resource:'command_points',amount:1},{resource:'funds',amount:10}];
const archer = findUnit(catalogs[1], 'archer');
archer.maxHp = 80;
archer.resources = {momentum:{current:3,capacity:40},command_points:{current:2,capacity:4}};
archer.weapons = [...findUnit(original, 'soldier').weapons, ...archer.weapons];
findCareer(catalogs[1], 'militia').masteryAbilities = ['heal','attack','heal'];
const specialPack = catalogs[2].find(pack => (pack.careers?.length ?? 0) > 0)!;
specialPack.careers = [...(specialPack.careers ?? []),
  defineCareer({id:'root-z',name:'Root Z',unitType:'soldier'}),
  defineCareer({id:'branch-a',name:'Branch A',unitType:'archer',tier:1,from:['root-z','militia'],minimumRank:2,minimumMastery:200}),
  defineCareer({id:'cycle-a',name:'Cycle A',unitType:'soldier',from:['cycle-b']}),
  defineCareer({id:'cycle-b',name:'Cycle B',unitType:'archer',from:['cycle-a']}),
  defineCareer({id:'orphan',name:'Orphan',unitType:'soldier',from:['missing-career']}),
];
findUnit(catalogs[2], 'soldier').formations = ['formation-loose','missing-formation','formation-line','formation-loose'];
findUnit(catalogs[3], 'archer').maxHp = 0;

// Deliberately malformed graphs exercise query termination without pretending
// these catalogs passed the independent source content-install validation gate.
function context(index: number) {
  const content = createContentCatalog(); new ContentPackInstaller(content).install(...original);
  for (const pack of catalogs[index]) {
    for (const item of pack.units ?? []) content.units.has(item.id) ? content.units.replace(item) : content.units.register(item);
    for (const item of pack.careers ?? []) content.careers.has(item.id) ? content.careers.replace(item) : content.careers.register(item);
    for (const item of pack.formations ?? []) content.formations.has(item.id) ? content.formations.replace(item) : content.formations.register(item);
  }
  return {...baseEngine.rules,content};
}
const content = createContentCatalog(); new ContentPackInstaller(content).install(...original);
const baseEngine = createBattleEngine({content});
const contexts = catalogs.map((_packs,index) => context(index));
const level = normaliseLevel({schema:2,id:'progression-fixture',name:'Progression fixture',terrain:Array(5).fill('.....'),
  units:[{x:2,y:2,unit:'soldier',owner:1},{x:1,y:2,unit:'soldier',owner:1},{x:3,y:2,unit:'soldier',owner:2}],
  victory:[{type:'routEnemies'}]});
const initial = baseEngine.createState(level);
initial.units[0].rank = 1; initial.units[0].rankProgress = 120; initial.units[0].hp = 50;
initial.units[0].career = {current:'militia',unlocked:['militia'],mastery:{militia:120}};
initial.players[0].resources.funds.current = 200;
initial.players[0].resources.command_points.current = 3;

type Operation = 'careerOptions'|'abilities'|'mastery'|'changeCareer'|'awardCareer'|'awardRank'|'formationOptions'|'formation'|'validateFormation'|'delta'|'rawAction'|'dispatch'|'sequence'|'derived';
interface Params { career?:string; formation?:string|null; amount?:number; action?:Action; actions?:Action[] }
const cases: unknown[] = [];
function derived(rules: typeof baseEngine.rules, state: GameState, unit: Unit) {
  const field = computeMoveField(rules,state,unit);
  return {abilities:unitAbilityIds(rules.content,unit),formation:formationInEffect(rules,state,unit,unit.formation),
    movementDelta:formationMovementDelta(rules,state,unit),
    move:{origin:field.origin,unit:field.unit,tiles:[...field.tiles],stops:[...field.stops]},
    damage:computeDamage(rules,state,unit,state.units.find(candidate=>candidate.owner===2)!)};
}
function record(name:string,operation:Operation,mutate:(state:GameState)=>void=()=>{}, params:Params={},catalog=1) {
  const state=structuredClone(initial); mutate(state); const before=structuredClone(state), unit=state.units[0];
  const rules=contexts[catalog], events:GameEvent[]=[]; let value:unknown=null, error:string|null=null;
  const emit=(event:GameEvent)=>{events.push(event);};
  try {
    switch(operation) {
      case 'careerOptions': value=careerOptions(rules,state,unit); break;
      case 'abilities': value=unitAbilityIds(rules.content,unit); break;
      case 'mastery': value=careerMastery(unit,params.career!); break;
      case 'changeCareer': changeCareer(rules,state,unit,params.career!,emit); break;
      case 'awardCareer': awardCareerProgress(rules.content,unit,params.amount!,emit); break;
      case 'awardRank': awardRankProgress(rules,unit,params.amount!,emit); break;
      case 'formationOptions': value=formationOptions(rules,state,unit); break;
      case 'formation': value=formationInEffect(rules,state,unit,params.formation??null); break;
      case 'validateFormation': validateFormationChange(rules,state,unit,params.formation??null); break;
      case 'delta': value=formationMovementDelta(rules,state,unit); break;
      case 'rawAction': {const execution=new ActionExecutionContext(state,rules); try {baseEngine.actionHandlers.dispatch(execution,params.action!);} finally {events.push(...execution.events);} break;}
      case 'dispatch': events.push(...createBattleEngine({content:rules.content}).dispatch(state,params.action!)); break;
      case 'sequence': {
        const engine=createBattleEngine({content:rules.content}), steps:unknown[]=[];
        for(const action of params.actions!) {
          let failure:string|null=null; let emitted:readonly GameEvent[]=[];
          try {emitted=engine.dispatch(state,action); events.push(...emitted);} catch(caught) {failure=(caught as Error).message;}
          steps.push({action,events:emitted,error:failure,after:structuredClone(state)});
        }
        value=steps; break;
      }
      case 'derived': value=derived(rules,state,unit); break;
    }
  } catch (caught) {error=(caught as Error).message;}
  cases.push({name,operation,catalog,params,before,value,error,events,after:state});
}
const noAllies=(state:GameState)=>{state.units=state.units.filter(unit=>unit.id!==initial.units[1].id);};
const id=initial.units[0].id;
const careerAction=(career='ranger'):Extract<Action,{kind:'changeCareer'}>=>({kind:'changeCareer',unit:id,career});
const formationAction=(formation:string|null='formation-line'):Extract<Action,{kind:'changeFormation'}>=>({kind:'changeFormation',unit:id,formation});

record('career default options','careerOptions');
for(const current of [null,'','unknown','ranger','militia','root-z','cycle-a','cycle-b','orphan'])
  record(`career roots ${String(current)}`,'careerOptions',s=>{s.units[0].career.current=current;}, {},2);
for(const rank of [0,1,2] as const) for(const mastery of [-2,0,119,120,219,220,400])
  record(`career rank ${rank} mastery ${mastery}`,'careerOptions',s=>{s.units[0].rank=rank;s.units[0].career.mastery.militia=mastery;});
record('unlocked bypasses paths rank and mastery','careerOptions',s=>{s.units[0].rank=0;s.units[0].career.mastery={};s.units[0].career.unlocked=['dragon-lord','ranger','ogre-warrior'];});
record('mastered option flags','careerOptions',s=>{s.units[0].career.mastery.ranger=220;s.units[0].career.mastery.shadow=219;});
for(const funds of [0,39,40,41,200]) record(`cost aggregation ${funds}`,'careerOptions',s=>{s.players[0].resources.funds.current=funds;},{},1);
record('missing cost account','careerOptions',s=>{delete s.players[0].resources.command_points;},{},1);
record('uncapped cost account','careerOptions',s=>{s.players[0].resources.funds.capacity=null;},{},1);
for(const amount of [-1,0,3,220]) record(`mastery ${amount}`,'mastery',s=>{s.units[0].career.mastery.ranger=amount;},{career:'ranger'});
record('mastery missing','mastery',()=>{},{career:'unknown'});
record('ability stable deduplication','abilities',s=>{s.units[0].learnedAbilities=['heal','attack','heal','capture','wait'];});
for(const requested of ['militia','unknown','ogre-warrior','ranger','knight-order']) record(`direct change ${requested}`,'changeCareer',()=>{},{career:requested});
for(const hp of [0,0.1,1,49,50,99,100,200]) record(`health ratio ${hp}`,'changeCareer',s=>{s.units[0].hp=hp;},{career:'ranger'},1);
record('career preserves history and compatible inventory','changeCareer',s=>{
  const u=s.units[0];u.resources.momentum={current:17,capacity:20};u.resources.funds={current:7,capacity:null};
  u.weaponState[findUnit(original,'soldier').weapons[0]].cooldownRemaining=4;
  u.statuses=[{id:'poisoned',remaining:3,stacks:1}];u.capture=40;u.formation='formation-line';
  u.learnedAbilities=['heal'];u.career.mastery.ranger=41;u.career.unlocked.push('ranger');
},{career:'ranger'},1);
record('insufficient direct change preserves state','changeCareer',s=>{s.players[0].resources.funds.current=39;},{career:'ranger'},1);
record('health invariant direct mutation order','changeCareer',()=>{},{career:'ranger'},3);
for(const operation of ['awardCareer','awardRank'] as const) for(const amount of [-3,-0.5,0,0.49,0.5,1,119,120,200,1000])
  record(`${operation} ${amount}`,operation,s=>{s.units[0].career.mastery.militia=119;s.units[0].rank=0;s.units[0].rankProgress=119;},{amount},1);
for(const current of [null,'','unknown']) record(`career award absent ${String(current)}`,'awardCareer',s=>{s.units[0].career.current=current;},{amount:100});
record('negative mastery award clamps first','awardCareer',s=>{s.units[0].career.mastery.militia=-50;},{amount:120});
record('mastery repeated crossing has no second reward','awardCareer',s=>{s.units[0].career.mastery.militia=120;},{amount:1},1);
record('known mastery abilities excluded','awardCareer',s=>{s.units[0].career.mastery.militia=119;s.units[0].learnedAbilities=['heal'];},{amount:1},1);
record('rank never downgrades','awardRank',s=>{s.units[0].rank=2;s.units[0].rankProgress=0;},{amount:1});
record('rank zero award still normalizes progress','awardRank',s=>{s.units[0].rankProgress=-4;},{amount:0});
for(const grid of ['square4','square8','hex']) for(const diagonal of [false,true]) for(const allied of [false,true]) {
  const mutate=(s:GameState)=>{s.rules.grid=grid;s.units[1].x=1;s.units[1].y=diagonal?1:2;s.units[1].owner=allied?1:2;};
  record(`formation options ${grid} diagonal ${diagonal} ally ${allied}`,'formationOptions',mutate);
  record(`formation derived ${grid} diagonal ${diagonal} ally ${allied}`,'derived',s=>{mutate(s);s.units[0].formation='formation-line';});
}
for(const formation of [null,'','missing','formation-line','formation-defensive','formation-loose']) {
  record(`formation in effect ${String(formation)}`,'formation',()=>{},{formation});
  record(`formation validation ${String(formation)}`,'validateFormation',()=>{},{formation});
  record(`formation isolated validation ${String(formation)}`,'validateFormation',noAllies,{formation});
}
record('same-team different owner supports formation','formation',s=>{const allied=structuredClone(s.players[0]);allied.id=3;s.players.push(allied);s.units[1].owner=3;},{formation:'formation-line'});
record('same-id neighbor excluded','formation',s=>{s.units[1].id=id;},{formation:'formation-line'});
record('dead neighbor follows source array membership','formation',s=>{s.units[1].hp=0;},{formation:'formation-line'});
record('formation options preserve declared order and duplicate','formationOptions',()=>{},{},2);
record('formation active need not be allowed by type','formation',s=>{s.units[0].type='dragon';},{formation:'formation-line'});
record('formation delta isolated','delta',s=>{noAllies(s);s.units[0].formation='formation-line';});
for(const operation of ['rawAction','dispatch'] as const) for(const kind of ['career','formation'] as const) {
  const action=kind==='career'?careerAction():formationAction();
  record(`${operation} ${kind} success`,operation,()=>{},{action});
  record(`${operation} ${kind} done`,operation,s=>{s.units[0].done=true;},{action});
  record(`${operation} ${kind} wrong owner`,operation,s=>{s.currentPlayer=2;},{action});
  record(`${operation} ${kind} missing`,operation,()=>{},{action:{...action,unit:99999}});
  record(`${operation} ${kind} initiative not actor`,operation,s=>{s.turnOrder.policy='initiative';s.turnOrder.activeUnit=s.units[1].id;s.rules.turnOrder='initiative';},{action});
}
for(const formation of [null,'formation-line','formation-loose']) record(`same formation ${String(formation)}`,'dispatch',s=>{s.units[0].formation=formation;},{action:formationAction(formation)});
record('same invalid formation still rejects','dispatch',s=>{noAllies(s);s.units[0].formation='formation-line';},{action:formationAction()});
record('career paid action','dispatch',()=>{},{action:careerAction()},1);
record('career denied action atomic','dispatch',s=>{s.players[0].resources.funds.current=0;},{action:careerAction()},1);
record('career post-action derived profile','derived',s=>{changeCareer(contexts[1],s,s.units[0],'ranger',()=>{});s.units[0].done=false;},{},1);
for(const kind of ['career','formation'] as const) for(const phase of ['over','deployment'] as const)
  record(`raw ${kind} phase ${phase}`,'rawAction',s=>{s.phase=phase;},{action:kind==='career'?careerAction():formationAction()});
for(const kind of ['career','formation'] as const)
  record(`dispatch ${kind} battle over`,'dispatch',s=>{s.phase='over';s.winnerTeam=2;s.endReason="fixture completed";},{action:kind==='career'?careerAction():formationAction()});
record('formation edit career consumption and rejected order rollback','sequence',()=>{},
  {actions:[formationAction('formation-line'),careerAction(),formationAction(null)]});
record('career switch back retains progress and returns health ratio','sequence',()=>{},
  {actions:[careerAction(),{kind:'endTurn'},{kind:'endTurn'},careerAction('militia')]});
record('insufficient payment action rollback then legal formation','sequence',s=>{s.players[0].resources.funds.current=39;},
  {actions:[careerAction(),formationAction('formation-defensive')]});
record('original shipped type rejects undeclared formation','dispatch',()=>{},{action:formationAction()},0);
writeFileSync(process.argv[2]??'/tmp/empire-progression-oracle.json',JSON.stringify({catalogs,cases}));
console.log(`Generated ${cases.length} original-source progression cases`);
