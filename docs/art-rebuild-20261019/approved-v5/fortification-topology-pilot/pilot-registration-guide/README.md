# Stone area pilot: technical guide for approval

No wall image generation or production change has begun. The PNG/SVG files are coordinate diagrams, not painted wall sprites or replacement assets.

- `pilot-registration-guide.json`: exact logical/native geometry, four masks, ports, expected opaque regions and gate requirements
- `pilot-common-technical-guide.png`: common footprint, cap, fixed anchor and projection
- `pilot-four-mask-guide.png`: masks 5/10/7/13 with joined and exterior boundaries
- `pilot-open-run-fixtures.png`: open-ended NS/EW runs and a 2x5 filled-top strip
- `pilot-common-coordinate-overlay.svg`: exact96-coordinate vector overlay; annotation only

## Items to approve before generation

1. Retain96 logical /192 HD,32px board pitch, source anchor(48,80), ground rectangle(32,48)..(64,80), full top rectangle(28,32)..(60,64).
2. Under the specified cap lift(-4,-16), only S/E vertical masonry faces are visible. N/W exposed boundaries are closed rear cap edges, not additional outward faces. Therefore mask7 may correctly be cap-only, with its W exterior edge treatment, while mask13 includes an E wall face. All caps remain full squares.
3. Propose1254px native source to match the available generator family. Native pitch418px; anchor(627,1045); cap x365.75..783.75,y418..836. The .75 boundaries are geometric coordinates, not rounded source cropping. If integer-native corners are required, change the common native size before any generation, not by per-source alpha fitting.
4. Four masks cannot close finite run ends. The pilot fixtures intentionally have continuation cuts; no invented end caps, rotation, mirror or gatehouse substitution. This does not enable a partial production kit.
5. Imagegen must author actual stone cap and faces. Exact technical export/clipping may reject exterior excursions; it may not paint missing cap, erase a black joining seam, normalize alpha or hide nonmatching ports. Failing joins require a new native generation/edit or common-contract revision.

The approved wall-straight/corner images were viewed as matte stone material references only. Their old oblique ground geometry and tall terminal piers are expressly excluded. No source prompt or painted wall master has been issued yet.
