# Zoom-limit affordances · 2026-11-11

Base: `ece8b332070ec0b843c0000e9abea502e8774350`.

## Reproduction and fix

In a real native C01 battlefield at 960×640, repeatedly clicking zoom out reached
0.5 but left the minus button visually enabled; further clicks did nothing. The
same missing availability projection applied to the upper 2.2 limit. The camera
already owned and enforced both limits correctly.

`BattleScreen` now derives each button's disabled state from that existing camera,
after the frame's pending zoom is applied. Busy/unbound blocking is preserved.
No extra zoom state, gameplay rules, assets, inputs, or save format were added.

Native desktop recheck at 960×640 confirmed minimum-minus disabled,
maximum-plus disabled, and reverse-direction recovery. This was a focused C01
interaction check, not full campaign, export, or performance acceptance.
No screenshots or previously blocked evidence are included in this change.

## Verification

- Build: zero warnings/errors. Native whitespace and source metadata passed.
- Negative control: new integration check on the unchanged implementation failed
  specifically at “Minimum zoom disables only zoom out in the same presentation
  frame.” Restoring the fix passed.
- CameraIntegrationChecks: 328 assertions, including limits, reverse buttons,
  busy/unbusy, rebinding and complete battle-state non-mutation.
- CameraChecks: 36 assertions.
- BattleScreenChecks: real-session integration passed.
- ReadyUnitNavigationChecks: 90 assertions passed, including 960/1280/1440 layouts.
- Native core suite: 17 passed, 0 failed, including all 16 shipped-level exact
  replay/save checks. All 16 action/event/receipt SHA256 traces and final state
  digests exactly match the prior full gate in
  `docs/hud-ready-units-20261111/verification/full-native.txt`.

Text transcripts are in `verification/`. These include the existing diagnostic
that the source environment catalog is marked `runtimeReady=false`; it is not
newly introduced here. The entire aggregate/native export gate was not rerun for
this presentation-only change.
