# Gravekeeper idle negative-control verification

Passed against tested source commit `869078976dd21f5d82932dc4151bdd03fa973c1a`, using the explicit frozen assemblies recorded in `frozen-assemblies-sha256.txt`. No production build, editor import, production data edit, commit or push was performed by this runner.

## Results

All four controls exited through the strict native diagnostic gate with exit 1 and the exact intended first assertion/test method:

- Static idle binding: `gravekeeper.breathe` → `static.idle`. Rejected by `CheckGravekeeperCastPlayback`: “Cast recovery selects the declared breathing appearance without replacing the canonical gravekeeper sheet.”
- One-millisecond transfer: 700/500/500/700 → 699/501/500/700, preserving the 2400 ms total. Rejected by `CheckGravekeeperIdlePlayback`: “The four authored anatomical phases occupy their exact half-open intervals.”
- Repeated exhale pose: last clip entry `breathe-exhale` → `breathe-high`, retaining four source pictures and unchanged spans. Rejected by that same exact half-open interval assertion. This tests repeated clip selection, not altered source pixels.
- Uniform presentation scale: 0.0792 → 0.0791. Rejected by `CheckGravekeeperIdlePlayback`: “Only the generic keeper idle selects the exact four-pose, 2400 ms breathing appearance and uniform scale.”

After byte-exact restoration, the entire strict AnimationChecks scene passed **6876 assertions**, exit 0, with no error/leak diagnostics. The summary's status and all four integrity flags are true. The failures above are intentional negative witnesses, not failing production runs.

## Isolation and identity

The runner privately copied the data and already-imported cache, then copied the three explicitly supplied frozen DLLs into the private runtime. Unchanged assets/code/scenes/fixtures were read-only references; each process had its own HOME/XDG directories. No editor or build was invoked. Each mutant started from the original JSON bytes and restored them in `finally`; the recorded restored hashes match after every case. The final positive used the original bytes.

`summary.json` contains every case's command, exact first error and expected method, log hashes, before/after source and private resource hashes (including native fixtures), restored JSON hashes, frozen/staged DLL hashes and source commit. Source resources, private inputs, assemblies and commit remained unchanged. `source-working-tree.diff` records the input checkout diff. Runtime stage and temporary user-data directories are not shipped in this evidence archive.

The exact runner is retained and its SHA256 matches the executed summary. It requires a fresh output directory outside the repository, disjoint from the explicit assembly directory, and an exact `--expected-commit`. It never accepts an arbitrary failure: a negative needs exit 1, exact first assertion text, the intended test method in that error's stack and no success marker. The restored positive needs exit 0 and full completion.

This result establishes four data-guard sensitivities and restored headless animation contracts. It does not itself establish native visual quality, complete-campaign acceptance, exported performance or remote CI.
