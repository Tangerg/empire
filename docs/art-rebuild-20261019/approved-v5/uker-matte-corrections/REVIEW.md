# Uker broad-matte corrections v2

## Scope and method

Exactly one built-in image_gen.imagegen edit was performed per target on 2026-10-20. Both original full sources were inspected before their edits. No CLI generation, additional attempt, source pixel cleanup, filtering, cropping, resizing or recoding was used. The returned PNGs were copied byte-exact to raw.png and master.png in each original target's candidates/broad-matte-v2 directory. Both are opaque RGB 1254×1254 with full square coverage. Original raw/master assets were not modified. Grave fragments and physical ruin were untouched. No shared code, catalog, runtime, manifest or Git write was performed.

The request and exact prompt are beside each candidate; provenance identifies its original edit target, hashes, returned file and processing policy. proposed-registration.json contains only the two candidate entries, not live registration. Original hashes are in original-hashes.json.

## Full-source visual review

Inspected full original and returned images, native32 strips on black/white/olive, enlarged diagnostics, and both actual-adjacency connected grids.

- Memory overlay: broad irregular painted mottling and small cracks are largely removed. Central ring, circular center, four ring anchors, upper-right/lower-left ghost-foundation squares and diagonal purple orientation remain readily recognizable, with the same taupe/purple identity. This is not pixel-exact geometry preservation: fine contour irregularities were straightened, some peripheral stone divisions and the lower-right slab shapes were simplified, and jagged purple overlay patch boundaries became more geometric. Purple is somewhat cleaner/brighter. Large planes still have slight smooth shading rather than literally one uniform RGB value.
- Reality rebuild: broad blotches are removed successfully. Rust-orange left, muted purple right, continuous jagged central violet fissure, and central segmented circular node survive. Main block arrangement and small stones remain recognizable. Some face bevels and stone edges were simplified or sharpened; the fissure's internal shading was flattened and violet appears brighter. Minor smooth gradients remain within several large planes. Thus semantic composition is retained, but exact contour/plane fidelity and strict single-color fills are not claimed.

These are materially cleaner source-style candidates, not evidence of perfect shape-locked editing. Recommend accepting the cleaner rendering only if the documented small geometry/tonal drift is acceptable. No further attempt was made or is implied.

## Native32 checks and limitations

build_review.py imports the existing read_material/render_material helpers, sampling the complete 1254×1254 square with established Lanczos reduction and historical masks. Each strip has 16 distinct cells. Alpha bytes match the original runtime strip exactly. Exact RGBA edge equality passes 64 east/west and 64 south/north allowed pairs per strip. native-review.json records per-cell, source, sample and edge results.

Memory overlay remains a repeated purple circular-junction network at native32; narrow masks still suppress most peripheral architecture. Reality rebuild retains the strong vertical fissure and contrasting orange/purple arms, and the repeated center node is conspicuous. Horizontal connections still juxtapose different side colors; mathematically identical outer edges do not mean seamless motif continuation. The source cleanup does not redesign topology, remove repeated-center behavior or establish whole-scene acceptance.

Native32 connected grids use actual cardinal occupancy on black, white and olive backgrounds. Enlarged files use nearest-neighbor solely for diagnosis. Parent owns candidate acceptance, registration and whole-pack integration.
