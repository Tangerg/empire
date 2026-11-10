# Lawful C01 archer-hit replay package

## Main route and real UI inputs

The selected `enemy-west` witness branches from the already accepted archer-shot
`right/before.json`. A new campaign chooses `steady-advance`, then replays the
original first 108 canonical commands and normal opponent AI steps. Every action,
event list and before/after state hash must match the preserved route exactly.
Only the final legal command differs. No state, HP, position, facing, controller,
seed or receipt is patched.

On turn 6, friendly archer 9 (`village-runner`) moves (21,13) → (22,13), then fires
`c01.ranger-bow` at enemy archer 14 (`vanguard-archer`) at (22,16). Enemy 14 faces
west and receives 30 damage, 78 → 48 HP. It naturally counterattacks for 28;
source 9 is facing south and finishes at 50 HP. Both survive. Before state hash
is `e78478df`; after is `7325ab46`. Receipt event SHA256 is
`c919c9f399931e5fe0110198158a004753ddd0c4cd7dea0a7b9c6448ca3d1ce5`.

Copy only the official `enemy-west/before.json` to an isolated QA user-data
folder as `grayflag-campaign-save.json`. Never replace a player's save. After a
fresh normal F9 load:

1. Space eight times selects unit 9. The real ready cycle is 1,3,4,5,6,7,8,9,10.
2. Right moves the logical cursor to (22,13).
3. Enter opens the destination preview and dispatches nothing.
4. Arm native capture (F8 in the existing diagnostic flow) before A.
5. A immediately commits the sole legal target. There is no separate target
   preview or extra confirmation in this path. Do not press Enter afterward.

The production `TacticalSelectionController` was exercised without a GUI. Its
actual emitted order exactly matches the legal engine command. All eleven input
operations, cursor coordinates, modes and dispatch boundaries are retained under
`enemy-west/verification-result.json:uiInputs`. Keyboard arrows follow logical
axes, not projected screen axes. If a HUD control has keyboard focus, Escape
releases that focus before beginning the fresh selection sequence.

This is a legal action/UI-controller proof, not a native-render or visibility
acceptance. Actual bow, body, feet, terrain and overlapping UI must be reviewed
in the native capture. Native inspection found source 9 obscured by a roof after
its move to (22,13). This route remains a lawful input and state witness, but
is not accepted as two-direction recoil footage. Use `southern-bows` for the
open-ground recording without changing map, state or camera behavior.

## Expected presentation clock, not captured observations

The existing production owners predict this 1640 ms sequence for `enemy-west`:

- 0–160 ms: one human movement step.
- 160–480: source 9 shot anticipation.
- 480/520/630/720: enemy 14 recoil frames 0/1/2/3.
- 800: enemy 14 resumes idle. Its independent impact persists to 900.
- 900: enemy 14 starts its ordinary 600 ms counter-shot.
- 1220: counter release; source 9 starts recoil.
- 1220/1260/1370/1460: source 9 recoil frames 0/1/2/3.
- 1500: enemy 14's counter-shot recovery finishes.
- 1540: source 9 resumes idle. Counter impact persists to 1640.
- 1640: the committed action's presentation completes.

These are code-derived boundaries, to compare with a separately recorded native
trace. The main victim uses mirrored recoil (canonical west). The counter victim
uses unmirrored recoil (canonical south). Neither counter temporarily aiming nor
art playback changes canonical facing.

## Bounded alternatives already retained

`friendly-east`: original step 57, enemy shield 16 at (17,6) attacks friendly
archer 6 at (16,6), 27 damage (78 → 51). Archer 6 faces east and counters for 12,
shield 100 → 88. Exact hashes are `05c5843c` → `869d3f58`. This official before is
already an AI turn; no manual human order should be inserted to force it.

`southern-bows`: a previously preserved continuation from legal-cast/heal-right,
not a new route search or edited state. Its first 108 commands match the same
shot route. At step 149, normal AI archer 18 moves (26,20) → (25,20), hits Elin 3
at (22,20) for 39 (78 → 39), then Elin counters for 20 (enemy 78 → 58). Both
are on the southern ground and face east/west. `ui-trigger-before.json` is the
official human checkpoint immediately before step 148 endTurn. A production
controller E emits exactly that endTurn; archer 18 is the first/only ready enemy,
so ordinary AI performs the saved attack. For the native recording, arm capture
on the home screen, then F9-load the official `southern-bows/before.json`.
Normal AI and its existing camera focus perform the attack automatically.
The 110 ms AI move means the same
two-bow interaction itself lasts 1590 ms, excluding the prior turn announcement.

## Correct save comparison after normal AI settles

Loading `southern-bows/before.json` normally executes exactly two AI orders:
step 149 is the archer attack/counter, then step 150 is `endTurn`. Control returns
to player 1 in round 9. Do not stop the AI or inject a human command between them.
The immediate attack `after.json` has hash `883f9cfd`; the complete native settled
save is `ui-settled.json`, hash `dc0736f6`.

The extra endTurn removes Elin's inspired status, adds two command points
(result 3), grants 200 funds (result 1230), regenerates 20 HP on unit 8, and
updates the normal turn/readiness fields. Actual F5 output must therefore be
compared with `ui-settled.json`, not merely the two archers' HP or the immediate
attack after-save. Compare every official campaign, battle, event and metadata
field, omitting only `campaign.savedAt` and `battle.savedAt` in detached comparison
copies. No other field may be normalized or rewritten.

`ui-settled-extra-actions.json` records the one additional endTurn with its
canonical action, complete events and before/after hash. The full 151-command
route is `ui-settled-derivation-actions.json`. `ui-settled-verification.json`
records official settled-save roundtrip and a fresh load of the native before
checkpoint followed by normal AI until the next human turn. Both actions' full
receipts, all state and the complete official settled save must agree.

## What the harness verifies

- Reconstructs official source campaign from normal story/choice operations.
- Matches all preserved prefix commands, hashes and events.
- Uses `AdvanceComputerStep` for normal opponent turns.
- Independently dispatches each committed action through a core loaded from the
  preceding official save, comparing complete state and all receipt events.
- Saves official before/after documents without manual serialization changes.
- Reloads both through `GrayFlagCampaign.Load` and checks complete official save
  equality except writer-generated timestamps in detached comparison copies.
- Reloads every witness before, repeats its actual next human/AI action and checks
  action, state and events equal the original result.
- Checks both participants visible, archer target, positive nonfatal damage.
- Exercises the real tactical selection controller for the main human command.

## Run

Use an existing compiled game assembly directory. The script builds only its
copied scratch harness; it never builds or writes the game. Output must be a new
or empty directory outside the repository.

    bash /path/to/legal-hit/derive.sh --repo-root /path/to/empire-work --assembly-dir /path/to/frozen-assemblies --output /new/empty/output

The current harness passed from working directory `/tmp` as
`legal-final-settled`, using the frozen implementation build for commit
`8893ad5555ab02fbc1fa08b876c5918fb30ced64`. Its scratch harness build completed
with zero warnings/errors, and the full derivation exited 0. `replay.txt` and
`replay.exit` retain the complete run. The source hash in
`primary-witness-comparison.json` matches the copied source actually executed.

Frozen implementation assembly SHA256 values:

- Empire.dll: 827d5a4edf0f3fb076f5d6d17ec2031fff1656e836f16ea5e5120d3b714169fd
- Empire.Core.dll: e757b4cd21b9d0c3a552a1b5b7a803f3573caa097cd9ea1737a34cd6eb9fb476
- GodotSharp.dll: 2142e62894176d7340f50581f8b2fe84e36ae64e1a85f1b2bd7737a7e8a5c8ba

`runtime-assemblies-sha256.txt` identifies these same three assemblies. Their
frozen files and the copies used by the executed harness were independently
checked. No game rebuild or GUI run was performed during this derivation.

The original 22 witness files remain unchanged from the earlier final-build
run: non-save JSON is byte-identical; official saves differ only in
`campaign.savedAt` and `battle.savedAt`. `primary-witness-comparison.json`
records that check. Source/opening documents, full action traces, candidates,
receipts, official save checkpoints and verification JSON are preserved here.
There are no copied binaries, build intermediates or duplicate harness sources.

## Native acceptance and complete save comparison

The main native visual acceptance is the **95-frame southern-bows recording**.
The settlement/roof route is retained only as lawful input/state evidence and
partial visibility evidence. In that route the moved friendly archer is hidden
by the roof, so it is not evidence that both recoil directions were visibly
accepted. The separate native recording and frame review own pixel, contact,
pose and occlusion acceptance; deterministic state comparison alone does not.

The native F5 saves are retained under `native/`. Full-document comparisons pass:

- `native/native-southern-output.json` equals
  `southern-bows/ui-settled.json` after normal AI attack plus endTurn, hash
  `dc0736f6`.
- `native/native-dual-output.json` equals `enemy-west/after.json`, hash
  `7325ab46`.

The sole permitted exclusions are `campaign.savedAt` and `battle.savedAt` in
detached comparison copies. All campaign data, battle state, event history,
metadata, statuses, resources and readiness fields are compared. The archived
native and expected saves themselves are never edited. Rerun this comparison:

    python3 docs/art-archer-hit-20261110/verification/legal-hit/verify-native-saves.py

`native-save-comparison.json` records the result and exact file SHA256 values.
`evidence-sha256.txt` covers this archived package. Final-build legal/state
verification, native visual recording, synthetic animation checks and the full
aggregate gate remain separate evidence; no one result implies the others.
