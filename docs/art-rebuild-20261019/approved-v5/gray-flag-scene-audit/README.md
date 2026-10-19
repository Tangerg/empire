# Complete first-chapter scene audit

Snapshot: `7bbeb52`, before the four rural foundation cells were rebound in `75f66fb`. Retained as the original read-only diagnosis; the subsequent same-source before/after review is in `../farmhouse-clearance-review/`.

- Actual c01-01 BoardView/SvgBoardSurface, entire 1012×680 production viewport, no omitted pieces
- 19 rule actors, 4 rule/terrain buildings, 206 scenery pieces, 408 ground pieces, 10 spatial overlays, no foreground source pieces
- Two additional ambient farmer sprites are scenery, not extra rule actors
- 227 standing-layer drawings comprise the 19 actors, 4 terrain buildings and 204 standing scenery pieces; two remaining scenery pieces are ground lighting
- Offline Inkscape rasterization with actual facing transforms; no live browser or GPU playback claim
- All actor IDs, types, theme bindings, actual runtime image paths, cells and facings are in `runtime-scene-provenance.json`
- At this source checkpoint, 18/19 actor instances use approved-v5 art: actor 4, `field-cleric`, `c01.gravekeeper`, `C01-UNIT-GRAVEKEEPER` still uses runtime-hd. Seven unique themes total, six approved-v5 themes. Parent integration `04bebb8` subsequently binds Gravekeeper; this old evidence is deliberately not relabeled as current

## Scale and entrance findings

The authored decorative farmhouse uses scale 0.70; its visible alpha≥128 extent is approximately 63×52.85 logical pixels. The nearby decorative granary uses 0.68, approximately 56.44×57.46 pixels. Against a 44-pixel humanoid reference, those heights are 1.20× and 1.31×. An ancillary grain store can remain small; enlarging both houses globally is not justified.

The farmhouse foot is (400,259.2), only 27.8 pixels behind the rule granary at (400,287). Its source-alpha area is 95.67% covered by later standing art, overwhelmingly that rule granary. The small decorative granary at (502.4,329.6) is not occluded. These are exact-drawing, alpha-weighted initial-pose diagnostics, not rule visibility or a general occlusion system. Pairwise coverage fractions must not be added.

The correct owner for a bounded farmhouse correction is its authored decoration x/y. It has no engine identity, cell allocation, navigation, trigger, or interaction fields. Raising its draw order would cover the rule building; increasing scale does not solve the close footprints. The approved follow-on moves only that visual placement, retaining scale and normal footpoint order.

`gray-flag-scene-crops.png` presents the central village and both camps from the same full image. Enlarged crops are nearest-neighbor displays of the original 1x raster, not independently altered art.

## Camps/foundations: complete-source verification

The original 1254×1254 keyed master and its complete alpha intermediate were reviewed before inspecting isolated cells. Their source hashes, quarter crops, actual runtime cells and manifest metadata are recorded in `camps-source-contract.json` and `camps-source-contract.png`.

- `village-square-foundation`: camps-foundations index 3, footprint metadata 4×3; a paved plaza with a well and market props. It is a flat foundation, used at 0.30 beneath each rule village and at authored 0.82 in this scene. Visible alpha≥128 dimensions are 133×92 logical pixels before placement scaling (39.9×27.6 at 0.30, 109.06×75.44 at 0.82)
- `abandoned-cart-stop`: index 13, footprint metadata 3×3; a covered wooden cart, wheels, muddy ruts, barrels/crates and a small lamp post. It is standing settlement scenery, absent in c01-01 and present four times in c01-02 at scales 0.308, 0.357, 0.302 and 0.340. It is not a mud or hay decal
- Both use 4×4 atlas layout, 144 logical / 288 high-density cells and default foot anchor (72,139), corresponding to (144,278) in the HD cell
- Original master quarter boundaries are fractional (313.5 pixels); displayed diagnostic crops use rounded boundaries only. They are not a new production registration contract
- Any new independently generated replacement needs a reviewed source foot and full-canvas registration. Do not infer the foot from alpha bounds, flatten the cart, or elevate the flat plaza into the standing layer
