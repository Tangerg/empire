# C06 pre-layering physical baseline — 2026-11-01

C06 remains the original painted winter scene. No layered art is bound yet.
The responsibility matrix lists 34 explicitly audited/occluding standing groups:
five houses, five shelters, eighteen pine groups and six rock groups. Five house
foundations (91 cells) and six rock foundations (75) cover all 166 canonical walls.
This is not an exhaustive art inventory: bridge rails/lamps, crates and low fences
are separately identified without inventing new collision cells or interactions.

## Corrected north supply post

The old source polygon [(930,212),(936,210),(936,242),(930,245)] was shifted right
onto bare snow, not actual wood. Both its material-depth mask and pointer polygon
now follow the inspected post at [(926,212),(930,211),(930,238),(926,240)].
Selection site, contact, depth front and canonical map/rules remain unchanged.

Fixed true wood (928,216)/(928,228) selects outpost (27,7). Bare snow at
(932,212)/(932,240) selects ground (28,6)/(28,7); actual Torren opaque body at
(932,216)/(932,228) in approach (28,7) selects Torren. Native regression deliberately
restores the old rectangle and requires all false-target cases to reproduce.
All 1134 legal empty centers and 180 entrance/approach offsets are checked. Existing
concealed north-goal refugee cue checks remain; this is not removing valid shelter
occlusion or making actors clickable through real roofs/posts.

Fresh build: zero warnings/errors. Native WhiteRiver: 1759 assertions including
this negative control and existing objective/cue/save contracts. No-AI route and
T4/T6/T14 fixtures are explicitly synthetic, not this run's C06 combat completion.
The GUI is currently occupied by actual C05, so C06 full visual/play acceptance
is still pending. Source crops and isolated native pixel probes support this fix.

## Unbound materials checkpoint

Two built-in image-generation outputs are saved, not bound: clean ground v1 and
night slate cottage v1. Ground retains exact 1643x957 canvas but still contains
five visible audited rock bodies; it is not accepted as a duplicate-free plate.
The sixth old audit label `south-foreground-rock` has no rock in the original PNG,
only snow/pines in its ROI. Its 12 canonical wall cells must remain unchanged, but
visual responsibility must be reviewed instead of fabricating an original rock.
The night cottage preserves source silhouette approximately; independent alpha
footing/door registration is still required. Transparent-margin inspected samples
remain alpha zero despite RGB backdrop color in the generated PNG.

## Later candidate review (not acceptance)

Clean v2/v3 still retain the west-bank rock and explicit pine2. Do not duplicate
them with independent objects yet. Three bridge decks retain the 40 inspected
canonical centers; low central bridge foundation may remain. The current night
slate palette asset has the wrong door/gable arrangement for the original houses.
NE/SE need the opposite-facing version (new candidate saved); NW needs a gable
door. North red needs its gable door/low annex, SW its additional gable door.
Three opaque corner fits alone cannot certify these architectures. The initial SE
slate fit of 5.004717 pixels fails the fixed five-pixel limit and is not rounded
into a pass. The original snow-pines-17 named polygon is also mainly snow/low grass,
not proof of a tree; actual roots, not old labels, must determine pivots.

## Bounded west-bank shore decision

Clean v4 removes pine2 but retains the existing west-bank stone shore. Retain this
continuous shore geology in the ground layer; do not add a duplicate independent
west-bank boulder. This decision is based on full actual actor alpha, not inability
to erase the source: opening and T4 records resolve to 14 appearances / 13 types,
four frames and both facings. Across 431 legal backside fixed foot positions,
neither inspected stone contours nor their larger conservative box intersect actor
alpha. Nearest inspected alpha is about 19.04 source pixels away. All 112 poses
placed at the illegal wall positive-control position intersect, validating the probe.
See the two adjacent JSON reports. This does not certify in-between motion, camera
rendering, future actor types or C06 gameplay. Four other actual standing rock groups
remain independent candidates; the southern rock label lacks an original rock and
needs a separate honest visual-responsibility decision. Bridges retain only their
continuous low deck/foundation; elevated rails/lamps require independent objects.

Three additional source-facing art candidates are saved with exact hashes: the
north red cottage with a smaller rear annex, southwest red dual-door cottage, and
open winter shelter. They are not bound or accepted before actual feet, annex,
post/open-gap, doorway and depth checks. No C05 live assets or game rules changed.
