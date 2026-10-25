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
fixtures explicitly contain no earlier victories. Full campaign carry-over,
full normal-AI escort victory and exported-package acceptance are separate gates.

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

### Current aggregate gate

`bash tools/godot/verify-native.sh` completed with exit 0 on 2026-10-25 after
these changes: formatting/metadata and three static validators; both builds;
all 16 independent native AI battles with exact save/replay; objective/outcome 91
and catalog 76 assertions; all 13 native presentation/application scenes including
C06 425, narrative 3303, camera 222, tactical HUD 125, result-flow 153 and navigation 288.
Expected malformed-save/storage rejection warnings are exercised by the navigation
suite; no process/teardown gate failed. C06's generic AI-versus-AI replay ended
after 130 legal actions with team 2 winning by elimination. That is a deterministic
replay result, **not** a successful escort or a human-play balance acceptance.
