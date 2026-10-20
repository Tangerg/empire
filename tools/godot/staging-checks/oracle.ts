import { readFileSync, writeFileSync } from 'node:fs';
import ts from 'typescript';
import { JSDOM } from 'jsdom';
import { CampaignRuntime, type CampaignState } from '../../../packages/campaign-engine/src/index';
import { ContentPackInstaller, createContentCatalog, type ContentPack } from '../../../packages/battle-engine/src/index';
import { candidate01CampaignAdapter, CANDIDATE_01_ART } from '../../../packages/story-candidate-01/src/presentation/index';
import { portraitSvg } from '../../../packages/game-ui/src/art/portraits';
import { PAL } from '../../../packages/game-ui/src/art/palette';
import { escapeHtml } from '../../../packages/game-ui/src/ui/html';

const output = process.argv[2];
if (!output) throw new Error('Usage: oracle.ts <oracle.json>');
const adapter = candidate01CampaignAdapter();
const packs: ContentPack[] = JSON.parse(readFileSync('godot/data/content-packs.json', 'utf8'));
const content = createContentCatalog(); new ContentPackInstaller(content).install(...packs);
const nodes = adapter.definition.nodes.filter(node => node.type === 'battle');
const art = (source: string) => ({ source, resourcePath: null });
const metadata = {
  schema: 1, title: adapter.title, battleTotal: nodes.length, joinAfter: adapter.joinAfter,
  resourceLabels: adapter.resourceLabels, relationLabels: adapter.relationLabels,
  portraits: Object.fromEntries(Object.entries(adapter.portraits).map(([id, source]) => [id, art(source)])),
  levels: Object.fromEntries(nodes.map(node => {
    const level = adapter.level(node.level), chapter = adapter.chapterOf(level), briefingId = adapter.briefingId(level);
    const story = adapter.story(briefingId);
    return [node.level, { chapter, order: adapter.levelOrder(level), chapterTitle: adapter.chapterTitle(chapter), briefingId,
      chapterArt: art(adapter.chapterArt(chapter)), briefingArt: story.scene === null ? null : art(adapter.storyArt(story.scene)) }];
  })),
};
const file = 'packages/game-ui/src/ui/campaign-game.ts';
const source = ts.createSourceFile(file, readFileSync(file, 'utf8'), ts.ScriptTarget.Latest, true);
const declaration = source.statements.find(node => ts.isClassDeclaration(node) && node.name?.text === 'StoryCampaignController');
if (!declaration || !ts.isClassDeclaration(declaration)) throw new Error('Missing StoryCampaignController source');
const methods = ['shell', 'renderBattleStaging'].map(name => {
  const method = declaration.members.find(node => ts.isMethodDeclaration(node) && node.name.getText(source) === name);
  if (!method) throw new Error(`Missing source method ${name}`);
  return method.getText(source);
});
const javascript = ts.transpileModule(`class SourceStaging { ${methods.join('\n')} }`, {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.None },
}).outputText;
const prototype = new Function('escapeHtml', 'portraitSvg', 'PAL', `${javascript}\nreturn SourceStaging.prototype;`)(escapeHtml, portraitSvg, PAL) as {
  shell(this: unknown, html: string, chapter: number): void;
  renderBattleStaging(this: unknown, levelId: string): void;
};
const initial = new CampaignRuntime(adapter.definition).snapshot();
const cases: unknown[] = [];
function record(name: string, state: CampaignState, levelId: string, pending: boolean, mutateNode = false): void {
  const original = nodes.find(node => node.level === levelId)!;
  const node = structuredClone(original);
  if (mutateNode && node.rosterBindings) node.rosterBindings = node.rosterBindings.filter(binding => binding.campaignUnit !== 'torren');
  state.currentNode = node.id;
  const definition = { ...adapter.definition, nodes: adapter.definition.nodes.map(candidate => candidate.id === node.id ? node : candidate) };
  const root = new JSDOM('<div id="root"></div>').window.document.getElementById('root')!;
  const receiver = { adapter: { ...adapter, definition }, content, runtime: { state }, options: { art: CANDIDATE_01_ART },
    root, pendingRequest: pending ? {} : null, battleTotal: nodes.length, shell: prototype.shell };
  prototype.renderBattleStaging.call(receiver, levelId);
  const text = (selector: string) => root.querySelector(selector)!.textContent;
  const resourceNodes = [...root.querySelector('.resource-row')!.children];
  const resources = resourceNodes.filter(node => node.tagName === 'B').map(node => ({ label: node.previousElementSibling!.textContent, amount: node.textContent }));
  const relations = [...root.querySelectorAll('.relation-list > div')].map(node => ({ label: node.querySelector('span')!.textContent,
    amount: node.querySelector('b')!.textContent, fill: Number((node.querySelector('i') as HTMLElement).style.getPropertyValue('--relation')) }));
  const expected = {
    campaignTitle: text('.campaign-topbar > div > span'), chapterHeading: text('.campaign-topbar strong'), progress: text('.campaign-progress b'),
    eyebrow: text('.staging-hero .campaign-eyebrow'), title: text('.staging-hero h1'), description: text('.staging-hero p'),
    actionNote: text('.staging-actions span'), startLabel: text('.staging-actions button'),
    roster: [...root.querySelectorAll('.campaign-unit')].map(node => ({ name: node.querySelector('b')!.textContent, status: node.querySelector('small')!.textContent })),
    resources, relations,
    chapterSource: root.querySelector('.campaign-backdrop-art')!.getAttribute('src'),
    briefingSource: root.querySelector('.staging-hero > img')?.getAttribute('src') ?? null,
  };
  cases.push({ name, state, node, level: adapter.level(levelId), pending, expected });
}
for (const node of nodes) for (const pending of [false, true])
  record(`${node.level} pending=${pending}`, structuredClone(initial), node.level, pending);
for (const completed of [0, 2, 3, 4, 6, 7, 8, 9, 10, 15]) {
  const state = structuredClone(initial);
  state.battleHistory = Array.from({ length: completed }, () => ({})) as CampaignState['battleHistory'];
  record(`join thresholds ${completed}`, state, 'c01-01', false);
}
const mixed = structuredClone(initial);
mixed.battleHistory = Array.from({ length: 10 }, () => ({})) as CampaignState['battleHistory'];
const units = Object.values(mixed.roster);
units[0].hpRatio = 0.005; units[0].rank = 2;
units[1].hpRatio = 0.995; units[1].rank = 1;
units[2].disposition = 'fallen';
mixed.resources = { supplies: 0, custom: 12.5, treasury: -1 };
mixed.relations = { cain: 0, mirelle: -7, unknown: 2, tasha: -0.5, refugees: 3, silverwood: 8, 'mountain-forge': 1 };
record('status labels rounding first-five relations and unknown ids', mixed, 'c01-01', true);
const empty = structuredClone(initial); empty.resources = {}; empty.relations = {};
record('empty campaign state labels', empty, 'c01-01', false);
record('unbound actor receives generic art', structuredClone(initial), 'c01-01', true, true);
writeFileSync(output, JSON.stringify({ metadata, unitNames: Object.fromEntries(content.units.keys().map(id => [id, content.units.get(id).name])), cases }) + '\n');
console.log(`SOURCE ${cases.length} staging renders from current StoryCampaignController and candidate adapter`);
