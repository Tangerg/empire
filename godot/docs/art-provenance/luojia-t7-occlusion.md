# C07 natural T7 painted occlusion repair — 2026-10-27

Source: the unchanged approved `assets/native/scenes/luojia.png` (1536 × 1024),
registered at 16 image pixels per canonical cell. Review used the real
`C07-natural-t7-start.json` save and native T7 screenshots, not a relocated game.

## Confirmed omissions

- `south-militia`, cell (42,36), has contact (680,584). Its original opaque
  silhouette spans approximately x662–697/y557–586. No previous material part
  overlapped it, despite the visible southwest inner wall in this region.
  `southwest-upper-wall-t7-depth` restores that masonry silhouette, including the
  terminal pier. Its ground front is the visible pier base at (695,618).
- `east-militia`, cell (66,24), has contact (1064,392). The house roof already
  correctly hides part of the actor, but the dense tree crown and trunk in front
  of it were absent from the material list. `east-inner-tree-t7-depth` traces
  those two connected visible materials; the trunk reaches ground at (1044,428).

The existing roof/facade parts and actor foot anchoring are unchanged. For the
original sprites, `groundFront == anchor`; there was no general actor depth
bias to correct. This repair does not enlarge visual rectangles, raise all
scenery above actors, or rewrite canonical movement, collision, sites, units or
saves. These patches use the established non-target `pass-through` contract:
opaque material hides actors from rendering and entity picking, while the
canonical ground query remains authoritative.

`LuojiaChecks.CheckPainting` includes detached visual probes at these two actual
T7 cells. It checks native material-versus-actor depth, real opaque sprite overlap,
shared drawing/picking visibility, and canonical snapshot immutability. These
probes are not a second normal-AI campaign run.

## Deliberate boundary

This is not a complete city segmentation. The northern barracks' grain sacks,
individual fence slats and neighboring cypress, and further garden foliage are
not newly traced here. Their true individual grounding/transparent gaps require
separate pixel review. A baked wall face or tree canopy at a cell's projected
contact does not by itself prove the canonical street underneath is wrong.
No ground-layout or collision changes are inferred from these occlusion images.
