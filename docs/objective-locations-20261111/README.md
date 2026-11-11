# Public objective location verification — 2026-11-11

Base: `f2e322a0561182a77af97ec455599b6a6f746442`.

- Core negative control deliberately removed zone targets and failed the exact
  six-cell C02 assertion. Restored implementation passed.
- Native camera integration passed 357 assertions (including keyboard location,
  unchanged selection/state, busy/rebind and hidden-objective boundaries).
- Real 960×640 native mouse checks verified C02's six east-bank destination cells
  and both C07 control locations. Selected civilian/pending command remained intact.
  Debug entry fixtures, not continuous campaign victories; no binary GUI evidence
  is included in this change.
- Full gate's core portion passed 17 groups; all 16 action/event/receipt SHA256
  traces and final state digests exactly match `hud-ready-units-20261111` baseline.
- Complete native gate was still running at this checkpoint; its final result
  will be recorded separately, not implied by the targeted checks above.

Prior denied evidence and attack-preview changes are not part of this slice.
See `godot/docs/OBJECTIVE-LOCATIONS.md` for scope and ownership.
