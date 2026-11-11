# Gravekeeper recoil negative-control verification

Passed against implementation commit `27de886c111600c64a868f9d57fc6868a6db45e4`, Godot tree `02b18b80aff45880bb58e503574b858c921449aa`, and tools tree `afff6c24fcf143fdff3a5a4facf2f373d1ad3290`, using the explicitly handed-off final frozen assemblies recorded in `frozen-assemblies-sha256.txt`.

No production data edit, build, editor import, commit, or push was performed by this runner.

## Results

All four controls returned strict diagnostic-gate exit 1 with exactly the intended first assertion and test method:

- **Static hit binding:** `gravekeeper.recoil` to `static.hit`. Rejected by `CheckGravekeeperCastPlayback`: “Casting does not replace the gravekeeper's other declared actions.” This existing binding guard runs before the focused recoil checker.
- **First interval:** `40/110/90/80` to `39/111/90/80`, preserving the 320 ms total. Rejected by `CheckGravekeeperRecoilPlayback`: “Every keeper recoil pose occupies its exact half-open 40/110/90/80 ms interval.”
- **Repeated final pose:** last selected frame `recoil-ready` to `recoil-recover`, retaining the four source pictures and unchanged spans. Rejected by that same exact interval assertion. This proves repeated clip selection is rejected; it does not mutate source pixels.
- **Uniform scale:** `0.0688` to `0.0687`. Rejected by `CheckGravekeeperRecoilPlayback`: “The shared keeper selects four original recoil poses at the exact uniform scale and 320 ms duration, without release or death policy.”

After byte-exact restoration, the complete strict AnimationChecks scene passed **7487 assertions**, exit 0, with no error, leak, crash, or unhandled-exception diagnostics. The negative failures are deliberate test-sensitivity witnesses, not failed production runs.

## Isolation and identity

Execution began only after the final frozen-assembly marker existed. The runner privately copied JSON data and the existing imported runtime cache, then copied the three explicitly supplied DLLs into that private runtime. Unchanged assets/code/scenes/fixtures were runtime read references; every process used separate HOME/XDG directories. It invoked neither import nor build. Every mutant began from the original JSON bytes and restored those exact bytes in `finally`.

`summary.json` records complete source and private-stage input hashes, native fixtures/imported resources, before/after DLL hashes, source commit/tree, working diff, commands, exact errors, log hashes, per-case mutations, and restored JSON hashes. All recorded source and private-stage resource hashes match before and after; every per-case JSON restoration matches; frozen/staged DLLs and source commit remain unchanged. `review.json` contains the compact verified result.

This archive retains the exact executed runner, summary, review, all raw and strict-gate logs, source diff, and frozen assembly hashes. Original logs are byte-exact copies matching the executed summary. `evidence-sha256.json` covers every archive file except itself. No staged project, HOME/XDG directory, imported cache contents, or DLL is included.

## Reproduction

Use an existing official Godot Mono executable, a checkout at the tested implementation commit with the already imported assets, and the corresponding explicitly frozen final assemblies. Keep source resources unchanged for the duration. Do not substitute an initial or newly discovered assembly set.

```sh
export GODOT_NET=/absolute/path/to/the/existing/official/Godot-Mono-executable
export DOTNET_ROOT=/absolute/path/to/the/existing/dotnet
export EMPIRE_CHECK_TIMEOUT_SECONDS=900
python3 run-negative-controls.py \
  --repo /absolute/path/to/repo \
  --output /absolute/path/outside/repo/new-negative-output \
  --assembly-dir /absolute/path/to/final-assemblies \
  --expected-commit 27de886c111600c64a868f9d57fc6868a6db45e4
```

The output directory must not exist and must be outside the repository and disjoint from the frozen DLL directory. The runner rejects unrelated failures: each negative requires strict exit 1, its exact first exception, the intended method in the exception block, and no completion marker. The positive requires strict exit 0 and full-suite completion. Missing resources, premature startup failures, and arbitrary nonzero exits do not satisfy a control.

These results establish four data-guard sensitivities and restored headless animation contracts. Native visual quality, complete-campaign acceptance, exported performance, and remote CI are separate.
