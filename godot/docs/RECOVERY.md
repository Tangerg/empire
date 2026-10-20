# Godot C# migration recovery checkpoint

This work reconstructs the migration from verified remote source commit
bfe9334a29630351ba4f46fd4c195cb3a72c36d9 after the cloud workspace reset.
The unpublished local checkpoints cc43ac3a, 904351d9, and 8bbca062 were not recovered.
Original TypeScript and art sources remain unchanged.

## Verified on 2026-10-20

- Microsoft .NET SDK 8.0.426 and Godot 4.6.3 .NET were downloaded from official
  vendor sources and checked against official SHA512/SHA256 metadata.
- Core and Godot presentation assemblies compile with zero warnings and errors.
- Eight console test suites pass: oracle state digests, UTF-16/numeric digest
  vectors, independent RNG streams, initial state construction, tactical grids,
  tile-art hashing, transactional rollback, and detached state/event ownership.
- TypeScript fixtures self-replay exactly: 142 / 119 / 135 actions, final hashes
  3942584e / f0fe6f08 / ecb676a7. These are test oracles, not native gameplay.
- Twenty selected images, the authored scene, actor overrides, content, and
  environment atlas metadata have been exported reproducibly.

## Not yet complete

C# gameplay handlers, lifecycle, combat, AI, campaign, saves and interactive UI
are not yet implemented. No fixture playback is used as a substitute for rules.
Native presentation checks and visual inspection are separate from compilation.

## Reproduce locally

Install official .NET 8 SDK and Godot 4.6.3 .NET. The cloud-specific helper
`tools/godot/env.sh` points only to workspace-local installations. On another
machine, use its own SDK PATH and Godot binary instead.

Run `npm ci`, `npx vite-node tools/godot/export-grayflag.ts`, then
`npx tsc --noEmit -p tools/godot/tsconfig.json`.
Run `dotnet run --project godot/Empire.Core.Tests -- godot/tests/oracle`.
Run `dotnet build godot/Empire.csproj`.

The .NET October 2026 runtime requires a writable XDG_RUNTIME_DIR for named
mutexes in this restricted cloud environment. The helper configures it locally;
no system settings or denied filesystem access are needed.
