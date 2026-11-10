# Legal Torren celebration checkpoint

## Native trigger

Load `candidate-001-before.json` through the normal native manual-save slot. This is the original campaign's first battle, human turn 13. Select ranger unit 9 at (27,18) and attack enemy ranger 18 at (27,19), using its normal bow. The submitted action is:

`{"kind":"command","unit":9,"path":[{"x":27,"y":18}],"command":{"ability":"attack","weapon":"c01.ranger-bow","target":{"x":27,"y":19}}}`

It makes no movement. The real attack kills the last opponent, followed by objective completion, defeat of player 2, and `gameOver` with winning team 1. Torren 2 is alive at (21,18), 98 HP, facing south, on team 1, visible before and after. The presentation's normal celebration filter therefore admits him, provided he is drawn in the native viewport. The harness does not supply a synthetic event or drive an animation.

Before state digest: `b4d583f2`. After: `15953326`. Actual event SHA-256: `d186808dc54cc2ecd2e977ce23ea26ba0f13e4212b586dc3b668a120d5f17c58`.

## Provenance

The public application starts a new shipped campaign, advances prologue and brief-01, chooses steady-advance, and calls BeginBattle. Every subsequent action is committed through the real GrayFlagSession. The opponent uses AdvanceComputerStep. This is a legal human strategy, not a default full-AI replay; the final attack is an actual player-executable order, not a claim about what the opponent pacer will automatically choose. The human player follows a deterministic, legal tactical strategy: Torren moves/waits at low-threat queried destinations toward (21,18); remaining orders use the existing AI proposal at aggression 0.58. If a proposed human attack would win after movement, the human instead legally moves and waits. This creates a later stationary terminal strike without modifying rules or state. The preview is a detached core battle loaded from the official save; its hypothetical receipt is never substituted for committed evidence.

The protection goal was legally changed from (23,16) to (21,18) after native inspection found the earlier final cell behind a granary roof. Native inspection confirmed the new final cell is open ground in front of the granary, with room above Torren for the raised sword.

There are 214 actual battle actions. The complete actual action/event/state-hash history is `derivation-actions.json`. Campaign setup actions are `opening-actions.json`. Source, before and after documents are written by GrayFlagCampaign.Save and SaveFile.WriteDocument. No controller, HP, state, RNG, or production file is edited.

## Reproduce

Requires existing .NET 8 and existing Empire.dll, Empire.Core.dll, GodotSharp.dll. No installation or production build occurs.

`./derive.sh --repo-root /absolute/path/to/empire-work --output /new/empty/outside-repository/directory`

Optional `--assembly-dir` selects already compiled assemblies. Output must be new or empty and outside the repository. Only the scratch harness builds; all CLI state and intermediates are inside output. Existing repository-adjacent empire-tools is used if dotnet is not on PATH.

Each actual committed action is independently dispatched from its preceding official save through the core Battle. Complete resulting state and actual events must agree. Both candidate documents are officially loaded and all saved fields round-trip, excluding only freshly generated campaign.savedAt/battle.savedAt timestamps from detached comparison copies. Reloading the before document and submitting the same next normal action reproduces the exact final state and receipt. The terminal guard checks victory, Torren's survival, his winning team, visibility on both sides, and a real gameOver event.

`verification-result.json` records the preserved candidate; `portable-run/verification-result.json` records the successful independent portable rerun, including equality with preserved evidence. `runtime-assemblies-sha256.txt` identifies the already built runtime. `evidence-sha256.txt` identifies preserved evidence bytes. Reruns get fresh official writer timestamps; compare state/event hashes and complete traces, not raw save bytes.

## Limits

This package proves legal derivation and core/application behavior. Separate [native acceptance](../../README.md#actual-victory-playback) now records all 100 frames of the terminal action, automatic salute and idle return, followed by a separate victory-screen capture. The terminal receipt includes attack, guard reaction, death, rank progress, objective changes and defeat before gameOver. The stationary final action fits the normal capture budget without altering mechanics or playback timing. No end-turn-only winning trigger was established.

The portable rerun exited 0 with 0 build warnings and 0 errors. Both repository-local and nonempty output attempts were rejected with exit 2. The portable rerun was launched from `/tmp`, using an external output directory and an already compiled assembly snapshot matching `runtime-assemblies-sha256.txt`. The snapshot was retained only in scratch to avoid concurrent production rebuilding; no DLLs are committed here. `portable-run/` contains the actual scratch-build and derivation logs, machine result and output-guard results. Build intermediates, generated translation lookup and alternate-save timestamp copies are not committed. The package harness itself checks full preserved source/before/after saves (excluding only official writer timestamps), setup actions, terminal receipt and the entire action trace.

## Exact native input bytes

The preserved files are byte-identical to the original verified candidate, not refreshed from the portable rerun:

- Before SHA-256: `4b4da235f178826876b6bfde6b1922d43c62013024d23673aacc8e86e874df77`.
- After SHA-256: `7a611175895adc7ec67c20435cad14836de2574979b9df0069bc3286ac18db30`.
