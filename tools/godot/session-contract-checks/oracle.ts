import { readFileSync, writeFileSync } from 'node:fs';
import { ContentPackInstaller, createBattleEngine, createContentCatalog, type Action } from '../../../packages/battle-engine/src/index';
import { GameSession } from '../../../packages/battle-engine/src/session';
import { hashState } from '../../../packages/battle-engine/src/replay';
const content = createContentCatalog();
new ContentPackInstaller(content).install(...JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8')));
const engine = createBattleEngine({ content });
const level = JSON.parse(readFileSync('godot/data/grayflag-level.json', 'utf8'));
for (const player of level.players) player.controller = 'human';
const session = new GameSession(level, engine);
const cases: object[] = [];
function record(operation: string, action?: Action) {
  let accepted: boolean;
  if (operation === 'undo') accepted = session.undo();
  else if (operation === 'restart') { session.restart(); accepted = true; }
  else if (operation === 'load') { session.load(session.save()); accepted = true; }
  else accepted = session.tryDispatch(action!) !== null;
  cases.push({ operation, action, accepted, canUndo: session.canUndo, hash: hashState(session.state), events: structuredClone(session.log),
    ...(action && accepted ? { handsOff: engine.actionHandlers.handsOffTurn(action) } : {}) });
}
const first = session.state.units.find(unit => unit.owner === session.state.currentPlayer)!;
record('dispatch', { kind: 'face', unit: first.id, facing: 'north' });
record('dispatch', { kind: 'face', unit: first.id, facing: 'east' });
record('undo');
record('dispatch', { kind: 'face', unit: 999999, facing: 'north' });
record('undo');
record('undo');
for (let i = 0; i < 45; i++) {
  const action = engine.chooseAiAction(session.state);
  record('dispatch', action);
  if (!engine.actionHandlers.handsOffTurn(action) && session.canUndo) {
    record('undo'); record('dispatch', action);
  }
}
record('load'); record('undo');
const actor = session.state.units.find(unit => unit.owner === session.state.currentPlayer && !unit.done)!;
if (actor) record('dispatch', { kind: 'face', unit: actor.id, facing: 'north' });
record('restart'); record('undo');
// A renamed/replaced handler is the negative control for name-based handoff inference.
const original = engine.actionHandlers.get('face');
const customHandlers = engine.actionHandlers.clone();
customHandlers.replace({ ...original, kind: 'face', handsOffTurn: true, execute: original.execute.bind(original) });
const custom = new GameSession(level, Object.create(engine, { actionHandlers: { value: customHandlers } }));
custom.dispatch({ kind: 'face', unit: first.id, facing: 'north' });
if (custom.canUndo) throw new Error('Source handoff metadata mutant was not detected');
const extraCases: object[] = [];
const smallLevel = {
  schema: 2, id: 'session-contract', name: 'Session contract', width: 6, height: 2,
  terrain: ['......', '......'], owners: [], rules: {}, victory: [{ type: 'routEnemies' }],
  players: level.players.map((player: object) => ({ ...player, objectives: undefined })),
  units: [{ key: 'ally', unit: 'soldier', owner: 1, x: 0, y: 0 },
    { key: 'enemy', unit: 'soldier', owner: 2, x: 1, y: 0, hp: 1 }],
};
const terminal = new GameSession(smallLevel as never, engine);
const unit = terminal.state.units[0];
const attack = engine.commandsAt(terminal.state, unit, unit).find(command => command.ability === 'attack')!;
const lethal: Action = { kind: 'command', unit: unit.id, path: [{ x: unit.x, y: unit.y }],
  command: { ability: 'attack', weapon: attack.weapon, target: { x: 1, y: 0 } } };
terminal.dispatch(lethal);
if (terminal.state.phase !== 'over' || terminal.canUndo) throw new Error('Terminal fixture did not finish with undo disabled');
extraCases.push({ name: 'terminal non-handoff', level: smallLevel, action: lethal,
  hash: hashState(terminal.state), events: terminal.log, canUndo: terminal.canUndo, handsOff: false });
const deploymentLevel = { ...smallLevel, units: [smallLevel.units[0], { ...smallLevel.units[1], x: 5, y: 1, hp: 100 }],
  scenario: { zones: [{ id: 'front', cells: [{ x: 0, y: 0 }, { x: 1, y: 0 }] }] },
  deployment: { order: [1], zones: [{ player: 1, zone: 'front' }] } };
const deployment = new GameSession(deploymentLevel as never, engine);
const deploymentSteps: object[] = [];
for (const action of [{ kind: 'deployUnit', unit: deployment.state.units[0].id, at: { x: 1, y: 0 } },
  { kind: 'finishDeployment' }] as Action[]) {
  deployment.dispatch(action);
  deploymentSteps.push({ action, hash: hashState(deployment.state), events: structuredClone(deployment.log),
    canUndo: deployment.canUndo, handsOff: engine.actionHandlers.handsOffTurn(action) });
}
extraCases.push({ name: 'deployment handoff', level: deploymentLevel, steps: deploymentSteps });
writeFileSync(process.argv[2], JSON.stringify({ level, cases, extraCases, metadata: engine.actionHandlers.keys().map(kind => ({ kind,
  handsOff: engine.actionHandlers.handsOffTurn({ kind } as Action) })), customHandoffCanUndo: custom.canUndo }));
