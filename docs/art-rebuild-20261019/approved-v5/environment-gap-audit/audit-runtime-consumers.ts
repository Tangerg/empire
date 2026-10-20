import { readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { content, engine, CANDIDATE_01_ART } from '/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019/tools/board-harness';
import { CANDIDATE_01_LEVELS } from '/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019/packages/story-candidate-01/src/index';
import { candidateReviewViewport } from '/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019/tools/art/candidate_review_viewport';
import { candidateMaterial } from '/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019/packages/story-candidate-01/src/presentation/candidate-01-terrain-materials';
import { candidate01EnvironmentScene } from '/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019/packages/story-candidate-01/src/presentation/candidate-01-environment';
const root='/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019';
const output='/workspace/shared/empire-art-rebuild/approved-v5/environment-gap-audit';
const env=JSON.parse(readFileSync(root+'/packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json','utf8'));
const atlasByFile=new Map(env.atlases.map(a=>[a.png2x.split('/').pop(),a]));
const sha=(s:any)=>createHash('sha256').update(typeof s==='string'||Buffer.isBuffer(s)?s:JSON.stringify(s)).digest('hex');
const levels=[];
for(const level of CANDIDATE_01_LEVELS){
 const state=engine.createState(level);const before=sha(state);
 const context={content,levelId:level.id,map:state.map,viewport:candidateReviewViewport(state.map)};
 const layers=CANDIDATE_01_ART.presentation.sceneLayers(context);
 const projectionSha256={all:sha(layers),...Object.fromEntries(Object.entries(layers).map(([key,value])=>[key,sha(value)])),editor:sha(CANDIDATE_01_ART.presentation.sceneLayers({...context,editing:true}))};
 const occurrences=[];const imageCounts={}; const worldConsumers={};const terrainCells={}; const wallCells=[];
 for(let y=0;y<state.map.height;y++)for(let x=0;x<state.map.width;x++){
   const id=state.map.tiles[y*state.map.width+x]; terrainCells[id]=(terrainCells[id]??0)+1;
   if(id==='wall'){
    const mask=[[0,-1],[1,0],[0,1],[-1,0]].reduce((m,[dx,dy],i)=>m|((x+dx>=0&&x+dx<state.map.width&&y+dy>=0&&y+dy<state.map.height&&state.map.tiles[(y+dy)*state.map.width+x+dx]==='wall')?1<<i:0),0);
    wallCells.push({cell:[x,y],mask});
   }
 }
 for(const [layer,pieces] of Object.entries(layers))for(const [index,piece] of pieces.entries()){
  const element=document.createElementNS('http://www.w3.org/2000/svg','g');element.innerHTML=piece.markup;
  for(const image of element.querySelectorAll('image')){
   const href=image.getAttribute('href')??'';const file=href.split('/').pop().split('?')[0];
   imageCounts[file]=(imageCounts[file]??0)+1;
   const a:any=atlasByFile.get(file);
   if(a){
    const ix=Math.round(-Number(image.getAttribute('x'))/a.cellWidth)+Math.round(-Number(image.getAttribute('y'))/a.cellHeight)*a.columns;
    const cell=a.cells?.[ix];const prop=element.querySelector('.candidate-environment-prop');
    occurrences.push({layer,index,atlas:a.id,cellIndex:ix,cellId:cell?.id??a.cellOrder?.[ix],origin:[piece.x,piece.y],...(piece.foot?{foot:piece.foot}:{}),...(prop?{transform:prop.getAttribute('transform')}:{}),href});
   }
   if(href.includes('/world-materials-v1/')&&!file.includes('masks')){
    const key=file.replace('@2x.png','');worldConsumers[key]=(worldConsumers[key]??0)+1;
   }
  }
 }
 if(sha(state)!==before)throw new Error('mutated battle');
 levels.push({id:level.id,name:level.name,size:[state.map.width,state.map.height],stateSha256:before,projectionSha256,levelSha256:sha(level),terrainCells,roadCorridor:!!candidate01EnvironmentScene(level.id)?.roadCorridor,layerCounts:Object.fromEntries(Object.entries(layers).map(([k,v])=>[k,v.length])),imageCounts,worldConsumers,wallCells,occurrences});
}
const sourcePaths=['packages/story-candidate-01/src/levels.ts','packages/story-candidate-01/src/presentation/candidate-01-map-scene.ts','packages/story-candidate-01/src/presentation/candidate-01-terrain-materials.ts','packages/story-candidate-01/src/presentation/candidate-01-crossing.ts','packages/story-candidate-01/src/presentation/candidate-01-world-material.ts','tools/art/environment-overrides.json','tools/art/connector-overrides.json','docs/art-rebuild-20261019/approved-v5/progress.json','packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json'];
writeFileSync(output+'/runtime-consumers.json',JSON.stringify({evidence:'Current pure production sceneLayers projection for all 16 shipped levels. Initial authored map state; no browser claim, no gameplay mutation, no test suite run.',sources:Object.fromEntries(sourcePaths.map(p=>[p,sha(readFileSync(root+'/'+p))])),levels},null,2)+'\n');
console.log(JSON.stringify(levels.map(l=>({id:l.id,ground:l.worldConsumers,emittedAtlases:Object.fromEntries([...new Set(l.occurrences.map(o=>o.atlas))].map(a=>[a,l.occurrences.filter(o=>o.atlas===a).length])),walls:l.wallCells.length})),null,2));
