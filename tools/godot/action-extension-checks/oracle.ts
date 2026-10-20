import { readFileSync, writeFileSync } from 'node:fs';
import { ContentPackInstaller, createContentCatalog } from '../../../packages/battle-engine/src/content-pack';
import { createBattleEngine } from '../../../packages/battle-engine/src/plugins/default';
import { CoreActionHandlers } from '../../../packages/battle-engine/src/actions';
import { IllegalActionError } from '../../../packages/battle-engine/src/domain/errors';
import { hashState } from '../../../packages/battle-engine/src/replay';
const packs = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const initial = JSON.parse(readFileSync('godot/tests/oracle/grayflag-initial-raw.json', 'utf8')).state;
const cases: any[] = [];
for (const phase of ['playing', 'deployment', 'over']) for (const during of [false, true]) {
 for (const mode of ['emit', 'fail', 'unknown', 'finish', 'begin']) {
  const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
  const handlers = CoreActionHandlers.clone().register({ kind: 'test-order', duringDeployment: during, handsOffTurn: true,
    execute(context: any) {
      context.state.scenario.variables.testOrder = 7;
      context.emit({ type: 'scenarioSignal', signal: 'test.order' });
      context.emit({ type: 'scenarioSignal', signal: 'test.order.again' });
      if (mode === 'fail') throw new IllegalActionError('test refusal');
      if (mode === 'finish') context.state.phase = 'over';
      if (mode === 'begin') context.state.phase = 'playing';
    },
  } as any);
  const engine = createBattleEngine({ content, actionHandlers: handlers });
  const state = structuredClone(initial); state.phase = phase;
  state.scenario.triggers = [{ id: 'test-after', timing: 'afterAction',
    condition: { type: 'eventCount', event: 'scenarioSignal', op: 'gte', value: 2 },
    effects: [{ type: 'setVariable', key: 'afterRan', value: true }, { type: 'emitSignal', signal: 'test.after' }] }];
  const before = structuredClone(state); let events: any[] = []; let error: string | null = null;
  const action = { kind: mode === 'unknown' ? 'missing-order' : 'test-order' } as any;
  try { events = [...engine.dispatch(state, action)]; } catch (caught) { error = (caught as Error).message; }
  cases.push({ phase, during, mode, before, action, events, error, after: state, hash: hashState(state) });
 }
}
writeFileSync(process.argv[2], JSON.stringify({ packs, keys: CoreActionHandlers.keys(), cases }));
