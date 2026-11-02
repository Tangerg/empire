# Northwest gate formal-loader study — work in progress

Unselected static reconstruction, not official C07 or gameplay. No commands/AI/save writers. Public runtime stays at the selected scene before this gate.

All drawing/Pick/bounds/depth use production AssembledGroundDocument/View and CompiledSceneSurface. Offline SectionedSurfaceExport and TexturedSurfaceExport emit explicit contacts once; assemble-formal.py maps their source coordinates into one surface and one map-structure owner at canonical cell (11,11). UV remains original texture pixels except the two explicitly changed mount materials. Runtime placement scale=1, anchor/contact=0 uses source-map-pixel geometry, not original-texture-pixel geometry; declared pixelSize is still the real texture's dimensions. No separate wrapper or second runtime alpha/depth implementation.

Retained failures:
- Old lookout-base contained pre-correction three-section support, unlike later six-section native-base. Formal right shield witness (180,116) wrongly exposed the rear actor. Keep old-lookout-base-right-back.log and lookout-base-SUPERSEDED.json; old left-only passes did not cover this.
- The legacy source-review road JSON omitted integerLatticeSampleCount. First migrated test failed initialization, not a valid geometry negative. The corrected runtime audit JSON has identical polygons and the expected inclusive1468 union.
- Two former mount faces sampled right-tower banner cloth at UV1080..1160,490..560, not masonry. Bounded material correction retained all polygons/supports but remained visually rejected as a thin sloping sleeve.
- New cap export first failed because its 0.5px right overhang exceeded the previous virtual support extent. The endpoint was explicitly extended115→116 without changing front164; this is structural continuation, not a newly measured physical foot.
- Corrected section ownership plus the old lookout cut exposed a legacy exporter issue: a fixed strip centre can lie outside the post-cut owned horizontal span. The offline contact now uses the actual remaining geometry span's midpoint, then samples the same authoritative support curve. Runtime guard is unchanged; no epsilon or fallback. The failed initialization is not a negative-control pass.

The bounded next platform design has a horizontal top, explicit3.5-source-pixel thickness and two contiguous upper-tower faces. It is new structural design, not pixel restoration. Current preview/test results must be read individually; neither geometry nor art final acceptance is claimed here. Independent wood leaf is not yet built.

Recreate an external probe project from these files, link assets/data and imported resources from the exact game checkout, build Probe.csproj against that checkout's fresh runtime DLL, and run only the explicitly static scenes. Do not run broader C06/C07 action tests. Import/cache/toolchain files and credentials are not part of evidence.

## Tower cap closure (2026-11-02 17:34 UTC)

The former platform image is retained in `platform-mount-1180.png`; corrected actual native image is `platform-single-owner-1180.png`. These are fixed detached display poses, not gameplay or a whole-gate acceptance.

Two independent causes were proven. The original top UV sampled 214 integer texels below the material opacity threshold (including alpha0). The new stone-face interior preserves world vertices/supports, and uses the existing opaque material contract. Separately, old base columns overlapped the new upper tower/cap: at source(96,85.3), old base front164.124 was in front of new cap164; adjacent old column front163.544 was behind, producing stepwise old dark pixels. PlatformBaseExport derives the new replacement union from the five actual new surface polygons and inverse-transforms that single union into the old base cut. It does not change lower arch geometry, ground foot support or runtime depth.

The first attempt subtracted five adjacent faces separately and produced a degenerate zero-area polygon. The production guard rejected it; logs remain as an initialization failure, not a geometry-negative success. Unioning before subtraction yields a connected10-vertex replacement and passes the same guard. No runtime epsilon or depth offset was introduced.

Verification:
- Current platform supports415/members662. This is a cost count, NOT a performance pass.
- Four existing real Torren appearance front/back positions passed with continuous1468-point opening, fixed two transparent lookout holes/two timber positives, site target and Ground approach. Detached source state unchanged; no game commands.
- New overlap-owner positive passed through the formal loader. Restoring the old overlapping base fails specifically `Tower cap replacement must have exactly its new surface owner...` in platform-owner-negative.log.
- Restoring old top UV under the intended opaque contract fails specifically `Opaque surface material includes transparency` at texel628,373, alpha0.007843138 in top-material-negative.log.
- The initial overlap checker accidentally assumed semantic Node names; runtime uses numbered surface slices. That failed positive is retained as WRONG-NODE-IDENTITY-NOT-VALIDATION and withdrawn. The corrected checker joins support order from the exact loaded surface document to runtime numbered slices; it adds no runtime API.
- Fresh external probe build:0 warnings/errors. Runtime production code unchanged; prior fresh production build remains separately attributed, not rerun here.
- Six paused evidence/save hashes remain unchanged (hash record only; no save bytes published).

Remaining: independent wood leaf, whole-gate default-scale review and bounded total-cost measurement. Top material remains simple and edge styling is not final art acceptance. OfficialC07 and unique selected assembly are untouched.

## Independent narrow leaf and bounded whole-layout cost

The original right wooden edge conflicts with the reconstructed right stone at source166,130 and169,104. The chosen study is explicitly a new narrower leaf, not a restored original silhouette: left/middle feet153,130 and161,134 retained; right foot165,136.5 is an inset redesign. Hidden hinge hardware is not certified. Thin edge/two rails reuse existing timber entirely inside that outline, sharing its sloping local supports. Leaf owns no site and remains pass-through; stone is the sole structure/site owner.

`NorthwestLeafProbe` uses real Torren east-facing appearance at canonical p positions9,7 (behind) and9,9 (front), without any action command. It qualifies only alpha-overlap points that can Pick the actor when the leaf visual is hidden. This eliminates independent ground/stone occlusion as an explanation. Restored leaf hides all254 qualified rear samples; all152 front samples remain pickable. These are formal depth/Pick checks, not framebuffer assertions; actual native images are separate evidence. Wrong background/foreground support variants both fail the intended independent leaf assertion. The final combined1468-road/stone/site/approach check passed after joinery, not just before it. State digests remain unchanged.

Images: whole-gate-narrow-leaf-1180.png (plain near), whole-gate-leaf-default125.png (plain default), whole-gate-leaf-joinery-default125.png (minimal joinery default). Gray outside the source canvas is the fixed diagnostic camera, not the actual default battle framing. Other generic houses are existing unselected/fallback responsibilities, not improvements from this gate.

Performance is measured, not accepted globally. Same1180x760 camera, one actual detached actor,60 warmup+120 measured native frames,Compatibility llvmpipe, no screenshot writes during sampling:
- Local1.25 view: selected fallback741 calls versus complete gate952; +211,28.5% is not equal-quality regression.
- Full-layout0.65 view:2477 versus2688 calls(+8.52%); scene nodes8359 versus9213(+854). Gate415 depth entries plus leaf12; all assembled ground5483. Whole-layout primitives296020→298046.
- Process time varies and software rendering is not a user hardware/FPS conclusion. One AB only, not a throughput acceptance.
- Read-only audit identifies29 entirely transparent native supports with zero alpha across every UV rectangle including a2-texel halo. No pruning has yet been adopted in this checkpoint.

Selected/official scene remain unchanged. Geometry/candidate visual evidence does not certify fullC07 gameplay, global art consistency, or full campaign performance.

## Bounded zero-contribution removal

The offline pruning candidate removes29 **whole** native-alpha supports, leaving386 stone supports/633 members, plus12 independent leaf supports. Every removed UV rectangle including a2-texel halo is exactly alpha0, not merely below an opacity threshold. All other polygons,UVs,contacts and relative order remain exact. The formal renderer explicitly uses Linear filtering without repeat; Godot documents that this samples the nearest four pixels, not mip levels ([official CanvasItem reference](https://docs.godotengine.org/en/stable/classes/class_canvasitem.html#enum-canvasitem-texturefilter)). This is not a global filter change or a proposed runtime cache.

Because IntersectsPointer intentionally uses conservative geometry, pruning also retains any support whose geometry bounds overlap the target-pointer bounds. The29 removed supports are disjoint from that target, preserving cue placement semantics as well as material/Pick. Two offline negative controls reject removal of nonzero material and disregard of this pointer contract.

Native default1.25 before/after world frame matches exactly:1180×688 region,0 differing pixels and0 maximum channel difference; only the72px diagnostic variant title is excluded. Actual screenshot whole-gate-pruned-default125.png. Fresh formal continuous1468-road/site/left-back check and independent leaf254 rear/152 front checks pass after pruning; six paused files unchanged.

Whole-layout120-frame sample:2688→2672 draw calls;9213→9154 scene nodes;298046→297462 primitives. Compared with the less-detailed current selected fallback baseline2477 calls, the final added cost is195 calls(+7.87%), not an equal-quality regression metric. No FPS/global performance acceptance follows. No further architecture/precision optimization is included.

This closes the bounded unselected gate study. Official C07 and selected assembly are still unchanged at this checkpoint. Any future selection must reference these sole final exported stone/leaf surfaces, not rebuild a second renderer or substitute old failed lookout data.

## Joined the sole selected assembly

The final stone and leaf now live in data/scenes/c01-07-northwest-gate-{stone,leaf}-surface.json and are referenced only by the existing selected catalog/layout. These runtime resources own the chosen geometry/UV/contact; external variants are frozen audit projections, not another advancing scene. Exact reviewed-resource SHA equality is recorded in selected-integration.json.

Current derived status advances61→63instances,7→8sites,5056→5454ground depth entries. The canonical16-site native inventory confirms the northwest fallback node is suppressed. Door94,132 targets canonical11,11; approach200,184 remains Ground12,11. The site centre itself remains Ground by the existing authored pointer contract, not a failed anchor move. Original barracks/granary/council targets and pass-through roofs remain correct. Repeated RenderState does not accumulate entries or sites.

Final selected checks: full assembly/static responsibility inventory; four stone front/back poses; independent leaf254 rear/152 front actual-alpha/Pick controls;1468continuous road samples; original lookout hole/post witnesses; unique station ownership. All initial engine snapshots remain unchanged. Production Rebuild and external probe build each0warnings/errors; indexed metadata154UIDs. No action/AI/save writer was invoked.

Actual selected default frame is selected-northwest-default125.png; world area equals reviewed candidate exactly. The first selected screenshot had a stale diagnostic suffix claiming unselected; it is retained as STALE-LABEL and not used for user-facing scope. Corrected label explicitly says selected candidate/official unchanged. selected-whole-layout065.png shows the full incomplete layout, including untouched generic placeholders and missing wall/yard responsibilities, not a final scene-quality claim.

Officialc01-07.json remains SHA5bd8cac7e2263dcffaf23b88c38b25515575678c3085a227e4a1f58f61abcd88; six paused-file hashes unchanged. Northeast lookout/leaf, other scene responsibilities, overall material/contact integration, global performance and separately authorized gameplay remain incomplete. This northwest gate is closed for this bounded batch; no further local material micro-iteration is scheduled.
