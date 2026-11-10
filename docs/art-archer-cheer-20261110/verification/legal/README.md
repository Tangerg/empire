# Legal C01 archer victory witness

Verified final-assembly run is archived in this directory. Earlier scratch run-001 was a failed harness attempt: a redundant destination click caused ChooseCommand to emit the one-target action immediately; the harness wrongly waited for another target click. No production defect, rule change or state patch was involved. Final runtime hashes are in runtime-assemblies-sha256.txt. The final harness selects unit 9, chooses its bow, then selects enemy 18.

## Derivation and evidence

Run derive.sh with --repo-root REPO --assembly-dir EXISTING_DLL_DIRECTORY --output NEW_EMPTY_EXTERNAL_DIRECTORY. Set EMPIRE_TOOLCHAIN_ROOT=/workspace/scratch/84c9fe948755/empire-tools if required. Only scratch harness binaries and isolated CLI data are written. Production files are read-only.

The route is the exact 214-action Torren salute route already committed in docs/art-torren-salute-20261110/verification/legal-victory. New campaign, prologue and brief, steady-advance, BeginBattle; normal GrayFlagSession actions and AdvanceComputerStep throughout. Full original setup, source save, all action/event receipts and terminal saves compare exactly, excluding only official writer timestamps. Every actual action is independently dispatched through a core battle loaded from its prior official save, with full resulting state and receipt equality. Official before/after roundtrips and final action replay pass. Production TacticalSelectionController emits the identical action and its dispatched complete official save equals after.json.

Target celebration subject: C01 archer 3, 57 HP, (26,19), east-facing, player/team 1, alive and visible to player 1 before and after. Archer 9 at (27,18) makes the final stationary bow shot against archer 18 at (27,19), killing the last enemy. Actual gameOver team=1 event starts the ordinary celebration cohort. Before digest b4d583f2, after15953326. Terminal event SHA256 d186808dc54cc2ecd2e977ce23ea26ba0f13e4212b586dc3b668a120d5f17c58.

before.json is the official save immediately preceding the final action. after.json and ui-settled.json are complete deterministic terminal documents. receipt.json is the actual terminal receipt. archer-verification.json deliberately records nativeAnimationRegistrationVerified=false: core victory is not evidence of actual native celebration playback.

## Native launch and input

launch-native.sh REPO VERIFIED_WITNESS EXPECTED_ASSEMBLIES NEW_QA_DIRECTORY validates current DLL bytes, isolates XDG save/config/cache directories, copies the official before document, and opens the ordinary NativeCapture scene. It writes native-input.json and, after exit, native-output.json. Native verification completed through the ordinary scene, normal close and complete saved-output comparison.

Use the normal load-save button (F9). From fresh idle selection, five Space cycles select archer9 (48HP). Wait for each fresh settled UI frame. With the pointer stationary, Enter chooses the same-cell stationary Destination; A chooses attack and auto-commits its sole admitted target18. TacticalSelectionController.Cycle sets the canonical unit cursor, Enter invokes same-cell Click, and ChooseCommand's single-target Destination branch emits the exact reference action. No movement is introduced. Alternatively select9, choose its bow without a destination click, then choose target18. The keyboard route avoids overlapping actor pointer hits.

The actual BattleScreen enforces a960x640 minimum. An800x520 launch request affects only the menu; the battle resizes to960x640. The helper therefore explicitly launches960x640. Smaller viewport suggestions are invalid for this application. At960x640, only109 full RGBA frames fit the existing256MiB capture budget. The full terminal transition cannot be captured within that budget: current pre-celebration duration is1560ms (bow release320 + impact420 with recoil320 overlapping + fatal pause40 + fall620 + fade160), plus the winning cohort's maximum celebration duration. Shot recovery600 overlaps and finishes before attackCompleted. A960ms celebration gives2520ms, about152 frames before scheduling overhead.

Use existing F8 during the already-running late death phase to start a selected capture window that includes every celebration phase and idle restoration. This deliberately excludes the prior bow action and earlier death phases; their receipt and deterministic state are proven separately, not visually recorded by that selected window. Do not change production timing, capture stride, budget, controller/state, HP, facing or receipts. Native full-action capture is not claimed. Default load camera places the cluster offscreen, so normal UI camera pan is necessary and must be documented as user-interface navigation, not a state edit.

Native exploration included one accidental zero-event wait onunit7, discarded by F9 reload without saving, and one invalid pointer target that changed no battle state. Neither is accepted witness evidence. Complete output-save equality remains required to distinguish the accepted run.

Compare the saved result with: python3 compare-native.py VERIFIED_WITNESS/ui-settled.json QA/native-output.json. It compares every saved field except campaign.savedAt and battle.savedAt on detached copies, not hashes alone.

## Native proof boundaries

The derivation-only archer-verification.json intentionally records nativeAnimationRegistrationVerified=false because the harness cannot prove rendered playback. Subsequent actual registration is separately established by native/accepted-proof.json and accepted-frames.jsonl. Complete native output equality is separately recorded. Screenshot inspection supplies visibility/occlusion evidence; core visibility and actor registration alone do not prove an unobscured screen image.

## Final native verification

The actual native-output document equals ui-settled.json in every field except the two official savedAt timestamps. F5 saved on the victory result screen; FinishBattle was not invoked. Native input/output, full native.log and complete comparison are preserved under native/.

Accepted motion-20261110-221509-002 contains100 contiguous960x640 frames at stride1, starting during late archer death. Target18 is removed at041. Elin3 and generic archer9 register archer-cheer-v1 at041; authored phases0–5 begin041/047/054/064/080/090, and both restore archer-breathe-v1 at099. Actor position/support plane stays unchanged, cheer scale stays constant and neither is mirrored. Native actor traces use projection-local coordinates, so screen visibility/occlusion comes from screenshot inspection, not raw position bounds. Torren2 salutes simultaneously then restores idle at095.

The initial earlier-start capture exceeded256MiB and saved no incomplete sequence. Its failure is retained honestly in native.log. The accepted later-start capture excludes the earlier shot/death frames while preserving all celebration phases. Parent operation used ordinary keyboard and camera input, with no production timing, stride, buffer or state changes. Full victory snapshot is native-002.png in the companion native evidence.
