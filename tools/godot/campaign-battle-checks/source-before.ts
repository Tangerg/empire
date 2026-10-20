import {readFileSync,writeFileSync} from 'node:fs';
import {ContentPackInstaller,createBattleEngine,createContentCatalog} from '../../../packages/battle-engine/src/index';
const lines=readFileSync(process.argv[2],'utf8').trim().split('\n').map(line=>JSON.parse(line));
const index=Number(process.argv[3]);
const content=createContentCatalog();new ContentPackInstaller(content).install(...JSON.parse(readFileSync('godot/data/content-packs.json','utf8')));
const engine=createBattleEngine({content}),state=engine.createState(lines[0].level);
for(const row of lines.slice(1)){if(row.kind!=='step'||row.index>=index)break;engine.dispatch(state,row.action);}
writeFileSync(process.argv[4],JSON.stringify(state,null,2));
console.log(`Reconstructed original source before-state for action ${index}`);
