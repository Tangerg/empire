# BORDER1–4 source-grounded export proposal

Code commit: `34c3de005f129b878aab35a72658adf359930755`.

`proposal-canonical-terrain-overrides.json` contains the exact four source paths,
SHA256 values, source dimensions and deliberately selected windows. It is a
proposal artifact, not the production catalog. `proposal-export-report.json`
was produced by read-only planning against the real main-worktree manifests and
masters. Every shared raw source was byte-compared with its main-worktree copy.
No production runtime file or manifest was written by this worker.

## Final windows

- BORDER1: side680; `[0,0,680,680]`, `[20,5,700,685]`, `[554,510,1234,1190]`, `[574,520,1254,1200]`
- BORDER2: full1254; `[0,0,1254,1254]` shared by every mask
- BORDER3: side600; `[0,0,600,600]`, `[654,65,1254,665]`, `[0,640,600,1240]`, `[654,640,1254,1240]`
- BORDER4: full1254; `[0,0,1254,1254]` shared by every mask

All sources are1254×1254 opaque RGB. There is one common logical material scale
within each topic; no per-mask zoom, arm-local rotation or invented route detail.
The opaque windows preserve complete local motifs. BORDER1 deliberately uses
nearby overlapping windows in two broader source regions rather than cutting
straw at arbitrary quadrants. Source selection was visually reviewed by the
source worker and parent, including native32-derived and64px fixtures.

## Which previews to review

Final opaque fixtures use `border1-broad-windows-*` and `border3-broad-windows-*`.
For each, `32` and `64` indicate physical cell size. `mixed-4x4` and
`repeated-2x2` copy exported cells directly. `inspection4x` is a nearest-neighbor
inspection enlargement of the mixed grid.

Final connected fixtures use `border2-full-canvas-*` and
`border4-full-canvas-*`: all16 masks in order plus isolated, end, NS/EW straight,
corner, T and cross fixtures. Inspection backgrounds are neutral presentation
backdrops, never output alpha or texture pixels.

Earlier `border1-proposed-windows-*` (side256) and
`border3-proposed-windows-*` (side320) are superseded diagnostics, retained for
honest comparison. They are not the final catalog proposal.

## Measured contracts

`proposal-qa-summary.json` is the short measurement record. All8 planned sheets
match the exact historical alpha SHA256 at their own density. Opaque output
counts are4 and connected counts16, totaling40 derived cells per density and
exactly4 canonical source identities, with no environment-cell increment.

Each opaque density passed all16 E/W plus16 S/N neighbor comparisons. Each
connected density passed all64 E/W plus64 S/N open-mask comparisons. Maximum
8-bit channel change from the approved outermost1px seam operation at1x/2x:
BORDER1=23/18, BORDER2=4/4, BORDER3=21/22, BORDER4=7/9. No resized interior pixel
was repainted, recolored or blurred. Exact source sampling and hashes are in the
full export report.

## Visual acceptance and limits

The final broad crops retain source identity and sensible material-scale motifs.
The central puddle (BORDER2) and central bootprint (BORDER4) remain complete in
all cores. Peripheral rut strokes can end at material boundaries without being
misrepresented as severed freestanding objects or authored cardinal turns.

Exact border-pixel equality does not make the overall tiling invisible.
BORDER1 retains a repeating square tonal-block rhythm. BORDER3 retains a
periodic diagonal furrow cadence and visible interruption/reset at some tile
borders. Both are less oversized than the earlier tight crops. The parent
accepted the final proposal as a canonical/native export with these explicit
caveats, not as complete landscape cohesion or current active-map improvement.
No new generation, whole-image recoloring or blur was used to conceal them.

The catalog currently has no gameplay-ground consumer. Formal registration,
quality/ledger acceptance, any actor-cohesion fixture and publication are parent
integration steps. This worker's production catalog remains empty.

## Verification

-14 new Python exporter tests passed
-13 existing surface exporter tests passed
-47 targeted TypeScript tests passed: canonical catalog/HD resolution, runtime
  ownership and density contracts
-Repository TypeScript typecheck passed
-Empty-catalog read-only exporter check, Python compilation and Git diff checks passed
-Full repository test suite/build not run by this worker
