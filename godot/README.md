# Empire: native Godot C# migration

## Run the GrayFlag campaign integration

Install Godot **4.6.3 .NET** (not the standard build) and Microsoft **.NET SDK 8**.
Open `project.godot`, build the C# project, and run it. From inside `godot`, use:

```sh
dotnet build Empire.csproj
godot --path .
```

The executable name depends on your Godot installation. In the approved cloud
workspace, `source ../tools/godot/env.sh` exposes the local SDK and `$GODOT_NET`.
The helper defaults to a sibling `empire-tools` directory; set `EMPIRE_TOOLCHAIN_ROOT` for another local installation. It has no `/workspace/shared` dependency. On Windows/macOS use the official SDK and Godot .NET editor directly. No TypeScript or browser process runs in the game.

The game opens the original prologue, briefing and authored choices, then starts the battle. Use Save/Load/Restart in the battle header; F5/F9 also work on story screens. Saves are in Godot `user://grayflag-campaign-save.json` and contain campaign, current battle and event history. Restart requires confirmation and restarts the current battle, not the whole campaign.

Select a friendly unit, choose an action category and destination, then confirm
the complete order. Movement commits the destination and waiting together;
attack orders include their exact firing position. End turn lets the original
ported AI play the opposing side. The native core revalidates every command.

From the repository root, `bash tools/godot/run-native.sh` builds, imports and
launches using the configured workspace-local toolchain. See
[run and package instructions](docs/RUN-AND-PACKAGE.md) for portable setup and
the explicitly pending stand-alone export/visual acceptance gates.

## Historical migration evidence (2026-10-20)

These earlier migration results are not a claim that the current full gate is green.
The c01-02 reconstruction and its current verification limits are recorded in
[Formal Three Bridges](docs/FORMAL-THREE-BRIDGES.md).

Three complete native battles reproduce TypeScript's independently generated
142, 119 and 135 action streams, ordered events and every intermediate state
hash. The original C# AI also chooses these streams independently. Core tests,
additional numeric/UTF-16/RNG edge tests, movement, lifecycle, damage and combat
oracles live in `Empire.Core.Tests` and `../tools/godot`.

```sh
# From the repository root:
dotnet run --project godot/Empire.Core.Tests -- godot/tests/oracle
dotnet build godot/Empire.csproj
# Then use the Godot .NET executable:
godot --headless --path godot res://Scenes/PresentationChecks.tscn
godot --headless --path godot res://Scenes/BattleScreenChecks.tscn
```

All 16 original levels also pass 2,983 independently selected native actions against the TypeScript oracle, including every event and state hash. This is independent-level execution, not proof of every campaign branch played end-to-end. Persistence has 96 focused checks. The application test covers story/choice, battle, midbattle save/load, 127 remaining matching actions and identical campaign result, plus atomic rejection of an invalid save.

```sh
bash tools/godot/application-checks/run.sh
dotnet run --project tools/godot/persistence-checks/Empire.Persistence.Checks.csproj -- .
bash tools/godot/campaign-battle-checks/run.sh
```

Selected art, actor identity bindings, world materials, map and content are local
resources. The TypeScript exporter is a development-only oracle/data tool.

All 12 original action kinds have native handlers. Focused differential suites for deployment (2,519 checks), career/formation progression (175), recruitment (25), and transport (81 direct cases plus 24 dispatch steps and save/resume guards) are reproducible with the corresponding `tools/godot/*-checks/run.sh` scripts. This does not establish complete extension-registry parity.

The original 34 scenario effects and 16 conditions were verified with 466 direct
source comparisons and 29 JSON-save/resume engine steps. Canonical `EcmaJson`
preserves valid JavaScript lone-surrogate strings and keys; 2,100 source vectors
verify the boundary. Catalog/level/deferred-reference checks reject unsupported
capabilities before gameplay. See `tools/godot/scenario-checks` and
`tools/godot/utf16-boundary-checks` for reproducible commands and limitations.

## Scope and remaining migration work

This is a playable-level integration checkpoint, not the finished migration.
The story/choice/battle/result and non-destructive save/load flow is integrated.
Water, structures, markers, fog, camera controls, lighting and shadows have native
implementations and runtime checks. Manual acceptance of the latest build remains
pending. Nine levels still lack authored scenery in the recovered source, other
story themes remain unported, and older exported packages do not represent the
latest source. Extension and localization contracts are documented in the native
capability map and quality audit, not inferred from screenshots.
See `docs/RECOVERY.md` for evidence and `docs/COMPATIBILITY.md` for explicit
malformed-input hardening differences. Original source and art are preserved.

## Reproducible verification

From the repository root in the configured local toolchain environment, run
`bash tools/godot/verify-native.sh` for formatting, compilation, current native
contracts, all 16 independent level save/replays, and actual headless
painted-surface, material, camera, battle/result/navigation scenes. These checks use isolated
save locations. Manual GUI and current exported-package acceptance are separate.

Material rendering contracts use an independent 8×5 fixture rather than requiring
painted campaign maps to retain procedural river/road nodes. Formal scene checks
use the same authored renderer/plane resolver as the application. Edge-decoration
checks cover nonempty terrain support and source registration separately; integrated
edge-overlay ground clipping is not covered by the current painted campaign scenes.
`LightingChecks` keeps the frozen source-style math oracle separate from native
integration: current campaign state and the independent material fixture both use
`BoardSceneRendering`. The material fixture explicitly covers foreground, topic,
standing and environment props; painted campaign art does not need those nodes.
See [lighting verification](docs/LIGHTING-VERIFICATION.md) for the focused command
and fixture recovery evidence.

Historical TypeScript source-v1 oracle suites are retained as development evidence;
they are not a current-schema acceptance mode. Stable shared RNG and grid vectors
remain cross-language contracts. Historical whole-campaign AI/action traces are not
a native art-layout or balance gate: native levels and AI policy have evolved
independently. Native full-level replay/save checks use the current content and
compare exact state, digest, and replay results without rewriting historical goldens. See
`docs/OBJECTIVE-OUTCOME-SCHEMA.md` before using them. Their regeneration requires
the original repository and locked Node dependencies; the native runtime does not.

## Formal painted second level

See [the c01-02 reconstruction contract](docs/FORMAL-THREE-BRIDGES.md) for the
explicit scene registration, four interactive houses, preserved campaign behavior,
three-round truce semantics, and validation boundaries.

The native structure lifecycle adds explicit disable and enable transitions (35
scenario effects total), one-time c01-03 liberation, and version-3 tactical/mission
rules. See [lifecycle contracts and verification](docs/STRUCTURE-LIFECYCLE.md).
