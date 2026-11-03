# Correct the C04 gate; classify native formatting debt — 2026-11-03

## C04 contract correction

The unchanged formal scene selected `c01-04-layered-ground.json`, but
`validate-blackflag-camp.mjs` still required the retired painted-ground binding.
The original static run failed at that path assertion before reaching its
negative controls. The native formal scene already required the assembled
binding and rejected a competing painted renderer.

Authority remains explicit:

- The C04 level in `grayflag-campaign-levels.json` owns gameplay authoring.
- `c01-04.json` selects the formal renderer and assembled document.
- The assembled document selects the object catalog and independent resources.
- Existing provenance supplies reviewed source-byte/dimension evidence. Its old
  unbound/preview status labels do not override the formal scene selection.
- The painted document and geometry audit remain explicit historical evidence,
  not a fallback renderer or another collision owner.

The validator now follows the selected file chain and checks the assembled
registration inside its testable validation boundary. It preserves the original
terrain, deployment, foundation, ownership, mission and campaign assertions.
It adds unique composition identities, resolved assets, the ten original
site-to-cell/body targets, reference-image dimensions and C04 source hashes.
The old painting's registration and byte integrity remain separately checked.
Setting formal readiness false can no longer skip presentation validation.

The reviewed current canvas is **1659×948**, while the historical painting is
**1660×948**. Both retain origin `[30,24]`, axes `[32,0]` / `[0,30]`, and scale
`1.25`. Nothing was resized or re-registered to satisfy the gate.

All game code, maps, images, placement geometry, collision authoring and saved
schemas remain unchanged. Physical contact/occlusion, alpha picking and actual
rendering are still checked by the native implementation, not reimplemented as
a second renderer in JavaScript.

## Verification

- Original static baseline: failed at the obsolete painted-path requirement.
- `node --check` and normal static validation: passed.
- Self-test: all 11 original semantic mutations and 28 added presentation/resource
  mutations rejected at their intended assertions. The unchanged baseline is
  checked again after mutation tests.
- Six invalid project resource paths rejected before any file read, with a valid
  project-relative path positive control. This also closes an absolute-path
  resolution error found while reviewing the new loader.
- Current native `BlackFlagCampChecks`: 332 assertions passed, exit zero, no
  error/leak/segfault/unhandled-exception diagnostics. It covers registration,
  all ten sites, material picking, 94 wall cells, physical supports, staging,
  save admission and detached legal-command mission probes. These are not new
  natural-route playthroughs or graphical acceptance.
- All four static scene validators passed independently.
- Metadata: 155 unique script UIDs and 14 positive/negative cases passed.
- Final diff review and `git diff --check`: passed.

The full `verify-native.sh` still stops at its earlier formatting stage. Its
later stages did not run as part of that aggregate; independently run checks
above are not represented as a full aggregate pass.

## Classification of the 184 formatting failures

The existing log has 68,130 whitespace diagnostics:

- **179 archived C# files, 68,077 diagnostics:** tracked historical probes and
  exporters in 58 C07 art-provenance directories. All are beneath tracked
  `.gdignore` markers and excluded from the game build by `docs/**/*.cs` in
  `Empire.csproj`. Some archive READMEs explicitly describe copying these files
  into separate external probe projects. They are evidence, not production
  composition paths.
- **5 active files, 53 diagnostics:** `BoardGroundView.cs` (2),
  `CompiledSceneSurface.cs` (9), `AssembledGroundChecks.cs` (12),
  `SceneSurfaceChecks.cs` (16), and `SceneSurfaceDrawChecks.cs` (14). These are
  real development-build sources; three are executable check scenes. The
  dedicated draw-check scene remains active even though it is not in the
  headless aggregate loop. These whitespace failures do not themselves prove
  behavioral bugs.

The cause of the archive noise is `format-native.sh`: its recursive Git
pathspecs select all native C# files, then folder-mode formatting does not apply
the project compiler exclusions or Godot's import exclusions. The first failing
format stage blocks the remaining aggregate gates.

No formatter boundary or source whitespace was changed in this batch. A scoped
follow-up should:

1. Establish a tested archive boundary only for documentation C# that is both
   compile-excluded and under an indexed `.gdignore` marker.
2. Preserve selection of tracked and untracked active sources. Add selector
   fixtures proving untracked markers cannot hide files and production, tools,
   Core and Core.Tests remain checked. Excluding every `.gdignore` directory
   would incorrectly drop active Core code.
3. Format only the five active failing files, review a whitespace-only diff,
   then run the unchanged substantive aggregate gates.

Do not reformat 179 archived evidence files or suppress the five active files
just to make the first stage green.
