import { readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { content, engine, shippedLevel, CANDIDATE_01_ART } from '/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019/tools/board-harness';
import { candidateReviewViewport } from '/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019/tools/art/candidate_review_viewport';
import { candidate01Asset } from '/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019/packages/story-candidate-01/src/presentation/candidate-01-assets';
const level=shippedLevel('c01-12'); const state=engine.createState(level);const layers=CANDIDATE_01_ART.presentation.sceneLayers({content,levelId:level.id,map:state.map,viewport:candidateReviewViewport(state.map)});
const root='/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019';
const output='/workspace/shared/empire-art-rebuild/approved-v5/environment-gap-audit';
const crossingPieces=layers.underUnits.flatMap((piece,index)=>{
 if(!piece.markup.includes('stone-crossing-horizontal@2x.png')&&!piece.markup.includes('crossings-fortifications@2x.png'))return [];
 return [{index,origin:[piece.x,piece.y],foot:piece.foot??null,markup:piece.markup}];
});
const record=candidate01Asset('C01-STRUCT-SUNKEN-BELL-SLUICE');
const structures=state.structures.map(s=>({state:s,definition:content.structures.get(s.type)}));
writeFileSync(output+'/c01-12-context.json',JSON.stringify({map:{width:state.map.width,height:state.map.height,rows:Array.from({length:state.map.height},(_,y)=>state.map.tiles.slice(y*state.map.width,(y+1)*state.map.width))},units:state.units.map(u=>({id:u.id,key:u.key,type:u.type,cell:[u.x,u.y],facing:u.facing,owner:u.owner})),structures,structureAsset:record,crossingPieces,levelScenario:level.scenario,composites:level.composites,structureSeeds:level.structures},null,2)+'\n');
console.log(JSON.stringify({size:[state.map.width,state.map.height],actors:state.units.length,crossings:crossingPieces.map(p=>({origin:p.origin,foot:p.foot,h:p.markup.includes('stone-crossing-horizontal')})),structures:structures.map(s=>({state:s.state,definition:s.definition})),asset:record},null,2));
