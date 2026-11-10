# Legal Torren hit witness

This package preserves a current-schema campaign checkpoint and reproduces exactly two legal actions through the production application/core APIs. It does not alter game state, rules, controllers, receipts, or production source, and it does not rebuild the game.

## Native action

Load `candidate-001-before.json` into an **isolated** native manual-save slot as `grayflag-campaign-save.json`. In NativeCapture, press **F8, then F9**. The checkpoint is already on AI turn 4, so the normal pacer immediately commits the normal AI's first action:

```json
{"kind":"command","unit":16,"path":[{"x":17,"y":7},{"x":16,"y":7}],"command":{"ability":"attack","weapon":"c01.legion-spear","target":{"x":15,"y":7}}}
```

- Shield 16, `north-shield`: (17,7) → (16,7), 120 → 110 HP.
- Torren 2, `campaign-torren`: remains (15,7), **72 → 43 HP**.
- Main hit: shield → Torren, `c01.legion-spear`, 29 damage, not killed.
- Counter: Torren → shield, `c01.border-blade`, 10 damage, not killed.
- Canonical before/after state digests: `f04b7e8f` → `c1d94756`.
- Canonical event SHA-256: `854477fb25ec86721038f10e36f6d9dba4660c9731d4f96221ec5958477380c0`.

The associated native recording completed at 1000×700 with 71 frames. Native playback evidence is maintained separately in this art review; the console harness is not a substitute for visual acceptance. The source-derived transition duration is approximately 1,185 ms: 110 ms movement, 75 ms main release, 420 ms impact, 160 ms counter release, and 420 ms counter impact. The existing 256 MiB capture budget permits 95 frames at this viewport without modifying recording code.

## Save provenance and timestamps

`source-campaign-save.json` is a byte-for-byte copy of the supplied original official save. Its SHA-256 is `2228e4c0a56006d88f83817b3fca8423a757d4a4bc68adafe70f4f07351ec3ca`. It is human turn 4, with Torren at (15,7), 72 HP, and shield 16 at (17,7), 120 HP.

The derivation loads that source using `GrayFlagCampaign.Load`, commits the normal human `endTurn`, and saves immediately before the normal AI's first action. After that action, it writes the expected after-save. Both snapshots use `GrayFlagCampaign.Save()` and `SaveFile.WriteDocument()`, retaining exactly the official application schema 4, campaign schema 1, and battle schema 3 fields.

**The preserved candidate save files were refreshed by the official writer after the native application had launched.** Their `campaign.savedAt` and `battle.savedAt` metadata are not claimed to match the earlier files copied into that native run. The canonical state, actions, event sequence, positions, and HP match the recorded scenario. Every rerun creates new official timestamps. The harness checks all other save fields for equality; it removes only these two timestamps from detached comparison copies, never from a written save.

The preserved files have these writer-produced timestamps:

- Source campaign: `2026-11-10T08:38:55.396Z`; battle: `2026-11-10T08:38:55.399Z`.
- Candidate before campaign: `2026-11-10T11:30:18.319Z`; battle: `2026-11-10T11:30:18.323Z`.
- Candidate after campaign and battle: `2026-11-10T11:30:18.549Z`.

`evidence-sha256.txt` identifies the preserved file bytes, independently of canonical state/event identity. These audit files are inputs to the reproduction script and are never rewritten by it.

## Portable reproduction

Requirements: an existing .NET 8 toolchain and already compiled `Empire.dll`, `Empire.Core.dll`, and `GodotSharp.dll`. By default the script uses `godot/.godot/mono/temp/bin/Debug` in the repository inferred from its own location. `--repo-root` and `--assembly-dir` can select a different checkout or existing assembly directory. If `dotnet` is not on PATH, the script sources the repository's existing `tools/godot/env.sh`; it does not install a toolchain.

From the repository root:

```sh
output="$(mktemp -d /tmp/empire-torren-hit.XXXXXX)"
docs/art-torren-recoil-20261110/verification/legal-hit/derive.sh --output "$output"
```

The output directory must be new or empty and outside the repository. The script copies the two harness source files there, reads the shipped localization PO into a scratch lookup, builds **only the copied harness**, and executes it against the existing game DLLs. All generated saves, lookup data, build output, `bin`, `obj`, and temporary files stay in that caller-provided directory. The production project is only read.

An actual portability run was executed from `/tmp`, with the repository supplied as a parameter:

```sh
# Starting in the repository root:
repo="$PWD"
(cd /tmp && "$repo/docs/art-torren-recoil-20261110/verification/legal-hit/derive.sh" \
  --repo-root "$repo" \
  --output /tmp/empire-torren-hit-portable-20261110)
```

Result: **exit 0**, scratch build with **0 warnings and 0 errors**, all verification assertions passed. Choose a fresh output directory when repeating this command. The exact command, build/derivation logs, assembly hashes, and machine-readable result are preserved under `portable-run/`.

## Verification

The harness asserts all of the following:

1. The original source has the expected SHA-256 and loads through current application/content admission.
2. Exactly two actions occur: an accepted human end turn, then the committed first normal AI action. No broader scene search is performed.
3. For each action, a separate real core Battle loads the preceding official save and commits the identical action. Its complete resulting state and actual `BattleReceipt.Events` equal the application session results.
4. Torren's HP is 72 → 72 for end turn and 72 → 43 for the selected hit.
5. The hit's before/after state digests and event hash equal the expected witness.
6. Both newly written before/after application saves reload through `GrayFlagCampaign.Load` and reproduce their complete corresponding battle state.
7. Every save field equals preserved evidence except the two official `savedAt` timestamps. The real-receipt evidence and complete two-action trace reproduce exactly.
8. The original source save remains byte-identical.

The script also refuses repository-local output and nonempty output directories. Both rejection paths were exercised with exit 2, followed by a successful checksum check of all preserved audit files. See `portable-run/guards.log`. No `bin`, `obj`, or `__pycache__` directory is present in this evidence package.

## Files

- `source-campaign-save.json`: preserved original official source save.
- `candidate-001-before.json`: official AI-turn checkpoint for native loading.
- `candidate-001-after.json`: official expected post-strike campaign save.
- `candidate-001-receipt.json`: detached evidence read from real commits, not a forged receipt or save document.
- `derivation-actions.json`: both legal action records and emitted events.
- `evidence-sha256.txt`: preserved source/candidate/action file checksums.
- `harness/Program.cs`, `harness/Probe.csproj`: readable, parameterized console harness using existing game assemblies.
- `extract-translation-lookup.py`, `derive.sh`: parameterized scratch setup and execution.
- `portable-run/`: actual successful portability and overwrite-guard verification records.
