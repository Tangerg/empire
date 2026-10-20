# C02 antler-knight combat pilot

Complete source quartet: idle, walk-a, attack, walk-b. All four built-in image generation calls and exact prompts are preserved under the topic master folder. Each master.png/raw.png pair is identical to its selected native result. No generated pixel cleanup, painting, recolor, cropping, or per-pose scaling was performed.

## Visual review

- Original combat board05 row3 and all four canonical runtime poses inspected before generation. Mounted cervid identity and cyan spear/shield retained; purple equipment-card spear deliberately not substituted.
- Idle: compact grounded four-legged stance, upright spear.
- Walk-a: extended forward stride, diagonally raised spear, open leg silhouette.
- Attack: forward braced lunge with level cyan spear, lowered head, and retained shield. No invented projectile or additional gameplay action.
- Walk-b: opposite step with near foreleg bent up and back; not mirrored walk-a. Same equipment side assignment.
- Native64 inspection: antler crown, mount, leaf shield and spear direction remain readable on black and white. Native32: mounted silhouette and action direction remain legible, but fine leaves and cyan nodes intentionally collapse; use native64 for detail inspection.

## Validation and integration

The common native canvas is 1254x1254. Every source passes existing alpha_report with edgeMax 1 and true transparent/opaque pixels. All source hashes differ and raw/master equality holds. Canonical frames are64x64, sheet256x64. No HD contract exists or was invented.

Measured actual planted sole regions are in measured-support.json, reproduced by measure_support.py. X is the equal-weight centroid of bottom-four-row centers for planted hooves, excluding lifted hooves. Y is the lowest planted sole. Source support points: idle[692.3025,1226], walk-a[738.1082,1212], attack[640.6942,1152], walk-b[668.0900,1211].

The corrected registration uses one whole-canvas renderWidth52, origin[3,10], and offsets idle[0,-1], walk-a[-2,0], attack[2,2], walk-b[1,0]. Every full canvas stays within64x64 and every translation is inside the unchanged exporter±2 guard. No independent fit, crop, alpha cleanup, or source regeneration. Earlier visual-estimate offsets[3,3] were superseded and are not suitable for registration.

Projected support X spans31.5679–31.7079 and Y spans59.7703–60.2584, giving only0.1401px horizontal and0.4880px vertical interphase drift. Canonical anchor[32,63] stays unchanged. Actual anchor errors by phase are idle[-0.2921,-3.1611], walk-a[-0.3926,-2.7416], attack[-0.4321,-3.2297], walk-b[-0.2961,-2.7831]. The common2.74–3.23px vertical clearance must be accepted explicitly by the coordinator; this is not an exact anchor match. At width52, attack support cannot be lower than64−52+1152×52/1254=59.7703 without clipping its full source canvas. Do not force63 by cropping or per-pose scaling.

Called existing export_frame_overrides.plan with isolated-topic-catalog.json: PASSED, one canonical256x64 output planned. Source hashes, dimensions, distinct decoded/rendered poses, alpha, full-canvas placement and bounded offsets passed. Saved its actual output to canonical-plan-1x.png and exact report to canonical-plan-1x.json. Runtime files were NOT written. The separate quartet-native64.png is byte-pixel-identical to this planned sheet.

Native64 white/black and enlarged nearest-neighbor views inspected: phase motion, shield, mount, crown and spear direction readable. Native32 is secondary zoom only, not a canonical or HD contract. Its bottom-edge Lanczos ringing reaches alpha3 with the tighter bottom placement and is recorded as a failed secondary diagnostic for affected phases; it is not used for acceptance and source pixels remain untouched. Canonical64 alpha passes.

No shared catalogs, manifests, runtime sheets, integration code, or progress records changed. Runtime scene playback and formal binding were not performed; no live scene consumer invented. Coordinator reviews this canonical preview before any binding.

Reproduce support with python docs/art-rebuild-20261019/approved-v5/scifi-combat-pilot/measure_support.py, then QA with build_qa.py in the same folder.
