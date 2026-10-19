/** Snapshot pure scene projections so a presentation change can prove its scope. */
import { createHash } from 'node:crypto';
import { writeFileSync } from 'node:fs';
import { content, engine, CANDIDATE_01_ART } from '../board-harness';
import { CANDIDATE_01_LEVELS } from '../../packages/story-candidate-01/src/index';
import { candidateReviewViewport } from './candidate_review_viewport';

const hash = (value: unknown) => createHash('sha256').update(JSON.stringify(value)).digest('hex');
const result = CANDIDATE_01_LEVELS.map(level => {
  const state = engine.createState(level);
  const context = { content, levelId: level.id, map: state.map, viewport: candidateReviewViewport(state.map) };
  const stateSha256 = hash(state);
  const layers = CANDIDATE_01_ART.presentation.sceneLayers(context);
  return { id: level.id, levelSha256: hash(level), stateSha256, afterRenderStateSha256: hash(state),
    groundSha256: hash(layers.ground), underUnitsSha256: hash(layers.underUnits), overUnitsSha256: hash(layers.overUnits),
    editorGroundSha256: hash(CANDIDATE_01_ART.presentation.sceneLayers({ ...context, editing: true }).ground) };
});
writeFileSync(process.env.CONTRACT_OUTPUT ?? '/tmp/empire-candidate-scene-contract.json', JSON.stringify(result, null, 2) + '\n');
