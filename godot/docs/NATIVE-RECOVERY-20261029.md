# Native recovery and scenery contract checkpoint — 2026-10-29

## Provenance, not a replacement history

The earlier cloud workspace disappeared between runs. Its unpushed local commits
`48387b5727baab38a154dd7afe929ea1a4a5912b`,
`41b750d7b1fb75d70710d15f62638269b21dfac8`, and
`4da9e73db2c8198f9d55b8670086a600c20f7e4d` are historical identifiers, not objects
recovered into this checkout. Their old local logs and saves are also unavailable.

This checkout was freshly cloned from the verified remote
`ef7441768ebc5c58ea955a1363e57793a60900c7`. The narrowly scoped fixes were rebuilt
from the recorded source changes and reviewed again. New checks below belong to this
new checkout; old results are not being relabeled as new execution evidence.

The earlier actual C01 normal-AI victory remains historical acceptance: turn 10,
9 friendly survivors, Roderick and the northern archer lost, followed by normal
settlement at 1/16. Its victory PNG was independently persisted in the user’s Library
(the private file reference is kept outside this public repository). The earlier terminal save and event log
cannot be re-audited here. C01 was not replayed to manufacture replacement evidence.

## Rebuilt fixes

- Equal-Z native tree ordering: a later-drawn opaque actor must receive its visible
  doorway click. The regression verifies Unit → Object → Unit when the native subtree
  order is reversed and restored; it does not globally favor actors.
- A child board's exit cancels playback before its parent screen exits. The screen's
  continuation, exception branch and final HUD refresh respect the board's exit state;
  deferred HUD native-instance access is guarded.
- Opening entrance tests require all four sites and actual opaque building pixels,
  not transparent sprite anchors. The synthetic cliff-cue witness owns a fixed
  in-memory actor shape rather than depending on character-art padding.
- Oath-pad motion tests match the existing 160 ms human linear support-plane movement.
  Popup-action tests await the existing native deferred dispatch. The escort UI fixture
  confirms its preview with W. No gameplay or production input contract was weakened.
- Existing formatter-only drift is corrected mechanically, separately recognizable by
  whitespace-normalized source comparison.

## C04 open-mouth regression

The old fifth probe below shelter `(21,12)` intersects the real neighboring
`camp-guard-1` at `(21,13)`. On the freshly restored PNG, it maps to source
`(251,185.5)` with alpha 253; an empty-ground expectation is false there.

The test now protects both independent contracts:

1. In the unchanged opening, the fixed probe must overlap real guard alpha and no
   foreground building material, and pick exactly `Unit(21,13,guardId)`.
2. A detached renderer-only copy with no units must return exact Ground hits for all
   original six shelters × five probes, including the formerly failing probe.
3. Restoring the unchanged opening must restore the same Unit hit, and the original
   canonical state must remain unchanged.

No probe is deleted, tolerance widened, production picker changed for C04, or battle
state altered to claim a played route. This is detached geometry verification.

## Historical scenery and current registration are separate contracts

The old oracle described a 27×17 C01 with 28 source frame placements. The approved
36×30 native C01 uses assembled ground and explicitly empty frame placements. Mixing
that old state with the new scene also violated the legitimate registration guard.

`SceneryChecks` now makes the boundaries explicit:

- The historical SVG oracle receives its missing original frame declarations as
  source-only inputs. Existing `foreground` and `covers` values are unchanged; removing
  the new `framePlacements` fields reproduces the old oracle exactly. Generator and
  frozen fixture share this shape. Missing vs explicitly empty declarations remains
  significant. Authored and generic frame resource/scale/anchor checks are retained.
- Current C01 integration uses the current engine-created state and
  `BoardSceneRendering.Load`; its assembled-ground identity and explicit empty frame
  contract are checked separately from historical frame counts.
- Cover artwork offsets/sizes/resources/shadows remain compared with the historical
  oracle. The old source foot `(16,31)` is explicitly an art reference, not the current
  geometry contact. Current cover positions and standing Z are independently checked
  against the registered canonical support plane.
- All seven original cover add/change/clear fixtures, no-state-mutation checks and
  resource disposal/rebind checks remain active.

The main native gate now also runs scenery checks with the explicit frozen oracle,
assembled-ground and broad-phase checks, popup UI flow, and lighting-lifetime regression.
A formerly optional stale oracle is no longer silently outside current verification.

## Current execution evidence

Official Godot 4.6.3 .NET (release checksum verified) and .NET SDK 8.0.427 are isolated
under the sibling `empire-tools`. Fresh temporary `XDG_DATA_HOME` directories prevent
save-writing checks from touching actual user saves. Current logs are under the sibling
`empire-c01-evidence-recovery`, not the lost earlier evidence directory.

Focused positive checks passed: C01 painted surface/doorway/cliff, C04 341 assertions,
Oath 123, lighting lifecycle 97, and scenery 8,753. Deliberate negative controls restore
the wrong tree ordering, omit the board-exit guard, leave actors in the empty-floor
fixture, and conflate the historical artwork foot with the current cell contact.
The expanded `bash tools/godot/verify-native.sh` completed on 2026-10-29 at 10:36 UTC
with exit code 0: formatting, metadata/asset validators, both native builds, all sixteen
independent AI/replay/save cases, objective/catalog boundaries and all nineteen native
scenes passed, including C04, scenery and lifecycle. No ERROR/leak diagnostics occurred.
Existing headless HUD pointer-injection tests emitted redundant mouse-enter warnings;
these are not runtime failure or visual acceptance. The full current log is
`verify-native.log` (SHA-256 `40d920b93c14c56c94c818b68d89398e7d17ec2d325fdd51c39becc45c3d1650`).
Manual GUI and export/package acceptance remain separate.

No art, map definitions, battle rules, new chapter, or gameplay balance change belongs
to this checkpoint. Native scene fixtures and independent replay checks are not new
normal-AI playthroughs or fresh user visual approval.
