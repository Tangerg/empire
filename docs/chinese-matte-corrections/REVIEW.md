# C03 broad-matte source corrections

Status: two unselected, versioned source candidates. No new topics; no catalog, manifest, runtime, progress or Git changes. Existing source masters and raw files are preserved.

## Verdict

Both candidates fix the specific excessive mottling/mosaic issue and are recommended for parent selection review. This is not a claim of pixel-identical silhouette or exactly two mathematical colors.

- Military token `c03-battle-prop-27872fa1`: bronze face, rim and ornament surfaces now have calm, continuous broad shading instead of tiny mottled facets. Square center remains transparent; cord loop opening, braided cord, tassel, studs and cloud-scroll identity remain. Cord and tassel retain structural facet detail, consistent with their geometry. At native64 the face is cleaner and the raised symbol reads more clearly. Original and candidate native opaque bounding boxes match. Source opaque-mask IoU with original: 0.986380; small contour drift exists, so exact pixel silhouette invariance is not claimed.
- Cauldron `c03-battle-prop-2f038eea`: pot body mosaic is removed; hearth rocks now use a handful of large planes instead of dozens of small ones. Both pierced handles, ladle, three food chunks, soup, flame, hearth and foreground logs remain. At native64 it retains identity and composition with less visual noise. Some log grain-like structural facets and roughly 4–6 planes on larger stones remain, but the known mosaic mismatch is resolved. Source opaque-mask IoU with original: 0.996301; small contour drift exists.

## Verification

Viewed each original and native generated source at full size, the approved engineering reference, full-source side-by-side comparison and native64 side-by-side on white, black and olive backgrounds.

Both raw/master pairs are byte-identical copies of native built-in imagegen output, 1254×1254 RGBA. Existing `validate_alpha` and `fit_canvas` were used without modification. Review exports use the entire source canvas, LANCZOS contain into 60×60 with origin (2,2), no crop and no alpha stripping. Both native64 exports have zero nonzero outer-edge pixels. Source edges retain native alpha=1 residue: token 1 pixel, cauldron 78 pixels. This passes the existing unchanged edge-alpha≤1 contract; residue was not removed.

Artifacts:
- `full-source-comparison.png`: reference / original / candidate.
- `native64-comparison.png`: actual native64 reference / original / candidate on three backgrounds.
- `candidate-evidence.json`: candidate paths, hashes, alpha results.
- Each candidate directory contains `raw.png`, `master.png`, `prompt.txt`, `source-evidence.json`, `alpha-qa.json`, `native64.png`.

Generation: one built-in `image_gen.imagegen` source-edit call per topic, original source as edit target and approved engineering master as style-only reference. Exact prompts and reference SHA-256 values are recorded in each candidate directory. No image-altering scripts were used. The persistence script only copies native bytes, performs the existing review export, computes QA and assembles review sheets.
