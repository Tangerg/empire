# Reproducible AI agenda measurement

This opt-in tool times agenda construction and complete AI decisions at six real enemy-turn witnesses from C01, C03 and C07. Each witness is reconstructed from the unchanged formal level plus admitted commands; the state digest and exact chosen action must match. Both input state and immutable catalog are checked for mutation. The six selected orders are early-turn move/wait decisions, not a representative sample of every combat or recruitment decision. This is per-call CPU/allocation measurement, not FPS, rendered-frame latency, or a carried full-campaign run.

Build the desired Core source as Release using the installed repository toolchain, then run:

```
source tools/godot/env.sh
dotnet build godot/Empire.Core/Empire.Core.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false
bash tools/godot/ai-agenda-benchmark/run.sh "$PWD/godot/Empire.Core/bin/Release/net8.0/Empire.Core.dll" candidate 0 > /tmp/agenda-candidate.jsonl
```

To compare an earlier implementation, use `git archive <commit> godot/Empire.Core` to materialize its Core project in a separate temporary directory, build it with the same SDK/configuration, and supply that absolute assembly path to the same runner. The runner builds its harness in a fresh temporary folder and never changes the selected Core assembly. Use identical content and witnesses, four warmups, eleven samples, and both forward/reverse rounds (`0`/`1`); alternate variant order between rounds. Tiered JIT is disabled for the measured processes to keep compilation policy fixed. Preserve individual samples rather than reporting one cold call. The JSON output carries all samples, measured allocation bytes, exact answer and witness digest.

The default core regression checks allocation with substantial headroom instead of timing: it rejects a return to full definition cloning but does not impose a machine-speed threshold. It also checks optional flags, precise failure/short-circuit behavior, ordering, objective-advisor distinctions, catalog independence, state/save invariants and unchanged formal actions. The complete canonical gate still replays every shipped battle.
