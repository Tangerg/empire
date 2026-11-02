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
