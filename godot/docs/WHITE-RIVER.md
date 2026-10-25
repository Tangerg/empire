# White River night crossing

C06 (`c01-06`, 白河夜渡) now uses a complete 1643×957 winter-night mother
painting, registered once to a 50×32 canonical board. Cool silver snow and blue
water contrast with warm bridge lamps and windows. No tiled floor, enlarged blank
margin, duplicate building bodies or baked actors were added.

## Mission owner and preserved contracts

`data/grayflag-campaign-levels.json#c01-06` remains the gameplay owner. Laiya
must survive and **all four** selected refugees/laborers must simultaneously
reach the six-cell `north-bank` zone. Arrival does not withdraw them. Losing one
escort does not invent a new immediate failure; the incomplete objective remains
pending. The thirteen-turn limit is unchanged: T13 remains playable and the
existing limit evaluation occurs on entry to T14. Objectives are evaluated first.

The original one-shot pursuit arrives at T4 (three actors); T6 emits the original
bridge-damage signal. This is a warning signal, **not** physical bridge collapse,
boat transport or a new ford restriction. The existing priority choice and
campaign progression are unchanged. The battle HUD projects `rules.turnLimit`;
story and objective copy refer to that deadline rather than owning another number.
A negative-control test rejects a competing numerical story deadline even when
its narrative expectation is changed to match.

All original actor identities, statistics, AI and mission rules are retained.
Only C06's spatial layout changes; the other fifteen level members are unchanged.
The historical opaque `north-pursuer` key now identifies the west-flank archer;
it is not player-facing directional copy. Southern pursuers and T4 entries stand
on the visible open approach, not foreground pine crowns. Actual campaign roster,
health and resources still depend on earlier choices and casualties.

## Ground and interaction

The board contains 1,134 connected non-naval walkable cells, 213 water cells,
166 blocked foundation/rock cells and 87 outside cells. The three physical bridge
surfaces contain 11 western timber, 18 central stone and 11 eastern timber cells.
Bridge centers are inside reviewed deck polygons; ordinary banks remain banks.
The actual movement owner validates each bank-to-bank route. Houses use complete
positive-area foundation/cell intersections; facade targets sit outside their
closed foundations. Roofs pass clicks through to independent ground.

Five village houses and five open watch/supply shelters preserve the original
neutral capturable-site counts. Ordinary capture, ownership, income and healing
are reachable through the existing interface. Village healing/income are 20/100;
outpost healing/income are 15/0. No invented rescue, production or boat action is
advertised. Open shelter mouths are traversable, not closed building footprints.

`scenes/c01-06-painted-ground.json` binds the canonical level digest and full
image. `docs/art-provenance/white-river-geometry.json` records reviewed physical
polygons and routes; it is not another runtime collision source. Painting bytes
are verified by the static validator. The generated source painting is preserved independently of runtime captures.

## Verification scope

- Static validator and seven semantic mutation probes passed: complete masks,
  actual decks, connected ground, preserved escort/timer/waves and source hashes.
- Godot 4.6.3 .NET import and .NET8 compilation passed (zero warnings/errors).
- Dedicated native checks cover production rendering/picking, all ten actual
  captures, capture-save round trips, ownership-specific healing/income, all three
  crossings, blocked water/foundations, deadline HUD mutation, T4/T6/T14 timing,
  strict retired-save rejection and current-save round trips.
- From the unchanged opening, all four escorts complete the primary objective at
  T7 through 27 legal movement/wait commands. Enemy sides only hand off in this
  focused test: this proves route feasibility, **not normal-AI combat balance**.

The retired 24×14 battle snapshot is incompatible and must be rejected atomically.
Campaign-only saves before this battle remain the supported entry. Detached GUI
fixtures explicitly contain no earlier victories. Full campaign carry-over and
exported-package acceptance remain separate from the normal-AI run below.

### Actual GUI checkpoint, 2026-10-25

The current source was opened on the cloud computer desktop using an isolated manual
application save. Default and wider views were inspected; the HUD showed 1/13 and
0/4. The second refugee moved normally from (24,19) to (24,17), on the central
bridge approach. The normal enemy AI then completed turn one. At human T2 all
21 units remained; Laiya had 116 HP and the four escorts retained 58/58/58/70 HP.
This is an opening-play check, not a complete combat victory. F5→observed F9→F5
preserved the entire campaign, played battle and 42-event sequence except new
save timestamps. The engine ran via software OpenGL; V-Sync was unavailable and
audio fell back to the dummy driver. The unrelated environment catalog still
reports its existing runtimeReady=false diagnostic; C06's own scene is ready.

The focused suite passed 425 assertions (431 when explicitly exporting GUI
fixtures); narrative passed 3303. Whitespace formatting and 289 unique script UID
checks passed. Full aggregate results are recorded separately when completed.

### Published C06 aggregate gate

`bash tools/godot/verify-native.sh` completed with exit 0 on 2026-10-25 after
the C06 scene changes (commit 9c4ce038): formatting/metadata and three static validators; both builds;
all 16 independent native AI battles with exact save/replay; objective/outcome 91
and catalog 76 assertions; all 13 native presentation/application scenes including
C06 425, narrative 3303, camera 222, tactical HUD 125, result-flow 153 and navigation 288.
Expected malformed-save/storage rejection warnings are exercised by the navigation
suite; no process/teardown gate failed. C06's generic AI-versus-AI replay ended
after 130 legal actions with team 2 winning by elimination. That is a deterministic
replay result, **not** a successful escort or a human-play balance acceptance.

### Natural normal-AI escort result, 2026-10-25

The published C06 source was played normally from its actual T2 GUI checkpoint
through **T8 objective victory** on the cloud computer desktop. No battle-state
edits, teleports, suppressed AI phases or casualty reloads were used. All four
escorts arrived simultaneously at full 58/58/58/70 HP; Laiya survived at 116 HP.
Eight allies and nine enemies remained, with 35 attack/counter engagements.
The four friendly battle deaths were Mirelle, Torren and both foot guards;
Elin routed rather than died. This was a successful withdrawal with losses,
not a no-casualty balance claim. Both T4 pursuit and T6 warning fired normally.

The terminal application save contains 405 events and objectiveCompleted/team 1.
Actual F5→F9→F5 preserved the entire campaign, battle and event sequence except
save timestamps. Normal settlement and both aftermath lines then advanced to
brief-07, with battle_06_won and exactly one victory in this detached campaign.
Torren remains fallen, Elin routed; existing hero-recovery rules return Mirelle
as available at 0.3 HP ratio. This does not certify a C01–C06 carry-over campaign.

Evidence is retained outside the source tree in empire-final-evidence/c06:
natural-t3 through natural-t8, natural-victory-t8 and its reloaded comparison,
natural-settlement-t8, natural-aftermath-saved, and native captures 004/005.
The actual settlement screenshot is Library libfile_d62e49127f2c8191a7a5de528a710f9d.

The run exposed a presentation issue: goal-area actors at (26,5)
and (27,6) can be fully hidden by painted roof/foliage occlusion. Shared picking
correctly follows visible pixels, so clicking concealed ground does not select
the hidden actor; keyboard selection remains available. The terminal result
above was secured before isolated diagnosis.

### Shared occlusion-cue acceptance, 2026-10-25

The shared board now gives mostly concealed render-visible units a compact
owner-colored pawn badge and leader. The filled badge is an explicit unit hit;
the original body keeps its depth. No mission, collision, map, unit or save rule
changed. The painted-interaction contract describes placement and crowded-case
diagnostics; this is not a guarantee of arbitrary-density marker packing.

The same genuine T8 checkpoint was loaded in an isolated GUI profile. Both
previously hidden refugees were mouse-selected through their distinct badges,
showing the canonical refugee HUD. The red-house right facade still selected
the neutral village with 20-per-turn healing; a shelter roof click still selected
forest ground. After zooming and panning, the moved badge still selected its
refugee. F9 rebuilt the visible cues correctly. F5→F9→F5 preserved the complete
original T8 campaign, battle and all 400 events except both save timestamps.
This is a presentation regression check, not another natural victory run.

Initial GUI testing caught a hidden-screen preparation defect that visible-board
fixtures missed. Construction now uses board-owned visibility, while live picks
still require tree visibility. A hidden-prepare→reveal regression passes; restoring
the incorrect whole-tree construction filter deliberately fails that guard.

Evidence: cue-captures/native-002 (overview), 003/004 (both refugees), 010 (village),
011 (roof), 012 (zoom/pan selection), 013 (load/reveal); cue-after-selection-t8 and
cue-after-reload-t8 saves. Canonical application SHA256 after removing only the two
savedAt values is 0b75761d0620301d45af2002067cf8f943ddd6b106b4d7058203a45ad2321e71.
Actual selected-refugee screenshot: Library libfile_96d95eadc0188191b28e839198e72fc7.

Measured cue-only warm rebuild overhead was approximately 0.27 ms at opening and
1.06 ms at T8. Matched seven-sample full-rebuild medians were 20.42/246.01 ms without
cues and 20.02/239.84 ms with cues; variation overlaps, so this does not claim a
speedup. These are rebuild timings, not frame-rate measurements. Temporary baseline
skips and production stopwatch instrumentation were removed before acceptance;
the explicit read-only test benchmark remains available.

The final cue source passed `bash tools/godot/verify-native.sh` with terminal exit 0:
formatting, 290 script UIDs, all three static validators, both clean builds, all
16 independent AI/save/replay battles and all 13 native scenes. Relevant updated
totals include DynamicState 530 and C06 430; camera 222, HUD 125, result-flow 153
and navigation 288 also passed. The separate animation scene passed 181 on this
same source. Expected malformed-save/storage-rejection warnings remained confined
to their negative navigation fixtures; no process/teardown gate failed.
