# Gravekeeper idle: lawful checkpoints and fresh final-build replay

This package separates the historical lawful witness derivations from their new
replay against the gravekeeper-idle implementation. No actor HP, position,
facing, camera, rule, RNG, receipt or event was patched to obtain these states.

## Final replay result

The portable probe completed with exit0 against implementation commit
`869078976dd21f5d82932dc4151bdd03fa973c1a` and the exact frozen DLLs recorded in
`final-replay/runtime-assemblies-sha256.txt`. Empire.dll SHA256 is
`2a6f890c63326c3490c09eb21bbf4c336eda5ebb08aa5f320badb16e57ff6ff7`.

- Cast route:133 accepted commands, before `5ade5a03`, after `05d9a950`.
- Clear right-heal route:179 commands, before `2c734d26`, after `e6c1b8e2`.
- Clear left-heal route:179 commands, before `6d581393`, after `993e2229`.

These491 orders are the sum of three complete routes with shared prefixes, not
491 unique scenarios. Each starts the public campaign and follows its normal
opening choices. Human orders use normal application dispatch; computer orders
use `AdvanceComputerStep`. Every action, before/after state hash and emitted
event equals its historical trace. Each action is also dispatched independently
through a core loaded from the preceding official save and compared in full.

All six final checkpoint saves pass official reload/roundtrip checks, and each
final player action is re-executed from its reloaded checkpoint. An independent
Python comparison additionally confirms all six saved documents equal their
historical counterparts except `campaign.savedAt` and `battle.savedAt`. Those
comparisons are in `final-replay/save-comparisons/`; they are replay-produced
save comparisons, not actual UI F5 evidence.

The scratch probe build had zero warnings/errors. It used existing frozen game
assemblies and did not build or import the production project. Its exact source
matches `source-package/harness/`. `final-replay/summary.json`, the full replay
output and source-tree identities retain the detailed result. No DLL binaries,
build caches, user directories or temporary probe outputs are committed here.

## Preserved native inputs

- `idle-right-input.json` is byte-identical to historical
  `source-package/heal-right/before.json`. Keeper4 is at (24,18), HP4, east-facing,
  not yet acted.178 accepted orders precede this no-action idle witness.
- `idle-left-input.json` is byte-identical to historical
  `source-package/heal-left/after.json`. The same keeper is west-facing and has
  already acted.179 accepted orders precede this no-action idle witness.
- `recovery-left-input.json` is byte-identical to historical
  `source-package/heal-left/before.json`. The final normal stationary Heal targets
  archer3 at (23,18), changes the keeper from east to west and heals39→69 HP.

The actor is surviving at low health; these files do not claim full health.
Nearby actors were legally moved away in the source route to expose the keeper's
body, shovel and lantern. Native visibility still requires inspection of the new
rendered artwork in the actual application.

`source-lineage.json` records these input SHA256 values and verifies all44 copied
historical package files against commit `f358d5bc`. Its preparation status is a
historical snapshot. The entire `source-package/`, including its earlier results
and assembly hashes, is preserved unchanged. Those older results do not describe
the new final build; only `final-replay/` does. Freshly timestamped replay outputs
do not replace the original immutable native input bytes.

## Reproduce the final-build replay

Supply an existing .NET8 installation and the exact frozen Empire.dll,
Empire.Core.dll and GodotSharp.dll. From the checkout:

```sh
EMPIRE_TOOLCHAIN_ROOT=/absolute/existing/empire-tools \
bash docs/art-gravekeeper-idle-20261110/verification/legal/source-package/derive.sh \
  --repo-root /absolute/empire-checkout \
  --assembly-dir /absolute/frozen-final-assemblies \
  --output /absolute/new-empty-directory-outside-checkout
```

The script copies and builds only the scratch probe. It refuses a nonempty
output or an output inside the repository. All writes remain outside this
evidence package; its source files are read-only inputs.

## Native recording and actual F5 equality

The normal application loads an exact input copy from its isolated manual slot
using F9. The two idle runs issue no battle command. The optional recovery run
executes the one published heal through the native interface. At960×640,
103 observations with `--capture-idle-stride=3` use241.40625MiB, below the existing
256MiB budget. First-to-last observations span306 fixed60Hz frames, or5100ms,
longer than two2400ms idle loops. Sampled video playback is20fps. Actual pose
wraps, actor geometry and trace cadence must still be checked.

After each accepted recording, F5 writes the actual application state. Compare
that output against its immutable input for either idle run, or against
`source-package/heal-left/after.json` for recovery:

```sh
python3 docs/art-gravekeeper-idle-20261110/verification/legal/compare-ui-save.py \
  /absolute/expected.json /absolute/actual-f5-output.json \
  --output /absolute/new-comparison.json
```

This compares the entire document and omits only the two writer timestamps from
detached copies. Both timestamps must differ to reject an untouched copied input.
Neither source file is modified. `comparison-self-test.json` records successful
timestamp-only acceptance and rejection of state/event mutations.

This legal replay package does not itself claim native rendered acceptance,
actual UI save equality, real-time performance or complete-campaign acceptance.
Fresh native captures and their F5 comparisons are recorded separately.
