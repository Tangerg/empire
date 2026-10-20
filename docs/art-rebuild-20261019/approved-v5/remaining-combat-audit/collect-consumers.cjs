const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
const repo='/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019';
const out=__dirname;
const ts=require(path.join(repo,'node_modules/typescript'));
function evaluate(relative){
 const source=fs.readFileSync(path.join(repo,relative),'utf8');
 const code=ts.transpileModule(source,{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.CommonJS}}).outputText;
 const exports={};const sandbox={exports,structuredClone,require(id){if(id!=='@empire/battle-engine')throw Error('Unexpected runtime import '+id);return {COMMAND_POINTS_RESOURCE:'command_points',FUNDS_RESOURCE:'funds'};}};
 vm.runInNewContext(code,sandbox,{filename:relative});return exports;
}
const c01=evaluate('packages/story-candidate-01/src/levels.ts').CANDIDATE_01_LEVELS;
const bindings=evaluate('packages/story-candidate-01/src/presentation/candidate-01-bindings.ts');
const builtins=fs.readdirSync(path.join(repo,'packages/content-ancient-empires/src/levels')).filter(p=>p.endsWith('.json')).map(p=>JSON.parse(fs.readFileSync(path.join(repo,'packages/content-ancient-empires/src/levels',p),'utf8')));
const placements=[],spawns=[];
function visit(value,level,at,trigger){if(!value||typeof value!=='object')return;if(value.effects&&value.id)trigger=value.id;if(value.type==='spawnUnits')for(const u of value.units)spawns.push({level:level.id,trigger,path:at,reason:value.reason,...u});for(const [k,v]of Object.entries(value))if(k!=='units'||value.type!=='spawnUnits')visit(v,level,at+'.'+k,trigger);}
for(const level of [...builtins,...c01]){for(const u of level.units)placements.push({level:level.id,...u});visit(level.scenario,level,'scenario',null);}
const unitArt=bindings.CANDIDATE_01_UNIT_ART;
for(const record of [...placements,...spawns]){record.art=record.key==='campaign-torren'?{topicId:'C01-UNIT-SWORDSMAN',appearanceId:'torren-stage01'}:unitArt[record.unit]??null;}
const manifest=JSON.parse(fs.readFileSync(path.join(repo,'packages/story-candidate-01/assets/final-fantasy-v1/manifest-final-fantasy-v1.json'),'utf8'));
const topics=manifest.assets.filter(a=>a.category==='combat-unit').map(asset=>{const matching=Object.entries(unitArt).filter(([id,b])=>b.topicId===asset.topicId);const initial=placements.filter(p=>p.art?.topicId===asset.topicId),spawned=spawns.filter(p=>p.art?.topicId===asset.topicId);return {topicId:asset.topicId,domainBindings:matching.map(([unit,art])=>({unit,...art})),initialPlacements:initial,reinforcements:spawned};});
const doc={method:'Read-only evaluation of unchanged C01 levels.ts and binding data using TypeScript transpileModule in isolated VM; runtime import supplies only actual funds/command_points constants. Builtin schema2 JSON units read directly. All scenario trees traversed for spawnUnits; no engine, browser, Vite, builder, Git or repository writes.',builtInLevels:builtins.map(l=>l.id),c01Levels:c01.map(l=>l.id),initialPlacementCount:placements.length,reinforcementCount:spawns.length,topics,namedElin:placements.filter(p=>p.key==='campaign-elin'),unboundDomainPlacements:placements.filter(p=>!p.art)};
fs.writeFileSync(path.join(out,'consumers.json'),JSON.stringify(doc,null,2)+'\n');
console.log(JSON.stringify({builtins:builtins.length,c01:c01.length,initial:placements.length,reinforcements:spawns.length,catalog:topics.length,unused:topics.filter(t=>!t.domainBindings.length).map(t=>t.topicId),ivra:topics.find(t=>t.topicId==='C01-UNIT-IVRA-GROWTH'),elinCount:doc.namedElin.length},null,2));
