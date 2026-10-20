# Source-backed camera checks

Run `bash tools/godot/camera-checks/run.sh` from the checkout. The script generates
live original TypeScript vectors in a temporary directory and runs an isolated
.NET 8 project linked directly to the presentation helper files, with no Godot or
core dependency. No fixture is used at runtime by the game.

Pass `--verify-guard` to compile an isolated temporary mutant with `> 6` instead
of `>= 6` drag activation and require the source oracle to reject it. The source
checkout is never mutated by this negative control.

Pass `--native` to copy the exact helper/contract/test files into a minimal temporary
Godot project, then build and run CameraChecks headlessly. It does not build Core
or alter the shared project's outputs. See `godot/docs/CAMERA.md` for source contracts and
the distinction between exact geometry/input policy and platform scroll timing.
