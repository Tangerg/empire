# C07 initial material review — unresolved defects

C07 remains a complete painted map with55 occluder groups/108 borrowed-pixel
parts and zero independent assembled objects. Its190 declared ground responsibility
polygons,16 sites and8 ingress points are indexed alongside this report. There is
no bridge/river in this map. Do not transplant C06 bridge tasks here.

Two physical foreground omissions are now corroborated by native initial-state
renderer fixtures, not just approximate polygon or cell-center tests:

1. Blue-roof wooden stand at source(910,170): canonical paved pose(56,11), actual
   sprite opaque and native Pick returns Unit2 over the real roof surface.
2. Training-fence upper wood at source(976,194): canonical paved pose(61,12),
   actual sprite opaque and Pick likewise returns Unit2 over foreground wood.

Both native cases retain the original state digest and report zero resource
issues. They are failing current presentation evidence, not a pass or actual
battle progression. `native-probe.cs.txt` is the renderer-only harness; no battle
commands, campaign carryover or real save are used. Same-window screenshots are
`c07-static-preview/north-pose-56-11-painted.png` and `north-pose-61-12-painted.png`.
An existing cue in the second case does not repair the omitted fixed wood pixel.

The small blue-roof object's lower portion has a solid golden wooden panel; its
dark upper opening is not proven a walk-through aperture. Do not substitute an
open four-post shelter or new interaction. Independent material registration must
preserve original supports, visual opening facts and canonical legal cells.

Existing four city gates already separate true pillar targeting from open arch
space. For example, southwest site(15,51) is not a physical building anchor: the
two true pillar feet are near source(148,902)/(286,948). This is a conversion
constraint, not a new gate-click defect. Preserve the existing gate, adjacent-unit,
roof-pass-through and empty-entry contracts. Two earlier T7 occlusion fixes are
existing coverage, not newly discovered failures.

Native opening/pose images are canonical initial fixtures (including the source
roster), explicitly not carried C06 outcomes or a C07 normal-AI run. The first
pose harness build had a local-variable shadowing error; corrected build has zero
warnings/errors and all stated native evidence uses that newly rebuilt binary.
No C07 production art, pointer data, terrain or rules change is made in this
checkpoint. Complete independent composition and real gameplay remain open.
