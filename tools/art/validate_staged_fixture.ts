/** Validate presentation-only staged cells against the untouched actual level. */
import { writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { createBattleEngine } from '@empire/battle-engine';
import { createState } from '../../packages/battle-engine/src/state';
import { Battlefield } from '../../packages/battle-engine/src/domain/battlefield';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '../../packages/story-candidate-01/src/index';
import { candidate01Level } from '../../packages/story-candidate-01/src/levels';
const content=createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const engine=createBattleEngine({content});
const level=candidate01Level('c01-02');
const state=createState(engine.rules,level);
const before=JSON.stringify(state);
const battlefield=new Battlefield(state,content);
const positions=[{x:8,y:5,owner:1},{x:9,y:6,owner:1},{x:8,y:7,owner:1},{x:13,y:5,owner:2},{x:12,y:6,owner:2},{x:13,y:7,owner:2}];
const validated=positions.map(position=>{
 const cell=battlefield.cellAt(position.x,position.y);
 if(!cell.canReceive('foot'))throw new Error(`Staged cell cannot receive infantry: ${JSON.stringify(position)}`);
 return {...position,presentationFacing:position.owner===2?'left-mirrored':'source-right',terrain:cell.terrain.id,elevation:cell.elevation,canReceiveFoot:true,occupant:cell.occupant?.id??null,structure:cell.structure?.id??null};
});
if(JSON.stringify(state)!==before)throw new Error('Validation mutated actual state');
const fixture={kind:'STAGED-presentation-only-not-initial-deployment-or-observed-gameplay',levelId:'c01-02',camera:[160,64,448,280],proportionSet:'P1',units:validated,actualStateSha256:createHash('sha256').update(before).digest('hex'),stateUnchanged:true,identitySubstitution:'All six actors deliberately use the existing calibration swordsman; owner indicated by blue/red rings; same source pose, right team mirrored through the renderer cell-center facing transform',markerContract:{contact:{cx:16,cy:29.2,rx:12.5,ry:4.2,fill:'#0b100d',opacity:.48},ring:{cx:16,cy:29.5,rx:11,ry:2.25,strokeWidth:1.8,fill:'none'},colors:{1:'#4d86b8',2:'#a8493f'}},limitations:['Selected scenery omissions remain; no added tree/building locations','No actual combat simulation, turn progression or unit stats are represented','All actual bridge rows remain present where they intersect the camera']};
writeFileSync(process.env.STAGED_FIXTURE_OUTPUT!,JSON.stringify(fixture,null,2)+'\n');
console.log(JSON.stringify(validated));
