# Chinese battle props, batch 03

Four candidate masters are ready for integration. No shared catalog, runtime, code, manifest, progress record, or Git state was modified by this worker.

## Identity and visual review

Each asset was generated separately with built-in image_gen.imagegen, true-alpha requested, using the complete original Chinese props atlas for semantic identity and approved bprop-engineering-1 master only for chunky style. Identity locations are visual semantic matches, not verified historical crop coordinates. The canonical manifest confirmed all four were previously uncompleted in approved-v5.

- c03-battle-prop-30a92ab8, 桥板工程组: blunt rectangular bundled bridge planks with two thick blue lashings. Distinct from pointed engineering stakes. Six broad beams, no terrain tile.
- c03-battle-prop-5cc33643, 户册证据组: blue stitched ledger stack, upright red ledger, and open cream register. Ruled marks are not legible writing.
- c03-battle-prop-6a51fb07, 官军门牌标识组: wooden official-army plaque with Chinese guardian-lion mask, finial, short post and large red tassel. The mask has more facet detail than an ideal two-tone mass, but remains one clear golden emblem at 64px.
- c03-battle-prop-6ecfadac, 折叠案生活组: crossed-leg camp writing desk, cream sheet, scroll and blue cup with two exaggerated brushes. Brush silhouettes remain identifiable at native size.

All four full sources and the actual-size 64px white/black/olive review sheet were visually inspected. No apparent clipping, rectangular background, detached debris, bright fringe or generated labels. Open desk-leg gaps and sign-tassel gaps preserve alpha. Compared to the earlier batch's token/cauldron these sources avoid mottled surface noise. They retain some broad decorative facets and wood notches beyond strict two-tone shading; the sign's face is the busiest form. These are integration candidates, not a claim of live-scene approval.

## Mechanical verification

All source PNGs are 1254x1254 RGBA and pass the existing validate_alpha. raw.png and master.png are byte-identical copies of native generated outputs, with SHA256 and exact prompts recorded per topic. Original returned file paths and output hints are preserved in generation-records.json and source-evidence.json. All original generation files remain in place.

Ledger and desk source edges are fully alpha zero. Bridge planks contain five source-edge pixels at alpha 1, and sign contains three at alpha 1. These native residues are preserved without cleanup. Every 64px export has exactly zero nontransparent outer-edge pixels.

Review export exclusively uses tools/art/export_overrides.py fit_canvas(source, (64,64), 2), containing the complete authored canvas in 60x60 at (2,2) with the existing resampling contract. No crop, recolor, pixel cleanup or alpha stripping.

## Handoff

- proposed-integration-candidates.json: four source paths and hashes for main integrator.
- delivery-manifest.json: canonical identities, evidence paths and review paths.
- native-review-white-black-olive.png: native-size review sheet.
- generation-records.json: exact final prompt text and original tool output-path metadata.
- persist.py: deterministic preservation and review-export script, without rerunning generation.
- Per-topic masters directories contain raw.png, master.png, prompt.txt, source-evidence.json and alpha-qa.json.
