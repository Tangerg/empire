# Empire: native Godot C# migration

## Run the current GrayFlag slice

Install Godot **4.6.3 .NET** (not the standard build) and Microsoft **.NET SDK 8**.
Open `project.godot`, build the C# project, and run it. From a terminal:

```sh
dotnet build Empire.csproj
godot --path .
```

The executable name depends on your Godot installation. In the approved cloud
workspace, `source ../tools/godot/env.sh` exposes the local SDK and `$GODOT_NET`.
No TypeScript or browser process runs in the game.

Select a friendly unit, choose an action category and destination, then confirm
the complete order. Movement commits the destination and waiting together;
attack orders include their exact firing position. End turn lets the original
ported AI play the opposing side. The native core revalidates every command.

## Verified behavior

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

Selected art, actor identity bindings, world materials, map and content are local
resources. The TypeScript exporter is a development-only oracle/data tool.

## Scope and remaining migration work

This is a playable-level integration checkpoint, not the finished migration.
The campaign/story flow, validated save/load UI, remaining engine action families,
full extension-registry parity and lighting/shadow polish are still in progress.
See `docs/RECOVERY.md` for evidence and `docs/COMPATIBILITY.md` for explicit
malformed-input hardening differences. Original source and art are preserved.
