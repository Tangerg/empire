const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
const repo='/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019';
const ts=require(path.join(repo,'node_modules/typescript'));
function evaluate(relative,suffix=''){
 const source=fs.readFileSync(path.join(repo,relative),'utf8')+'\n'+suffix;
 const code=ts.transpileModule(source,{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.CommonJS}}).outputText;
 const exports={};const sandbox={exports,structuredClone,require(id){if(id!=='@empire/battle-engine')throw Error('Unexpected runtime import '+id);return {COMMAND_POINTS_RESOURCE:'command_points',FUNDS_RESOURCE:'funds'};}};
 vm.runInNewContext(code,sandbox,{filename:relative});return exports;
}
const story=evaluate('packages/story-candidate-01/src/story.ts','export const auditStory=STORY; export const auditChoices=CHOICE_STORY; export const auditScenes=SCENES;');
const levels=evaluate('packages/story-candidate-01/src/levels.ts');
const bindings=evaluate('packages/story-candidate-01/src/presentation/candidate-01-bindings.ts');
const builtins=fs.readdirSync(path.join(repo,'packages/content-ancient-empires/src/levels')).filter(p=>p.endsWith('.json')).map(p=>JSON.parse(fs.readFileSync(path.join(repo,'packages/content-ancient-empires/src/levels',p),'utf8')));
const initial=[],reinforcements=[];
function visit(value,level,at,trigger){if(!value||typeof value!=='object')return;if(value.effects&&value.id)trigger=value.id;if(value.type==='spawnUnits')for(const u of value.units)reinforcements.push({level:level.id,trigger,path:at,reason:value.reason,...u});for(const [k,v] of Object.entries(value))if(k!=='units'||value.type!=='spawnUnits')visit(v,level,at+'.'+k,trigger);}
for(const level of [...builtins,...levels.CANDIDATE_01_LEVELS]){for(const unit of level.units)initial.push({level:level.id,...unit});visit(level.scenario,level,'scenario',null);}
const storyCode=fs.readFileSync(path.join(repo,'packages/story-candidate-01/src/presentation/candidate-01-story.ts'),'utf8');
const portraits=Object.fromEntries([...storyCode.matchAll(/^  (\w+): candidate01AssetUrl\('([^']+)'\)/gm)].map(m=>[m[1],m[2]]));
const chapterArt=Object.fromEntries([...storyCode.matchAll(/^  (\d+): '([^']+)'/gm)].map(m=>[m[1],m[2]]));
const campaignCode=fs.readFileSync(path.join(repo,'packages/story-candidate-01/src/campaign.ts'),'utf8');
const roster=[...campaignCode.matchAll(/\{ id: '([^']+)', unitType: '([^']+)', owner: (\d+), rank: (\d+), tags: \[([^\]]+)\] \}/g)].map(m=>({id:m[1],unitType:m[2],owner:Number(m[3]),rank:Number(m[4]),tags:[...m[5].matchAll(/'([^']+)'/g)].map(x=>x[1])}));
const doc={method:'Read-only TypeScript transpile/evaluation of story.ts, levels.ts and bindings.ts; exact source portrait/chapter/roster table extraction; builtin JSON reads. No engine, Git, image generation, source edits or builder execution.',
 stories:story.auditStory.map(s=>({id:s.id,chapter:s.chapter,title:s.title,date:s.date,location:s.location,scene:s.scene,speakers:s.beats.map(b=>b.speaker),mission:s.mission??null})),choices:story.auditChoices.map(s=>({id:s.id,chapter:s.chapter})),sceneAliases:story.auditScenes,storyPortraits:portraits,chapterArt,characterTypeBindings:bindings.CANDIDATE_01_CHARACTER_ART,roster,rosterBindings:levels.CANDIDATE_01_ROSTER_BINDINGS,
 levels:levels.CANDIDATE_01_LEVELS.map(l=>({id:l.id,name:l.name,chapter:l.extra.chapter,order:l.extra.order,briefingId:`c01/brief-${String(l.extra.order).padStart(2,'0')}`})),initialActors:initial,reinforcements};
fs.writeFileSync(path.join(__dirname,'consumers.json'),JSON.stringify(doc,null,2)+'\n');
console.log(JSON.stringify({stories:doc.stories.length,choices:doc.choices.length,sceneTopics:[...new Set(doc.stories.map(s=>s.scene))],portraits:doc.storyPortraits,roster:doc.roster,actors:initial.length,reinforcements:reinforcements.length},null,2));
