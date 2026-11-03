# Correct the C04 and native formatting gates — 2026-11-03

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

The C04 validator correction left game code, maps, images, placement geometry,
collision authoring and saved schemas unchanged. Physical contact/occlusion, alpha picking and actual
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

At the C04-fix checkpoint, `verify-native.sh` stopped at its earlier formatting
stage. Its later stages did not run in that invocation; the independent checks
above are not represented as a full aggregate pass. The subsequent formatter
repair and its separate acceptance run are recorded below.

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

## Format-source ownership correction

`select-native-format-sources.py` retains the existing Git-visible source scope,
including untracked active C# files. Documentation archives may be omitted only
when the main project declares its unconditional `docs/**/*.cs` exclusion, an
indexed and still-present `.gdignore` marks that documentation subtree, and no
active project evaluates the file as a compile item.

MSBuild owns compile membership. The selector evaluates the 59 active project
entrypoints without building or restoring, includes their declared configurations,
and follows project references with the inherited configuration. This includes
Godot export configurations and the existing isolated-harness `EmpireRoot`
invocation contract. A source or archived probe referenced again by main, Core,
tests or tools remains checked. Imported compile items are evaluated by MSBuild,
not approximated by a second glob interpreter. Unsupported multi-target projects
or project-reference property overrides fail explicitly pending boundary review.

The measured selection is **385 of 564** existing Git-visible C# files:
155 game/presentation files, 154 Core files, 5 Core.Tests files and 71 tool files.
Exactly the original **179** documentation archives in **58** directories are
omitted. Core and Core.Tests remain checked despite their `.gdignore` markers.
All five active formatting-debt files remain selected. No archive, asset, scene,
map, project compilation rule or runtime behavior was changed.

The shell gate preserves NUL-delimited filenames, propagates Git/MSBuild/selector
failures, bounds evaluation and formatting, and refuses an empty source set
instead of accidentally formatting the entire folder. Selection completes before
any formatter invocation. The aggregate runs isolated selector regression tests
before the normal formatting stage; substantive acceptance stages are unchanged.

Only the five active sources listed above were formatted with the official SDK.
A Roslyn syntax-token comparison against their pre-format contents found exactly
the same token kinds, text and values, including structured trivia and string
contents. This is stronger evidence than a line-based whitespace diff, which
cannot distinguish multiline formatting from token changes.

## Formatter repair verification

- Focused official whitespace verification of the five sources: passed.
- Roslyn token equivalence for all five files: passed; no parse errors.
- Read-only evaluated repository selection: passed, 385 retained / 179 archived.
- All 30 isolated selector/formatter regressions passed, including real formatter
  rejection and repair of malformed active source while archived bytes stayed
  identical. These ran again successfully inside the aggregate.
- Full 385-source whitespace check, source metadata and all four static scene
  validators passed inside the aggregate.
- Main game and Core.Tests builds passed with zero warnings/errors. All 11 Core
  groups passed, including 16 independent full-level save/replays with 3,379 legal
  actions. This does not establish a victorious carried-state campaign route.
- Objective/outcome (91 assertions) and external catalog ownership (76 assertions)
  passed. Godot editor import completed; it warned about an existing nested
  historical `project.godot`, which remains excluded from game import.
- Native Narrative (3,306 assertions), OathPad (123), DynamicState (745),
  PaintedSurface and SceneSurface (60) checks passed with clean process teardown.
- The aggregate then **failed**, exit 1, in `AssembledGroundChecks` at the unchanged
  `Tree/SourceStructureContact` lookup. The renderer already owns a separate
  `Tree__ground_contact` floor projection, while this test still expects the old
  standing-sprite child. Both implementations were already present in baseline
  `1375167b7`; the archived contact-shadow-depth audit records why the renderer
  moved that shadow onto the ground. No assertion was skipped or weakened.
- The 15 subsequent native stages did **not run** in this aggregate. This is not
  a complete acceptance pass. The stale shadow-contract checks identified a separate
  correction against the floor projection's actual invariants, recorded below.
- Bash/Python syntax, Roslyn token equivalence, final diff review and
  `git diff --check` passed. Art, map, scene and provenance files have no diff.

The aggregate was run with the supported bounded budgets
`EMPIRE_CHECK_TIMEOUT_SECONDS=900 EMPIRE_REPLAY_TIMEOUT_SECONDS=1200 bash tools/godot/verify-native.sh`.
The failure was a concrete missing-node/null-reference error, not a timeout.
The selector itself measured 161 seconds with the default 300-second bound.

Actual graphical acceptance and GPU pixel checks require a graphical renderer;
headless scene assertions do not substitute for them.

## Subsequent assembled-ground contract repair

The [separate test-contract repair](NATIVE-CONTACT-SHADOW-CONTRACTS-20261103.md)
corrected the obsolete contact-shadow and material draw-grouping expectations,
added 32 deliberate native negative controls, and brought LightingChecks into the
aggregate. The complete aggregate then passed on 2026-11-03, exit zero, including
all 22 native scenes. Runtime source, original maps and art remained unchanged.
This later pass does not retroactively turn the failed checkpoint above into a
successful run. Graphical/export acceptance and broader art completeness remain
separate.
