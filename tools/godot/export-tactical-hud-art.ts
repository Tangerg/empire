import {readFileSync,writeFileSync,mkdirSync,copyFileSync,existsSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {createHash} from 'node:crypto';
import {CANDIDATE_01_ART} from '../../packages/story-candidate-01/src/presentation/index';
import {CANDIDATE_01_ACTOR_ART,resolveCandidate01PresentationBinding} from '../../packages/story-candidate-01/src/presentation/candidate-01-actor-art';
import {CANDIDATE_01_UNIT_ART,CANDIDATE_01_WEAPON_ART,CANDIDATE_01_ABILITY_ART,CANDIDATE_01_STATUS_ART} from '../../packages/story-candidate-01/src/presentation/candidate-01-bindings';
import {candidate01AssetUrl} from '../../packages/story-candidate-01/src/presentation/candidate-01-assets';
const prefix='/packages/story-candidate-01/assets/final-fantasy-v1/';
function image(source:string){if(!source.startsWith(prefix))throw new Error('Unknown HUD art source '+source);const relative=source.slice(prefix.length);const input=resolve('.'+source);const output=resolve('godot/assets',relative);mkdirSync(dirname(output),{recursive:true});if(!existsSync(output))copyFileSync(input,output);else if(!readFileSync(input).equals(readFileSync(output)))throw new Error("Existing HUD art differs from source: "+output);return {source,resourcePath:'res://assets/'+relative,sha256:createHash('sha256').update(readFileSync(input)).digest('hex')};}
const portraits:any={};
for(const [type,actorKey] of [...Object.keys(CANDIDATE_01_UNIT_ART).map(type=>[type,undefined]),...[...CANDIDATE_01_ACTOR_ART].map(([key,binding])=>[binding.expectedUnitType,key])] as [string,string|undefined][]){
 const context=actorKey?{actorKey}:undefined;const binding=resolveCandidate01PresentationBinding(CANDIDATE_01_ACTOR_ART,type,context);
 const markup=CANDIDATE_01_ART.resolve(p=>p.portraitMarkup?.(type,'#224466',context));if(!markup)throw new Error('Missing source portrait '+type);
 const card=binding.characterTopicId?image(candidate01AssetUrl(binding.characterTopicId)):null;
 if(card&&!markup.includes(card.source))throw new Error('Source portrait did not use declared card');
 if(!card&&!markup.includes('candidate-portrait-image'))throw new Error('Missing source frame portrait');
 portraits[actorKey??type]={unitType:type,actorKey:actorKey??null,card};
}
const icons:any={};for(const [kind,bindings] of [['weapon',CANDIDATE_01_WEAPON_ART],['ability',CANDIDATE_01_ABILITY_ART],['status',CANDIDATE_01_STATUS_ART]] as const)for(const [id,topic] of Object.entries(bindings)){const source=candidate01AssetUrl(topic);const markup=CANDIDATE_01_ART.resolve(p=>kind==='weapon'?p.weaponIcon?.(id):kind==='ability'?p.abilityIcon?.(id):p.statusIcon?.(id));if(!markup?.includes(source))throw new Error('Icon mismatch '+id);icons[kind+':'+id]=image(source);}
writeFileSync('godot/data/tactical-hud-art.json',JSON.stringify({schema:1,portraits,icons,sourceIconKeys:Object.keys(icons)},null,2)+'\n');
console.log(`Exported source HUD art: ${Object.keys(portraits).length} portraits, ${Object.keys(icons).length} icons.`);
