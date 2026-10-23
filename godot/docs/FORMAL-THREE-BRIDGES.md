# Formal c01-02 painted valley reconstruction

This batch reconstructs the formal second campaign battle from published commit
`1eb68e537c70df92fa7f7f48d97a1b807e74473e`. It is not recovery of the unavailable
local-only `671f3454` commit. No new art or first-level scene changes are included.

## Ownership and composition

- `grayflag-campaign-levels.json` remains the canonical gameplay definition.
- `scenes/c01-02.json` explicitly selects the painted ground registration. The
  application validates that registration against the canonical, pre-campaign-context
  level, then composes the normal campaign session, screen, and battle engine.
- Painted registrations hash UTF-8 `EcmaJson.Stringify(level)`, not source-file
  formatting. Both painted samples use this same validation path.
- The whole published 1672 × 941 painting retains uniform scale. Foreground
  polygons reuse pixels from that same image; there is no replacement terrain mesh.
- A logical painted object can explicitly own a map-structure body at its selection cell; material parts do not each create a site.
  The ground owns the painted body; canonical map ownership supplies the dynamic
  banner. `BoardView` must not add another building sprite at an owned body.
- Missing scene files now fail rather than manufacturing an empty scene. Nine
  existing unillustrated levels have explicit metadata; this does not supply
  new authored art for those levels.

## Intentional level changes

The tactical footprint is 41 × 38, following the published three-bridge sample.
All twenty original unit keys/types/owners, both commanders, the three inherited
roster bindings, player resources, story choices, and victory/failure definitions
remain intact. The other fifteen canonical level definitions are unchanged.

The original nine abstract capture cells are replaced by the four visible houses:

| Cell | Canonical terrain | Initial owner |
| --- | --- | --- |
| (7, 9) | c01.outpost | 1 |
| (34, 9) | c01.outpost | 2 |
| (4, 33) | village | neutral |
| (35, 24) | village | neutral |

This is an intentional capture-layout change, not merely a visual reskin.
The existing outpost is not a headquarters and has no production or owner-turn
resource grants. This batch does not add recruitment or sample-only aid/barricade
content to the formal campaign.

The three civilians must still reach `east-safety`; the unmarked ballista must
still be destroyed, and Laiya must survive. Safety now lies on open ground just
beyond the middle stone bridge, not the distant eastern village. Static orthogonal
minimum paths are 23, 22, and 24 cells, approximately eight full movement rounds
before tactical delays. This is not a balance or natural-playthrough claim.
The agricultural fence at (5, 17) is outside the playable footprint.

The bridge promise protects all middle-bridge cells for three complete rounds.
The existing `turnEnd` trigger requires `turn >= 3` and `currentPlayer == 2`, so it
expires after the enemy's third turn and emits its signal once. Rejecting the
promise removes both that rule and its matching signal trigger without removing
unrelated scenario rules.

## Verification boundaries

- The initial reconstruction compiles with .NET SDK 8 and Godot 4.6.3 .NET.
- `bash tools/godot/application-checks/run.sh` includes actual `endTurn` dispatches
  across the promised and no-promise boundaries.
- `CampaignNavigationChecks` extends the explicitly controlled first-level terminal
  fixture through real result/story/choice/staging callbacks into c01-02, checking
  roster inheritance, twenty actors, building ownership, and F5/F9 state equality.
  This fixture is not evidence of a naturally played first-level victory.
- With a real display, that scene accepts
  `--capture-c01-02=/absolute/existing-directory/battle.png` and saves the rendered
  viewport after frame presentation. Headless execution cannot supply that image.
- Full battle balance, a natural second-level victory, macOS runtime, and a fresh
  exported package require separate acceptance. Read the actual run report rather
  than treating the existence of a check as a passed result.

## Open-road starting formation

An initial real-renderer capture exposed actors standing at tree, shrub, or rock
pixels despite traversable logical cells. Their start cells are corrected rather
than preserving those invalid visual positions. The civilians now start at
(12, 10), (13, 12), and (14, 11), on the visible west approach, with static shortest
routes of 23/22/24 cells through the middle bridge. All three still require about
eight full movement rounds before tactical delays. Nearby escorts and two enemy
actors likewise move off obvious vegetation/rock contact points. Unit identities,
mission rules, safety-zone cells, and map terrain are not changed by this refinement.

The native navigation fixture also selects the second civilian and commits its
first open-road escort move through the real battle-screen destination callback,
waits for presentation to finish, then verifies F5/F9 and captures the restored
viewport. This is early interaction evidence, not a complete escort playthrough.
Default framing and overall battle balance still require separate visual acceptance.

## Published-baseline verification limitation

The unchanged opening historical AI oracle fails at action 0 on both this branch
and an independently compiled exact `1eb68e` Core/test/input extraction. Its golden
trace is not rewritten or bypassed in this reconstruction. The standard 300-second
full-Core gate also times out while serially replaying all sixteen levels. Before
the final starting-formation correction, it completed c01-01 through c01-06,
including 364 exact-save/replay actions for c01-02. Those older formation results
are not asserted for the corrected starting positions. The full gate is therefore
not green; focused native GUI and application results must be reported separately.

## Reconstruction run (2026-10-21)

The corrected formation passed the real Linux OpenGL navigation run with a clean
exit, including the formal campaign entry, first civilian move, completed movement
presentation, and F5/F9 restoration. The captured viewport is actual engine output,
not a concept image. The first-level ending was a controlled fixture; no natural
first- or second-level victory is claimed. The run used dummy audio, so audio is
not validated. Expected malformed-save warnings belong to intentional rejection
checks, not successful load cases.

The shared [painted interaction contract](PAINTED-INTERACTION-CONTRACT.md) separates material draw coverage from pointer contours and preserves Ground/Unit/Object subjects. This schema migration does not alter this chapter's canonical battle data.
