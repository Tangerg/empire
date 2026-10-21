import { readFileSync, writeFileSync } from 'node:fs';
import { ContentPackInstaller, createBattleEngine, createContentCatalog, type Objective } from '../../../packages/battle-engine/src/index';
import { AiTurnContext, TileAppraisal } from '../../../packages/battle-engine/src/ai';
const content = createContentCatalog();
new ContentPackInstaller(content).install(...JSON.parse(readFileSync('godot/data/content-packs.json','utf8')));
const engine = createBattleEngine({ content });
const level = JSON.parse(readFileSync('godot/tests/oracle/source-v1/grayflag-level.json','utf8'));
const initial = engine.createState(level);
const planning = { rules: engine.rules, objectiveAdvisors: engine.aiObjectiveAdvisors, abilityEvaluators: engine.abilityAiEvaluators };
const cases: unknown[] = [];
function record(name: string, mutate: (state: typeof initial) => void, choose = false) {
  const state = structuredClone(initial); mutate(state);
  const aggression = 0.58;
  const context = new AiTurnContext(planning, state, state.currentPlayer, { aggression });
  const appraisals = state.units.filter(unit => unit.owner === state.currentPlayer).slice(0,3).flatMap(unit =>
    [unit, {x: 8,y: 9}, {x: 12,y: 7}].map(at => ({unit: unit.id, at: {x:at.x,y:at.y}, value: new TileAppraisal(context,unit,at).value})));
  // TS spreads an entire unit into some Coord values. Compare the Coord contract,
  // not those incidental extra fields that appraisal never reads.
  const agenda = { ...context.agenda, mission: { ...context.agenda.mission,
    destinations: context.agenda.mission.destinations.map(destination => ({...destination, at: {x:destination.at.x,y:destination.at.y}})) } };
  cases.push({ name, state, aggression, agenda, threat: [...context.threat], appraisals,
    ...(choose ? { action: engine.chooseAiAction(state, { aggression }) } : {}) });
}
record('baseline', () => {}, true);
for (const grid of ['square8','hex']) record(`grid ${grid}`, state => {state.rules.grid = grid;});
for (const mode of ['guard','patrol','retreat']) record(`directive ${mode}`, state => {
  state.scenario.zones.goal = [{x:8,y:9},{x:12,y:7}];
  state.units.filter(unit => unit.owner === 1).forEach(unit => {unit.directive = {mode,zone:'goal',waypoints:[],cursor:0};});
}, true);
record('patrol route', state => {state.units[0].directive = {mode:'patrol',waypoints:[{x:8,y:9},{x:12,y:7}],cursor:1};});
record('wounded recovery', state => {state.units[0].hp=15; const tile=9*state.map.width+8;state.map.tiles[tile]='village';state.map.owners[tile]=1;},true);
record('threatened headquarters',state=>{for (const [x,y,owner] of [[3,9,1],[22,8,2]]) {const tile=y*state.map.width+x;state.map.tiles[tile]='castle';state.map.owners[tile]=owner;}state.units.find(unit=>unit.owner===2)!.x=7;});
record('charging threat and orphan', state => {
  const foe=state.units.find(unit=>unit.owner===2)!;
  state.pendingCasts=[{caster:foe.id,owner:2,ability:'attack',weapon:'mage_meteor',origin:{x:foe.x,y:foe.y},target:{x:8,y:9},declaredAt:state.actorTurns,resolveAt:state.actorTurns+2},
    {caster:9999,owner:2,ability:'attack',weapon:'mage_meteor',origin:{x:0,y:0},target:{x:8,y:9},declaredAt:state.actorTurns,resolveAt:state.actorTurns+2}];
});
record('recruit affordable',state=>{state.commanders=[];state.players[0].resources.funds.current=2000;const tile=9*state.map.width+8;state.map.tiles[tile]='castle';state.map.owners[tile]=1;},true);
record('recruit cap',state=>{state.commanders=[];state.rules.maxUnitsPerPlayer=1;const tile=9*state.map.width+8;state.map.tiles[tile]='castle';state.map.owners[tile]=1;},true);
record('tactic available',state=>{state.players[0].resources.command_points.current=6;},true);
const objectives: Objective[] = [
 {type:'routEnemies'}, {type:'captureHQ'}, {type:'holdAllVillages'}, {type:'surviveTurns',turns:10},
 {type:'eliminate',selector:{owner:2}}, {type:'destroy',structures:['target']},
 {type:'neutralizeComposite',composite:'composite'}, {type:'protect',selector:{keys:['campaign-laiya']},minimumAlive:1,untilTurn:10},
 {type:'escort',selector:{keys:['campaign-laiya']},zone:'goal',count:1}, {type:'control',zone:'goal'},
 {type:'escort',selector:{keys:['campaign-laiya']},zone:'missing',count:1},
 {type:'optional',objective:{type:'routEnemies'}},
 {type:'failOn',condition:{type:'turnAtLeast',turn:10},objective:{type:'protect',selector:{keys:['campaign-laiya']},minimumAlive:1,untilTurn:10}},
 {type:'sequence',objectives:[{id:'done',type:'routEnemies'},{id:'next',type:'control',zone:'goal'},{id:'last',type:'surviveTurns',turns:10}]},
 {type:'all',objectives:[{type:'routEnemies'},{type:'control',zone:'goal'}]},
];
for (const [index,objective] of objectives.entries()) record(`objective ${index} ${objective.type}`, state => {
 state.players[0].objectives=[{id:'focus',...objective}];state.players[0].objectiveStates={done:{id:'done',status:'completed',hidden:false}};
 state.scenario.zones.goal=[{x:8,y:9},{x:12,y:7}];
 state.structures=[{id:'target',type:'gate',owner:2,x:14,y:7,hp:100,disabled:false,statuses:[]}];
 state.composites=[{id:'composite',parts:['target'],minimumNeutralized:1,tags:[]}];
 for (const [x,y,owner] of [[3,9,1],[22,8,2]]) {const tile=y*state.map.width+x;state.map.tiles[tile]='castle';state.map.owners[tile]=owner;}
});
writeFileSync(process.argv[2] ?? '/tmp/empire-ai-oracle.json', JSON.stringify(cases, (_key,value) => value instanceof Map ? [...value] : value instanceof Set ? [...value] : value));
console.log(`Generated ${cases.length} source AI analysis cases`);
