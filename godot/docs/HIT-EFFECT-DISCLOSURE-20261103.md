# Unit-target forecast and conditional hit effects

## Reproduction

The unchanged C01-14 campaign state exposes a misleading confirmation forecast.
After 85 legal recorded orders, Torren moves `(7,7) → (7,8) → (8,8)` and attacks
the golem at `(9,8)` with `c01.border-blade`. The mounted HUD displays counter
damage 28 and HP `98 → 70`. The accepted action then applies a blocked push and
10 collision damage, leaving 60 HP. Nothing in the old forecast disclosed it.

`tests/native/c01-14-collision-forecast.json` stores only the original commands
and expected identities, not another copy of the map or content. The test loads
the shipped level, checks initial hash `10b86025`, replays to `c41b8dad`, arms the
real screen's unchanged path, and commits exactly one order to `2c48b02d` with
the original complete event hash. Before the repair it failed on the missing
visible collision disclosure, after proving the actual 28/10/60 receipt.

## Ownership and scope

- `BattleCombat.Forecast` owns the selected strike, counter and support payloads.
  `CombatPlan.CounterEffects` brings the already-selected counter's detached
  effects into the same plan as the other hits. Execution consumes those facts
  rather than looking up a second counter payload later.
- `Land` remains the owner of whether an effect actually runs. A hit recipient
  must remain on the field after damage and morale consequences. Counter effect
  payloads are not discarded merely because the direct forecast predicts death:
  an earlier composed effect can change HP before the counter lands.
- `BattleCombat.CollisionDamageAmount` owns the existing configured-damage
  rounding and clamping. Both forced movement and the effect formatter use it.
  Actual loss is still capped by the health owner; the HUD does not simulate
  blocked movement, later status changes, departures or a full command.
- `WeaponHitEffectText` is an open, injected presentation registry. It consumes
  immutable definitions, localizes compact and detailed conditional descriptions,
  and validates coverage against the composed effect registry. No effect-kind
  switch or collision algorithm lives in a widget. New kinds need a formatter;
  unknown kinds do not disappear silently.
- `GrayFlagSession.ProjectHud` exposes at most one emphasized summary per
  primary/collateral attack, counter and support phase. The extra-effect count
  precedes potentially truncated text. Ordered full details remain in the
  scrollable calculation dossier. New effect entries require both participants
  to be visible before their names, effects or counts are exposed.
- The HP scope is explicit: only main strike and counter, excluding movement,
  support and hit-effect aftermath. C01-14 continues to show direct HP 70 and
  separately warns that a blocked counter push may inflict another 10 damage.
  It does not falsely turn that gauge into a complete final-HP simulation.

This slice covers the existing unit-target exchange forecast. It does not add a
new ground-area forecast, change target selection, rewrite combat rules, or claim
that the whole existing fog-of-war forecast policy is resolved. The previously
accepted town artwork, map, manual save and autosave semantics are unchanged.

## Verification

Focused coverage includes the real mounted C01-14 regression, counter payload
selection/isolation/execution, absent counters, death and morale departure,
healing before an initially lethal counter, fractional collision requests and
HP caps, formatter extensions and failure cases, injected catalogs, and bounded
native forecast layout with long names and repeated effects at 960×640 and
1180×640. Actual session-projection fixtures exercise all three phases, multiple
visible and fog-hidden collateral hits, stable emphasis selection, leading extra
counts, ordered complete details, and repeated-projection purity.

The counter ownership suite passes. Deliberately restoring the old execution-time
counter payload lookup makes it fail on the consumed payload; restoring the
correct source byte-for-byte restores a pass. The pure presentation suite passes
133 assertions, including fractional collision requests and custom effect kinds.
The complete mounted BattleScreen scene, actual multi-effect session projections,
399-assertion HUD scene and 73-assertion dossier focus scene pass. The known
counter's collision amount fits the compact line without ellipsis. The largest
tested forecast retains its guidance inside the lower safe half, and all 24
detail rows remain in the scrollable dossier. Closing it restores focus after
the existing deferred focus handoff. Builds have zero warnings and errors.

An independent copied-DLL verifier replayed all 3,379 historical commands and
9,648 ordered events across sixteen original levels, with zero AI calls. Every
receipt, before/after digest, event, final state, save and exported replay is
unchanged. Native save/load and replay verification also pass. This took 97.319
seconds; the baseline files remain byte-identical. These are independent battles,
not a carried-state campaign completion. Full aggregate results are recorded
after the final verification run.

Native layout follows Godot's [container ownership](https://docs.godotengine.org/en/stable/tutorials/ui/gui_containers.html)
and [explicit scene dependencies](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html):
containers lay out the controls, and the HUD receives projected facts rather than
querying or advancing battle state. Headless controls, layout geometry and input
tests are not graphical or exported-product acceptance.
