import { appendFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { ContentPackInstaller, createBattleEngine, createContentCatalog, type GameEvent, type GameState, type LevelData } from '../../../packages/battle-engine/src/index';
import { hashState } from '../../../packages/battle-engine/src/replay';
import { CampaignRuntime, CampaignBattleBridge, createCampaignSave, loadCampaignSave, type CampaignDefinition, type BattleRequest } from '../../../packages/campaign-engine/src/index';
import { applyCandidate01BattleContext, applyCandidate01BattleResultPolicy } from '../../../packages/story-candidate-01/src/campaign';

const directory = process.argv[2];
if (!directory) throw new Error('Pass a temporary output directory.');
mkdirSync(directory, { recursive: true });
const selection = process.argv[3] ?? 'first';
if (!['first', 'last'].includes(selection)) throw new Error('Choice strategy must be first or last.');
const humanAggression = process.argv[4] === undefined || process.argv[4] === 'default' ? null : Number(process.argv[4]);
if (humanAggression !== null && (!Number.isFinite(humanAggression) || humanAggression < 0 || humanAggression > 1)) throw new Error('Human-side policy aggression must be in [0, 1].');
const id = `${selection}-${humanAggression ?? 'default'}`;
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
writeFileSync(file, JSON.stringify({ kind: 'header', id, selection, humanAggression, definition, initial: runtime.snapshot() }) + '\n');
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
try {
  checkpoint('initial story');
  while (runtime.state.status === 'active') {
    const node = runtime.node();
    if (node.type === 'choice') {
      const choices = runtime.choices();
      const choice = choices[selection === 'last' ? choices.length - 1 : 0];
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
        const action = engine.chooseAiAction(battle!, human && humanAggression !== null ? { aggression: humanAggression } : undefined);
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
      console.log(`SOURCE route ${id}: ${summary.level} ${summary.outcome}, ${step} actions, ${summary.reason}`);
      checkpoint(`after result ${summary.level}`);
    }
  }
  const summary = { id, status: runtime.state.status, actions, results, final: runtime.snapshot() };
  write({ kind: 'final', ...summary });
  writeFileSync(join(directory, `${id}.summary.json`), JSON.stringify(summary, null, 2));
  console.log(`SOURCE route ${id}: terminal ${runtime.state.status}, ${results.length} actual battles, ${actions} actions`);
} catch (caught) {
  const error = caught instanceof Error ? { name: caught.name, message: caught.message, stack: caught.stack } : { message: String(caught) };
  write({ kind: 'error', error, campaign: runtime.snapshot(), request, battle, actions });
  writeFileSync(join(directory, `${id}.error.json`), JSON.stringify(error, null, 2));
  throw caught;
}
