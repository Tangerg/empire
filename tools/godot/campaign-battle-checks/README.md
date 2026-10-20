# Native execution of every authored campaign battle

Run `bash tools/godot/campaign-battle-checks/run.sh [temporary-output-directory]` after exporting campaign content and installing the repository toolchain.

The source TypeScript independently normalizes each of the 16 original levels and plays it to its actual terminal state. Native C# then independently chooses every action and compares the action, emitted events and resulting state hash at each step, including initial state. It uses the original player AI settings and deterministic level seeds. This is not a roster-transformed campaign sequence and does not force victories.

There is no invented step or turn cap. Authored turn limits remain battle rules. Source exceptions, incomplete fixtures and missing terminal states are reported as failures, not accepted as passes. The generator prints its continuing action count so a nonterminating source run can be diagnosed separately.

Fixtures are compact temporary JSONL streams, not checked-in giant battle snapshots. On a native divergence, the runner preserves the exact level/action index/phase/error, native snapshot and chosen action, then replays the source to reconstruct its before-state. A focused rerun can use:

```sh
source tools/godot/env.sh
dotnet run --project tools/godot/campaign-battle-checks/CampaignBattleChecks.csproj -- godot /tmp/output/c01-02.jsonl /tmp/output
```

Production code never reads these fixtures.
