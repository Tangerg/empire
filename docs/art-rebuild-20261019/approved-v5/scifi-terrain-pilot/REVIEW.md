# C02 Farlight terrain pilot: four reviewed material candidates

Generated 2026-10-20 with built-in image_gen, one call per topic. No CLI fallback. No shared catalog, runtime, manifest, code or git changes were made by the art worker.

## Selection and source identity

The first four terrain records in final-scifi-v1/manifest-final-scifi-v1.json are cargo-hold, living-deck, projection-garden and service-pipes. The complete original terrain-board-04.png was inspected. Its top row is cargo / living / pipes / projection, so garden and pipes must NOT be assigned by lexical position. The four entire original runtime strips were inspected and preserved under original-reference. Subject reference is named and hashed in each master's provenance.json.

The historical build_final_scifi_v1.py build_terrain implementation explicitly uses opaque materials, four sampled variants for ordinary ground, and a single opaque material clipped to sixteen exact historical NESW masks for connected ground. This is not a sprite or scene replacement. The requested floor interpretation retains cargo tie-downs and plates, warm residential mat/panel materials, green cultivation inserts/cyan service channels, and graphite/bronze pipe junction material respectively. Tall room furnishings are intentionally not represented as traversable ground.

## Registration

Use proposed-registration.json as the explicit candidate entry set. Raw and master are byte-identical, unchanged generated files. Every directory contains exact request.json, prompt.txt, provenance.json, raw.png and master.png.

Cargo and living are 1254-square: four distinct 627-square quadrant windows, ordered a/b/c/d = top-left/top-right/bottom-left/bottom-right.

Garden output is 1269×1240 rather than the requested square. No aspect fit or stretch was applied. Four explicit 620×620 corner windows preserve scale and omit a 29px center vertical strip; this is deliberate material sampling, not a silently inferred crop. Review shows all four biological/floor materials remain readable. The raw canvas remains intact for audit.

Pipes is one 1254-square opaque material with a central hub and four cardinal branches. Existing render_material/canonical_masks applies the established 32px core/arm shapes. All sixteen outputs have distinct geometry; no connectivity state is dropped or painted over. The derived atlas uses true RGBA transparency, not black/checkerboard background.

## Verification and review

Native output strips and six-times nearest-neighbor enlargements are in this directory. all-native32.png shows all four at actual runtime size. Native view confirms cargo/living/garden remain visually distinct in palette and surface language, with four distinct variants each. Pipe branches remain readable across all sixteen masks. Strong matte contours survive reduction; no labels or blank margins appear.

native-review.json includes source/file hashes, source windows, sample/seam reports, exact edge contract results, per-cell pixel/alpha hashes and output alpha hashes. Distinct cells: 4, 4, 4, 16.

Original and candidate alpha strips match exactly:
- Variants 32: f47a8ec3e9aff2318d896942282ad4fe37d6391c82914f54a5da8a37de1300c6
- NESW 32: c9ca0ff6335c19893b720ceed9b74cea27b0a9b225696f1efa0b9216de85ef3a

Existing edge matcher modifies only the established sampled outer boundary contract. It passes exact RGBA equality for every allowed neighbor pair. These previews are export evidence, not hand-edited replacements.

## Integration recommendation and limits

Recommend registering all four exact entries with the parent's narrow C02/native32 exporter extension after independent inspection. Preserve current manifest dimensions, topic IDs, four variant ordering and NESW bit ordering. Do not fabricate a C02 HD manifest or reuse C01 content-pack paths. Do not run the historical all-category builder: its manifest-complete.json source is absent in this checkout and rebuilding unrelated assets is outside scope.

No C02 terrain-specific presentation consumer was found by topic search under packages/story-candidate-02/src. Existing C02 index.ts is a campaign scaffold, unlike C01's richer presentation package. Therefore updating canonical runtime art alone is not proof of a live C02 battlefield route; that requires separate consumer verification by the integration owner. No gameplay rules or path semantics should be inferred from floor art.
