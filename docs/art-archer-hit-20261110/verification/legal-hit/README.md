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
in the native capture. Enemy 14 is near the settlement and may be partly occluded.

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
so ordinary AI performs the saved attack. This is available only as a backup if
the preferred route's pixels are obstructed. The 110 ms AI move means the same
two-bow interaction itself lasts 1590 ms, excluding the prior turn announcement.

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

The current final harness passed as `legal-hit-run05` against frozen baseline
assemblies, with no build warnings/errors. `legal-hit-run04` retains the same
main witness and initial opposite-facing backup; run05 adds only the already
known southern continuation. Preliminary scratch runs 01/02 contained harness
compile errors; run03 rejected an unavailable preferred destination. They are
not positive results and do not invalidate the independently complete run04/05.

Frozen baseline assembly SHA256 values:

- Empire.dll: d4c705bd3878370c4263213f351e223efe9ceba376fceca98ea3983bd1349d2a
- Empire.Core.dll: 53249b2974a552dbc691f3c77932bbae41bd8afbf6704a130a09e8a0b06e5e7f
- GodotSharp.dll: 2142e62894176d7340f50581f8b2fe84e36ae64e1a85f1b2bd7737a7e8a5c8ba

Repeat against the final frozen implementation build before promoting any of
these files into the formal reviewed evidence. Final-build gameplay equality is
separate from synthetic animation checks, native visual recording and the full
aggregate gate. None is implied by this harness alone.
