# Tactical parity checks

Run from the repository root, after the main GrayFlag exporter has generated its data and action cases:

```sh
./node_modules/.bin/vite-node tools/godot/tactics-checks/export-vectors.ts
source tools/godot/env.sh
dotnet run --project tools/godot/tactics-checks/Empire.Tactics.Checks.csproj -- .
```

The TypeScript exporter obtains expected values from the original engine. It covers every opening GrayFlag unit, sampled live-game states, and synthetic terrain, occupancy, control-zone, square8, hex, elevation, cliff, structure, overlay, status, commander and formation cases.

The C# executable compares ordered reachable fields, path costs and predecessors, stopping cells, attack/heal targets, threat unions and direct line of sight. It also checks every live wait command against its complete state and event oracle, rejection atomicity, and replacement of untrusted path interiors by the canonical path. Directive vectors cover all four standing orders across all three tilings, absent/unknown/empty/named zones, patrol routes, and cursor wraparound. Registry checks verify extension, replacement, cloning, and sealing.

Off-board destination rejection is an intentional safety correction, not TypeScript parity. The regression covers flattened-index aliasing; see `godot/docs/COMPATIBILITY.md`.

Oracle JSON is test-only data. Production code never reads it. The runner exits nonzero on any mismatch.
