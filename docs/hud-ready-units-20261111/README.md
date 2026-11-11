# Ready-unit HUD verification, 2026-11-11

Implementation: `1532052fae86584df30fbc267b4dd8a39af8a650`.
Aggregate registration contract: `3204d655becbb6c41992915c328575ec89debc7b`.
Base: `5dc64dc7ab386e946ef7a5081c4cfa28fff7105c`.

The new read-only navigation button discloses the currently selectable actor
count beside the explicit end-turn control. Its selection cohort and navigation
path are shared with battlefield Space. See the [behavior and design
references](../../godot/docs/READY-UNIT-NAVIGATION.md).

The focused check drives real C01 sessions through native mouse, touch and
keyboard input. Its 90 assertions cover counting, source order and wraparound,
state/event non-mutation, wait/undo, exhaustion, stale callbacks, application
busy state, rebinding, deployment, AI turns, destruction and viewport bounds.
The existing BattleScreen integration and all 1,243 native localization messages
also pass. Compiler output has zero warnings and errors; format and indexed
source-companion checks pass.

`missing-actor-negative.txt` is an intentional failure: inserting `Skip(1)` into
the shared canonical-actor query makes the initial ready count incorrect. The
focused check rejects it. The exact source was restored, rebuilt and rerun; the
successful post-restoration output is `ready-units.txt`.

The gate process self-tests deliberately increased their exact expected calls
from 32 to 33 (one editor import plus 32 scenes), and explicitly require the new
scene. Both timeout-budget and isolated-save-namespace checks remain enforced.

The complete `bash tools/godot/verify-native.sh` run exited 0 on the aggregate
registration commit above. It includes all 16 independent-level save/replays,
32 native scenes, 8,257 animation assertions and 9,810 navigation assertions.
The last played-flow scene completed the first two battles with 415 legal orders,
then 21 orders in the third opening; all 22,261 flow assertions passed. This is
not a third-battle victory or full-campaign acceptance claim.

Reproduce the focused scene after build/import with the command in the behavior
document. Run `bash tools/godot/localization-checks/run.sh` for the independent
catalog suite and `bash tools/godot/verify-native.sh` for the aggregate. The
negative-control log is an expected rejection, not a passing production run.
`manifest.json` records the code revision and SHA-256 of every retained log.

All runs use isolated temporary data. These logs do not establish manual GUI,
physical-device, exported-package, or complete campaign-balance acceptance.
The evidence is text-only. No artwork assets were changed in this round.
