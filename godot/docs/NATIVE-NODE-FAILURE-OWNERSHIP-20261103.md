# Own native nodes before fallible setup — 2026-11-03

## Review scope and contracts

The review followed `AGENTS.md`, `PROJECT_RULES.md`, and Godot's guidance on
[self-contained scenes](https://docs.godotengine.org/en/4.6/tutorials/best_practices/scene_organization.html),
[parent/child lifetime](https://docs.godotengine.org/en/4.6/classes/class_node.html#class-node-method-queue-free),
and [C# signal lifetime](https://docs.godotengine.org/en/4.6/tutorials/scripting/c_sharp/c_sharp_signals.html#disconnecting-automatically-when-the-receiver-is-freed).

The canonical battle/session remains the only writer of game facts. Screen,
board, AI pacing and result views retain their existing responsibilities. No
new writer, state arbitration, dependency framework or speculative architecture
rewrite was justified. The verified defect concerns native node ownership when
presentation preparation fails.

## Original-code negative evidence

The same native checks were first run against the original implementations:

- Freeing an unbound `BattleScreen` left its eager Board and Overlay alive: two
  orphan nodes. The existing failed-`PrepareMounted` fixture had manually freed
  those objects, so it did not protect the production ownership contract.
- Three repeated missing-image marker projections preserved their documented
  rule-derived drawings but each leaked three partially initialized Sprites.
- Three missing structure-image attempts each leaked their unparented node.
- A marker ID conversion failure after sprite creation leaked two nodes.
- NaN and positive/negative infinity facing vectors each leaked one badge node.
- Duplicate player IDs and a non-array players document each leaked one root.

The twelve independent dynamic failure cases reported nineteen orphan nodes.
The tests record failure before cleaning up their negative controls. They retain
and check the original exception types, relevant messages and missing-resource
identity; missing marker images must still return the existing fallback and its
diagnostic. The source manifest and supplied state remain unchanged.

These are explicit fault-injection cases, not evidence that shipped art is
missing or that ordinary gameplay currently crashes. No marker fixture is
represented as an actual combat-death playthrough.

## Repair

`BattleScreen` now owns the lightweight MapArea → World → Board subtree from
construction. The public, pre-bind configurable Board keeps its identity.
Resource-dependent Overlay construction remains at the existing interface-build
boundary. The final hierarchy, child order, anchors, clipping, mouse filters and
z values are unchanged. Normal parent teardown owns cleanup; there is no
parallel unbound-node cleanup path or teardown on temporary unmount.

`DynamicStateView` resolves pure input and resource lookups before allocating
native objects where practical, immediately attaches structural/marker nodes to
the projection root, and frees a partially built badge aggregate on failure.
Existing root cleanup can now reach its children. Shared cached textures retain
their existing owner. Errors are rethrown without replacement; the marker-only
missing-art boundary and its text are unchanged.

No artwork, map, campaign data, gameplay rule, save schema or manual-save policy
changes. The accepted C07 scene and selected assembly retain their original
SHA-256 values.

## Reliable native measurement

The debug orphan API is checked using a newly allocated node and its full
instance ID before empty differences can count as success. Godot 4.6's generated
C# typed-array indexer truncates these IDs, as documented in
[Godot issue 114242](https://github.com/godotengine/godot/issues/114242).
`NativeNodeChecks` centralizes the official suggested untyped-array/Variant
reading, preserves all 64 bits, and disposes the underlying array wrapper.
The first attempt deliberately failed the probe rather than accepting an empty
or incorrect observation. This helper is test-only.

Lifecycle coverage includes detached/mounted, immediate/queued destruction,
pre-bind Board settings, remount/rebind, actual session load/restart/scene exit,
and application preparation failure without caller-owned cleanup.

## Completed verification

- Production and core-test builds: zero warnings and errors.
- Full core suite: 11 groups passed. All 16 shipped levels reached their native
  conclusion after 3,379 legal AI actions in total and preserved exact save,
  replay state and outcome digests. These are independent level runs, not a
  carried-state campaign victory or a balance assessment.
- Lighting lifetime: 106 assertions, including three actual session
  load/rebind/restart/scene-exit cycles and the four unbound teardown cases.
- Dynamic state: 745 assertions, including all 16 initial level projections,
  nine canonical structures and all twelve failure-ownership cases. This
  projection suite is not an additional 16-level gameplay run.
- Campaign navigation: all 17 groups passed, 7,061 assertions in this run.
  Frame-sampling assertion counts vary with runtime.
- Camera integration: 313 assertions; tactical HUD: 379; campaign UI: 205;
  battle result flow: 371. Full BattleScreen real-session integration passed.
- All eight Godot processes exited zero without error, leaked-resource,
  segmentation-fault or unhandled-exception diagnostics. Expected injected
  storage/refusal and source-readiness warnings remain visible.
- Changed-source whitespace, indexed metadata (155 unique script UIDs), all
  14 metadata positive/negative cases, and `git diff --check`: passed.
- Independent read-only review of both production repairs: no blockers.
- Reed Village, White River and Luojia static semantic validators passed.

The full `verify-native.sh` aggregate remains blocked at its formatting stage
by 184 untouched files; none is a file changed here. At this checkpoint, the
separately run Black Flag Camp static validator also failed because it still
required C04's former
`paintedGroundPath`, whereas the existing scene selects assembled ground. Both
that validator and C04 scene were byte-identical to the starting commit. Neither
gate was weakened or changed. Revisit these existing gate contracts before
claiming a green aggregate; preserve the accepted scene registration when doing
so. The subsequent C04 static-gate correction is recorded in
[NATIVE-VERIFICATION-GATES-20261103.md](NATIVE-VERIFICATION-GATES-20261103.md);
it resolves the obsolete scene binding without changing the map or artwork.

No GUI/visual acceptance, exported package or TypeScript-suite pass is claimed.
The cloud environment still has no usable graphical display. Native headless
lifecycle/control checks cannot establish the rendered HUD's appearance.
