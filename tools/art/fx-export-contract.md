# Registered FX phase export

`export_fx_overrides.py` is a deliberately narrow exporter for the first slash
pilots C01-FX-01 (slash), C01-FX-02 (pierce), C01-FX-03 (arrow impact), and C01-FX-04 (blunt impact). It does not register art, edit manifests, generate artwork,
clean alpha, or approve motion. Further topics require their own reviewed phase
semantics before adding a contract; generic growth/burst/decay labels would be
wrong for effects such as construction or capture.

## Input

Pass an explicit JSON catalog to the CLI. This example is a schema illustration,
not a registration; replace the paths and hashes with verified master files:

```json
{
  "schemaVersion": 1,
  "assets": {
    "C01-FX-01": {
      "kind": "fx-phases-v1",
      "phases": [
        {"phase": "onset", "source": "packages/story-candidate-01/assets/final-fantasy-v1/masters/pilot/onset.png", "sha256": "<64 lowercase hex characters>"},
        {"phase": "sweep", "source": "packages/story-candidate-01/assets/final-fantasy-v1/masters/pilot/sweep.png", "sha256": "<64 lowercase hex characters>"},
        {"phase": "peak", "source": "packages/story-candidate-01/assets/final-fantasy-v1/masters/pilot/peak.png", "sha256": "<64 lowercase hex characters>"},
        {"phase": "dissipate", "source": "packages/story-candidate-01/assets/final-fantasy-v1/masters/pilot/dissipate.png", "sha256": "<64 lowercase hex characters>"}
      ],
      "placement": {"scale": 0.5, "origin": [0, 0], "sourcePivot": [32, 32]}
    }
  }
}
```

The illustrated placement assumes all four source canvases are 64×64. Paths are
repository-relative and must resolve inside this topic's owning pack `masters/`.
Sources must be PNG RGBA with exactly equal canvas dimensions, different decoded
pixels and nonempty visible content. Raw outer-edge alpha may be at most1; alpha2+ is rejected. Final padded output perimeters must be exactly0. The explicit
phase array must have exactly the reviewed order, without repeated or arbitrary
names. Source hashes lock provenance; changing labels cannot prove the artwork
actually depicts those phases. That still needs visual review.

The single scalar scale applies to both axes of every complete source canvas.
Scaled width and height must be positive integers; origin must be nonnegative
integer logical pixels. The whole transformed canvas must fit within 32×32.
`origin + sourcePivot * scale` must equal the canonical asset anchor `[16,16]`.
There is no per-phase crop, fit, shift, scale, pivot, or unit-foot contract.

## Output and reproducibility

```sh
python tools/art/export_fx_overrides.py path/to/reviewed-catalog.json > fx-report.json
python tools/art/export_fx_overrides.py path/to/reviewed-catalog.json --check > fx-check.json
diff fx-report.json fx-check.json
python -m unittest discover -s tools/art -p 'test_fx_overrides.py' -v
```

The existing canonical record owns geometry, animation timing and pivot. The
existing HD record must be 256×64 at density 2. Destination paths are fixed to
`runtime/fx/fx-01.png` and `runtime-hd/fx/fx-01.png`; no destination is supplied
by the override. Output is four 32×32 cells at 1× and four 64×64 cells at 2×.
All sources and destinations are validated and both sheets constructed before
the first file write. Check mode compares decoded RGBA dimensions and every
pixel, without writing. Source files and JSON catalogs are never rewritten.

Lanczos resampling is applied separately to each full authored source canvas at
the shared scale, followed by unmasked paste. Native-resolution identity exports
retain every RGBA byte, including a wholly translucent final phase with maximum
alpha 104. Resampling at other sizes naturally changes edge samples; it never
opacity-normalizes a fade or multiplies alpha by itself. The FX alpha gate
requires nonempty content, raw perimeter alpha≤1 and strictly clear final perimeters, but no near-opaque
core. Unit and prop alpha gates remain independent and unchanged.

Reports include ordered source file and decoded-pixel hashes, source canvases,
source/output alpha bounding boxes, nonzero counts, maximum alpha, each output
frame and sheet decoded hash, density, common transform and logical anchor.
Distinct source/output hashes reject duplicates but do not establish meaningful
motion. An opaque interior panel can also pass the mechanical alpha tests.

## Runtime and required visual review

The 24 canonical FX anchors are corrected from unused `[16,24]` to the actual
runtime pivot `[16,16]`. `candidate01FxPicture` now consumes the metadata directly
and rejects absent or invalid anchors. The complete existing BoardPicture is
unchanged for all 24 topics: origin `[-16,-16]`, HD texture URL, 32×32 logical
frames, order `[0,1,2,3]`, 12 fps, normal source-over, and looping only for FX-09
through FX-12. A synthetic non-centered anchor test proves metadata is the owner.

No hit/heal lifetime, damage value, gameplay rule, overlay or consumer binding is
changed. Clip frame starts remain 0, 83⅓, 166⅔ and 250 ms. Non-loop clips hold the
last frame until the existing hit deletion at 420 ms or heal deletion at 500 ms.

Before registering the slash pilot:

- Inspect authored onset → sweep → peak → dissipate semantics at native 32px and
  64px, retaining real growth/fragmentation and a fixed effect origin
- Inspect over black, white, bright stone, green ground and dark earth for
  internal backing, fringe, unintended light or clipping
- Review exact 12 fps playback and the held final-frame interval, not only a
  contact sheet or unique hashes
- Observe the first-level c01.border-blade hit path with its existing white burst
  circle and damage number. Keep these overlays out of the art itself
- Check the actual renderer uses the HD strip. A staged preview is not live-game
  verification; exporter reports leave both motion review and playback unapproved

## Engineering verification (2026-10-20)

- 24 FX exporter tests pass; all 145 Python override tests pass
- 226 focused runtime/assets/FX/animation/surface/hit-effect tests pass, including
  all 24 complete legacy BoardPictures and non-centered-anchor consumption
- The non-centered test rejects an intentionally restored hardcoded-center
  implementation; the real implementation was restored and its suite passes
- Typecheck and all four app builds pass
- Before/after replay output is byte-identical for all 60 shipped-level/profile
  runs, including event streams and outcome digests
- The full TypeScript suite in the bounded sparse checkout reports 1,202 passed
  and 284 failed: 283 missing-source-file errors and one pre-existing linked-file
  existence check for omitted documentation/assets. Full-checkout verification
  is still required; these failures were not suppressed or assertions relaxed
- Review identified and resolved source/output aliasing through a redirected
  masters directory and invalid destination ancestor/file types. Regression
  tests prove these fail before base or HD output writes

No generated art, new source registration, source alpha edit, live playback or
semantic animation approval is included in this engineering change.

## Reviewed next impact phase contracts

FX02 keeps the original fixed-axis puncture: onset, longer sweep, peak needle/chips, translucent dissipate. FX03 uses approach, impact, burst, dissipate: short arrow approaching down-right, cream impact at its tip, arrow removed with radial chips, then residue. These meanings are verified against full original source rows in the source audit. Enabling a contract does not approve generated artwork or register a topic. All shared-canvas, transparent-edge, density and timing gates remain identical.

## Bounded source residue tolerance

The reviewed arrow burst revision contained exactly four perimeter pixels at alpha1 after one targeted imagegen repair (the prior candidate had81). Actual black/white/color compositing and padded32/64 exports determine acceptance; no alpha values are changed. Source-only allowance is at most1, matching the existing static source ingestion contract. Alpha2+ still rejects before writes. All final cell borders remain alpha0. Reports record measured edge maxima/counts and tolerance separately for sources and outputs. This is not a general license to accept visible halo, cutoff, or increasingly relaxed thresholds.

## Blunt-impact source semantics

FX04 is the original atlas01 fourth-row sequence: hollow ochre elliptical impact rings, paired dust puffs, expanding stone flecks and settling residue. The reviewed phase order is contact/expand/peak/settle. It is not a weapon, spark starburst or opaque ground disk. Shared target-centered registration and existing lantern-staff/engineer-hammer/rune-hammer/golem-slam bindings remain unchanged. Contract support does not approve or count generated art.

## Ordinary-fire source semantics

FX05 preserves original atlas02 row1: small orange ignition, medium curled flame, tall broad yellow-core orange flare, and small dying flame/embers. Its phase order is ignite/rise/flare/embers. It is ordinary fire, distinct from FX06 oath-fire rune rings. The existing c01.dragon-breath target-centered hit binding remains unchanged. Source semantics and shared-canvas export support do not approve or count unfinished generated phases. The authored flame root is measured separately from the fixed center pivot; it is not silently individually aligned.

## Oath-fire source semantics

FX06 preserves the original atlas02 second-row amber angular rings, top/bottom diamond sigils, and open center. Phases low-ring/rising-ring/sealed-ring/fading-fragments are distinct from ordinary flame. Existing gray-oath/oath-dispel/inquisitor-flame/oath-bolt target-hit bindings keep12fps/420ms. No invented readable oath language or source opacity cleanup. Contract support alone does not approve generated artwork.

## FX07 siege debris
Original label and actual knight-charge/satchel-charge/forge-burst consumers identify physical siege debris, not frost. Ordered phases: airborne-burst, falling-pieces, falling-remnants, settled-heap. The final opaque wood/stone heap is intentional; existing nonloop12fps target-hit lifetime420ms removes the node. Common1254 canvas/28px content transform and strict source alpha<=1/final2px alpha0 gates remain unchanged. No per-phase fitting or forced opacity fade.

## FX08 memory impact
Original gray ordinary-person memory echoes and amber name papers, not a skull. Four phases clustered-echoes/rising-echoes/dissolving-echoes/wisp-remnants, authored translucency for dissolving people and no people in final wisps. Actual ghost-touch/colossus-sweep targethit uses same16,16 center, normalblend,12fps nonloop420ms. Commonnative1254canvas transform unchanged; no programmedfade, sourcealpha<=1 and final2pxalpha0 gates unchanged.

## FX18 restraint and FX19 veil contracts
FX18 preserves amber metal bands and diamond buckles: scattered-bands, aligning-ring, closed-restraints, settled-restraints. FX19 starts with the original dense gray-green leaf smoke, then curling-veil, veil-breakup, leaf-wisps; do not invent a low-opacity onset. Both remain canonical four-frame, 12fps nonloop center-pivot effects. No current literal consumer or runtime lifetime has been established for either. Source review and exporter support do not count pending sources as bound or prove observed motion. Gameplay visibility and hidden-unit rules are unchanged.

## FX20 support guard and FX21 structural damage
FX20 is an open-left, right-convex blue-gray crescent with amber sparks at its right-middle impact point: bare-crescent/contact-spark/spark-spray/residual-sparks. It is not an opaque heraldic shield or a shield bearer. FX21 preserves gray stone/brown timber fragments followed by localized warm dust: debris-lift/rubble-scatter/dust-impact/settling-dust. Its legacy RGBA atlas has opaque gray/white backing, so raw mode alone is not transparency evidence. Both use four shared-canvas12fps nonloop phases. No current literal consumer is established; existing structural hit events select weapon FX01–08 and do not confer their420ms lifetime on FX21. Pending source generation is not formal completion.

## FX22 assembly and FX23 link failure
FX22 uses brass hex nut, silver-gray bolt and small rubble: loose-hardware/seating-impact/repair-swirl/finished-fit. The bolt tilts/seats while the nut/contact remain registered; source-specific warm reflected light in the blue-swirl phase is documented, not programmatically recolored. FX23 keeps two gray-stone/gold/cyan-core beacons while their connecting beam destabilizes and breaks into cyan square fragments: intact-link/unstable-link/link-breakup/disconnected-beacons. Exclude opaque atlas backing and the neighboring lower-row flag fragment. Both retain12fps nonloop contracts, no established literal consumer or invented420/500ms lifetime. Contract support does not count unfinished art as formal.
