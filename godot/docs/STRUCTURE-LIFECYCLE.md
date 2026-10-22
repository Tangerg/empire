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

The control node uses four original transparent low-pad images: lit intact,
lit damaged, intact unlit Disabled, and broken unlit Destroyed. Captured explicitly
reuses the normal physical image; ownership is a separate flat perimeter mark.
The mark and HP gauge disappear for inactive states. Uniform authored scale and
central walkable anchors register the originals without editing their pixels.

Every authored structure binding declares `depthRole: surface | standing`.
Missing and unknown roles fail catalog admission. Surface art uses the board's
existing Surface depth band and canonical cell picking, with no standing hit mask.
Standing structures retain their standing visuals and banners, including when
an authored lifecycle bitmap is still physically tall. Missing bound art fails
explicitly rather than drawing a different state. The unbound mother-root remains
an intentional rule-derived drawing, not an authored-depth fallback.

See [flat node art registration](FLAT-NODE-ART.md) for source provenance,
registration tolerances and native verification. No gameplay, map geometry,
structure HP, collision, or lifecycle rule is owned by these pictures.

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
