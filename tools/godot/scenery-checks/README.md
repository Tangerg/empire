# Source cover and perimeter checks

Run `bash tools/godot/scenery-checks/run.sh` with the existing official toolchain.
It regenerates an oracle by executing the original TypeScript renderer, verifies
original half/full-cover PNG bytes and SHA256, builds the native project, imports
resources, and runs the native checks with clean process exit required.

The checked-in `godot/tests/oracle/board-scenery.json.gz` is the same source oracle,
not invented scenery. A Node-free aggregate can run:

```sh
"$GODOT_NET" --headless --path godot res://Scenes/SceneryChecks.tscn -- --scenery-oracle=res://tests/oracle/board-scenery.json.gz
```

The 16 real source frames distinguish the two authored frame arrays from the
14 deterministic generic perimeters. Generic perimeter is not an authored scene
restoration: nine source scene documents remain absent. Comparisons cover exact
atlas/image identity, cell, contacts, flip, scale, opacity and strong edge-shadow
selector. Native comparisons allow source SVG serialization rounding (0.006px
contacts; 0.0006 local-transform units); this is not rendered pixel equivalence.

Seven live cover changes compare actual `battlefieldFeaturePieces` output with
native art identity, source anchor/scale, ordering/count and current-state refresh.
The strongest written cover selects only artwork, never combat rules. Cover CSS
filters use logical image coordinates; generic environment group filters use the
source atlas coordinate system before the group scale. Negative assertions reject
wrong density and weaker topic-shadow ownership. Resource and lifecycle checks
exercise mounted sprites, cache reuse, rebind/disposal and missing-versus-explicitly
empty frame arrays. `check-mutants.sh` rejects changed source-oracle contacts,
shadow selectors and cover artwork after a successful native build.

No GUI/pixel acceptance or desktop performance claim is made by this suite.
