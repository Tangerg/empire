import { readFileSync, writeFileSync } from 'node:fs';
import ts from 'typescript';
import { IllegalActionError, DomainInvariantError } from '../../../packages/battle-engine/src/domain/errors';
const file = 'packages/game-ui/src/ui/game.ts';
const source = ts.createSourceFile(file, readFileSync(file, 'utf8'), ts.ScriptTarget.Latest, true);
const declaration = source.statements.find(node => ts.isClassDeclaration(node) && node.name?.text === 'GameController') as ts.ClassDeclaration;
const method = declaration.members.find(node => ts.isMethodDeclaration(node) && node.name.getText(source) === 'runAiTurns')!;
const javascript = ts.transpileModule(`class SourceLoop { ${method.getText(source)} }`, {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.None },
}).outputText;
const flush = async () => { for (let i = 0; i < 12; i++) await Promise.resolve(); };
type Action = { kind: string; handsOff?: boolean; refuse?: boolean; invariant?: boolean; human?: boolean; terminal?: boolean };
async function scenario(name: string, actions: Action[], times: number[], settlement = 0, fallbackRefuses = false) {
  let now = 0, selected = 0, committed = 0, human = false, thrown = false;
  const attempts: string[] = [], logs: string[] = [], samples: object[] = [];
  const sleepers: Array<{ at: number; resolve: () => void }> = [];
  let release: (() => void) | undefined;
  const sleep = (ms: number) => new Promise<void>(resolve => sleepers.push({ at: now + ms, resolve }));
  const prototype = new Function('sleep', 'IllegalActionError', `${javascript}; return SourceLoop.prototype;`)(sleep, IllegalActionError);
  const controller = {
    aiRunning: false, busy: false, disposed: false, state: { phase: 'playing' },
    get isHumanInput() { return human; },
    refresh() {}, board: { setState() {} }, pushMessage(message: string) { logs.push(message); },
    async march() {},
    async settle() { if (settlement && !release && now < settlement) await new Promise<void>(resolve => { release = resolve; }); },
    session: {
      chooseAiAction() { return actions[selected++] ?? { kind: 'unexpected', invariant: true }; },
      dispatch(action: Action) {
        attempts.push(action.kind);
        if (action.invariant) throw new DomainInvariantError('broken rule');
        if (action.refuse) throw new IllegalActionError('refused');
        committed++; if (action.human) human = true; if (action.terminal) controller.state.phase = 'over'; return [];
      },
      tryDispatch(action: Action) {
        try { return this.dispatch({ ...action, refuse: fallbackRefuses, human: !fallbackRefuses }); }
        catch (error) { if (error instanceof IllegalActionError) return null; throw error; }
      },
      engine: { actionHandlers: { handsOffTurn(action: Action) { return action.handsOff ?? false; } } },
    },
  };
  const running = prototype.runAiTurns.call(controller).catch(() => { thrown = true; });
  await flush();
  for (const at of times) {
    now = at;
    if (release && at >= settlement) { const resolve = release; release = undefined; resolve(); }
    for (const sleeper of [...sleepers]) if (sleeper.at <= now) { sleepers.splice(sleepers.indexOf(sleeper), 1); sleeper.resolve(); }
    await flush();
    samples.push({ at, committed, busy: controller.busy, attempts: [...attempts], logs: [...logs], thrown });
  }
  controller.disposed = true;
  for (const sleeper of sleepers) sleeper.resolve();
  release?.(); await flush(); await running;
  return { name, actions, settlement, fallbackRefuses, samples };
}
const cases = [];
cases.push(await scenario('normal and handoff after settlement', [{ kind: 'first' }, { kind: 'handoff', handsOff: true, human: true }], [0, 9, 10, 99, 100, 219, 220], 10));
cases.push(await scenario('handoff boundary', [{ kind: 'custom-handoff', handsOff: true, human: true }], [0, 119, 120]));
cases.push(await scenario('terminal pause', [{ kind: 'lethal', terminal: true }], [0, 89, 90]));
cases.push(await scenario('refused attempted nonhandoff', [{ kind: 'bad', refuse: true }], [0, 89, 90]));
cases.push(await scenario('refused attempted handoff', [{ kind: 'bad-handoff', handsOff: true, refuse: true }], [0, 119, 120]));
cases.push(await scenario('refused fallback then recovery', [{ kind: 'bad', refuse: true }, { kind: 'recovered', human: true }], [0, 89, 90, 179, 180], 0, true));
cases.push(await scenario('invariant propagates', [{ kind: 'broken', invariant: true }], [0, 90]));
// Execute the original guard without real-time sleeps.
let guardCalls = 0;
const guarded = {
  aiRunning: false, busy: false, disposed: false, state: { phase: 'playing' }, isHumanInput: false,
  refresh() {}, board: { setState() {} }, async march() {}, async settle() {}, pushMessage() {},
  session: { chooseAiAction() { guardCalls++; return { kind: 'wait' }; }, dispatch() { return []; },
    engine: { actionHandlers: { handsOffTurn() { return false; } } } },
};
const immediate = new Function('sleep', 'IllegalActionError', `${javascript}; return SourceLoop.prototype;`)(async () => {}, IllegalActionError);
await immediate.runAiTurns.call(guarded);
if (guardCalls !== 2000 || guarded.busy) throw new Error('Source loop guard contract changed');
// Disposing during the post-settlement pause must not produce another action.
let cancelCalls = 0, wake: (() => void) | undefined;
const disposed = { ...guarded, session: { ...guarded.session, chooseAiAction() { cancelCalls++; return { kind: 'wait' }; } } };
const cancellable = new Function('sleep', 'IllegalActionError', `${javascript}; return SourceLoop.prototype;`)(
  () => new Promise<void>(resolve => { wake = resolve; }), IllegalActionError);
const pending = cancellable.runAiTurns.call(disposed);
await flush(); disposed.disposed = true; wake?.(); await pending;
if (cancelCalls !== 1 || disposed.busy) throw new Error('Source disposal committed a stale action');
writeFileSync(process.argv[2], JSON.stringify({ cases, guardCalls, cancelCalls }));
