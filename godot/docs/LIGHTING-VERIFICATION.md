# Native lighting fixture boundary

The immutable `tests/oracle/lighting.json` protects source-style gradients, shadow
presets, source-over composition and contact geometry. It is not a current native
campaign-state fixture. Do not regenerate campaign goldens to satisfy presentation
identity checks.

`LightingChecks` constructs native state with `BattleEngine` and resolves ground
and plane together with production `BoardSceneRendering`:

- Current c01-01 exercises the registered painted ground, actor source frames,
  shared shadow cache, controlled animation and unchanged canonical state.
- The existing independent 8×5 material fixture, extended locally with one village
  tile and four props, exercises procedural field lights, actual structure contacts
  and explicit environment, standing, topic and foreground props. Lights are composed inside the logical ground viewport and consumed by
  projected ground surfaces, not compared to actor depth in another canvas.

## Recovery evidence, 2026-10-22

The old integration paired a historical 27×17 c01-01 state with the current 36×30
painted authored scene. Production correctly rejected that identity mismatch before
rendering. After separating those responsibilities, a real scene run exposed the
additional obsolete direct-child light paths; the production logical-viewport
composition is the intended path. Neither failure justified a renderer fallback,
relaxed identity check or updated golden. No production code or source oracle changed.

Focused reproduction, after sourcing `tools/godot/env.sh` from repository root:

```sh
dotnet build godot/Empire.csproj --nologo --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
"$GODOT_NET" --headless --path "$PWD/godot" res://Scenes/LightingChecks.tscn -- "--lighting-oracle=$PWD/godot/tests/oracle/lighting.json"
```

Use isolated temporary `XDG_DATA_HOME` for lifecycle scenes. Require process exit
zero and no error/leak diagnostics, not merely a PASS line. Headless checks establish
native resources and composition contracts, not GPU screenshot acceptance.

Verified on this fixture repair: native build (zero warnings/errors), repository
native formatter, source metadata (278 unique script UIDs), `LightingChecks`
(1,503 assertions), and one `LightingLifetimeChecks` launch (94 assertions across
three real-session load/restart/scene-change cycles). Both scene processes exited
zero without error/leak diagnostics. The full unchanged campaign gate and visual
acceptance were not rerun for this test-only repair.
