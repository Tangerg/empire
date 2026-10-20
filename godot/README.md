# Empire: native Godot C# migration

## Run the GrayFlag campaign integration

Install Godot **4.6.3 .NET** (not the standard build) and Microsoft **.NET SDK 8**.
Open `project.godot`, build the C# project, and run it. From a terminal:

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

## Verified behavior (2026-10-20)

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
dotnet run --project tools/godot/application-checks -- .
dotnet run --project tools/godot/persistence-checks/Empire.Persistence.Checks.csproj -- .
bash tools/godot/campaign-battle-checks/run.sh
```

Selected art, actor identity bindings, world materials, map and content are local
resources. The TypeScript exporter is a development-only oracle/data tool.

## Scope and remaining migration work

This is a playable-level integration checkpoint, not the finished migration.
The story/choice/battle/result and validated save/load flow is integrated. Remaining work includes water and other terrain rendering, authored scenery for nine levels absent from the recovered source, structure/composite/scenario visual feedback, lighting/shadows, other engine action families (including deployment and transport), full extension-registry parity, and native packaged desktop exports. These presentation omissions must not be mistaken for gameplay-rule completion. Other story themes remain unported.
See `docs/RECOVERY.md` for evidence and `docs/COMPATIBILITY.md` for explicit
malformed-input hardening differences. Original source and art are preserved.
