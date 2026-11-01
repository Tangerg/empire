# Southern supply — independent main-body candidate

## Scope and owners

The existing selected assembly now contains one independent southern warehouse main body, bringing it to **25 instances / 6 semantic owners / 2074 depth entries**. The previous 24 definitions/order/placements are exact. No new live preview scene or renderer wrapper was added. `c01-07.json` remains the original 55-group production scene, with SHA `5bd8cac7e2263dcffaf23b88c38b25515575678c3085a227e4a1f58f61abcd88`; selected remains `runtimeReady=false`.

The pale main building is separate from the original long canvas, poles, shelves, crates and bales. Those are **absent**, not silently part of the main body's depth. The semantic site `(70,50)` and approach `(70,51)` do not identify its physical foundation. Current main contact is source `(1240,774)`, approximately the visible near wall corner (about ±2 source pixels); concealed left/right feet and the modest door are design reconstruction, **not precise recovery of unseen source pixels**. Original uniform `front=(1259,781)` and legacy facade mask include cargo imagery and are not independent proof of a house foot.

Original and candidate native screenshots use the same 1180×812 viewport, 2.5 zoom, focus `(77,44)` with recorded view metadata. These are static display fixtures, **not actual C07 gameplay or whole-assembly visual equivalence**. The original full site has rack/canvas occlusion that may be legitimate. The 180-pose original survey of two actual C07 appearances found overlaps, but does not establish a main-house cut-head bug independently of these other objects. Do not relabel it as that reproduction.

## Real-foot fix

The first independent candidate used an ideal three-point support line. Irregular bottom stone pixels extended below it; the normal ground-depth partitions visibly clipped them into teeth. See `southern-supply-main-candidate-context.png` versus `southern-supply-main-foot-corrected-context.png`. No texture threshold, alpha deletion, shadow cover-up or rendering rule changed.

`export-southern-supply-support.py` identifies the manually inspected physical foundation band, not the whole image's lowest alpha. For each narrow 8-texture-pixel span (1.4 source-scene pixels at scale .175), it records the deepest actual solid base texel's bottom boundary. Outside the wall-foot range, only roof overhang support is extrapolated. The generated sprite `depthStrips` is the sole advancing runtime support fact; the script and alpha witnesses are reproducible provenance, not another active rendering model. The generated RGBA remains intact. The black/brown RGB visible when viewing transparent pixels is mostly alpha zero; it was not erased based on RGB appearance.

128 physical foot texel witnesses retain body coverage and lie in front of their terrain surface. The current native image shows intact foundation stones. Wrong old ideal-line support is independently tested as a negative, not simply checked for any failure.

## Interaction and actual-appearance contracts

- Physical door target → `Object(70,50)`.
- Original **and candidate** semantic cell centre → `Ground(70,50)`, approach → `Ground(70,51)`. The initial fixture incorrectly demanded Object at the off-building site centre; its failure is retained, then corrected against an explicit original-scene baseline. The building was not moved to make that assertion pass.
- All 256 pixel centres in the approach cell remain clear of this body.
- Actual C07 Torren at `(81,48)` and `(76,49)`, engineer at `(82,47)`: body-relative front visibility; Torren `(76,41)`: body-relative back occlusion, with ordinary occluded-unit cue retained in the native image. All use renderer clones on canonical `p` cells; no command, AI, move, turn or save.
- Overlap samples respectively 95,145,140,603; zero wrong body ordering. Representative front samples pick the real unit; representative back samples have ground behind the actor and body in front, not the withdrawn ground-already-in-front false control from the earlier granary investigation.

## All 60 foundation audit cells — inventory, not full closure

`southern-supply-foundation-responsibility.json` enumerates every unchanged canonical blocked cell. At centre samples:45 body-covered /15 not. Across each 16×16 cell:36 full body-silhouette cells,20 boundary/mixed cells,4 with no main silhouette. Screen silhouette is explicitly **not** proof of a complete physical footprint.

The 4 zero-body cells are `(72,46)`, `(73,47)`, `(74,47)`, `(79,48)`. Original responsibility groups assign these to middle crates, right grain stack and east sack/crate stock. Their missing objects remain work, not a reason to enlarge the house or alter collision. Partial cells retain exact pixel counts and cross-group ownership references; tiny boundary intersection is not described as full coverage. Next local work is the actual canvas/cargo needed to restore these responsibilities, then continuous courtyard/wall routes. No blanket generic replacement of the remaining 25 original foundations.

## Verification and limits

Fresh external probe build and source-import completed. Positive static main test passed. The failed first large overlap survey was interrupted (native shutdown error); only the bounded 180-pose survey is counted. A fixture local-variable compile failure and the invalid site-centre assertion are preserved, not treated as successful tests. Targeted flat-front support rejects 140 front-character overlap samples at the local-depth assertion; old ideal-line support rejects 80 physical foot samples at the clipping assertion. The all-background variant fails earlier at the door-hidden-by-ground assertion and is **not** counted as a targeted back-role rejection. Final production-build results are listed in `verification.json`.

True paused C06 save evidence hashes remain unchanged; C06/C07 gameplay did not advance. Production C01–C06 rendering code was untouched; the broad gameplay runner was not invoked. Full C07 performance, full scene foundation/courtyard/material/cue acceptance remain pending. This adds 192 sprite depth entries, not a frame-rate success claim.

Bright orange dense roof lines, sharp edges versus softer terrain and weak contact shading are acknowledged cross-scene art debts. No further cosmetic generation loop was used to claim final art quality.
