# Legal Torren death witness

This package derives Torren's real death from the preserved official turn-4 campaign save. It executes exactly 22 legal application actions: the human only ends turns; every computer action is selected and committed by `GrayFlagSession.AdvanceComputerStep()`. No state fields, battle rules, controllers, unit values, or receipts are edited.

## Immediate native trigger

Copy `candidate-001-before.json` into an **isolated** native manual-save slot as `grayflag-campaign-save.json`. In NativeCapture, press **F8, then F9**. F8 arms recording and F9 loads the official checkpoint. It is already AI turn 6, and the normal pacer's first action is the lethal strike:

```json
{"kind":"command","unit":16,"path":[{"x":16,"y":7}],"command":{"ability":"attack","weapon":"c01.legion-spear","target":{"x":15,"y":7}}}
```

- Shield 16 (`north-shield`) remains at (16,7), 102 HP.
- Torren 2 (`campaign-torren`) starts at (15,7), **16 HP**, facing east.
- The real attack deals 16 damage with `killed: true`; the core emits `death` and creates corpse marker 1 at (15,7).
- Torren leaves live units. The corpse's `fallenUnit.hp` is 0.
- There is no movement and no counterattack. Shield rank changes 0 → 1, and the real `found_matching_orders` scenario signal follows. The battle remains playing.
- Before state digest: `c1578a7a`.
- After state digest: `ebb17884`.
- Event SHA-256: `92b2f43f282b42db31fd6c3729fb32ee2eab82a4dfb6c63d595d6faf9cdb851b`.

With the accepted 620 ms death clip and 160 ms terminal fade, the source-derived sequence is approximately 1,315 ms: 75 ms release + 420 ms hit + 40 ms fatal pause + 780 ms death. That is about 79 frames at 60 fps, below the existing 95-frame budget at 1000×700. This is a source-derived duration estimate, not a frame-count promise. The subsequent native capture contains 78 complete stride-1 observations and reaches Torren retirement; see `../../README.md` and `../native-fall-proof.json`. This harness does not control presentation time or replace that separate display verification.

## Provenance and derivation

`source-campaign-save.json` is byte-identical to the original official source in `docs/art-torren-recoil-20261110/verification/legal-hit/source-campaign-save.json` at repository commit `50ecca87`. Source SHA-256: `2228e4c0a56006d88f83817b3fca8423a757d4a4bc68adafe70f4f07351ec3ca`.

- Step 0: normal human end turn 4.
- Step 1: AI turn 4, shield moves one cell and hits Torren 72 → 43; Torren counters for 10.
- Steps 2–9: remaining normal AI turn 4 actions, including endTurn.
- Step 10: normal human end turn 5.
- Step 11: AI captures with unit 14.
- Step 12: shield strikes Torren 43 → 16; Torren counters for 8.
- Steps 13–19: remaining normal AI turn 5 actions, including endTurn.
- Step 20: normal human end turn 6.
- Step 21: first normal AI action of turn 6 kills Torren.

The harness writes both snapshots through `GrayFlagCampaign.Save()` and `SaveFile.WriteDocument()`, preserving application schema 4, campaign schema 1 and battle schema 3. The saved timestamps are writer output, not manually substituted:

- Original source campaign `2026-11-10T08:38:55.396Z`, battle `2026-11-10T08:38:55.399Z`.
- Preserved candidate before campaign/battle `2026-11-10T12:06:05.078Z`.
- Preserved candidate after campaign `2026-11-10T12:06:05.191Z`, battle `2026-11-10T12:06:05.192Z`.

Reruns create new official timestamps. Comparisons omit only `campaign.savedAt` and `battle.savedAt` from detached comparison copies, never from written save files. `evidence-sha256.txt` identifies exact preserved bytes, separately from canonical state/event identity.

## Reproduce

Requirements: an existing .NET 8 toolchain and already compiled `Empire.dll`, `Empire.Core.dll` and `GodotSharp.dll`. This script never installs software or rebuilds production code.

```sh
output="$(mktemp -d /tmp/empire-torren-death.XXXXXX)"
/path/to/legal-death/derive.sh --repo-root /path/to/empire-work --output "$output"
```

`--assembly-dir` may point to another existing assembly directory. Otherwise the script uses `godot/.godot/mono/temp/bin/Debug`. It uses `dotnet` from PATH or the existing repository-adjacent `empire-tools` toolchain (`EMPIRE_TOOLCHAIN_ROOT` may override this). The output must be new or empty and outside the repository. All build intermediates, CLI state and generated results stay in that output directory; source evidence and game files are read-only. Existing first-use toolchain metadata is copied when available, preventing unnecessary initial configuration in constrained environments.

The completed reproduction was launched from `/tmp` with output `/tmp/empire-torren-death-portable-20261110`. Its records under `portable-run/` show exit 0 with **0 warnings and 0 errors**. The preserved before/after saves, complete 22-action trace, and real lethal receipt matched exactly except for the two official timestamp fields. Runtime assembly hashes are retained so a later rebuilt presentation DLL cannot be confused with the assemblies used for this derivation.

## Assertions

1. Original source SHA-256 is exact and remains unchanged.
2. Every action uses the real application session, without editing state or choosing an AI action manually.
3. For all 22 actions, an independent core Battle loads the preceding official save and dispatches the same real action; complete state and actual receipt events match the session.
4. The selected lethal action has the expected before/after hashes, event SHA-256, 16 → 0 HP and a real `killed: true` event.
5. Both candidate saves reload through official `GrayFlagCampaign.Load`; complete battle state and all saved fields round-trip, excluding only writer timestamps.
6. Loading the before-save and advancing the normal AI once reproduces the exact action, state and event sequence.
7. Preserved before/after save fields, lethal receipt and complete action trace reproduce.

Native input copies remain byte-identical to the initially derived candidate files; the portable rerun did not refresh them.

Files: official source/before/after saves; detached real-receipt evidence; complete legal action history; portable harness and translation lookup extractor; script; build/derivation logs; machine-readable verification; byte hashes. There is no `bin`, `obj` or generated translation lookup in this evidence package.

Overwrite guards were exercised: repository-local output and nonempty output both refused with exit 2. See `portable-run/guards.txt`. Build and derivation logs are preserved as `.txt` to avoid the repository’s `*.log` ignore rule.
