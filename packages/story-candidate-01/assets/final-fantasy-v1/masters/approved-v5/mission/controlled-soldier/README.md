# Controlled soldier: approved-v5 source bundle

Four full-body, genuinely transparent built-in imagegen masters for C01-MISSION-CONTROLLED-SOLDIER. Selected order: idle, walk-a, carry-or-work, walk-b.

The original controlled-soldier atlas row, canonical strip and HD strip were inspected, with the released-veteran row compared for the control-hardware cue. Identity remains the worried clean-shaven young ordinary soldier in gray segmented armor, gray riveted helmet, dark brown belt/boots, physical ochre-gold collar and wrist bands, and two empty bare hands. The duty pose is restrained on-duty attention. No equipment, aura, faction mark or supernatural action was introduced.

## Fixed technical contract

- Adult reference: helmet crown y40 to lowest supporting idle sole y963 = 923 native pixels
- One fixed full-source-canvas scale: 44/923 = 0.047670639219934995
- Diagnostic frame: 104×64 logical pixels, origin [8,4]
- Canonical source foot: [807.75,963]; logical anchor [46.50595882990249,49.9068255687974]
- Common source extent: 1586×993; at most two transparent right/bottom padding pixels, export only
- Per-pose support offsets in registration-evidence.json, maximum 1.8591549295774648 logical pixels
- Every original generated PNG, native.png, raw.png and master.png is byte-identical for each selected pose
- No source cleanup, matte removal, pixel editing, recoloring, crop, independent pose sizing or source padding

The SVG exports place each complete native image at its actual width/height times the same fixed scale. Any remainder to the common extent is implicit transparent space, so no padding pixels are written into the masters. The exported PNGs and composites are technical QA only.

## Review files

- qa-native44-three-backgrounds.png and its 3× nearest-neighbor view: all four poses on black, white and scene green
- qa-walk-6.25fps.gif: native 104×64 idle/walk-a/idle/walk-b cycle
- qa-sequence.gif: native 104×64 all-pose sequence
- Per-pose qa-foot-measurements.png: measured boot sole bands
- source-evidence.json, registration-evidence.json, qa-review.json, bundle-manifest.json and SHA256SUMS.json
- Per-pose prompt.txt, exact request.json, generation-result.json and request-provenance.json

First walk-b generation repeated walk-a's stride and is preserved under walk-b/rejected-attempt-01, with its exact request, raw native bytes and rejection reason. A cuff-inspection plan was superseded before generation; it is retained only under planning/cuff-inspection-superseded-not-generated.

Consumer audit remains catalog-only. The original source audit found zero actual actor bindings or scene placements. No repository file was changed, no runtime consumer was fabricated, and no Git action was taken. Parent coordinator owns integration and final acceptance.
