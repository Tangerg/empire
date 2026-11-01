# Western front box — bounded static general-appearance regression

## Scope
This fixes a static rendering defect for an existing ballista appearance. **C07 initial roster contains no ballista**; no actual C07 ballista gameplay event is claimed. The13actual friendly appearances were individually projected at56,10west against the same two fixed top samples and all had no alpha there. Torren/wolf-rider are actual C07 appearances and are checked separately in front of the box. No command, save, real gameplay, canonical terrain/rule/site change.

## Original failure and reused material
At legal56,10p, real ballista idle/west sprite scale[-.31,.31] has alpha at930163/931163 (box top). Original painted renderer has no covering part and selects the actor at both points, although original box base928179/935182 is in front. New independent three-face wood geometry preserves7positive material/base and2negative paving points. Texture comes from existing front cargo PNG; whole-sprite two-foot fit was rejected because source top/front witnesses became transparent despite.466px fit residual. No new image generation.

`west-cargo-geometry.json`, built by `c07-west-cargo-geometry.py`, owns implemented geometry. The helper atomic-material-geometry file is a separate **source-aligned proposal**, not a second live owner: both retain the two source base contacts; implemented far-right completion940179.5 is explicitly new design where source merges with adjacent barrel, not a third certified foot. It is not asserted to restore the full9-cell cargo responsibility.

## Affected validation
- Fresh build0warnings/errors. Native positive exit0;9/9material samples true to frozen contract.
- Ballista back:2/2real-alpha samples now blocked. Torren57,11 and58,12 front: both real-alpha samples visible/pickable. Canonical C07 wolf-rider57,11west: its real-alpha front sample remains visible/pickable.
- Deliberate box-depth-behind mutant reaches **West cargo actual-alpha back/front depth contract failed**, with valid result JSON; not merely exit1.
- Reused external texture/polygon renderer `TexturedPropGround` replaces canopy-only duplicate implementation. Bounds/prefix/texture explicitly belong to geometry; state/collision unchanged. Existing canopy positive still completes, filled-canopy mutant reaches **Frozen canopy material contract failed** at original969165.
- Screenshots: west-cargo-ballista-before-v1.png / after-v1.png same camera zoom8, explicitly NOT roster/gameplay; west-cargo-torren-front-v1.png zoom4 actual C07 appearance. All rendering issue counts0, canonical state digest unchanged.

## Remaining limits
Unbound local scene candidate only. Main building, canopy, two boxes, fence and blue stand are partial C07 assembly, with rear awnings/stock and full footprint coverage incomplete. Ground/material sharpness, repeated slate seams and contact shadows remain art integration debt. Performance NOT passed; no new native C07 play or authorization retry. Next work prioritizes actual C07 unit/site/doorway impact rather than expanding this absent-roster case.
