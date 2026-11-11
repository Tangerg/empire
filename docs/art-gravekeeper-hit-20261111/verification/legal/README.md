# Lawful C01 gravekeeper hit witnesses

## Scope and provenance

Both checkpoints are official application saves derived from a new public
GrayFlag campaign. The opening chooses `steady-advance`. Commands 0–163 exactly
match the accepted gravekeeper idle/heal-right history, including complete action,
event, and before/after state identities. The route then takes normal friendly
move/heal actions through public application dispatch. Every opponent order is
selected by `AdvanceComputerStep`; no opponent command is invented or injected.

The keeper, its health, equipment, facing, map, RNG, camera, and core rules are
never patched. Paths come from the normal move-field query and targets from the
normal command options. A legitimate heal sets each facing before the enemy turn.
Each complete route has 175 committed orders, including the final normal AI
endTurn. The shared first 164 are counted in each full route, not unique scenarios.

## Final implementation replay

The final replay passed against the exact frozen hit-build DLLs for implementation
commit `27de886c111600c64a868f9d57fc6868a6db45e4`, Godot tree
`02b18b80aff45880bb58e503574b858c921449aa`, and tools tree
`afff6c24fcf143fdff3a5a4facf2f373d1ad3290`. The freezer marker records
`2026-11-11T00:20:33Z`. Frozen and actually loaded scratch assembly copies match:

- Empire.dll: `9c26ba406a55703a0d5a0b07141cfc094ece56b0842157ec47a03d465f0ec9c0`
- Empire.Core.dll: `ab6a38e4b863a6a09acce6505aac93a4fe72975f0c9e41329d1b8645a7c56faa`
- GodotSharp.dll: `2142e62894176d7340f50581f8b2fe84e36ae64e1a85f1b2bd7737a7e8a5c8ba`

`final-replay/` retains the exit0 run, zero-warning/error scratch build, exact
source/assembly identities, complete final route traces, official saves, and
independent Python full-save comparisons. Both 175-entry traces match the prior
package byte-for-byte, including every action, state digest, and event. All eight
witness saves and the original campaign save compare equal except the two writer
timestamps. The production camera-focus query also passes unchanged. The archive was then
invoked by absolute path from `/tmp`; that separate portable rerun also exited 0,
reproduced all traces and eight witness saves, and used the identical frozen DLLs.

Counting is explicit: the 350 retained route entries contain the same 164-order
prefix twice. The harness executes that prefix once, followed by each 11-order
branch: 186 main derivation dispatches. It separately repeats three checkpoint
orders per facing, for six more dispatches. Each dispatch has an independent
core state/event comparison. These are not 350 unique scenarios, nor 350 separate
main-loop dispatches. `final-replay/summary.json` records these counts.

The native input bytes under `right/` and `left/` remain the original immutable
planning checkpoints. Fresh final replay saves are under `final-replay/right/`
and `final-replay/left/`; only writer timestamps differ. `planning-replay/` remains
historical evidence from the previous idle-build DLLs, not the final hit build.

This final replay proves lawful final-build state, events, reloads, expected UI
outputs and camera routing. Actual native pixel/frame acceptance, actual UI F5
save equality and the complete repository gate are separate checks. This probe
did not run them. No game DLL binaries or build caches are included here.

## Exact native inputs

| Route | Keeper before | Real attack | Hash before → immediate after → settled |
|---|---|---|---|
| right | Unit4, (23,18), east-facing, 37 HP | Enemy18 moves (25,20)→(24,20), ranger bow 33 damage, keeper 37→4 | `abc88921` → `ae3b6da9` → `a660a150` |
| left | Unit4, (23,18), west-facing, 37 HP | Same normal AI move and attack, keeper 37→4 | `459afc95` → `f3a5082b` → `d54d535c` |

Use `right/before.json` or `left/before.json`. Both are at computer turn 10 just
before step 173. Both recipients survive. The shooter remains 58 HP. No counter is
emitted in this interaction. The next ordinary computer action, step 174, is
endTurn, which returns control to human player 1 in turn 11.

The east branch moves archer3 to (21,19), Roderick5 to (21,21), shield10 to
(27,18), guard8 to (24,18), then keeper4 to (23,18) and heals guard 8, 109→112.
The west branch instead moves archer3 to (22,18) and the keeper heals it, 39→69.
Both keep the foreground cells (23,19), (23,20), (24,19) empty. The same keeper
shares the new art binding; this is not a separate hero-specific sprite.

## Normal native keyboard and camera route

Use a fresh isolated QA user-data directory, never a player's existing slot.
Copy the chosen unchanged `before.json` as `grayflag-campaign-save.json`. Launch
the real normal game through the existing NativeCapture scene and motion mode.
Do not use the idle-capture flag, a synthetic fixture, or any state/camera flag.

1. On the initial home screen, press F8 once to arm the next presentation.
2. Press F9 once to load the official manual save.
3. Allow normal AI and presentation to run without additional battle input.
4. After the attack and the next AI endTurn settle, press F5 once.
5. Compare the actual F5 save with the chosen `ui-settled.json`.

The accepted archer-hit southern recording used this exact home-F8→F9 pattern.
The existing NativeCapture arm waits until a board is presenting, so home-screen
arming is retained across ordinary save load. The load itself is not a committed
battle order. Do not arm after loading, when the normal AI may already have acted.
Do not use the human `ui-trigger-before.json` for this capture: E would commit
endTurn first and consume the one-transition capture arm. That alternate official
checkpoint and its verified production-controller E order are retained only as
additional playability/state evidence.

The ordinary camera route is specifically checked. `BattlefieldFocus` follows
only visible **moving** AI commands. Therefore these witnesses intentionally use
the legal one-step archer movement. The actual session transition is passed to
`BattlefieldFocus.CommandDestination`, which returns cell (24,20), and the query
is verified not to alter the official save. `BattleScreen.PresentCommitted`
centers the existing camera on that destination before it starts playback. The
keeper at (23,18) is one column and two rows from the followed source. There is no
manual camera mutation, panning automation, altered timing, or camera flag.

The earlier bounded scratch candidate at keeper (24,18) had a stationary enemy
attack, which would not trigger this camera follow. It is not the packaged input.
Initial F9 framing alone is not relied upon.

Unit4 and the acting enemy are visible to player 1. The keeper is on southern
plain ground and known foreground occupancy is cleared. This is a source/state
visibility assessment, not native pixel acceptance. The final recorder must still
inspect actual feet, shovel, lantern, body, hit overlay, roofs, and HUD clipping.

## Full replay and integrity checks

The harness rebuilds only a copied isolated scratch probe against **existing**
Empire.dll, Empire.Core.dll, and GodotSharp.dll. It does not build/import production.
For every committed action, a separate core loaded from the preceding official
save independently dispatches the actual canonical action. Complete state and
all emitted events must agree. The preserved prefix must match its historical
trace exactly. On subsequent runs, both complete new routes and saved documents
must also match this package, omitting only official writer timestamps.

For each route, before, after, human-trigger, and settled official saves reload
through `GrayFlagCampaign.Load` and resave with every field unchanged except
`campaign.savedAt` and `battle.savedAt`. Fresh loads repeat the witness AI attack,
its exact full receipt, and the normal AI endTurn, checking full settled equality.
A fresh load of the human-trigger checkpoint accepts the production tactical
controller's E-emitted endTurn and produces the exact native-before document.

```sh
EMPIRE_TOOLCHAIN_ROOT=/absolute/existing/empire-tools \
bash /absolute/legal-package/derive.sh \
  --repo-root /absolute/empire-checkout \
  --assembly-dir /absolute/existing/frozen-final-hit-assemblies \
  --output /absolute/new-empty-directory-outside-checkout
```

The output must be new or empty and outside the repository. No writes occur
inside the evidence package. Supply a .NET8 toolchain. Run from any working
directory. Source references the existing historical prefix in the checkout.

Actual native F5 comparison:

```sh
python3 /absolute/legal-package/compare-ui-save.py \
  /absolute/legal-package/right/ui-settled.json \
  /absolute/actual-native-f5-save.json \
  --output /absolute/new-comparison.json
```

Substitute `left` for the other facing. Compare the whole saved document, not
only HP or state digest. The only excluded values are the two writer timestamps,
in detached comparison copies. Both timestamps must be fresh to reject an
untouched copied input. The helper's self-test rejects HP, position, facing,
event, campaign, extra-field and boolean/number changes. No archived save is
rewritten during comparison.

## Evidence boundaries

`planning-replay/` preserves the original baseline frozen-DLL derivation.
`final-replay/` is the completed replay against the final hit-build DLLs identified
above. The source harness and immutable native inputs are unchanged. Actual
native images, frame/pose verification, actual F5 output equality, and the
repository's aggregate gate must be recorded separately. Passing the legal
replay does not imply those other forms of acceptance.
