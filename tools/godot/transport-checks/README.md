# Native transport verification

Run from the repository root with the already approved local toolchain:

```sh
tools/godot/transport-checks/run.sh --verify-guard
```

The script reads `tools/godot/env.sh`, regenerates expectations from the original
TypeScript rules, and builds native C# into an isolated temporary directory. It
requires the existing `node_modules` and .NET SDK. It does not install packages,
modify the main project's build outputs, or delete artifacts. Its printed
temporary directory contains the oracle, binaries, and any mismatch reports.
Generated vectors and binaries are not repository deliverables. TypeScript is
only a development-time oracle, never a runtime bridge.

## Verified result

On 2026-10-20 the complete command passed:

- 81 direct rule/departure/return cases: query results, refusal text and exception
  category, ordered events, complete state at the end, and a state digest at
  **every individual event emission**.
- 24 real native dispatcher steps across 17 sequences: ownership, action
  entitlement, carrier action completion, side/initiative handoffs, lifecycle,
  receipt snapshots, and rollback. Source-side assertions require the intended
  successful boarding/landing/handoff steps to succeed; campaign objectives are
  replaced with neutral rout objectives so an unrelated failure cannot end these
  fixtures prematurely.
- Four source-authored save documents, compared in full, followed by eight
  resumed dispatch steps. Cases include changed carrier ownership and a
  historical fallen snapshot sharing an identity with a living passenger.
- Ten explicitly native malformed-save guards, including missing/nontransport
  carriers, carrier-as-passenger, duplicate living IDs/keys, missing runtime
  fields, unknown owners/statuses, and zero living HP. Rejection must be a
  `StoredDocumentException` and must not mutate the supplied document.
- A deliberately broken boarding capture reset is detected at the expected
  emission-time digest, not merely by compilation or an unrelated failure.

Normal and deliberate-mutant builds both completed with zero warnings and
errors. The mutant process intentionally exits unsuccessfully; the runner
checks the exact semantic mismatch before reporting guard success.

## Coverage and ownership

The live oracle calls `transports.ts`, `unit-return.ts`, `damage.ts`, `morale.ts`,
and the normal scenario withdrawal/engine dispatch paths. Fixtures cover the
installed `c01.supply-wagon` and infantry passengers, valid transport profile
variants, refusal precedence, state-order menus, square4/square8/hex geometry,
edge clipping, blocked terrain/structures, cliffs, deep snapshot isolation,
status/morale/reaction retention, and carrier death/rout/surrender/withdrawal.
Departure tests include commander consequences and pending casts.

Native implementation:

- `BattleTransport(JsonArray packs, MovementRules movement)` owns boarding,
  landing, and their eligibility queries. `PassengersOf` is static; instance
  queries are `EmbarkRefusal`, `CarrierOptions`, `DisembarkRefusal`, and
  `PassengerOptions`. Commands are `Embark(state, unitId, carrierId, emit)` and
  `Disembark(state, carrierId, unitId, at, emit)`.
- The engine dispatcher owns commandability and ownership checks. Boarding only
  finishes the passenger's action; landing also finishes the carrier's action
  in the dispatcher, after the landing event, exactly like the source.
- `BattleDamage` remains the sole owner of carrier loss and passenger departure
  consequences. This slice did not modify or duplicate it.
- `BattleUnitReturn.Restore(state, marker, UnitReturnRequest, emit)` owns the
  source return/reset transition. It preserves identity rather than allocating
  a new unit. Revival clears statuses, restores morale/reaction and waits until
  the next turn. Ordinary disembarking deliberately does none of those resets.
- `BattleTransport.StoredStateIssues(state)` expects an already shape-checked
  state and owns transport relationships. Persistence owns shared shape,
  identity, owner, HP and content-reference checks. Native relational guards are
  documented hardening beyond the source reader, not claimed source-reader
  rejection parity. Capacity, tag restrictions and owner equality are **not**
  rechecked on load: these may change after boarding.

Full-dispatch tests access the real engine's private registry only within this
test executable to feed adversarial snapshots without adding a production
restore bypass. Save/resume tests use the public save/load APIs.

This suite verifies the transport slice. It does not substitute for the complete
campaign replay gates, renderer testing, or unsupported future resurrection
abilities/scenario effects; those must compose the shared return owner rather
than reimplement its transitions.
