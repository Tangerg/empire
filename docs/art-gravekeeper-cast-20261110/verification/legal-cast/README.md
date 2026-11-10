# C01-01 lawful gravekeeper cast and clear-front healing checkpoints

## Final player-executable native inputs

| Route | Native input | Final action | Actual result | Complete route |
|---|---|---|---|---|
| Magic attack | `cast/before.json` | Unit4 (18,19), stationary lantern-staff → enemy13 (18,18) | Damage16: target24→8; counter25: source32→7; both survive | 133 commands |
| Right-facing heal | `heal-right/before.json` | Unit4 (24,18), stationary Heal → ally8 (25,18) | Heal3: target109→112; source remains4 | 179 commands |
| Left-facing heal | `heal-left/before.json` | Unit4 (24,18), stationary Heal → ally3 (23,18) | Facing east→west, Heal30: target39→69; source remains4 | 179 commands |

The two healing input files are byte-identical to the scratch `right-02/before.json` and `left-02/before.json` used for the native 48-frame recordings. Cast input bytes remain unchanged. Native pixel locations depend on the window/camera; cell coordinates above are authoritative.

All participants occupy plain ground south/front of the buildings and pass the native player-1 visibility query. Each final path contains only the source cell. For both heals, normal commands first move unit5 to (21,21) and unit10 to (27,18), clearing foreground cells (24,19), (24,20), (25,19). Right heal also moves unit3 to (21,19); left heal instead moves that injured archer to (23,18) to become the recipient. No target health changes during these moves.

The magic attack is evidence of the real attack release and counter, not a clear isolated pose proof: the vertically adjacent target's damage number and hit FX can obscure the source's face/arm. The clear-front right/left healing recordings provide the complementary actor-pose evidence. The old crowded healing checkpoint is retained only in external QA history and is no longer a formal visual acceptance input.

## State and receipt identities

- Cast step132: before `5ade5a03`, after `05d9a950`.
- Right heal step178: before `2c734d26`, after `e6c1b8e2`.
- Left heal step178: before `6d581393`, after `993e2229`.

Each route directory preserves the exact native `before.json`, `after.json`, actual `receipt.json`, full `derivation-actions.json`, `opening-actions.json`, `source-campaign-save.json`, and verification results. The before/after files are actual public application saves, never manually patched.

## Provenance and complete replay

Every route starts a new public `GrayFlagCampaign`, advances the opening story, chooses `steady-advance`, and calls `BeginBattle`. Human commands are accepted through normal application dispatch; opponent actions are chosen only by normal `AdvanceComputerStep`. No HP, controller, state, scenario, receipt, RNG or combat-rule edits are used. Low health and healing eligibility come from real recorded combat.

The clear-front routes retain the original first175 accepted commands, then add three legal friendly move-and-wait commands and the final stationary heal. Move paths came from the native move-field query, and availability/targets from native command options. The complete179-command traces include those exact actions and actual receipts.

`harness/Program.cs` replays all three routes from original setup. For every step it checks the actual action, before/after hashes and events against the preserved full trace. It also independently dispatches that action through the native core loaded from the preceding official save and compares full resulting state and emitted events. It verifies original setup save, final native saves, official before/after roundtrips, then repeats the final player command from the reloaded checkpoint. Only writer timestamps are omitted from detached save comparisons. Source actors must survive and final commands must be stationary human gravekeeper actions.

## Reproduce without a production build

```sh
bash docs/art-gravekeeper-cast-20261110/verification/legal-cast/derive.sh \
  --repo-root /path/to/empire-work \
  --assembly-dir /path/to/frozen/existing/assemblies \
  --output /new/empty/sibling-scratch-directory
```

Use .NET8 and existing `Empire.dll`, `Empire.Core.dll`, `GodotSharp.dll`. The script defaults to the checkout's existing Godot Debug assembly directory if `--assembly-dir` is omitted. It builds only a copied scratch harness, never the game. Output must be new or empty and outside the repository. No generated binaries are stored in this evidence package.

`portable-run/` records the final three-route replay called by absolute path from `/tmp`, with frozen presentation build `Empire.dll` SHA256 `23f3ea10fdbb1bc4f0329c9c995cfe32ff1545a412126b6c90671a0575d9cd9c`. The new-output run succeeded with exit0; repository-contained and nonempty output each explicitly failed with exit2 without changing the target. Actual logs, assembly hashes and full route verification results are preserved there. Original checkpoint bytes were not rewritten by replay. `evidence-sha256.txt` covers all source and evidence files except itself.
