# C03 equipment batch 05

Four distinct built-in imagegen source candidates are complete and verified in isolation. Only the four new approved-v5 equipment master directories and this new batch evidence directory were written. Shared source registry, runtime, manifests, progress, implementation and Git were not modified by this worker.

## Canonical roles and legacy audit

Reserved identities retain their canonical topic IDs, labels, native 48x48 size, anchor [24,24] and exporter padding 2:

- c03-equip-4dad36ef 云梯队主装备组: a steep five-rung assault ladder with two-wheel base and rear brace.
- c03-equip-58b98436 边军斥候主装备组: a plain leather riding saddle, two large stirrups and an ivory rolled map.
- c03-equip-5e64c3a6 火箭营主装备组: four red-tipped historical-fantasy fire-arrow rockets in a compact wooden rack.
- c03-equip-6a62b51e 铁锤破阵手主装备组: one oversized twin-faced dark iron hammer on a short wooden haft with three broad grip wraps.

The canonical manifest, current package manifest, ordered 07-topic-catalog, 02-asset-scope and ART-DIRECTION were read. Scope explicitly identifies 云梯队 as engineering and 火箭营 as ranged. The scout and hammer identities follow their explicit canonical labels. None of these objects adds a gameplay mechanic or claims an exact historical loadout.

The complete equipment-board-source.png, actual builder input board equipment-board-01.png and approved broad-matte token source were visually inspected. These informed textual prompts, not imagegen reference arguments. Their exact SHA-256 values and reference roles are in each source-evidence.json.

The old builder iterates the 8x6 board row-major and zips it to category topics, then sorts the final manifest by topicId. Its original manifest-complete.json input is absent. Therefore the surviving ordered 07-topic-catalog was used to reconstruct the likely builder slots, supported by the actual preserved legacy pixels. Sorted output-manifest positions are explicitly not treated as source slots. This reconstruction limitation is retained in legacy-builder-audit.json.

All four are role reconstructions, not exact legacy-slot recovery:

- Ladder crew: reconstructed row 2, column 7 and unchanged legacy runtime show a planked bridge ramp. Full-atlas row 3, column 1 supplies the wheeled assault-ladder vocabulary.
- Frontier scout: reconstructed row 2, column 1 and preserved legacy show an entire saddled horse. The new saddle/map grouping intentionally abstracts the scout equipment using atlas row 2, column 2 and row 4, column 7. An older remake already used saddle/map; this source retains that role while adopting the broad-matte approved-v5 style.
- Rocket company: reconstructed row 3, column 2 and unchanged runtime show a wheeled battering ram. The actual rocket-rack vocabulary is row 3, column 6.
- Hammer breaker: reconstructed row 4, column 5 and preserved legacy show grain containers. Atlas row 6, column 1 supplies the hammer/tool vocabulary. An older remake already used a warhammer; that identity is retained without copying the older generated pixels.

No original evidence or current runtime file was overwritten. The copied -legacy-native.png and -before-runtime.png files retain their exact bytes and separate hashes. legacy-native-enlarged-review.png displays both states for inspection.

## Visual review and limitations

All four complete 1254x1254 generated sources, the complete source contact sheet on white/black/olive, and actual native48 exports on those backgrounds were visually reviewed. The ladder rung gaps and stirrup holes remain transparent. All subjects are contained; no baked opaque plate, checkerboard, cast shadow, white halo or rectangular alpha haze was observed. Native48 silhouettes remain distinct: tall ladder, saddle with large loops, four red tips on a rack, and diagonal broad-headed hammer.

The sources use smooth broad matte color planes, heavy contours, minimal secondary shading and no mottled microtexture. Limitations are disclosed, not postprocessed:

- All sources use more than two literal color values through antialiasing and a few broad shaded facets. Some surfaces retain subtle smooth gradients. They do not form granular clusters, scratches or microscopic texture.
- The ladder rails have a few sparse elongated broad color accents and the wheel rims have several broad facets. The main five rungs and open gaps are clear.
- The scout map reads as an ivory roll; scout function is a canonical role interpretation, not a uniquely identifiable mapped location. The rolled map uses three cord bands rather than the requested single cord. Large stirrup bevel highlights remain simple and matte.
- The rocket rack is a deliberately simplified game symbol based on the atlas. Its unlit red conical tips and ochre cylinders are not a technical depiction of historical construction or a claim of exact historical authenticity.
- The hammer has a narrowed center and enlarged end faces rather than one unbroken cuboid. Broad edge bevels remain, but there is no shiny metal microfaceting.
- The requested ten-percent margins were not consistently followed. Core bounds remain fully contained, and unchanged source/perimeter/native export guards pass. No crop, recenter or source correction was used to manufacture margins.

No correction attempts were needed or made. Parent acceptance, formal integration and live-game review are separate and not claimed here.

## Integrity and verification

Each master directory contains raw.png, master.png, prompt.txt, source-evidence.json and alpha-qa.json. All raw/master pairs are exact byte-for-byte copies of their respective separate built-in imagegen outputs. Four SHA-256 values are distinct. All generated source PNGs are genuine RGBA with transparent exterior and near-opaque subject.

No crop, repaint, filtering, recoloring, resampling, alpha removal, cleanup, thresholding or manual pixel repair was applied to the masters. Background composites and reduced native previews are separate review artifacts only.

Source perimeter results under the existing edgeMaxAlpha <= 1 guard:

- Ladder: alpha0 throughout the perimeter.
- Scout: 42 perimeter pixels at alpha1, preserved unchanged.
- Rocket rack: 4 perimeter pixels at alpha1, preserved unchanged.
- Hammer: alpha0 throughout the perimeter.

All four native48 perimeter edges are exactly alpha0. The existing exporter fits the entire authored square to 44x44 at [2,2] using LANCZOS, without alpha-bounds cropping. Native size, [24,24] anchor and padding 2 remain unchanged. Saved native previews match export_overrides.plan pixel-for-pixel.

A private temporary fixture containing copied unchanged manifests and these exact source bytes successfully ran export and export(check=True), with identical reports for all four. Production runtime was never written. See export-plan-qa.json, isolated-export-check.json and batch-alpha-integrity-qa.json.

## Parent handoff

- integration-candidates.json: four-entry isolated source catalog fragment.
- delivery-manifest.json: identities, paths, hashes and evidence.
- native-review-white-black-olive.png: true 48px exports.
- full-source-review.png: complete sources at full resolution on olive.
- full-source-review-white-black-olive.png: complete reduced source contact sheet.
- Each -source-white-black-olive.png: full-resolution source on three backgrounds.
- generation-spec.json and per-master prompt.txt: exact prompt set.
- persist.py: one-time persistence and isolated validation procedure, not a runtime integration command.

The parent remains sole owner of acceptance, registry/progress writes, formal export/check and integration tests. C01 shaman was not touched.
