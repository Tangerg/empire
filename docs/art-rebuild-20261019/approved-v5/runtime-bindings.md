# Approved v5 runtime bindings

The existing final-fantasy manifest owns logical geometry, frame/state order and
declared art footprints. The existing tactical-HD manifest selects the texture.
There is no approved-v5 override layer or fallback to an older sheet.

| Topic | Logical frame | HD frame | Logical registration pivot | Declared art footprint |
| --- | --- | --- | --- | --- |
| C01-UNIT-SWORDSMAN | 96 × 64 | 192 × 128 | 48.44444444444444, 53.27777777777778 | 1 × 1 |
| C01-STRUCT-LORNE-KEEP | 160 × 160 | 320 × 320 | 80.60847880299252, 140.21945137157107 | 2 × 2 |

Sheets live in `packages/story-candidate-01/assets/final-fantasy-v1/runtime-approved-v5/`:

- `combat-unit/unit-swordsman.png` and `unit-swordsman@2x.png`
- `structure/struct-lorne-keep.png` and `struct-lorne-keep@2x.png`

These are byte-for-byte copies of the registered `sheet@1x.png` / `sheet@2x.png`
exports under `animation/swordsman/export` and `buildings/lorne-keep/export` in
this directory. `tools/art/export_registered_sequence.py` and each bundle's
`registration.json` are their generation source. The swordsman's superseded
entry in `tools/art/frame-overrides.json` has been removed; the old small-frame
exporter must not regenerate these topics. Registration configs currently carry
workspace source paths; portable source-path migration is a separate task.

The board uses full logical frames and exact fractional pivots. HD density only
changes sampling resolution. Unit icons and roster/inspector portraits now fit
the declared strip bounds rather than clipping to a hard-coded 32 × 48 window.

The swordsman plays idle `[0]`, walk `[1, 3]` and attack `[2]`. Keep's atlas order
is normal, damaged, captured. Its existing capturable-terrain binding selects
normal or captured; it has no destructible Keep gameplay binding to select
damaged. The damaged frame is validated as an atlas state, not claimed as a new
in-game damage mechanic. Declared footprints remain unchanged; terrain, unit,
occupancy, collision and gameplay definitions are untouched.

Regression tests verify manifest geometry and errors, resolved HD URLs, PNG
dimensions and source-byte identity, pivots, animation clips, state windows,
icon/portrait bounds, and declared footprints. Browser playback and visual
occlusion acceptance remain separate checks; static tests do not establish them.

## Verification on 2026-10-19

- TypeScript typecheck passed
- All four application builds passed; generated game, editor and experience-lab
  bundles contain the new fractional-anchor validation and approved geometry
- All 104 architecture checks passed in isolation
- All 35 presentation, raster-adapter and SVG-surface checks passed
- All 81 Python art-tool tests passed
- Restoring the old integer-only anchor validator makes the new subpixel-anchor
  regression fail, as intended
- Calibration extraction now calls the engine's public `idx` helper; its JSON
  and SVG output are byte-identical before and after that change
- Full Vitest is not green: the existing keyboard UI test raises an unhandled
  `container.scrollTo is not a function` in jsdom. This was reproduced with the
  pre-binding HEAD presentation files and manifests restored. Parallel-load
  architecture timeout and SVG-clock failures passed when rerun in isolation
- No browser playback or visual occlusion review was performed
