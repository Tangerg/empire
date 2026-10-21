import { appendFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { ContentPackInstaller, createBattleEngine, createContentCatalog, type GameEvent, type GameState, type LevelData, type Action, type Unit, type Coord, type ReachableTile, threatMap } from '../../../packages/battle-engine/src/index';
import { hashState } from '../../../packages/battle-engine/src/replay';
import { CampaignRuntime, CampaignBattleBridge, createCampaignSave, loadCampaignSave, type CampaignDefinition, type BattleRequest } from '../../../packages/campaign-engine/src/index';
import { applyCandidate01BattleContext, applyCandidate01BattleResultPolicy } from '../../../packages/story-candidate-01/src/campaign';

const directory = process.argv[2];
if (!directory) throw new Error('Pass a temporary output directory.');
mkdirSync(directory, { recursive: true });
const selection = 'first';
const humanAggression = null;
const id = 'staged-refugees';
const file = join(directory, `${id}.jsonl`);
const stamp = '2026-10-20T00:00:00.000Z';
const definition: CampaignDefinition = JSON.parse(readFileSync('godot/data/grayflag-campaign.json', 'utf8'));
const levels: LevelData[] = JSON.parse(readFileSync('godot/data/grayflag-campaign-levels.json', 'utf8'));
const content = createContentCatalog();
new ContentPackInstaller(content).install(...JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8')));
const engine = createBattleEngine({ content });
const bridge = new CampaignBattleBridge(levelId => {
  const found = levels.find(level => level.id === levelId);
  if (!found) throw new Error(`Unknown campaign level ${levelId}`);
  return found;
}, content);
let runtime = new CampaignRuntime(definition);
let request: BattleRequest | null = null;
let battle: GameState | null = null;
let events: GameEvent[] = [];
let actions = 0;
const results: unknown[] = [];
writeFileSync(file, JSON.stringify({ kind: 'header', id, selection, humanDriver: 'staged-refugees-v2', humanAggression, definition, initial: runtime.snapshot() }) + '\n');
const write = (row: unknown) => appendFileSync(file, JSON.stringify(row) + '\n');
function checkpoint(label: string) {
  const document = { schema: 1, campaign: createCampaignSave(definition, runtime.state, stamp), request,
    battle: battle ? engine.saveBattle(battle, stamp) : null, events };
  write({ kind: 'checkpoint', label, document });
  // Actually reconstruct both writers through their original save readers. This
  // is a continuation check, not a generated snapshot standing in for play.
  const stored = JSON.parse(JSON.stringify(document));
  runtime = new CampaignRuntime(definition, loadCampaignSave(stored.campaign, definition).state);
  request = stored.request;
  battle = stored.battle ? engine.loadBattle(stored.battle) : null;
  events = stored.events;
}

/** Chooses only canonical reachable stops and command options; this is a test player, not a rule. */
function waitAt(state: GameState, unit: Unit, target: Coord = { x: unit.x, y: unit.y }) {
  const field = engine.space.moveField(state, unit);
  const board = engine.space.board(state);
  const risk = threatMap(engine.rules,state,state.currentPlayer);
  const score = (a: ReachableTile) => board.distance(a,target)*10 + (risk.get(a.index) ?? 0)*(unit.key?.startsWith('refugee-') ? 4 : 0.5);
  const stops = [...field.stops].map(i => field.tiles.get(i)!).sort((a,b) => score(a)-score(b) || a.cost-b.cost);
  for (const at of stops) {
    const option = engine.commandsAt(state, unit, at).find(c => c.ability === 'wait');
    const path = engine.space.pathTo(field,state,at);
    if (option && path) return {kind:'command' as const,unit:unit.id,path,command:{ability:'wait'}};
  }
  throw new Error('No canonical wait for ' + unit.key);
}
function humanAction(state: GameState): Action {
  const actors = engine.actors(state).filter(u => engine.canAct(state,u));
  const threats = state.units.filter(u => u.owner === 2);
  for (const healer of actors.filter(u => engine.content.units.get(u.type).abilities.includes('heal'))) {
    const field = engine.space.moveField(state,healer);
    let best: { score: number; action: Action } | null = null;
    for (const index of field.stops) {
      const at = field.tiles.get(index)!;
      for (const option of engine.commandsAt(state,healer,at).filter(c=>c.ability==='heal')) for (const target of option.targets) {
        const ally = state.units.find(u=>u.x===target.x&&u.y===target.y)!;
        const missing = engine.content.units.get(ally.type).maxHp-ally.hp;
        const score = Math.min(missing,30) + (ally.key?.startsWith('campaign-')?15:0);
        const path = engine.space.pathTo(field, state, at);
        if (path && missing >= 15 && (!best || score > best.score)) {
          best = { score, action: { kind: 'command', unit: healer.id, path, command: { ability: 'heal', target } } };
        }
      }
    }
    if(best) return best.action;
  }
  if (state.levelId === 'c01-01') {
    const endangered = actors.find(u=>['campaign-torren','campaign-elin'].includes(u.key??'') && u.hp<50);
    if(endangered) return waitAt(state,endangered,{x:10,y:8});
    return engine.chooseAiAction(state);
  }
  const southernThreat = threats.some(u => ['unmarked-ballista','south-bank-bow','battery-screen'].includes(u.key ?? ''));
  for (const unit of actors) {
    if (unit.key?.startsWith('refugee-')) {
      if (southernThreat) return waitAt(state,unit,{x:15,y:17+Number(unit.key.slice(-1))});
      const slot = Number(unit.key.slice(-1));
      const target = unit.x < 19 ? {x:18,y:18} : {x:23,y:18+slot};
      return waitAt(state,unit,target);
    }
    if (unit.key === 'campaign-laiya' && unit.hp < 65) return waitAt(state,unit,{x:7,y:21});
    if (unit.key === 'campaign-elin' && unit.hp < 20) return waitAt(state,unit,{x:7,y:21});
  }
  return engine.chooseAiAction(state);
}

try {
  checkpoint('initial story');
  while (runtime.state.status === 'active') {
    const node = runtime.node();
    if (node.type === 'choice') {
      const choices = runtime.choices();
      const choice = choices.find(c => c.id === (node.id === 'choice-first-command' ? 'steady-advance' : 'accept-truce')) ?? choices[0];
      if (!choice) throw new Error(`No available choice at ${node.id}`);
      runtime.choose(choice.id);
      write({ kind: 'choose', node: node.id, choices, choice: choice.id, after: runtime.snapshot() });
      checkpoint(`after choice ${node.id}`);
    } else if (node.type !== 'battle') {
      runtime.advance();
      write({ kind: 'advance', node: node.id, after: runtime.snapshot() });
    } else {
      const raw = runtime.beginBattle(bridge);
      request = applyCandidate01BattleContext(raw, runtime.state);
      battle = engine.createState(request.level);
      events = [];
      let step = 0;
      write({ kind: 'begin', raw, request, campaign: runtime.snapshot(), initialHash: hashState(battle) });
      console.log(`SOURCE route ${id}: ${request.levelId}, funds=${battle.players.find(player => player.id === 1)?.resources.funds?.current}, roster=${request.rosterBindings.length}`);
      checkpoint(`begin ${request.levelId}`);
      while (battle!.phase !== 'over') {
        const player = battle!.players.find(player => player.id === battle!.currentPlayer)!;
        const human = player.controller === 'human';
        const action = human ? humanAction(battle!) : engine.chooseAiAction(battle!);
        const emitted = engine.dispatch(battle!, action);
        events.push(...emitted);
        write({ kind: 'step', index: step, human, action, events: emitted, hash: hashState(battle!) });
        step++; actions++;
        if (step % 100 === 0) console.log(`SOURCE route ${id}: ${request!.levelId}, ${step} actions, turn ${battle!.turn}`);
        if (step % 67 === 0 && (battle as GameState).phase !== 'over') checkpoint(`mid ${request!.levelId}:${step}`);
      }
      const result = bridge.result(request!, battle!, events);
      const projected = applyCandidate01BattleResultPolicy(result, runtime.state);
      const summary = { level: request!.levelId, outcome: result.outcome, turn: battle!.turn, actions: step, winnerTeam: battle!.winnerTeam, reason: battle!.endReason };
      checkpoint(`concluded ${request!.levelId}`);
      runtime.completeBattle(projected);
      request = null; battle = null; events = [];
      write({ kind: 'complete', result, projected, summary, after: runtime.snapshot() });
      results.push(summary);
      if (results.length === 2 && summary.outcome === 'victory') break;
      console.log(`SOURCE route ${id}: ${summary.level} ${summary.outcome}, ${step} actions, ${summary.reason}`);
      checkpoint(`after result ${summary.level}`);
    }
  }
  const summary = { id, status: runtime.state.status, actions, results, final: runtime.snapshot() };
  write({ kind: 'final', ...summary });
  writeFileSync(join(directory, `${id}.summary.json`), JSON.stringify(summary, null, 2));
  console.log(`SOURCE opening route ${id}: ${results.length} actual battles, ${actions} actions; campaign remains ${runtime.state.status}`);
} catch (caught) {
  const error = caught instanceof Error ? { name: caught.name, message: caught.message, stack: caught.stack } : { message: String(caught) };
  write({ kind: 'error', error, campaign: runtime.snapshot(), request, battle, actions });
  writeFileSync(join(directory, `${id}.error.json`), JSON.stringify(error, null, 2));
  throw caught;
}
