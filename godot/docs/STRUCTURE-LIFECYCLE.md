# Native structure lifecycle

`BattleCombat` owns structure HP and disabled transitions. Scenario effects are
thin delegates with existing structure-reference validation. Combat and scenario
changes remain inside `Battle.Dispatch`, including trigger history and event
counts; failed actions commit neither state nor events.

- `disableStructure { id }` preserves HP and owner. A live, enabled structure
  emits `structureDisabled { structure, at }`; repeated disable or disable of a
  destroyed structure is a no-op. Unknown identities are errors.
- Repair preserves the existing positive-repair reactivation contract. A real
  disabled-to-enabled transition emits `structureEnabled { structure, at }`, even
  when HP was already full and zero HP was restored. HP gain alone still emits
  `structureRepaired`. Negative/nonfinite repair amounts are invariant errors;
  zero is a no-op, as is repair on a non-repairable definition.
- Destruction is HP zero and retains its separate destroyed event. Disabling is
  not damage, capture, or destruction.

The three native c01-03 close routes retain their original conditions, signals,
and gate variable writes. They now disable the node. A single once-only
`afterAction` rule releases oathbound units when it is disabled OR destroyed.
Direct attacks therefore free them without adding a victory route. An arbitrary
friendly visitor is not the named escort; repair never re-enslaves freed units.
The turn-15 route uses ordinary turnStart → afterAction sequencing.

## Presentation and localization

Destroyed takes precedence over Disabled, then Damaged, Captured, Normal. Each
bound structure must explicitly bind all five logical roles. Missing bindings
fail catalog admission. Disabled and Destroyed hide active owner banners and HP
gauges; stored HP and owner remain unchanged. Lifecycle notices use zh_CN message
keys and coordinates, not English event identifiers. No special animation is
invented: receipt settlement rerenders canonical state.

**Remaining authored-art dependency:** existing console art only supplies normal,
damaged, and captured frames; even its old Destroyed binding points to damaged.
The explicit Disabled binding currently points to the same existing physical
frame. That frame still includes a glow and is NOT accepted as an unlit console.
This batch changes logical classification and active decorations, not bitmap
pixels. A later approved visual batch must supply genuine disabled/destroyed art
and update their explicit bindings. No new images, map geometry, placements, or
per-node visual flags are introduced here. The unbound mother-root continues to
use the existing rule-derived rendering.

## Persistence and verification

Tactical and mission rules are both version 3. Old version-2 ruleset saves/replays
are explicitly rejected, with no compatibility adapters or migration; loading an
old scenario must not retain its superseded damage/release behavior. Current
save/replay carries the existing disabled field, once ledger, and event counts;
no duplicate lifecycle state or new storage schema is needed. Historical TS
execution/oracle data is not updated to mirror this native change.

Run the focused real-engine regression:

```sh
source tools/godot/env.sh
dotnet run --project godot/Empire.Core.Tests -- godot/tests/oracle --only 'structure lifecycle and redstone routes'
```

It exercises all three routes through ordinary native actions, actual direct
attack destruction, full-HP reactivation, one-time liberation, save/replay,
reference rejection and transaction rollback. DynamicStateChecks covers logical
roles and active decoration visibility using real Godot resources. The broader
current acceptance command is `bash tools/godot/verify-native.sh`.
