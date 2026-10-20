import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import { JSDOM } from 'jsdom';
import { createBattleEngine } from '../../../packages/battle-engine/src/index';
import {
  CampaignBattleBridge,
  CampaignRuntime,
  type CampaignNode,
  type CampaignState,
} from '../../../packages/campaign-engine/src/index';
import { loadCampaignState } from '../../../packages/game-ui/src/application/campaign-storage';
import {
  StoryCampaignController,
  type StoryCampaignAdapter,
} from '../../../packages/game-ui/src/ui/campaign-game';
import { CANDIDATE_01_CONTENT_PACK } from '../../../packages/story-candidate-01/src/index';
import {
  candidate01CampaignAdapter,
  CANDIDATE_01_ART,
} from '../../../packages/story-candidate-01/src/presentation/index';
import { createTestCatalog } from '../../../packages/test-content/src/index';

interface SourceController {
  readonly root: HTMLElement;
  readonly runtime: CampaignRuntime;
  beat: number;
  readonly disposed: boolean;
  renderStory(presentationId: string, ending: boolean): void;
  renderChoice(presentationId: string): void;
  nextBeat(): void;
  finishCampaign(): void;
}

type Operation = 'nextBeat' | 'finish';
type Status = CampaignState['status'];

const output = process.argv[2] ?? process.env.EMPIRE_NARRATIVE_ORACLE_OUTPUT;
if (!output) throw new Error('Usage: export-oracle.ts <oracle.json>, or set EMPIRE_NARRATIVE_ORACLE_OUTPUT.');

const adapter = candidate01CampaignAdapter();
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const engine = createBattleEngine({ content });
const options = { engine, art: CANDIDATE_01_ART };
const initialState = new CampaignRuntime(adapter.definition).snapshot();
const battleNodes = adapter.definition.nodes.filter(node => node.type === 'battle');
const narrativeNodes = adapter.definition.nodes.filter(node => node.type === 'story' || node.type === 'ending');
const choiceNodes = adapter.definition.nodes.filter(node => node.type === 'choice');
const storyOwners = new Map(narrativeNodes.map(node => [node.presentation, node.id]));
for (const node of battleNodes) {
  const presentationId = adapter.briefingId(adapter.level(node.level));
  if (!storyOwners.has(presentationId)) storyOwners.set(presentationId, node.id);
}
const stories = [...storyOwners.keys()].map(id => ({ id, ...adapter.story(id) }));
const choices = [...new Set(choiceNodes.map(node => node.presentation))]
  .map(id => ({ id, ...adapter.choice(id) }));
assert.equal(stories.length, 35, 'The shipped source story inventory changed.');
assert.equal(stories.reduce((count, story) => count + story.beats.length, 0), 91,
  'The shipped source beat inventory changed.');
assert.equal(choices.length, 11, 'The shipped source choice inventory changed.');

const dom = new JSDOM('<!doctype html><html><body></body></html>', { url: 'https://narrative-fixture.invalid/' });
const globals = { document: dom.window.document, localStorage: dom.window.localStorage };
const originalGlobals = new Map(Object.keys(globals).map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
for (const [key, value] of Object.entries(globals)) {
  Object.defineProperty(globalThis, key, { configurable: true, value });
}

function required(root: ParentNode, selector: string): Element {
  const element = root.querySelector(selector);
  if (!element) throw new Error(`Source narrative render is missing ${selector}.`);
  return element;
}

function text(root: ParentNode, selector: string): string {
  return required(root, selector).textContent?.trim() ?? '';
}

function primary(root: ParentNode) {
  const button = required(root, '.campaign-primary');
  return {
    primaryLabel: button.textContent?.trim() ?? '',
    primaryAction: button.getAttribute('data-campaign-act'),
  };
}

function shell(root: HTMLElement) {
  const progress = required(root, '.campaign-progress') as HTMLElement;
  const fill = required(root, '.campaign-progress i') as HTMLElement;
  const backdropSource = required(root, '.campaign-backdrop-art').getAttribute('src');
  assert.equal(required(root, '.campaign-backdrop-wash').getAttribute('src'), backdropSource);
  return {
    campaignTitle: text(root, '.campaign-topbar > div > span'),
    chapterHeading: text(root, '.campaign-topbar strong'),
    progress: text(root, '.campaign-progress b'),
    progressFraction: Number(fill.style.getPropertyValue('--progress')),
    progressSegments: Number(progress.style.getPropertyValue('--segments')),
    backdropSource,
  };
}

function storyRender(root: HTMLElement) {
  const meta = [...required(root, '.story-meta').children].map(element => element.textContent ?? '');
  assert.equal(meta.length, 3);
  const mission = root.querySelector('.mission-brief');
  return {
    ...shell(root),
    kicker: meta[0],
    date: meta[1],
    location: meta[2],
    title: text(root, '.story-card h1'),
    summary: text(root, '.story-summary'),
    mission: mission ? [...mission.children].map(row => ({
      label: text(row, 'small'), text: text(row, 'b, span'),
    })) : null,
    speaker: text(root, '.dialogue-copy small'),
    text: text(root, '.dialogue-copy p'),
    portraitSource: root.querySelector('.dialogue-portrait')?.getAttribute('src') ?? null,
    narration: required(root, '.dialogue-row').classList.contains('narration'),
    beatDots: [...root.querySelectorAll('.beat-dots i')].map(dot => dot.classList.contains('active')),
    ...primary(root),
  };
}

function choiceRender(root: HTMLElement) {
  return {
    ...shell(root),
    eyebrow: text(root, '.choice-card > .campaign-eyebrow'),
    prompt: text(root, '.choice-card > h1'),
    context: text(root, '.choice-card > p:not(.choice-note)'),
    options: [...root.querySelectorAll('.choice-option')].map(option => ({
      id: option.getAttribute('data-choice'),
      number: text(option, '.choice-number'),
      label: text(option, 'strong'),
      detail: text(option, 'p'),
      consequence: text(option, 'small'),
    })),
    note: text(root, '.choice-note'),
  };
}

function fixture(nodeId: string, status: Status = 'active', selectedAdapter = adapter, seed = initialState) {
  // These are presentation fixtures, not a played campaign or fabricated victories.
  const state = structuredClone(seed);
  state.currentNode = nodeId;
  state.status = status;
  dom.window.localStorage.clear();
  let exitCount = 0;
  const controller = new StoryCampaignController(selectedAdapter, state, () => { exitCount++; }, options);
  const source = controller as unknown as SourceController;
  return { controller, source, state, exits: () => exitCount };
}

const storyCases: unknown[] = [];
const choiceCases: unknown[] = [];
const transitionCases: unknown[] = [];
const lifecycleCases: unknown[] = [];
const projectionCases: unknown[] = [];

function recordStory(presentationId: string, nodeId: string, beatIndex: number, ending: boolean, status: Status) {
  const view = fixture(nodeId, status);
  try {
    view.source.beat = beatIndex;
    const before = view.source.runtime.snapshot();
    view.source.renderStory(presentationId, ending);
    assert.deepEqual(view.source.runtime.snapshot(), before, 'Rendering changed campaign state.');
    assert.equal(dom.window.localStorage.length, 0, 'Rendering persisted campaign state.');
    storyCases.push({
      name: `${presentationId} beat=${beatIndex} ending=${ending} status=${status}`,
      presentationId, nodeId, beatIndex, ending, status, state: view.state,
      expected: storyRender(view.controller.root),
    });
  } finally {
    view.controller.dispose();
  }
}

function transitionSnapshot(view: ReturnType<typeof fixture>) {
  const saved = loadCampaignState(adapter.definition);
  assert.equal(saved.rejected, null, 'Source transition produced an invalid campaign save.');
  const button = view.controller.root.querySelector('.campaign-primary');
  return {
    nodeId: view.source.runtime.state.currentNode,
    status: view.source.runtime.state.status,
    beatIndex: view.source.beat,
    exitCount: view.exits(),
    disposed: view.source.disposed,
    state: view.source.runtime.snapshot(),
    savedState: saved.state,
    primaryLabel: button?.textContent?.trim() ?? null,
    primaryAction: button?.getAttribute('data-campaign-act') ?? null,
  };
}

function recordTransitions(node: CampaignNode, initialBeat: number, status: Status, operations: Operation[]) {
  const view = fixture(node.id, status);
  try {
    view.source.beat = initialBeat;
    if (node.type !== 'story' && node.type !== 'ending') throw new Error(`Not a narrative node: ${node.id}`);
    view.source.renderStory(node.presentation, node.type === 'ending');
    const expected = operations.map(operation => {
      const before = transitionSnapshot(view);
      if (operation === 'nextBeat') view.source.nextBeat();
      else view.source.finishCampaign();
      const after = transitionSnapshot(view);
      if (node.type === 'ending' && operation === 'nextBeat') {
        assert.deepEqual(after, before, 'Ending nextBeat must be a complete no-op.');
      }
      if (node.type === 'ending' && operation === 'finish') {
        if (before.status === 'active') {
          assert.equal(after.status, node.outcome);
          assert.equal(after.exitCount, 0);
          assert.equal(after.disposed, false);
          assert.deepEqual(after.savedState, after.state);
          assert.ok(after.state.completedNodes.includes(node.id));
        } else {
          assert.equal(after.exitCount, before.exitCount + 1);
          assert.equal(after.disposed, true);
          assert.deepEqual(after.state, before.state);
          assert.deepEqual(after.savedState, before.savedState);
        }
      }
      if (node.type === 'story' && operation === 'nextBeat') {
        const count = adapter.story(node.presentation).beats.length;
        if (before.beatIndex < count - 1) {
          assert.equal(after.beatIndex, before.beatIndex + 1);
          assert.deepEqual(after.state, before.state);
          assert.deepEqual(after.savedState, before.savedState);
        } else {
          assert.equal(after.beatIndex, 0);
          assert.equal(after.nodeId, node.next);
          assert.deepEqual(after.savedState, after.state);
        }
      }
      assert.deepEqual(after.state.battleHistory, initialState.battleHistory,
        'Narrative fixtures must not manufacture battle results.');
      return after;
    });
    transitionCases.push({
      name: `${node.id} beat=${initialBeat} status=${status}`,
      nodeId: node.id, initialState: view.state, initialBeat, operations, expected,
    });
  } finally {
    view.controller.dispose();
  }
}

type LifecycleOperation =
  | { kind: 'capture' | 'click'; selector: string }
  | { kind: 'renderStory' | 'renderChoice'; nodeId: string }
  | { kind: 'clickCaptured' | 'unmount' | 'mount' | 'dispose' };

function recordLifecycle(name: string, nodeId: string, operations: LifecycleOperation[], nativeParity = true) {
  const view = fixture(nodeId);
  dom.window.document.body.append(view.controller.root);
  let captured: HTMLElement | null = null;
  const eventErrors: unknown[] = [];
  const onError = (event: ErrorEvent) => {
    eventErrors.push(event.error ?? new Error(event.message));
    event.preventDefault();
  };
  dom.window.addEventListener('error', onError);
  const snapshot = () => ({
    ...transitionSnapshot(view),
    mounted: view.controller.root.isConnected,
    capturedInRoot: captured === null ? null : view.controller.root.contains(captured),
    capturedConnected: captured?.isConnected ?? null,
  });
  try {
    const expected = operations.map(operation => {
      const before = snapshot();
      switch (operation.kind) {
        case 'capture': captured = required(view.controller.root, operation.selector) as HTMLElement; break;
        case 'click': (required(view.controller.root, operation.selector) as HTMLElement).click(); break;
        case 'clickCaptured': {
          assert.ok(captured, 'Lifecycle fixture must first capture a button.');
          const stale = !view.controller.root.contains(captured);
          captured.click();
          if (stale || before.disposed) {
            assert.deepEqual(snapshot(), before, 'A detached or disposed source button responded.');
          } else if (!before.mounted) {
            assert.notDeepEqual(snapshot(), before,
              'Source root removal alone currently preserves its delegated click listener.');
          }
          break;
        }
        case 'renderStory':
        case 'renderChoice': {
          const node = adapter.definition.nodes.find(candidate => candidate.id === operation.nodeId);
          assert.ok(node);
          view.source.runtime.state.currentNode = node.id;
          view.source.beat = 0;
          if (operation.kind === 'renderStory') {
            assert.ok(node.type === 'story' || node.type === 'ending');
            view.source.renderStory(node.presentation, node.type === 'ending');
          } else {
            assert.equal(node.type, 'choice');
            if (node.type !== 'choice') throw new Error('Choice fixture references another node kind.');
            view.source.renderChoice(node.presentation);
          }
          break;
        }
        case 'unmount': view.controller.root.remove(); break;
        case 'mount': dom.window.document.body.append(view.controller.root); break;
        case 'dispose': view.controller.dispose(); break;
      }
      if (eventErrors.length > 0) throw new Error(`Source DOM event failed in ${name}.`, { cause: eventErrors[0] });
      return snapshot();
    });
    lifecycleCases.push({
      name, nodeId, initialState: view.state, initialBeat: 0, initialMounted: true,
      nativeParity,
      interpretation: nativeParity
        ? 'Source delegated-click behavior is a parity target.'
        : 'Source unmounting does not dispose the root listener. Native unmounted/remounted callback rejection is intentionally stronger, not source parity.',
      operations, expected,
    });
  } finally {
    dom.window.removeEventListener('error', onError);
    view.controller.dispose();
    view.controller.root.remove();
  }
}

function recordProjection(
  name: string, description: string, selectedAdapter: StoryCampaignAdapter,
  state: CampaignState, presentationId: string, beatIndex = 0,
) {
  const view = fixture(state.currentNode, state.status, selectedAdapter, state);
  try {
    const story = selectedAdapter.story(presentationId);
    view.source.beat = beatIndex;
    view.source.renderStory(presentationId, false);
    assert.deepEqual(view.source.runtime.snapshot(), state, 'A projection fixture changed campaign state.');
    assert.equal(dom.window.localStorage.length, 0, 'A projection fixture persisted campaign state.');
    const art = (source: string) => ({ source, resourcePath: null });
    projectionCases.push({
      name, description, state, story: { id: presentationId, ...story }, beatIndex, ending: false,
      speakers: selectedAdapter.speakerNames,
      presentation: {
        schema: 1, title: selectedAdapter.title, completionLabel: selectedAdapter.completionLabel,
        battleTotal: selectedAdapter.definition.nodes.filter(node => node.type === 'battle').length,
        portraits: Object.fromEntries(Object.entries(selectedAdapter.portraits).map(([id, source]) => [id, art(source)])),
        chapters: { [story.chapter]: { title: selectedAdapter.chapterTitle(story.chapter), art: art(selectedAdapter.chapterArt(story.chapter)) } },
        storyScenes: story.scene === null ? {} : { [story.scene]: art(selectedAdapter.storyArt(story.scene)) },
      },
      expected: storyRender(view.controller.root),
    });
  } finally {
    view.controller.dispose();
  }
}

function recordEdgeCases() {
  const node = narrativeNodes.find(candidate => candidate.type === 'story' && adapter.story(candidate.presentation).beats.length > 1);
  assert.ok(node);
  const choice = choiceNodes[0];
  assert.ok(choice);
  const primarySelector = '.campaign-primary';
  const choiceSelector = '.choice-option';
  const capture = (selector = primarySelector): LifecycleOperation => ({ kind: 'capture', selector });
  const click = (selector = primarySelector): LifecycleOperation => ({ kind: 'click', selector });
  const clickCaptured: LifecycleOperation = { kind: 'clickCaptured' };
  const renderStory: LifecycleOperation = { kind: 'renderStory', nodeId: node.id };
  const renderChoice: LifecycleOperation = { kind: 'renderChoice', nodeId: choice.id };
  recordLifecycle('detached-next-beat-double-click', node.id, [capture(), clickCaptured, clickCaptured]);
  recordLifecycle('old-primary-after-story-rebind', node.id, [capture(), renderStory, clickCaptured, click()]);
  recordLifecycle('old-exit-after-story-rebind', node.id, [
    capture('[data-campaign-act="exit"]'), renderStory, clickCaptured, click('[data-campaign-act="exit"]'),
  ]);
  recordLifecycle('old-story-button-after-choice-rebind', node.id, [capture(), renderChoice, clickCaptured, click(choiceSelector)]);
  recordLifecycle('old-choice-after-choice-rebind', choice.id, [capture(choiceSelector), renderChoice, clickCaptured, click(choiceSelector)]);
  recordLifecycle('old-choice-after-story-rebind', choice.id, [capture(choiceSelector), renderStory, clickCaptured, click()]);
  recordLifecycle('disposed-story-root-does-not-dispatch', node.id, [capture(), { kind: 'dispose' }, clickCaptured, click('[data-campaign-act="exit"]')]);
  recordLifecycle('disposed-choice-root-does-not-dispatch', choice.id, [capture(choiceSelector), { kind: 'dispose' }, clickCaptured]);
  recordLifecycle('unmounted-source-story-root-still-dispatches', node.id, [capture(), { kind: 'unmount' }, clickCaptured], false);
  recordLifecycle('unmounted-source-choice-root-still-dispatches', choice.id, [capture(choiceSelector), { kind: 'unmount' }, clickCaptured], false);
  recordLifecycle('remounted-source-root-keeps-old-listener', node.id, [capture(), { kind: 'unmount' }, { kind: 'mount' }, clickCaptured], false);

  const bridge = new CampaignBattleBridge(adapter.level, content);
  const retreatSeed = structuredClone(initialState);
  retreatSeed.currentNode = battleNodes[0].id;
  const runtime = new CampaignRuntime(adapter.definition, retreatSeed);
  const request = adapter.applyBattleContext(runtime.beginBattle(bridge), runtime.state);
  const battle = engine.createState(request.level);
  runtime.completeBattle(adapter.applyBattleResultPolicy(bridge.result(request, battle, [], 'retreat'), runtime.state));
  assert.equal(runtime.state.battleHistory.length, 1);
  assert.equal(runtime.state.battleHistory[0].outcome, 'retreat');
  const returnedNode = runtime.node();
  assert.equal(returnedNode.type, 'story');
  if (returnedNode.type !== 'story') throw new Error('Retreat fixture did not return to its source briefing.');
  recordProjection('nonzero-progress-after-runtime-retreat',
    'One explicit retreat fixture was recorded through the real runtime and battle bridge; no victory or played battle is claimed.',
    adapter, runtime.snapshot(), returnedNode.presentation);

  const ending = narrativeNodes.find(candidate => candidate.type === 'ending');
  assert.ok(ending);
  const zeroDefinition = { ...adapter.definition, start: ending.id, nodes: [ending] };
  recordProjection('zero-battle-campaign-progress',
    'Controlled valid campaign definition retains one original ending node and no battle nodes.',
    { ...adapter, definition: zeroDefinition }, new CampaignRuntime(zeroDefinition).snapshot(), ending.presentation);

  const state = structuredClone(initialState);
  state.currentNode = node.id;
  const sourceStory = adapter.story(node.presentation);
  const unknownSpeaker = 'oracle-unknown-speaker';
  assert.equal(adapter.speakerNames[unknownSpeaker], undefined);
  assert.equal(adapter.portraits[unknownSpeaker], undefined);
  recordProjection('unknown-speaker-and-null-scene',
    'Controlled source adapter projection replaces only the first speaker key and scene; the source renderer supplies fallback text, narration, and chapter art.',
    { ...adapter, story: id => id === node.presentation ? {
      ...sourceStory, scene: null,
      beats: sourceStory.beats.map((beat, index) => index === 0 ? { ...beat, speaker: unknownSpeaker } : beat),
    } : adapter.story(id) }, state, node.presentation);

  const portraitBeat = sourceStory.beats.findIndex(beat => Boolean(adapter.portraits[beat.speaker]));
  assert.ok(portraitBeat >= 0);
  const speaker = sourceStory.beats[portraitBeat].speaker;
  recordProjection('known-speaker-without-portrait',
    'Controlled adapter removes one existing speaker portrait; narration follows portrait absence, not narrator identity.',
    { ...adapter, portraits: Object.fromEntries(Object.entries(adapter.portraits).filter(([id]) => id !== speaker)) },
    state, node.presentation, portraitBeat);
}

try {
  for (const [presentationId, nodeId] of storyOwners) {
    const story = adapter.story(presentationId);
    for (let beatIndex = 0; beatIndex < story.beats.length; beatIndex++) {
      recordStory(presentationId, nodeId, beatIndex, false, 'active');
    }
  }
  for (const node of narrativeNodes) {
    const count = adapter.story(node.presentation).beats.length;
    if (node.type === 'story') {
      recordTransitions(node, 0, 'active', Array.from({ length: count }, () => 'nextBeat'));
    } else {
      for (const status of ['active', 'completed', 'failed'] as const) {
        for (let beatIndex = 0; beatIndex < count; beatIndex++) {
          recordStory(node.presentation, node.id, beatIndex, true, status);
          recordTransitions(node, beatIndex, status, status === 'active'
            ? ['nextBeat', 'finish', 'nextBeat', 'finish']
            : ['nextBeat', 'finish']);
        }
      }
    }
  }
  for (const node of choiceNodes) {
    const view = fixture(node.id);
    try {
      const before = view.source.runtime.snapshot();
      view.source.renderChoice(node.presentation);
      assert.deepEqual(view.source.runtime.snapshot(), before, 'Rendering a choice changed campaign state.');
      assert.equal(dom.window.localStorage.length, 0, 'Rendering a choice persisted campaign state.');
      choiceCases.push({
        name: node.presentation, presentationId: node.presentation, nodeId: node.id,
        state: view.state, expected: choiceRender(view.controller.root),
      });
    } finally {
      view.controller.dispose();
    }
  }
  recordEdgeCases();
  writeFileSync(output, JSON.stringify({
    schemaVersion: 1,
    fixtureDescription: 'Source-rendered presentation fixtures seeded by CampaignRuntime, then currentNode/status selected explicitly. No battle results are fabricated; this is not a campaign playthrough.',
    definition: adapter.definition,
    sourceDocuments: { speakers: adapter.speakerNames, stories, choices },
    coverage: {
      stories: stories.length,
      beats: stories.reduce((count, story) => count + story.beats.length, 0),
      choices: choices.length,
      endingNodes: narrativeNodes.filter(node => node.type === 'ending').length,
      storyRenders: storyCases.length,
      transitionCases: transitionCases.length,
      lifecycleCases: lifecycleCases.length,
      projectionCases: projectionCases.length,
    },
    storyCases, choiceCases, transitionCases, lifecycleCases, projectionCases,
  }, null, 2) + '\n');
  console.log(`SOURCE ${storyCases.length} story renders, ${choiceCases.length} choices, ${transitionCases.length} transitions, ${lifecycleCases.length} DOM lifecycle and ${projectionCases.length} edge projections from the real StoryCampaignController.`);
} finally {
  for (const [key, descriptor] of originalGlobals) {
    if (descriptor) Object.defineProperty(globalThis, key, descriptor);
    else Reflect.deleteProperty(globalThis, key);
  }
  dom.window.close();
}
