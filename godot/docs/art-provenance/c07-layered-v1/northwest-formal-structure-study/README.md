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
