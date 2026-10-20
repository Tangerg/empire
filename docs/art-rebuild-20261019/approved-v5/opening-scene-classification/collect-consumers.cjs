const fs = require('node:fs'), vm = require('node:vm'), path = require('node:path');
const root = process.cwd();
const ts = require(path.join(root, 'node_modules/typescript'));
const storyPath = 'packages/story-candidate-01/src/story.ts';
const code = ts.transpileModule(fs.readFileSync(storyPath, 'utf8') + '\nexport const auditStory = STORY;',
  { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS } }).outputText;
const evaluated = {};
vm.runInNewContext(code, { exports: evaluated }, { filename: storyPath });
const story = evaluated.auditStory;
const text = fs.readFileSync(path.join(root, 'packages/story-candidate-01/src/presentation/candidate-01-story.ts'), 'utf8');
const prologue = story.find(s => s.id === 'c01/prologue');
const result = { method: 'Read-only TypeScript transpilation/evaluation of STORY and source table inspection',
  storyCount: story.length, openingSceneConsumers: story.filter(s => s.scene === 'C01-CH01-S00').map(s => s.id),
  prologue: { id: prologue.id, scene: prologue.scene },
  portraitTopics: [...text.matchAll(/^  \w+: candidate01AssetUrl\('([^']+)'\)/gm)].map(m => m[1]),
  chapterTopics: [...text.matchAll(/^  \d+: '([^']+)'/gm)].map(m => m[1]),
  menuTopic: text.match(/CANDIDATE_01_MENU_ART = candidate01AssetUrl\('([^']+)'\)/)[1] };
fs.writeFileSync(path.join(__dirname, 'consumers.json'), JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify(result));
