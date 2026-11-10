# Torren's authored hit recoil

This slice replaces only `campaign-torren`'s `hit` binding. Four independently
painted pictures show initial recoil, peak compression, recovery and ready guard.
Their 40/110/90/80 ms intervals total 320 ms, entirely within the existing 420 ms
impact presentation. The game rules, damage, event order, other actors, attack
preview, accepted thrust and breathing idle remain unchanged. Death remains the
explicit static placeholder; this is not a completed death animation.

## Source and extraction

`source-row.png` was produced with the built-in image generation tool using
`prompt-v1.txt`. The official `torren-stage01-v2.png` and accepted breathing row
supplied identity, equipment, rendering and ready-stance references. This adapts
[sprite-gen's component-row method](https://github.com/aldegad/sprite-gen); it is
not a run of its CLI or Grok pipeline. No separate image provider was used.

Independent source and reduced-size reviews found no identity, handedness,
equipment or contact blocker. The spine, shoulders, neck, knees and arms change
pose. The runtime does not translate, rotate, scale or fade the actor to invent
recoil. Sword and shield remain held throughout.

`extraction-review.json` records the source hash, four original RGBA components,
rectangles, support anchors and single 0.0738 presentation scale. Significant
alpha components (>16/255 and >1000 pixels) identify the four figures. Original
nonzero-alpha pixels within three pixels of each component are retained; 11,925
distant pixels of at most 4/255 alpha are excluded. Every extracted figure has
four transparent padding pixels. There is no painting, interpolation or individual
frame rescaling during extraction.

`verify-extraction.py` reproduces the runtime atlas from original source pixels
and checks the manifest hash, geometry, anchors and four time intervals. It needs
Pillow, NumPy and SciPy. An in-memory single-channel pixel mutation is rejected;
the negative-control record is under `verification/`. The manifest owns live
geometry/timing; this document records the reviewed source, not a second runtime
catalog.

`native-size-contact.png` compares accepted idle then all four recoil frames in
both directions. The 3x copy is nearest-neighbor magnification. `anchored-loop.gif`
is an offline diagnostic, not game footage or proof of real-time performance.

## Integration and verification

Production integration is data-only. `BoardView.Impact` already starts the declared
hit pose for positive committed damage and samples it with the existing receipt
clock. Recoil returns to breathing at 320 ms while the original impact completes
at 420 ms. Healing and zero damage do not start recoil. Fatal hits retain the
existing separate fatal beat and ordered death presentation. No production C#
owner, timer or combat rule changed.

Production-art checks cover four distinct padded images, half-open intervals,
release gating, both mirrors, fixed root/contact/scale, shared impact and idle
handoff, coarse ticks, cancellation, reduced motion, healing/zero-damage exclusion,
and fatal-to-death ordering. Synthetic receipt checks are explicitly separate
from the lawful native-battle recording. Existing tests receive only the narrow
intentional named-Torren hit exception.

The focused native AnimationChecks run passed **3,473 assertions** with no
error/leak diagnostics; the build completed with zero warnings/errors. In a
separate project-data copy, changing the peak span from 110 to 109 ms produces
exit 1 at the new explicit 320 ms recoil assertion. The normal manifest was not
modified by this negative control. Its log is separate from the positive run.

### Actual native battle

The real application loaded a current-schema checkpoint derived only through
legal session actions. On the enemy turn, shield 16 moved one cell (17,7)→(16,7)
and struck Torren at (15,7) for 29 damage (72→43 HP). Torren then counterattacked
for 10. The unchanged normal AI, domain damage and receipt presentation performed
this action; no state edits, synthetic receipts or new UI hook were used.
The legal source and before/after receipt verification are under
`verification/legal-hit/`.

`verification/native-recoil-lossless.mp4` retains all **71 full-viewport frames**
at 1000×700 and 60 simulated frames per second, stride 1. Each decoded RGB frame
was compared byte-for-byte with its original native PNG. The proof JSON records
all PNG/RGB/video hashes, the reviewed assembly, frame cadence and exact pose
transitions. JSONL retains every raw actor observation. This is simulation-time
recording, not measured real-time performance.

The four recoil poses occupy observations 10–28, breathing resumes at 29,
counterattack begins at 35, and breathing resumes again at 56. The support point
is constant in all 71 observations; the actor root remains fixed throughout the
received hit and until the counterattack. Independent review of actual frames
found no blocking identity, equipment, groundedness, scale or transition defect.
The impact flash and damage number cover some chest detail, but the helmet dip,
shoulder recoil and recovery remain legible. This is frame-by-frame visual
acceptance, not a claim that facial details remain readable at map size.

This native capture covers one facing. The mirrored direction has static
reduced-size review and controllable-clock coverage, not a second recorded battle.
The native session closed cleanly, with no ERROR/leaked-object diagnostic; its log
retains the existing unsupported V-Sync warning and runtimeReady=false diagnostic.

The full aggregate is still running. Exported-build and complete-campaign
acceptance are not claimed, and the remaining character actions remain separate
work.
