# C14 target-location metadata fixture migration

Base: `088c0bf4`. The prior C14 `score-valves.targetZones` addition legitimately
changed full state hashes. Two older recorded witnesses still expected the
pre-metadata state. No engine, gameplay, action, or event expectations are changed.

## Independent proof

`tools/godot/c14-target-metadata-checks` explicitly identifies two independent
witnesses: status feedback (130 actions, checkpoints 56 and 129) and displacement
(59 actions, checkpoint 58). The earlier 85-action collision proof is not used.
Each witness creates fresh canonical battles. The legacy level differs only by
removing the exact validated `score-valves.targetZones = [valve-a, valve-b]` field.
Every action is dispatched independently on both battles, so invalid orders fail.
After every action, complete ordered events and complete canonical JSON must be
identical after removing only that one field from the current snapshot.

The tool pins original action-sequence SHA-256 values and legacy checkpoint hashes
from the base. It also pins the original checkpoint event SHA-256 values. The
current fixtures must match the recomputed current hashes in default verify mode.
`--calculate` prints candidate hashes without writing files; it still enforces
legacy state/event anchors and full two-track equality.

## Exact migration

| Witness | Position | Old | Current |
| --- | --- | --- | --- |
| Status | Initial | 10b86025 | 0e41b5f8 |
| Status | 56 before | dd871f64 | 170a6193 |
| Status | 56 after | 7c78b141 | 1c832d84 |
| Status | 129 before | cb85a026 | a6221ce1 |
| Status | 129 after | 2eeda816 | c70103c1 |
| Displacement | Initial | 10b86025 | 0e41b5f8 |
| Displacement | 58 before | 1fc73c28 | 78332f81 |
| Displacement | 58 after | 60fee9f7 | 0450955e |

Only these eight hash values changed in the two fixtures. All actions, event
hashes, checkpoint indices and other chapters remain unchanged.

## Reproduce

```sh
bash tools/godot/c14-target-metadata-checks/run.sh
bash tools/godot/c14-target-metadata-checks/run.sh --negative-control
```

Both commands exited 0. Default mode independently verified both complete action
sequences. Negative-control mode intentionally changes the current level's
`turnLimit` rule, not display metadata, and requires canonical-state comparison to
reject that difference for each witness. An accepted mutation fails the harness.
The recorded `NEGATIVE CONTROL REJECTED` lines are expected harness successes,
not successful replay of altered rules.

Evidence: `verified.txt` and `negative-control.txt`. This focused proof does not
claim the full native BattleScreen or Animation scenes passed; those remain
separate gates run by the coordinating task.

## Final affected integration gates

After migrating only the proven hashes, the complete, unmodified native entry
points passed under `native-process.sh` with independent isolated XDG data roots:

- `BattleScreenChecks`: full real-session integration passed, including all
  C01/C14/C03 status witnesses and the prior mother-root slice's genuine missing-art
  notice checks. No temporary subset entry point or skipped test remains.
- `AnimationChecks`: all 8257 presentation assertions passed; C14 checkpoint 58 retained
  exact ordered impact events `[2,7,11,13]` and its original receipt.
- Native project build: zero warnings/errors; native whitespace format and source
  metadata checks passed.
- `fixture-scope.txt` records structural JSON comparison against `088c0bf4`: only
  the eight listed C14 state hashes differ; all other values are unchanged.

No fresh full Core suite, all-native aggregate, or remote CI pass is claimed.
This change has no game runtime/source-art modification and required no new GUI
session. Prior withheld evidence and private saves are not included.
