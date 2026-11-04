# Tactical movement-field reuse, 2026-11-04

## Measured player flow

Baseline: `9f62daf69d9aed26bc01a2a6fdd2f921017b5693`.

The existing native pointer witness starts the real application with fresh isolated
saves, completes the opening story and choice, enters C01-01, selects its actor
with Space, and visits 16 reachable ground cells using native mouse events. The
actor, battle revision and digest stay unchanged. It also verifies current terrain
text, retained HUD sections, dossier input and return-to-opener focus.

Despite retaining unchanged HUD controls, each sweep still allocated 58,185,288
managed bytes. The adapter computed the same movement field independently for
MoveStops, PathTo and Threat during every overlay refresh. The source TypeScript
GameSession already reuses fields per unit and state stamp, forwarding the same
field to both path and threat policies (`packages/battle-engine/src/session.ts`).

The new per-event allocation guard rejects the baseline implementation at native
exit 1. With reuse, all three initial fixed sweeps allocate 16,572,744 bytes, a
71.5% reduction. The guard allows 2 MiB per visited cell; it deliberately has room
above the measured fixed cost without admitting the original repeated traversal.
The isolated real-flow runner passes 225 assertions and exits 0.

These measurements cover synchronous native input handling and current-thread
managed allocation, not GPU work, physical input latency or device FPS. Initial
fixed CPU samples were 5,537.121, 1,675.379 and 1,468.262 ms per sweep; the spread
is too large to claim a timing improvement from this run. Allocation is the
stable measured improvement. No font, texture, map, combat rule or save meaning
was changed.

## Ownership and lifetime

The composed ITacticalSpace remains the only source of movement, path and threat
rules. SelectionQueries already owns a detached snapshot and immutable revision
stamp. Its private unit-keyed dictionary now retains only the policy-produced
MoveField for that snapshot. A new adapter has a new dictionary, including after
a dispatch, undo, rebind or load; no global cache or invalidation protocol exists.

All public reads still perform their original pre- and post-revision checks.
Visible-unit admission happens before lookup. Invisible units remain in the full
canonical snapshot because they can still affect blocking and control zones.
PathTo and ThreatTiles continue through the composed policy, with the same field
object explicitly supplied. MoveField's mutable extension contract is retained;
projected stops, paths and threats are fresh read-only copies, not extra caches.

## Regression coverage

BattleScreenMovementQueryChecks runs inside the existing native battle suite:

- All 16 shipped levels, first two actors each: ordered stops, threat, origin/end
  paths and out-of-map path results match fresh queries through composed Space.
- Move-, path- and threat-first access compute one field per unit per adapter.
- Units, adapters, bindings, sessions and a saved/reloaded session stay isolated.
- Hidden and unknown subjects fail before any policy call; hidden units are not
  removed from snapshots passed to the spatial policy.
- Warmed adapters reject all three reads after dispatch and undo; fresh adapters
  recompute. A successful command inside a policy callback exercises the read's
  postcondition, rather than just its entry check.
- Custom mutable fields, explicit Stops without traceable Tiles, custom paths and
  custom threats keep their identity and behavior. Previously returned lists
  remain detached and cannot be edited by their caller.
- Queries preserve the authoritative state, revision, events and action history.

The existing spatial architecture check and its field-bypass mutation anchor
were updated for the shared-field helper without removing their assertion.

## Verification status at implementation checkpoint

Debug compilation and the isolated new contract scene pass. The original
allocation regression fails and the fixed real-flow check passes, as above.
Four deliberate behavioral mutations compile successfully and exit 1 at their
intended runtime assertions: removing reuse, omitting the field from ThreatTiles,
bypassing the composed PathTo, and dropping the post-read revision check. The
updated static field-forwarding guard also rejects its bypass mutation.

The full current native gate is recorded below once it completes; an
implementation checkpoint is not a full-gate claim.

## References

- [Godot CPU optimization](https://docs.godotengine.org/en/stable/tutorials/performance/cpu_optimization.html): measure representative work and remeasure the change.
- [Godot scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html): keep responsibilities focused and dependencies explicit.
- Repository `AGENTS.md` and `PROJECT_RULES.md`: one owner per fact, measured optimization, composed policy substitution and meaningful negative controls.
