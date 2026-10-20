# Existing Wasteland and Militia source review

Both existing sources are ready for formal binding with the explicit caveats below. This review did not bind them, export into the repository, generate artwork, mutate source files, or change Git state.

## Verified

- Exact original full-cell pixels and topic mapping: slot 7 `C01-FACTION-WASTELAND`; slot 8 `C01-FACTION-MILITIA-MERCENARY`
- Preserved master/raw/generated-origin bytes are identical and match recorded SHA-256. Stored requests, prompts and reference hashes also match
- Current exporter `validate_alpha()` accepts both source images and both native outputs. Current `fit()` performs full-canvas containment with 2px padding, and exactly reproduces the existing 128px proofs
- All three intended equipment/material objects remain recognizable at actual 128px on white, dark and olive backgrounds. Complete shield, cloth and weapon outlines; approved bold contours and broad matte facets; no visible magenta key, opaque plate or external glow
- Current canonical contracts remain 128×128, centered `[64,64]`, with no frames/variants or HD record

## Truthful caveats

- Militia's tiny source clasp groove is not retained. The accepted generated version has a plain gold boss. The parent accepted that documented nonsemantic stylization; this is not exact motif fidelity
- Both sources miss their prompt margin targets. Their silhouettes remain complete, and the actual exporter margin/edge gates pass. Parent accepted this documented deviation
- Wasteland's source perimeter is fully zero-alpha. Militia has 97 perimeter pixels at alpha 1, allowed by the existing validator; neither export has any nonzero-alpha outer-edge pixels
- At very low alpha, resizing fringes connect neighboring objects. All three substantial cores are separate at alpha ≥64. Therefore, do not say every gap pixel is exactly transparent or infer a cropped/merged silhouette from a low-threshold component count alone
- Stored provenance and raw bytes were checked, but the original tool response was not independently recovered or replayed

## Files

- `independent-qa.json`: exact hashes, mechanical checks, alpha evidence, component measurements and visual findings
- `verify_existing.py`: repeatable read-only source/exporter checks; writes only the independent QA report in this shared directory
- `final4-existing-output-inventory.json`: recovered existing Dragon candidate and bounded final-four file inventory

Dragon Covenant already had a generated PNG outside its kit folder. It depicts a literal balance-scale emblem, gray scale swatch and separate buckled loop. Preserve/recover this existing output before considering any regeneration. Its request linkage is plausible but unverified. The initial search found no separately mapped remake for the other three final factions. By the final inventory snapshot, the handling worker had populated Dragon and Oath Controlled raw/master files. Named Dead and Late Alliance were still prompt-only at that snapshot. Historical anonymous outputs were not exhaustively classified.
