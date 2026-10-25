# Luojia: City Without a Lord

C07 (`c01-07`, 无主之城) is an early-spring urban battle in 自由城洛岬.
Its complete 1536×1024 mother painting uses pale limestone, warm roofs and spring
foliage rather than C06's winter palette. The image has no baked actors or UI.
The approved generated source is preserved as Library
`libfile_848be92342dc8191af3ac4e511fbe159`; the runtime copy is byte-identical.
The original source remains separately preserved as
`libfile_20a8c7c507dc8191a4178502c2d2caf3`. The revised image opens the council
and supply courts and rearranges canopy stocks; it is not a lossless pixel edit.
Extra residential roofs are noninteractive physical scenery, not extra income sites.

## Mission and interaction ownership

`data/grayflag-campaign-levels.json#c01-07` remains the only gameplay owner.
Laiya must survive and the granary and militia-barracks control achievements must
both complete. Existing objectives **latch completion**: losing a previously
captured target does not reset that achievement. This is sequential capture,
not an invented requirement to hold both simultaneously. Laiya's critical loss
takes precedence; objective completion precedes the timeout check. T15 remains
playable and the existing limit evaluates upon entering T16.

The 23 original actor identities, statistics, morale, AI and campaign roster
bindings are preserved. The scene retains exactly sixteen capturable sites:
eight neutral houses, four neutral civic field posts, and four gate outposts
(one friendly, one hostile, two neutral). The two mission targets are ordinary
field-post sites, not production barracks. Houses heal 20 and grant 100 funds;
posts heal 15 and grant no income. No recruitment, repair, rescue, door-opening
or political-choice capability is invented by the painting.

Collision comes from canonical terrain, not visual roofs. Reviewed physical
foundations, garden enclosures, walls, fences and the central statue base are
blocked. Open courtyards and visibly open street/gate approaches remain routes.
Facade targets select canonical sites; roofs pass through to independent ground.
Painted foreground patches borrow unchanged pixels from the same full image.

`docs/art-provenance/luojia-geometry.json` records reviewed polygons, site anchors
and route evidence; it is not a competing runtime collision source.
`scenes/c01-07-painted-ground.json` binds the canonical level digest and painting.
Application schema 4 captures the canonical initialization-source digest once,
then validates the saved campaign-entry request before publishing loaded state.
Both the retired 23×15 source and the published pre-planter 96×64 source are
rejected. Legal battle-time terrain changes still round-trip. **All old schema-3
application wrappers, including campaign-only wrappers, are deliberately rejected.**
There is no silent migration; raw CampaignSave and core battle/replay contracts
are unchanged. See [Compatibility](COMPATIBILITY.md#application-initialization-source-binding-schema-4).

## Spatial and presentation changes

The final global 16-source-pixel lattice is 96×64, not a claim that the retired
23×15 battle retains its old pace. There are 3,797 main-network foot cells, 2,281
reviewed solid cells, 65 excluded architecture/edge projection cells, and one
truthful isolated paving cell. Council cell (35,47) is a tiny furniture nook
with diagonal physical access but no orthogonal whole-cell entrance between
its rack, bench and crates. It remains ground, not an invented wall/outside tile.
Every deployment, all sixteen sites/approaches, eight ingresses and intended
street/court routes belong to the main component; only this reviewed nook may
remain isolated. All eight
visible street ingresses and four gate arches connect; narrow open courts are
not silently closed to accommodate a coarser grid.

The existing painting/plane `worldScale` is 1.0. The rejected 1.6 candidate made
people too small relative to doorways. Shared character presentation scales and
all movement statistics remain unchanged. Adjacent actors overlap at shoulders
and legs like a close formation, but tested side-by-side and front/back heads
and torsos remain readable and independently mouse-selectable. Wider cavalry /
guard pairs also retain substantial independent production-picking regions.
No per-building warp, hidden collision relaxation or new scale knob was added.

Deployment is deliberately redistributed over the connected city. The militia
near the northern canopy stands on east paving, not on its stone garden post.
Static enemy approach lower bounds span 2–8 movement orders; these exclude
combat and later blockers. The declared capture strategy uses the existing
forward north militia for the granary and east militia-2 for the militia hall;
the central group protects Laiya and supports the streets. Their opening
shortest routes are 17 and 25 steps respectively. A production-command probe
completes both targets in 15 movement/capture orders by T9 with enemy turns only
handed off. **That is route feasibility, not normal-AI victory.**

The original Torren shortcut crossed omitted stocks. A strict draft produced a
56-step detour; after the approved art edit and ground-base re-registration,
Torren's actual (40,25) deployment has an 18-step route. The narrow canopy
passage has only four source pixels of cell-edge clearance from the traced
wooden flowerbox base. It is not advertised as a broad or robust 48–64px lane;
foliage is handled by depth occlusion, not used as a physical ground footprint.
No route count, movement statistic or timer was forced to match the old map.

## Corrected checkpoint evidence

- Natural T3 play after `bb427e56` exposed a real missing planter foundation:
  Bran legally entered (56,23). That run and all outcomes remain preserved as
  historical failure evidence, not accepted geometry or a completed battle.
- The correction audits small physical planters, posts, stocks and benches,
  registers their actual bases and matching visual occluders, and retraces the
  three generatively edited areas. No spawn-only masking or collision relaxation.
- Static validation passes fourteen semantic negative controls, including
  erased true nook paving, invented isolated streets, strict foundation overlap,
  gate identity and missing/misdirected routes.
- Native `LuojiaChecks` passes 2,036 assertions: sixteen captures/economics,
  production movement/rendering/picking, current courtyard paving, blocked former
  planter cell, latching/deadline rules, immutable initialization binding, atomic
  old same-size source rejection and legitimate terrain-mutation save round-trip.
- The corrected independent C07 AI replay completed 311 legal actions, reached
  the turn limit without a winner, and matched exact save/replay. This is
  mechanics coverage, not objective-victory balance acceptance.
- Actual GUI attempted the old Bran destination: it is now an impassable wall,
  with unchanged battle state and zero events. Nearby paving to (58,25) accepted
  a real four-step order. Council and supply court ground picks stayed walkable.
- Loading the preserved old T3 application save shows explicit refusal and
  leaves the current played session unchanged. Current F9/F5 round-trip preserves
  exact application, campaign, battle and event data except save timestamps.
- Final `tools/godot/verify-native.sh` passed with exit 0 after the pending-camp
  expectation update: all sixteen independent AI save/replays and native scenes,
  including 288 campaign-navigation assertions. The separate animation gate
  passed 181 assertions with exit 0. These do not claim exported-package acceptance.
- Corrected opening is version 1 of Library
  `libfile_1165e448a984819191be14ed17398b71`; the earlier opening remains version 0.

Full normal-AI objective victory on the corrected source and latest exported
package acceptance remain open. Fresh natural play uses the declared forward
north/east strategy; the pre-correction run is not resumed or relabelled.


## Earlier checkpoint evidence (bb427e56) and remaining work

- Static geometry/site/digest validation passes twelve semantic negative controls,
  including swapped gate ownership and missing/misdirected ingress routes.
- Native `LuojiaChecks` passes 1,877 assertions: all sixteen captures and economics, actual rendering /
  picking, route movement, latched objectives, Laiya precedence, deadline,
  retired-save rejection and current save round trips.
- Native GUI inspected the final default city, corrected guard contact, and
  adjacent horizontal/vertical actor selections at scale 1.0. Rejected scale
  evidence remains separate and unchanged.
- A genuine normal enemy phase advanced T1 to T2. F5 → F9 → F5 preserved exact
  battle state, campaign state and all 22 events; only save timestamps changed.
- The independent C07 AI replay exercised 323 legal actions, reached the turn
  limit without a winner, and preserved exact save/replay. This is deterministic
  mechanics coverage, not objective-victory balance acceptance.
- The final opening screenshot is Library
  `libfile_1165e448a984819191be14ed17398b71` (not the initial menu screenshot).

That earlier detached fixture did not claim earlier campaign victories or a
carried C06 outcome. At that checkpoint `tools/godot/verify-native.sh` passed
with exit 0, including all sixteen
independent AI save/replays, current native scenes and source metadata/format.
The separate animation presentation gate also passed 181 assertions with exit 0.
