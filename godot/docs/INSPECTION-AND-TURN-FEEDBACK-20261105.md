# Persistent structure inspection and viewport turn feedback

## Player-visible failures

Implementation baseline: `757574c8cb687c6beab46448a12196f1f2fa43f9`.
The two original native reproductions used frozen `6ef0201040df93a26ac4a37fe68a1c3ae543066d`
assemblies. The intervening camera-activation change did not alter their affected
selection, HUD projection, announcement code or formal map data.

In unchanged C01-09, an actual opaque-object click on chain-west at (5,6)
shows the standalone structure facts, but moving the pointer to the dossier
button removes them before the dossier can be read. The controller remains
Idle and the facts depend on transient hover. Chain-north at (7,4) is the
positive control: its capturable Site selection survives the same route.
The initial state digest is `6fefe68e`; neither route commits an action.

In unchanged C01-03, pan to either extreme and end the turn through native E
input. The turn-start receipt exists, but its banner belongs to the transformed
board. At 960×640 the held band's screen Y range is 1064.375–1106.875 at one
extreme and −505.875–−463.375 at the other. At 1180×812 the opposite range is
−333.875–−291.375. Thus all four original cases put the announcement outside
the visible viewport. The same receipt still reaches the journal once.

These are native scene/input, geometry and lifecycle observations using
Godot 4.6.3 .NET, not screenshots or desktop/GPU visual acceptance.

The same complete inspection flow exposed an additional keyboard-entry defect.
After the actual new-campaign/story/choice/StartBattle path, clicking a unit
naturally releases button focus. A native Tab reaches the window but cannot
start HUD traversal. In Godot 4.6, the no-focus search starts from direct
Viewport Control children; the production application mounts the battle beneath
a Node. Existing dossier traversal checks begin with an already-focused control
and therefore do not cover this entry case.

## Ownership and implementation

The tactical selection controller now owns standalone Structure inspection,
using its existing destination coordinate and reset lifecycle. The session's
revision-guarded `HasStructure` query answers canonical presence, including
disabled structures and ruins; it is not an attack-legality query. The HUD
projects current facts at the selected coordinate rather than retaining a
second snapshot. Its dossier entry uses the structure title and offers the
existing Back intent as Close selection. Closing just the dossier preserves
the inspected subject, while Escape/Back, explicit reselection and the existing
dispatch/rebind lifecycle clear or replace it normally.

Visible units, recruitment, sites and reachable movement previews retain their
priority. Pointer Ground and decorative objects are not promoted to units or
structures. Explicit keyboard focus can inspect a known structure, but hidden
units do not become visible, targetable or inspectable through that route.
Target/Tactic/Disembark input still accepts only declared targets.

BattleScreen also owns the explicit first Tab/Shift+Tab transition from map
input into Godot's native focus chain. It only supplies an entry while no control
is focused; subsequent traversal, popup focus and activation remain native.
Modified keys and repeated initial Tab do not start traversal. Escape retains
its existing two-level behavior: release HUD focus first, then Back on the map.

BattleScreen now supplies a full-viewport feedback Control to BoardView as an
explicit dependency. Each playback owns its own world and viewport effect roots;
only the turn announcement uses the viewport root. The original local timing,
34px band, 15px font, colors and fade remain, with normalized horizontal slide
anchors so resizing does not restart the animation. The host is above the map's
live cursor/range layers and below HUD chrome, and its controls neither consume
pointer input nor accept focus.

The original playback clock and ordered EventPresented channel remain the only
owners of time and journal publication. Cancellation releases the captured roots
and settles the old after-state before resolving waiters: a waiter may
synchronously start a replacement, whose state and effects must not be overwritten
by the old playback. Identity checks also fence normal completion and failure.

No authored scene, artwork, battle rule, save format or committed-gameplay
semantics change. This follows Godot's documented dependency injection and
CanvasItem transform ownership:
[scene organization](https://docs.godotengine.org/en/4.6/tutorials/best_practices/scene_organization.html),
[CanvasItem](https://docs.godotengine.org/en/4.6/classes/class_canvasitem.html).

## Verification

- The pure tactical suite passes 540 checks, including explicit Object/Ground
  boundaries, hidden occupants, movement-preview priority, target modes,
  stale revisions, cancellation and keyboard focus. All three isolated selection
  regressions are rejected at their intended behavioral assertions.
- Native C01-09 pointer and keyboard inspection passes 53 isolated assertions;
  the actual application startup/map-to-HUD keyboard route passes 62. Independently
  restoring the hover-owned HUD projection fails at the real pointer handoff;
  restoring the no-op Tab entry fails after the real production map click.
- Native viewport/lifetime coverage passes 123 isolated assertions: all four
  extreme framing cases, one real busy-hover case, resize in each phase,
  ordered-once receipts, refresh, cancellation, reentrant replacement, failure,
  reduced motion, rebind and unmount/reattach. Three independent regressions
  (field-centered Y, early waiter cancellation and lower feedback layer) each
  fail at the intended assertion after successful compilation.
- The first new busy-hover fixture incorrectly assumed that an extreme view of
  the dimetric bounding rectangle contains a pickable cell. Diagnostic counts
  proved it had zero visible interactive centers. The four extreme geometry
  cases remain unchanged; a separate normal-framing case requires a real cursor.
  No production fallback or skipped assertion was introduced.
- Main-tree focused native runs pass: TacticalHudChecks 704 assertions,
  AnimationChecks 967, CampaignNavigationChecks 9114 with zero failed groups.
  These use production commit `795cf868c`; the subsequent test-only commit removes
  a redundant viewport-exit notification between HUD cases. The final aggregate
  rebuild verifies this cleanup and restores the original warning baseline.

Final tested source: `4f77faef7f8fb6bfd9d6ec033c9807c10de87cdc`.

- The exact aggregate command completed uninterrupted from 2026-11-05 02:01:38
  to 02:51:26 UTC (49 minutes 48 seconds), exit 0:
  `EMPIRE_CHECK_TIMEOUT_SECONDS=900 EMPIRE_REPLAY_TIMEOUT_SECONDS=1200 bash -x tools/godot/verify-native.sh`.
- Static validators, formatting/source metadata, builds, 15 core groups, boundary
  suites, editor import and all 30 native scenes pass. All 31 raw native process
  logs pass the strict exit/diagnostic boundary. The 40 warning headers exactly
  match the preceding full gate; there are no new warnings.
- All 16 formal AI workloads retain the baseline's 3425 ordered legal commands,
  action/event/receipt trace hashes and final state digests, including exact
  save/replay round trips. These independent workloads do not claim a full
  carried-state campaign victory or gameplay balance acceptance.
- Final aggregate: HUD 704 assertions, animation 967, campaign navigation
  9114 with zero failed groups. Frame-polling totals can vary between runs.
- ExportRelease configuration build also passes with zero warnings/errors. The
  verified Debug assembly hashes remain unchanged by that build.

This is not a packaged export. Desktop input, screenshots and GPU visual
acceptance remain separate and are not claimed. The final evidence-only commit
changes this report, not the tested runtime, content, checks or build setup.
