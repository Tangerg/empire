# C07 normal-AI victory and C08 continuation

## Scope

This adds regression evidence, not gameplay, UI, art or production AI changes. The
campaign definition is cloned with only its start set to `brief-07`, matching the
existing detached C07 contract checks. There is no fabricated C01–C06 history.
Headless model/projection checks do not establish GUI usability, authored scene
completion, frame delivery, full-campaign continuity or balance acceptance.

## Actual played result

The recorded input contains 263 legal orders. Every enemy order (106 total) must
match the unchanged production AI and is executed through `AdvanceComputer`.
Player orders use production `Dispatch`, with revision, action receipt and canonical
state checked after every order. The initial state is `378ff3e0`; C07 reaches its
primary control objective on turn 12, terminal hash `ce680ba7`, with 699 events.
The ordered event SHA-256 is
`6408777b0fc56c4fd2bdd3248c9fa420225d57914e952d2e6b707d845d8241fb`.

The witness player policy uses ordinary queried attacks/healing, then routes
otherwise-waiting allies toward the two authored objectives. It changes no unit,
stat, terrain, turn limit or opponent. An earlier two-militia-only route lost under
normal opposition; that is not evidence of a balance defect. An independent-level
route was also rejected at its initial hash because campaign initialization is
different. The committed witness was regenerated from the real campaign request.

## Settlement and carry-over

The regression checks terminal save/load, no automatic settlement, exactly-once
explicit settlement and rejection of duplicate settlement. Torren remains fallen;
Bran receives the authored 0.3 hero health floor; Elin carries 40 rank progress.
The actual C08 briefing, contract choice and staging are traversed. Both contracts
retain distinct flags, relation/institution effects and funding: paying the debt
costs two treasury and projects 40 fewer starting battle funds. Both branches
round-trip their full campaign and battle state. Expected survivor/new-companion
bindings and carried health, morale, rank, progress and resources are asserted.
C08 opening hashes are `d9e1ef40` (ledger) and `ef5b44a4` (paid debt).

## C08 presentation boundary

The production board projects all 20 actual units without mutating battle state.
It uses a canonical rectangular field where authored ground geometry is absent.
The scene and environment still report `runtimeReady=false`; the ground diagnostic
says the generic perimeter backdrop remains unmigrated. These are incomplete art
signals, not an empty battlefield or a demonstrated transition crash. No acceptance
claim or new progression block is added based on author metadata. No graphical
display was available, so visual/player-interaction acceptance remains open.

## Reproduction

The fixture is `godot/tests/native/c01-07-normal-ai-continuation.json` and runs as
part of `res://Scenes/LuojiaChecks.tscn`, already included in `verify-native.sh`.
Use the repository toolchain (`source tools/godot/env.sh`), build `Empire.csproj`,
and run the scene through `tools/godot/native-process.sh` with
`EMPIRE_CHECK_TIMEOUT_SECONDS=900`. Use an isolated `XDG_DATA_HOME` for generated
fixture saves. The retained route-generator source explains how the witness was
found; the regression itself needs only the committed fixture, not this generator.

The prior complete 31-scene/16-replay aggregate gate passed on production code
`37978970122f07564dd74f93e3b63863a7553f7c` (see PATCH-BOUNDS report). This change
adds only tests/evidence. Its focused gate results are recorded separately below;
they do not claim a fresh complete aggregate run.

## Focused verification

- Godot 4.6.3 mono, final complete `LuojiaChecks` process: exit 0,
  **33,463 assertions**; the native error/leak/exception gate passed.
- Build: zero warnings/errors. Source metadata: 199 unique script UIDs with
  indexed companions. Selected C# whitespace verification and `git diff --check`
  passed. The staged tested C# sources matched the committed sources byte-for-byte.
- Independent read-only review found no material remaining gap after the paid
  branch funds, full campaign save/load and exact roster-binding checks were added.
- Negative control: replace enemy order 14 with `endTurn` in the isolated test
  fixture only. The process exits 1 at the explicit normal-AI equality assertion,
  before false success can be reported. The original fixture was restored and
  verified byte-for-byte against the source fixture. The expected-error log is
  retained separately from the passing final log.

Small build/native/format logs, explicit exit records and route-generator source
are retained in `docs/verification/c07-continuation-20261108/`. The generator
project reference records the original scratch layout; adjust it to the checked-out
`Empire.Core.csproj` if rerunning discovery rather than the committed regression.
