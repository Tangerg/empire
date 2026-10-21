import { readFileSync, writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { Abilities, defineAbility } from '../../../packages/battle-engine/src/abilities';
import { commandOptions } from '../../../packages/battle-engine/src/actions';
import { unitWeapons } from '../../../packages/battle-engine/src/combat';
import { AiTurnContext, TileAppraisal, DefaultAbilityAiEvaluators } from '../../../packages/battle-engine/src/ai';
import { DefaultTacticalSpace } from '../../../packages/battle-engine/src/tactical-space';
import { TacticalGrids } from '../../../packages/battle-engine/src/tactical-grid';
import { DefaultBattleResources } from '../../../packages/battle-engine/src/resources';
type Data = Record<string, any>;
const clone = <T>(value: T): T => structuredClone(value);
const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const original = JSON.parse(readFileSync('godot/tests/oracle/grayflag-initial-raw.json', 'utf8')).state;
const def = (family: string, id: string, source = packs): any => source.flatMap((pack: any) => pack[family] ?? []).find((entry: any) => entry.id === id);
const cases: Data[] = [];
function run(name: string, config: Data = {}) {
 const customPacks = clone(packs);
 if (config.resourceWeapon) {
  const weapon = def('weapons','soldier_sword', customPacks);
  weapon.resourceRequirements = [{subject:'squad',resource:'charge',amount:1}];
  weapon.resourceCosts = [{subject:'squad',resource:'charge',amount:1}];
 }
 if (config.tactic) Object.assign(def('tactics','rally',customPacks), {target:'self',radius:0,costs:[{resource:'command_points',amount:2}]});
 const content = createContentCatalog(); new ContentPackInstaller(content).install(...customPacks);
 const abilities = Abilities.clone().register(defineAbility({id:'pulse',name:'Pulse',
  usable: () => !config.disableAbility,
 }));
 abilities.register(defineAbility({id:'charge',name:'Charge',weaponFor:(_rules,q)=>q.weaponId?content.weapons.get(q.weaponId):null,
  weaponChoices:(rules,q)=>unitWeapons(rules.content,q.unit)}));
 const abilityAiEvaluators = DefaultAbilityAiEvaluators.clone()
  .register({ability:'pulse',score:context=>(config.evaluatorScore??1000)+context.at.x-context.at.y*0.01})
  .register({ability:'charge',score:context=>context.option.weapon==='soldier_javelin'?2000:1000});
 class Space extends DefaultTacticalSpace {
  override moveField(state: any, unit: any) {
   const field=super.moveField(state,unit);
   if(config.stop!==undefined) field.stops=new Set([config.stop]);
   if(config.brokenParents) for(const tile of field.tiles.values()) if(tile.from!==-1) tile.from=tile.index;
   return field;
  }
  override pathTo(field: any,state: any,to: any) { return config.rejectPath?null:super.pathTo(field,state,to); }
  override threatOf(state: any,unit: any,field?:any) { return config.threat?new Set<number>(config.threat):super.threatOf(state,unit,field); }
  override attackTargets(state:any,unit:any,from:any,weapon:any) { return config.noTargets?[]:super.attackTargets(state,unit,from,weapon); }
  override board(state:any) { if(config.poisonBoard) throw new Error('AI must resolve grids, not space.board'); return super.board(state); }
 }
 const resources=DefaultBattleResources.clone();
 if(config.aiWeight!==undefined) resources.adapters.replace({...resources.adapters.get('command_points'),aiWeight:config.aiWeight});
 if(config.fundsAvailable!==undefined) resources.adapters.replace({...resources.adapters.get('funds'),account:()=>({read:()=>({current:config.fundsAvailable,capacity:null}),write:()=>{}})});
 resources.adapters.register({id:'charge',name:'Charge',subjectKind:'squad',integer:true,aiWeight:1,account:(subject:any)=>{
  const account=subject.squad.resources.charge;
  return {read:()=>account,write:(current:number)=>{account.current=current;}};
 }} as any);
 resources.subjects.register({kind:'squad',subjectFor:(context:any)=>({kind:'squad',squad:context.player}),ref:(subject:any)=>({kind:'squad',id:subject.squad.id})} as any);
 const grids=TacticalGrids.clone();
 const space=new Space(content,grids);
 const engine=createBattleEngine({content,abilities,abilityAiEvaluators,space,grids,resources});
 const state=clone(original);
 state.map={width:7,height:7,tiles:Array(49).fill('plain'),owners:Array(49).fill(0),captureProgress:Array(49).fill(0),elevation:Array(49).fill(0),cliffs:[],directionalCover:[]};
 function actor(id:number,owner:number,x:number):any {
  const type='soldier',definition=def('units',type,customPacks);
  return {...clone(original.units[0]),id,key:`ai-${id}`,type,owner,x,y:3,hp:definition.maxHp,done:false,formation:null,commanderId:null,
   reaction:definition.defaultReaction,statuses:[],learnedAbilities:[],resources:clone(definition.resources),
   weaponState:Object.fromEntries(definition.weapons.map((weapon:string)=>[weapon,{cooldownRemaining:0,resources:clone(def('weapons',weapon,customPacks).resources)}]))};
 }
 state.units=[actor(1,1,2),actor(2,2,5)];state.commanders=[];state.structures=[];state.composites=[];state.pendingCasts=[];state.embarkedUnits=[];state.markers=[];
 state.rules.moraleEnabled=false;state.rules.counterAttack=false;state.rules.grid=config.grid??'square4';
 state.scenario.engagementRules=[];state.scenario.zones={};state.scenario.variables={};state.scenario.eventCounts={};
 for(const player of state.players){player.objectives=[];player.objectiveStates={};player.resources.funds.current=config.recruit?3000:0;player.resources.command_points.current=6;player.resources.charge={current:config.chargeBalance??2,capacity:8};}
 if(config.ability)state.units[0].learnedAbilities.push(config.ability);
 if(config.tactic){state.commanders=[{...clone(original.commanders[0]),id:'test-commander',unitId:1,owner:1,tactics:['rally'],usedTactics:[]}];state.units[0].commanderId='test-commander';}
 if(config.recruit){state.map.tiles[8]='castle';state.map.owners[8]=1;}
 if(config.resourceWeapon)state.units[1].x=3;
 if(config.done)state.units[0].done=true;
 const planning={rules:engine.rules,abilityEvaluators:engine.abilityAiEvaluators,objectiveAdvisors:engine.aiObjectiveAdvisors};
 const context=new AiTurnContext(planning,state,1,{aggression:0.58});
 const expected={action:engine.chooseAiAction(state,{aggression:0.58}),threat:[...context.threat],
  appraisals:[{x:2,y:3},{x:4,y:3}].map(at=>new TileAppraisal(context,state.units[0],at).value),
  commands:commandOptions(engine.rules,state,state.units[0],{x:2,y:3}).map(option=>option.key),
  actors:context.actors().map(unit=>unit.id),distance:context.board.distance({x:0,y:0},{x:2,y:2})};
 cases.push({name,config,state,expected});
}
run('default planning');
run('registered ability and evaluator',{ability:'pulse'});
run('custom ability unusable',{ability:'pulse',disableAbility:true});
run('replaced evaluator changes choice',{ability:'pulse',evaluatorScore:-1000});
run('second weapon ability selected profile',{ability:'charge'});
run('space move field restricts stop',{ability:'pulse',stop:23});
run('space path query is independent of AI field trace',{ability:'pulse',stop:24,rejectPath:true});
run('broken parent links cannot produce action',{ability:'pulse',stop:24,brokenParents:true});
run('space board query is independent of AI geometry',{ability:'pulse',poisonBoard:true});
run('space target filter reaches AI command menu',{noTargets:true});
run('space threat replacement reaches appraisal',{threat:[23,24,25]});
run('space empty threat removes danger',{threat:[]});
run('resolved square8 geometry',{grid:'square8'});
run('resolved hex geometry',{grid:'hex'});
run('custom resource sufficient weapon',{resourceWeapon:true});
run('custom resource rejects weapon',{resourceWeapon:true,chargeBalance:0});
run('default tactic economics',{tactic:true});
run('resource AI weight suppresses tactic',{tactic:true,aiWeight:1000});
run('resource AI weight favors tactic',{tactic:true,aiWeight:0.25});
run('recruitment default funds',{recruit:true});
run('resolved resource account rejects recruitment',{recruit:true,fundsAvailable:0});
run('resolved resource account funds recruitment',{recruit:true,fundsAvailable:3000});
run('finished actors omitted',{done:true});
const json=JSON.stringify({packs,cases});writeFileSync(process.argv[2],json);if(process.argv[3])writeFileSync(process.argv[3],gzipSync(json));
console.log(`Generated ${cases.length} original TypeScript AI planning cases`);
