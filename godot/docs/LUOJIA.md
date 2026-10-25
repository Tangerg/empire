# Luojia: City Without a Lord

C07 (`c01-07`, 无主之城) is an early-spring urban battle in 自由城洛岬.
Its complete 1536×1024 mother painting uses pale limestone, warm roofs and spring
foliage rather than C06's winter palette. The image has no baked actors or UI.
The approved generated source is preserved as Library
`libfile_20a8c7c507dc8191a4178502c2d2caf3`; the runtime copy is byte-identical.
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
The retired 23×15 battle snapshot is rejected by current campaign admission;
campaign-only saves before the battle remain a supported entry. This is the
existing dimension/identity check, not a claim of arbitrary same-size migration
protection.

## Spatial and presentation changes

The final global 16-source-pixel lattice is 96×64, not a claim that the retired
23×15 battle retains its old pace. There are 3,929 connected foot cells, 2,164
reviewed solid cells and 51 explicitly excluded decorative pockets. All eight
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
combat and later blockers. A separate unchanged-opening, production-command
probe captures both objectives in 16 movement/capture orders by T9, with enemy
turns only handed off. **That is route feasibility, not normal-AI victory.**

## Acceptance evidence and remaining work

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

Full normal-AI objective victory and latest exported-package acceptance remain
open. The detached fixture does not claim earlier campaign victories or a
carried C06 outcome. `tools/godot/verify-native.sh` passed with exit 0, including all sixteen
independent AI save/replays, current native scenes and source metadata/format.
The separate animation presentation gate also passed 181 assertions with exit 0.
