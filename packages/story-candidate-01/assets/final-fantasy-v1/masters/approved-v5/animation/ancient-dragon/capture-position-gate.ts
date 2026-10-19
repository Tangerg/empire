import {writeFileSync} from 'node:fs';
import {content,engine,shippedLevel,CANDIDATE_01_ART,emptyOverlay} from '/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019/tools/board-harness.ts';
import {unitPicture,facesLeft,BoardView,SvgBoardSurface} from '/workspace/scratch/84c9fe948755/empire-art-rebuild-20261019/packages/game-ui/src/index.ts';
const level=shippedLevel('three-bridges'),state=engine.createState(level),grid=engine.rules.grids.get('square4');
const actors=state.units.map(u=>({id:u.id,type:u.type,owner:u.owner,cell:[u.x,u.y],facing:u.facing,left:facesLeft(grid,u.facing),worldFoot:[u.x*32+16,u.y*32+31],picture:unitPicture(CANDIDATE_01_ART,content.units.get(u.type),'#3f7fd8',{actorKey:u.key})}));
const layers=[];
const board=new BoardView({content,grid,art:CANDIDATE_01_ART,renderer:scene=>{const s=new SvgBoardSurface(scene);const orig=s.setLayer.bind(s);s.setLayer=(layer,pieces)=>{layers.push({layer,pieces});orig(layer,pieces)};return s}},state,{onTileClick:()=>{},onTileEnter:()=>{},onLeave:()=>{},onSecondary:()=>{},onScale:()=>{}});
board.render(emptyOverlay());
writeFileSync('/workspace/shared/empire-art-rebuild/approved-v5/animation/ancient-dragon/position-gate-actual.json',JSON.stringify({levelId:level.id,actors,layers},null,2));board.dispose();console.log('Captured actual actor pictures and environment placement metadata; no raster scene or repository writes.');
