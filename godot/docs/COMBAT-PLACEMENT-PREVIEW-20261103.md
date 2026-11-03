# Forecast the attack's actual placement — 2026-11-03

## Reproduced player impact

In the unchanged C01-11 opening, Tasha (unit 6) can move from (4,8), through
(4,9), to (4,10), then use `c01.satchel-charge` against west-ghost (unit 19) at
(6,10). The native battle screen displayed strike 56 / counter 13, leaving
defender HP 12 / attacker HP 79. Confirming that exact legal command instead
produced strike 50 / counter 11 and HP 18 / 81.

At the destination, adjacent Bran enables Tasha's existing defensive formation.
The forecast supplied a hypothetical attack coordinate to range/terrain queries,
but formation, commander and other spatial relationships still read units at
their original positions. The actual command relocated the attacker before
building its combat plan. All four mounted HUD damage/HP assertions failed
before the repair. No level, actor, stat or rule was changed for reproduction.

## One spatial state per preview

`BattleCombat.PreviewAttack` owns a detached hypothetical placement. It clones
the complete state once, resolves the attacker by ID inside that copy, and uses
the existing relocation and facing owners to place it at the selected destination
facing its target. The existing `Forecast` then builds the sole combat plan.

The tactical adapter, HUD projection and AI attack scoring use this entry.
Commander aura, formations, occupancy, flanking, structure attacks, support and
counter calculations therefore consult the same projected battlefield, including
relationships where the moving unit is somebody else's commander or neighbor.
No modifier provider gets a separate position exception. The input state remains
unchanged even when placement or plan admission fails.

## Scope and execution semantics

This means an attack at a hypothetical placement with current HP, resources,
statuses and reaction availability. It is not a simulation of the movement path:
the API has no path and does not predict movement-triggered parting-shot damage,
death or later cast resolution.

The existing `Forecast` still evaluates its supplied battlefield with its supplied
attack geometry. A stored cast origin need not be the caster's current position;
parting shots can use the departed coordinate after movement already occurred.
Treating all coordinate arguments as relocation would change those battle rules.
`ExecuteAttack`, cast resolution, parting shots and execution of the precomputed
strike/counter/support plan are deliberately unchanged.

Corrected preview values may change AI choices and the UI's best firing spot.
Those are expected consequences of correcting their shared input, not permission
to change recorded-command execution or weaken replay checks.

## Verification and measured cost

- The mounted `BattleScreenChecks` case first failed all four damage/HP assertions,
  then passed with the same unchanged level and exact command. Its full native
  scene and the 385-assertion HUD suite passed. These are headless controls and
  functional checks, not visual acceptance.
- The core placement suite compares 197 unit attacks and 24 structure attacks
  across all sixteen unchanged openings with real accepted-command receipts and
  final HP. Separate square4 synthetic cases cover entering/leaving formation and
  aura, a moving commander/formation neighbor, support, counter flanking/facing,
  structure aura, and failed-preview isolation. They do not claim every grid,
  interception combination or hit-effect sequence.
- A deliberately incomplete repair passed the projected attacker with the old
  battlefield into `Forecast`. It still passed the formal-opening sweep but
  failed the moving-commander/neighbor fixture. Restoring the correct source
  byte-for-byte restored a passing core suite.
- Before changing the calculation, the original DLL recorded sixteen complete
  independent battles: 3,379 commands and 9,648 ordered events. The repaired DLL
  executed those exact commands without asking AI. Every receipt, before/after
  digest, event, final state, save and exported replay remained identical. These
  are independent level replays, not a carried-state campaign victory.
- A parallel verification run exceeded the existing 120-second second-AI-turn
  bound in `CampaignNavigationChecks`. After other CPU-heavy probes finished,
  the identical binary and unchanged test passed all groups, including both
  real AI handoffs and victory-to-next-staging flow. No timeout or assertion was
  relaxed. Keep heavy integration verification serial when assessing this bound.

Full-state projection has a measured allocation cost. On the same unchanged
C01-11 state and identical 41 legal attack candidates, with tiered compilation
disabled and two warmup passes, seven traversals each allocated 4,360,008 bytes
before versus 15,504,232 after. Five AI choices each allocated 25,078,448 versus
36,222,672 bytes. Both versions chose the same opening capture and left the input
unchanged. Median traversal times were 14.97 versus 70.34 ms; AI choice times
were 140.99 versus 254.91 ms under concurrent verification load. Those wall-clock
observations are not a stable performance ratio. The repair keeps the simpler,
complete state boundary rather than introducing an unmeasured cache or partial
state projection.

The fresh sixteen-level AI-driven replay gate also passed. Eleven final digests
and action counts stayed identical. With the same deterministic aggression 0.58,
the changed independent battles were:

- C01-08: 174 → 144 actions; team 1 win → team 2 win.
- C01-11: 201 → 268 actions; team 1 win → turn limit.
- C01-13: 198 → 195 actions; team 1 still won, with a different final state.
- C01-14: 220 → 339 actions; team 1 win → turn limit.
- C01-16: 485 → 378 actions; turn limit → team 2 win.

Both sides use the automatic driver in these checks. Changed outcomes document
the effect of corrected scoring, not improved difficulty, human play balance, or
campaign completion. Fixed historical commands still reproduce exactly as above.

Final serial `verify-native.sh` passed with exit 0 on 2026-11-03, 16:48:34–17:27:03
UTC (38 minutes 29 seconds). It includes formatting/metadata/static contracts,
zero-warning/error builds, twelve core groups, sixteen full-level save/replays,
and all twenty-three native scenes. The unchanged 120-second AI-turn guard passed
again inside this aggregate. No leaked-object, unhandled-exception or native error
diagnostics were present. GUI and export acceptance remain separate.
