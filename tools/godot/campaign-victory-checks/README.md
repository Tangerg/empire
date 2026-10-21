# Genuine carried-state winning opening

This checks two consecutive legally played campaign victories, not a complete
winning campaign and not independent per-level resets. The test player exists
only in this harness. It chooses `steady-advance` and `accept-truce`, prioritizes
canonical healing, retreats wounded retinue, and stages refugees away from
projected enemy fire. Every movement stop, path and command comes from the
original engine queries. Other human actions and all opponent actions use the
unchanged engine AI. No stats, rules, roster dispositions or outcomes are edited.

From the repository root:

```sh
bash tools/godot/campaign-victory-checks/run.sh
bash tools/godot/campaign-victory-checks/run.sh --regenerate
```

The default replays the deterministic compressed source trace. `--regenerate`
plays the source campaign again in a temporary directory, requires its JSONL to
be byte-identical to the checked-in trace, and then performs the native replay.
It needs the repository's existing Node and .NET toolchains; it installs nothing.

C# replays player input through `GrayFlagSession.Dispatch`, independently chooses
every opponent action, compares every ordered event sequence and state digest,
and checks campaign requests, choices, real carryover and 14 source checkpoints.
It reloads its own native schema-2 document at each checkpoint. The source wrapper
is not imported: its mutable request field is intentionally absent from native
saves, while regenerated request projections are compared exactly. It refuses losses, incomplete traces and unexpected final state.
The source also restores its own checkpoints, so continuation is exercised by
both implementations. Production code never reads these fixtures.

Verified opening:

- c01-01: victory, turn 8, 136 actions. Native/source carryover to c01-02 is Laiya
  116 HP, Torren 3 HP and Elin 78 HP, with their actual rank/resources retained.
- c01-02: victory, turn 15, 292 actions. All three refugees survive at full HP.
- Total: 428 legal actions, exact source/native events and states, 14 save/resumes.
  Campaign remains active at `aftermath-02`; later battles are not covered.

Important: battle 2 wins through the existing elimination fallback, not escort
completion. The three refugees remain west of the river. The source
`evaluateVictory` in `packages/battle-engine/src/victory.ts` awards victory when
only one living team remains before considering objective completion. This test
preserves that behavior and does not claim the escort objective was fulfilled.
Elin dies in battle 2 and is genuinely unavailable afterward.

To preserve a native opening battle-2 application envelope for labeled visual
inspection, supply an unused temporary output path. This still runs the entire
comparison and writes the save directly from the native application's canonical
`BeginBattle` state, after the first real victory:

```sh
source tools/godot/env.sh
dotnet run --project tools/godot/campaign-victory-checks -- . \
  godot/tests/oracle/campaign-victory-opening.jsonl.gz /tmp/c01-02-opening.save.json
```

`proof.json` records the checked baseline, data/trace hashes and verified scope.
The abandoned rear-holding experiment is retained separately in the investigation
workspace, not shipped as a successful fixture. None of these files touches a
user save or changes the original TypeScript engine.
