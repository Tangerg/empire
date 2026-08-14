/**
 * Replays every shipped level under several AI profiles and prints the event
 * stream and outcome digest of each run.
 *
 * `AGENTS.md` asks for this by name: "Prove a behaviour-preserving refactor
 * instead of asserting it. Replay every shipped level and compare the event
 * streams and outcome digests." Until now the script that did it lived outside
 * the repository, which meant the repo's own standard of proof depended on a
 * scratch file — and it was lost once already. It is a tool, not a test: a test
 * asserts a property, and this answers "did anything at all change", which is a
 * question you answer with `diff`.
 *
 *   npm run --silent sweep > /tmp/before.txt
 *   ...make the change...
 *   npm run --silent sweep > /tmp/after.txt
 *   diff /tmp/before.txt /tmp/after.txt
 *
 * `--silent` matters: npm's own banner would otherwise be four lines of diff
 * that mean nothing.
 *
 * Empty diff means every shipped battle unfolded action-for-action as before.
 * A non-empty one has to be characterised, not accepted.
 *
 * Composition is explicit here exactly as in an application entry point: the
 * catalog is built and the packs installed, because there is no ambient content
 * to inherit.
 */
import {
  ContentPackInstaller,
  createBattleEngine,
  createContentCatalog,
} from '../packages/battle-engine/src/index';
import { hashState } from '../packages/battle-engine/src/replay';
import { COMMON_CONTENT_PACK } from '../packages/content-common/src/index';
import { ANCIENT_EMPIRES_CONTENT_PACK } from '../packages/content-ancient-empires/src/index';
import { ANCIENT_EMPIRES_LEVELS } from '../packages/content-ancient-empires/src/levels/index';
import {
  CANDIDATE_01_CONTENT_PACK,
  CANDIDATE_01_LEVELS,
} from '../packages/story-candidate-01/src/index';

/**
 * Three profiles, because one is not a sweep.
 *
 * A cautious, a middling and an aggressive army take different routes through
 * the same rules, so a change that only affects units that choose to charge
 * shows up in exactly one of the three.
 */
const AGGRESSIONS = [0.35, 0.58, 0.8];

/**
 * Action cap per run.
 *
 * High enough that every shipped level resolves well inside it, low enough that
 * a rule change which stalls the AI ends the run instead of the sweep. A run
 * that hits the cap says so in its `actions=` line.
 */
const ACTION_LIMIT = 800;

const content = createContentCatalog();
new ContentPackInstaller(content).install(
  COMMON_CONTENT_PACK,
  ANCIENT_EMPIRES_CONTENT_PACK,
  CANDIDATE_01_CONTENT_PACK,
);
const engine = createBattleEngine({ content });

const levels = [...CANDIDATE_01_LEVELS, ...ANCIENT_EMPIRES_LEVELS];
const out: string[] = [];

for (const level of levels) {
  for (const aggression of AGGRESSIONS) {
    out.push(`### ${level.id} @${aggression}`);
    const state = engine.createState(level);
    // Both sides driven by the engine's own AI: a sweep is about the rules, and
    // a human seat would make the run unreproducible.
    for (const player of state.players) {
      player.controller = 'ai';
      player.ai = { aggression };
    }
    let actions = 0;
    while (state.phase !== 'over' && actions < ACTION_LIMIT) {
      const action = engine.chooseAiAction(state);
      const events = engine.dispatch(state, action);
      actions++;
      for (const event of events) out.push(JSON.stringify(event));
    }
    out.push(
      `END ${level.id} winner=${state.winnerTeam} turn=${state.turn} actions=${actions} hash=${hashState(state)} reason=${state.endReason}`,
    );
  }
}

process.stdout.write(out.join('\n'));
