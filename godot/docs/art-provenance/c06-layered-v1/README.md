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

## Complete exposed-edge fit checkpoint

The current six-material JSON checks include actual alpha lower edges, not merely
three convenient corners. North red v3 exceeds its original complete envelope at
the annex; combined minimax 6.85px fails the unchanged5px threshold. Original north
house does show a front notch/wing; v2 is not rejected merely for that architecture,
but its actual full-building feet/edge still fail at13.82px. Do not replace the true
wing foot with a higher main-body sample. Both northern ridge rock fits also fail
(6.33/9.10px); central and southwest rocks fit at3.69/4.84px. SW dual-door house needs
the combined-outline transform (4.86px), not the three-corner fit whose door steps
extend outside. These are exposed-foot geometry results only, not native selection,
hidden-rear responsibility, complete scenery acceptance or gameplay.

## Explicit five-house native preview (not formal C06)

`c01-06-houses-preview.json` is runtimeReady=false and not selected by campaign.
It binds only five registered house candidates to clean ground v4 for same-camera
native proportions review. Other standing objects are intentionally absent; the
existing canonical outpost fallback bodies (square towers) remain visible where
new shelters are not yet bound. They are not new map features or approved art.
The final independent scene must replace them through existing ownership, not add
new collision or treat this partial image as full visual acceptance.

North red inset-wing v5 full lower-edge fit is3.184364px. Northwest and opposite
slate fits and southwest complete-edge fit use stored exact parameters. Initial
facade pointer contours in this partial preview still require actual door/roof,
entrance, neighbor-unit and all-wall responsibility tests before formal adoption.
New short shelter and separate NW ridge remain unbound; their failed whole-height
or whole-edge constraints are recorded rather than silently using only good feet.

### 08:40 partial assembly boundary
The seven-object `c01-06-assembly-preview` adds NW/SW short shelters to the five-house diagnostic only. It is runtimeReady=false and is not the production campaign scene. Fresh external native renderer captures both before/after at default 1.25 camera in 1180x812; NW fits frozen three feet and approximate roof height (max foot coordinate error4.755627); SW retains foot-only fit (4.484694), raising its roof about20.36 source pixels for explicit visual review. Neither has native pointer/actor/hidden-support acceptance; current broad body pointer contour remains provisional. Surrounding trees, bridge rails, lamps and furniture are incomplete. No real C06 battle has been played.

The old scenery audit contains conservative polygons that include bare snow/grass. In particular NE ridge's former lower-left/near-center polygon vertices are not both physical stone toes. Reports fitting those polygon vertices are historical diagnostics, not authoritative proof of true rock-foot mismatch. Actual rock surface correspondence and the complete right-hand extent are under review; no collision or map changes are made to fit art.

### 08:58 candidate checkpoint
Partial assembly now has all10 sites (5houses/5shelters) and9separate pines; 3pine contacts are inferred under original occlusion, not recovered visible roots. Only the prior five-house/two-shelter/nine-tree stages have native screenshots; latest mirrored/north shelters are static-only. North target contour is restricted to observed vertical wood, preserving source positive928216/928228 and excluding snow932212 despite overhanging beam alpha. Final native picking/depth/cue checks remain pending. Production C06 stays painted, and current real play belongs to that old production scene with the earlier north-post fix. No hot production change during play.

### 09:15 expanded partial preview
Six rails, twelve bridge lamps and five rock representations added to the unbound preview (42total). Lamp supports include9visible estimated feet and3explicit inferred construction joins above retained bridge piers; they are not declared recovered ground feet. NW corrected cliff passes11frozen material witnesses and the former road-spill transparency negative. NE ledge and small rocks remain proportion/layout candidates with explicit canonical-wall obligations, not old three-foot matches. The south foreground rock is an intentional NEW visual expression of an existing bare wall cluster, not claimed original material restoration. No production scene/terrain/rules changed. Native review of this expanded stage follows; full actor-depth/pointer/footprint gates still pending.

### 09:38 partial preview refinement and alpha regression
53-object preview adds eleven separately rooted pines at source crown locations, with hidden roots explicitly inferred rather than called visible. Four wood rails now align terminal posts with inner lamp support edges instead of confusing original interior posts with full rail endpoints. Post spacing/height changed slightly under uniform scale; native bridge join images improve, full actor/depth/road tests remain pending. NW shelf v6 restores broad right shoulder rather than a narrow peak; three old high rock-face witnesses are no longer covered and full wall responsibilities are NOT passed.

Six same-camera 1180x812 views were captured through the native independent renderer. This is NOT the paused real C06 battle. Production remains painted; real play is T1/20events/10done with movement authorization blocked, exact partial checkpoint is privately backed up.

A true black halo on SW house v2 was reproduced in the southwest native view. Built-in imagegen alpha-v3 removes seven exterior haze samples to alpha0, with unchanged registration/contact/scale. Existing old file remains a failing negative control. Current-C06 transparent candidate audit covered34images: only old SW house had a broad connected weak-alpha exterior; unused triple-pine candidate has64isolated weak pixels, not a broad halo. No renderer threshold, shadow rule or gameplay change. Raw transparent RGB on the west rail looked like fog in a viewer, but its exterior alpha was0; no replacement was adopted.

Historical correction: old SW lower-outline alpha>=200 included haze. The old280,808 left-foot witness and former1029,983 overflow witness were not proven solid masonry. New actual left masonry280,799 replaces only that invalid witness; original front795,988/right1340,803 remain opaque and retained. At exactly the existing transform the three real feet remain within4.858763source pixels and all1070new lower-material column terminals within2.386592pixels. `python3 tools/art/check_c06_candidate_alpha.py` verifies background negative control, real feet, complete lower chain and unchanged preview registration. This is not hidden rear-wall or runtime picking acceptance.

Unfinished visual duties: border forest groups, absent yard fences/furniture, residual NE/SW continuous cliffs vs independent ownership, full canonical-wall support, scene alpha/pointer/actor-depth, north goal cue. No production adoption or new victory claimed.

### 10:00 yard and shelter pointer checkpoint
Preview now has95objects: original yard areas regain26low fence bays and16simplified crate/barrel pairs. These are presentation-only pass-through props, not new collision, inventory or sites. Source-route registration explicitly preserves gate openings. Shorter beam assets avoid shrinking whole short fences; overlong runs split instead of growing posts. A SW left segment initially used a crop edge rather than a real post; full original photo corrected its route. Paired supplies use separate lower-support depth sections, not one universal crate front. Border forest and full footprint/depth acceptance remain unfinished.

Four shelter provisional broad pointer contours were a real regression against the existing roof-pass-through/wood-post-target contract. Narrow true-wood contours replace them without touching roof alpha or visual depth. Fresh external native probe build succeeded0warnings/0errors, scene diagnostics onlyruntimeReady=false (expected). 1134legal centers scanned;11previous object interceptions become2actualSWwood hits. All180site/approach points stayGround;10actual wood positive samples target correct unchanged sites; north wood/snow controls unchanged. All20isolated Torren site/approach poses have visible selectable body pixels. NE sites retain353/213of379pixels selectable; four hidden raw-alpha probes are intentionally NOT counted as visible-body failures. The fully concealed north refugee fixture still has a correctly selectable cue. Zero rendering issues and original initial state unchanged. These are isolated poses, not paused T1 progression or victory.

Native static images assembly95-* and three separately watermarked actor-pose views are stored in private evidence. Current production scene is unchanged. Snapshot statistics are not whole-scene acceptance; remaining rock/foundation, border vegetation, all-appearance occlusion and real-play duties remain.
