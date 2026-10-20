const fs = require('node:fs');
const path = require('node:path');
const root = process.argv[2];
if (!root) throw new Error('Pass the repository root');
const ts = require(path.join(root, 'node_modules/typescript'));
const file = path.join(root, 'packages/story-candidate-01/src/story.ts');
const source = ts.createSourceFile(file, fs.readFileSync(file, 'utf8'), ts.ScriptTarget.Latest, true);
const aliases = new Map();
const rows = [];
function walk(node) {
  if (ts.isVariableDeclaration(node) && node.name.getText(source) === 'SCENES') {
    const obj = ts.isAsExpression(node.initializer) ? node.initializer.expression : node.initializer;
    for (const p of obj.properties) aliases.set(p.name.getText(source), p.initializer.text);
  }
  if (ts.isCallExpression(node) && node.expression.getText(source) === 'p') {
    const a = node.arguments;
    const selector = a[6];
    const topic = ts.isPropertyAccessExpression(selector) ? aliases.get(selector.name.text) : selector.kind === ts.SyntaxKind.NullKeyword ? null : selector.text;
    rows.push({id:a[0].text,chapter:Number(a[1].text),title:a[3].text,location:a[5].text,topic,line:source.getLineAndCharacterOfPosition(node.getStart(source)).line+1});
  }
  ts.forEachChild(node, walk);
}
walk(source);
const council = rows.filter(r => r.topic === 'C01-CH05-S26');
const allowed = new Set(['c01/chapter-three-ending']);
const unexpected = council.filter(r => !allowed.has(r.id));
const result={kind:'read-only AST audit',allStoryCount:rows.length,councilStoryConsumers:council,derivedStagingLevels:council.filter(r=>r.id.startsWith('c01/brief-')).map(r=>r.id.replace('c01/brief-','c01-')),proposedRegression:{expectCouncilConsumers:[...allowed],unexpectedConsumers:unexpected.map(r=>r.id),passes:unexpected.length===0&&council.length===allowed.size}};
console.log(JSON.stringify(result,null,2));
process.exitCode = result.proposedRegression.passes ? 0 : 1;
