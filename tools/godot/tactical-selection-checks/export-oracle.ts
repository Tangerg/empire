import { writeFileSync } from 'node:fs';
import { JSDOM } from 'jsdom';
import { GameController } from '../../../packages/game-ui/src/ui/game';
import { createBattleEngine, normaliseLevel, GameSession, idx, recruitOptions, areEnemies, sameCoord, type Coord } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { IDLE, UnitSelection, DestinationSelection, TargetSelection, RecruitSelection, TacticTargetSelection, DeploymentSelection, type Selection, type SelectionContext } from '../../../packages/game-ui/src/ui/selection';
import { emptyOverlay, type BoardOverlay } from '../../../packages/game-ui/src/ui/board';
const engine = createBattleEngine({ content: createTestCatalog() });
const level = normaliseLevel({ schema: 2, id: 'selection-oracle', name: 'selection', width: 6, height: 2,
  terrain: ['..b...', '......'], owners: [{ x: 2, y: 0, owner: 1 }],
  units: [{ x: 0, y: 0, unit: 'soldier', owner: 1 }, { x: 3, y: 0, unit: 'soldier', owner: 2 }],
  players: [{ id: 1, name: 'P1', team: 1, color: '#3f7fd8', controller: 'human', resources: { funds: { current: 9999, capacity: null } } },
    { id: 2, name: 'P2', team: 2, color: '#d8483f', controller: 'ai', resources: {} }], rules: {}, victory: [{ type: 'routEnemies' }] });
const session = new GameSession(level, engine);
const state = session.state;
const cell = (index: number) => ({ x: index % state.map.width, y: Math.floor(index / state.map.width) });
const key = (at: Coord) => `${at.x},${at.y}`;
const cells = Array.from({ length: 12 }, (_, i) => cell(i));
const data = state.units.map(unit => {
 const stops = [...session.moveField(unit).stops].map(cell);
 const commands = Object.fromEntries(stops.map(at => [key(at), session.commandsAt(unit, at).map(o => ({ ...o, hostile: session.rules.abilities.get(o.ability).engagement !== null }))]));
 const attacks: Record<string, unknown> = {};
 for (const at of stops) for (const option of session.commandsAt(unit, at)) {
   if (!option.weapon) continue;
   for (const target of option.targets) {
     const plan = session.attackPlan(unit, target, at, option.weapon);
     attacks[`${key(at)}:${key(target)}:${option.weapon}`] = {
       strikeDamage: plan.primaryUnit?.strike.damage ?? 0,
       counterDamage: plan.primaryUnit?.counter?.damage ?? 0,
       splashDamage: plan.unitHits.filter(h => !h.primary).reduce((sum, h) => sum + h.damage.damage, 0) + plan.structureHits.filter(h => !h.primary).reduce((sum, h) => sum + h.forecast.damage, 0),
       terrainDefense: session.content.terrains.get(state.map.tiles[idx(state.map, at.x, at.y)]).defense,
       hasPrimaryUnit: !!plan.primaryUnit, affectedCells: plan.affectedCells,
     };
   }
 }
 return { id: unit.id, at: { x: unit.x, y: unit.y }, canAct: engine.canAct(state, unit), visible: true, owner: unit.owner,
   stops, commands, attacks, paths: Object.fromEntries(stops.map(at => [key(at), session.pathTo(unit, at)])),
   threat: [...session.threatOf(unit)].map(cell), control: [...session.controlZoneAgainst(unit)].map(cell) };
});
let selection: Selection = IDLE;
let cursor: Coord | null = null;
const context = (): SelectionContext => ({ session, state, isHumanTurn: true, cursor,
 hoverTarget: cursor && selection.targets.some(at => sameCoord(at, cursor!)) ? cursor : null,
 canAct: unit => engine.canAct(state, unit), isVisible: () => true });
function view() {
 const overlay = emptyOverlay(); selection.paint(context(), overlay);
 return { kind: selection === IDLE ? 'Idle' : selection instanceof UnitSelection ? 'Unit' : selection instanceof DestinationSelection ? 'Destination' : selection instanceof TargetSelection ? 'Target' : selection instanceof RecruitSelection ? 'Recruit' : 'unknown',
   unitId: selection.unitId, destination: selection instanceof DestinationSelection || selection instanceof TargetSelection ? selection.dest : selection.recruitAt,
   ability: selection instanceof TargetSelection ? selection.ability : null, weapon: selection instanceof TargetSelection ? selection.weapon ?? null : null,
   targets: selection.targets, path: overlay.path, selected: overlay.selected, move: [...overlay.move].map(cell), attack: [...overlay.attack].map(cell), helpful: [...overlay.heal].map(cell), controlled: [...overlay.controlled].map(cell) };
}
const ops = [ ['click',0,0], ['hover',1,0], ['click',2,0], ['back'], ['click',3,0], ['hover',3,0], ['back'], ['back'], ['back'], ['click',2,0], ['click',5,1], ['click',0,0], ['click',1,1], ['click',0,0], ['click',3,0], ['click',5,1], ['click',0,0], ['click',3,0], ['click',3,0] ] as const;
const trace = ops.map(op => {
 let action: unknown = null;
 if (op[0] === 'hover') cursor = { x: op[1], y: op[2] };
 else if (op[0] === 'back') selection = selection.back();
 else { const result = selection.click(context(), { x: op[1], y: op[2] }); action = result.action ?? null; selection = action ? IDLE : result.selection; }
 return { op, action, view: view() };
});
const output = { units: data, trace,
 actors: engine.actors(state).map(u => u.id),
 recruits: cells.filter(at => !state.units.some(u => sameCoord(u, at)) && recruitOptions(session.rules, state, at).length > 0),
 enemies: state.units.flatMap(a => state.units.filter(b => areEnemies(state,a.owner,b.owner)).map(b => `${a.id}:${b.id}`)),
 steps: Object.fromEntries(cells.map(at => [key(at), session.rules.space.board(state).grid.directions.map(d => session.rules.space.board(state).grid.step(at,d.id)).filter(to => to.x >= 0 && to.y >= 0 && to.x < 6 && to.y < 2)])) };

// Execute the shipped keyboard handler, not a rewritten switch used as an oracle.
const dom = new JSDOM('<html><body></body></html>', { url: 'https://selection-fixture.invalid/' });
for (const [name, value] of Object.entries({ document: dom.window.document, window: dom.window,
 getComputedStyle: dom.window.getComputedStyle.bind(dom.window), DOMParser: dom.window.DOMParser, HTMLElement: dom.window.HTMLElement, SVGElement: dom.window.SVGElement,
 requestAnimationFrame: (): number => 1, cancelAnimationFrame: (): void => {} })) {
 Object.defineProperty(globalThis, name, { configurable: true, value });
}
dom.window.HTMLElement.prototype.scrollTo = () => {};
const game = new GameController(level, () => {}, { engine });
interface ControllerAccess {
 selection: Selection; cursor: Coord | null; inspect: unknown; busy: boolean;
 onKey(event: KeyboardEvent): void; handleClick(at: Coord): Promise<void>;
 dispatch(action: unknown): Promise<void>; refresh(): void; recruit(type: string): Promise<void>; pickDeployUnit(id: number): void; session: GameSession; handleHover(at: Coord): void; board: {handlers:{onLeave():void}}; overlay(): BoardOverlay; cancel():void;
}
const source = game as unknown as ControllerAccess;
source.refresh = () => {};
let keyAction: unknown = null;
source.dispatch = async action => { keyAction = action; source.selection = IDLE; };
function overlayView(controller:ControllerAccess,width=6) {
 const overlay=controller.overlay();
 const cell=(index:number)=>({x:index%width,y:Math.floor(index/width)});
 return {selected:overlay.selected,cursor:overlay.cursor,path:overlay.path,move:[...overlay.move].map(cell),attack:[...overlay.attack].map(cell),helpful:[...overlay.heal].map(cell),controlled:[...overlay.controlled].map(cell),threat:[...overlay.threat].map(cell)};
}
// The full source controller composes selected/inspected rings around selection.paint.
for (let i=0;i<ops.length;i++) {
 const op=ops[i];
 if(op[0]==='hover')source.handleHover({x:op[1],y:op[2]});
 else if(op[0]==='back')source.cancel();
 else await source.handleClick({x:op[1],y:op[2]});
 Object.assign(trace[i].view,overlayView(source));
}
source.selection=IDLE;source.cursor=null;source.inspect=null;
const keyboardOps = [ ['key',' '], ['key','ArrowRight'], ['key','Enter'], ['key','Escape'],
 ['click',2,0], ['key','a'], ['key','Tab'], ['key','d'], ['key','w'], ['key','Escape'], ['key','Enter'] ] as const;
const keyboard = [];
for (const op of keyboardOps) {
 keyAction = null;
 let prevented = false;
 if (op[0] === 'click') await source.handleClick({x:op[1],y:op[2]});
 else {
   const event = new dom.window.KeyboardEvent('keydown', {key:op[1],cancelable:true});
   source.onKey(event); await Promise.resolve(); prevented=event.defaultPrevented;
 }
 keyboard.push({op,action:keyAction,prevented,cursor:source.cursor,
   kind: source.selection === IDLE ? 'Idle' : source.selection instanceof UnitSelection ? 'Unit' : source.selection instanceof DestinationSelection ? 'Destination' : source.selection instanceof TargetSelection ? 'Target' : source.selection instanceof RecruitSelection ? 'Recruit' : 'other',
   unitId:source.selection.unitId});
}
const strange = { ability: 'ability\ud800', weapon: 'weapon\udfff', commander: 'commander\ud800', tactic: 'tactic\udfff', type: 'unit\ud800' };
const utf16Target = new TargetSelection(state.units[0].id, {x:2,y:0}, session.pathTo(state.units[0],{x:2,y:0})!, strange.ability, strange.weapon, [{x:3,y:0},{x:4,y:0}]);
const utf16Tactic = new TacticTargetSelection(strange.commander,strange.tactic,[{x:3,y:0},{x:4,y:0}]);
source.selection = new RecruitSelection({x:2,y:0}); keyAction = null; await source.recruit(strange.type);
const utf16 = { strange, command: utf16Target.click(context(),{x:3,y:0}).action,
 tactic: utf16Tactic.click(context(),{x:3,y:0}).action, recruit:keyAction };
source.selection=IDLE;source.busy=true;
source.handleHover({x:3,y:0});
const hoverLeave={hover:{cursor:source.cursor,inspect:(source.inspect as {id:number}|null)?.id??null},leave:{} as {cursor:Coord|null;inspect:number|null}};
source.board.handlers.onLeave();
hoverLeave.leave={cursor:source.cursor,inspect:(source.inspect as {id:number}|null)?.id??null};
const overlays=[];
for(const [name,picked,hover,busy] of [
 ['selected',true,{x:1,y:0},false],['selected-enemy',true,{x:3,y:0},false],['enemy',false,{x:3,y:0},false],
 ['friendly',false,{x:0,y:0},false],['busy-enemy',false,{x:3,y:0},true],['busy-selected',true,{x:3,y:0},true],
] as const) {
 source.selection=IDLE;source.cursor=null;source.inspect=null;source.busy=false;
 if(picked)await source.handleClick({x:0,y:0});
 source.busy=busy;source.handleHover(hover);
 overlays.push({name,picked,hover,busy,hidden:false,view:overlayView(source)});
}
game.dispose();
const fogLevel=normaliseLevel({...level,width:20,height:2,terrain:['....................','....................'],elevation:undefined,
 units:[{x:0,y:0,unit:'soldier',owner:1},{x:19,y:0,unit:'soldier',owner:2}],owners:[],rules:{fog:true}});
const fogGame=new GameController(fogLevel,()=>{},{engine});const fogSource=fogGame as unknown as ControllerAccess;
const hiddenUnit=fogSource.session.state.units.find(unit=>unit.owner===2)!;
if(fogSource.session.isUnitVisible(1,hiddenUnit))throw new Error('Hidden overlay fixture must really be hidden.');
fogSource.handleHover({x:19,y:0});
overlays.push({name:'hidden',picked:false,hover:{x:19,y:0},busy:false,hidden:true,
 view:overlayView(fogSource,20)});
fogGame.dispose();
const deploymentLevel = normaliseLevel({...level,
 units: [{x:0,y:0,unit:'soldier',owner:1},{x:1,y:0,unit:'soldier',owner:1},{x:3,y:0,unit:'soldier',owner:2}],
 scenario: {zones:[{id:'blue',cells:[{x:0,y:0},{x:1,y:0},{x:0,y:1}]}]},
 deployment:{order:[1],zones:[{player:1,zone:'blue'}]} });
const deploying = new GameController(deploymentLevel,()=>{},{engine});
const deploymentSource = deploying as unknown as ControllerAccess;
let deploymentDispatches=0;
deploymentSource.dispatch=async()=>{ deploymentDispatches++; };
const roster=deploymentSource.session.deploymentRoster()!.units;
const deploymentPicks=roster.map(unit=>{
 deploymentSource.pickDeployUnit(unit.id);
 return {id:unit.id,at:{x:unit.x,y:unit.y},selected:deploymentSource.selection.unitId,
 kind:deploymentSource.selection instanceof DeploymentSelection?'DeploymentUnit':'other',dispatches:deploymentDispatches,view:overlayView(deploymentSource)};
});
deploying.dispose();dom.window.close();
writeFileSync(process.argv[2], JSON.stringify({...output,keyboard,utf16,deploymentPicks,hoverLeave,overlays}, null, 2));
