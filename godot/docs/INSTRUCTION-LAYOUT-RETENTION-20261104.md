# Native instruction layout retention

Date: 2026-11-04 UTC
Baseline: `eaf1225f958664f33b45686aac1ed21529ae19d2`
Code checkpoint: `73ec6a1c4da522a6ae746ca72af694001bc3e97a`

## Measured player path

The unchanged C01-01 campaign was entered through the real New Campaign, story/choice, staging and battle flow. Native Space selected an actor; real viewport mouse events inspected 16 reachable terrain cells in three selected-unit and three idle samples. Canonical terrain labels, current guidance, selected actor, dossier interaction, battle revision and state digest were checked.

A temporary native phase timer showed instruction reconstruction accounted for approximately 94% of the remaining HUD work. Selected-unit instruction work cost 314.258 / 325.771 / 289.669 ms per 16 events; idle instruction work cost 94.818 / 103.339 / 101.786 ms. This diagnosis includes instrumentation effects and is not a device-frame-rate measurement. The temporary instrumentation was removed.

A new actual campaign guard failed on the original source (native exit 1): unchanged guidance was not retaining its native control during terrain-only updates. The optimized real-session run passed all 426 assertions and recorded selected-unit synchronous input times of 102.156 / 139.004 / 102.820 ms, versus 482.635 / 458.730 / 462.588 ms in the preceding baseline's complete aggregate. Idle times were 34.004 / 33.861 / 40.040 ms. Managed allocation remains broadly similar; the improvement removes unnecessary native text/control work, not a large domain allocation.

The final full aggregate repeated this path successfully. Selected-unit samples were 120.749 / 91.502 / 117.984 ms with 16,511,176 allocated bytes each. Idle samples were 35.226 / 34.873 / 39.296 ms with 7,986,816 / 8,518,040 / 8,518,040 allocated bytes. Host scheduling and cold work vary, so individual observations are preserved rather than presented as universal timing guarantees.

These are headless native input/CPU measurements. They are not rendered visual acceptance, GPU measurements, or device-FPS promises.

## Ownership and implementation

`GrayFlagSession` continues to own all battle facts and guidance text. `TacticalHud` retains native rendering only when the exact stamp, busy state, guidance text, visibility and actual primary-action composition remain compatible. Forecast entry/exit, changed actions/text/visibility and all existing context invalidations take the normal rebuild path.

The primary-action comparison includes `target`; it cannot mistake unchanged commands elsewhere in a projection for the currently displayed primary section. A non-primary trailing target does not unnecessarily invalidate the unchanged command guidance.

Layout callbacks now follow the lifetime of the concrete owned hint, tray and actions, rather than the generation of the Apply call which created them. They validate that those exact controls remain live direct children of this HUD. Moving a stale hint to another live container cannot resize the current tray. Hint exit continues to release its action-minimum-size subscription. Deferred focus and scroll generation guards are unchanged.

Native button minimum sizes still drive growth and contraction; the existing text and tray sizing calculations are unchanged. A focused regression also exposed a native theme-metric boundary: changing an ellipsized label from font size 13 to 25 left its rectangle at 22 pixels high while its two current lines required 79 pixels. Godot's ellipsized/autowrapped minimum-size contract need not change with those metrics, so MinimumSizeChanged alone is insufficient. The hint now listens to the native ThemeChanged signal with deferred delivery, after the label's theme cache notification, and refits from current native line metrics. This does not change the shipped font or expose a new setting.

No map, town composition, asset, font, gameplay rule, save format or save behavior changed. No second layout model, battle cache or general HUD diff framework was added.

## Verification

The existing actual C01 idle-guidance suite is now included in `tools/godot/verify-native.sh`; it was previously outside the aggregate despite covering this changed UI. The complete gate therefore has 40 stages and 26 native scenes when including ExportRelease compilation.

New labeled native fixtures exercise stable hint/action/tray ownership, post-retention action growth and shrink, text-metric changes and restoration, current text/tooltips, stable listener counts, disconnection on exit, stale reparented controls, actual primary ordering, target/forecast transitions, independent idle revision/binding/busy invalidation, art reconfiguration and remount without Apply. Existing real campaign pointer tests check both selected and idle retention while maintaining canonical data and native dossier keyboard/pointer behavior.

A focused positive lifetime control passed 51 assertions. Nine deliberate production mutations each compiled successfully and exited 1 at the intended guard:

1. Disable instruction reuse: retained native composition guard.
2. Restore the old Apply-generation callback check: action growth/layout guard.
3. Ignore changed hint text: current text/tooltip guard.
4. Ignore the actual primary-action selection: primary composition guard.
5. Remove exact hint ownership: reparented stale-control guard.
6. Keep the old action listener connected: subscription cleanup guard.
7. Ignore busy changes on the idle branch: independent context invalidation guard.
8. Remove the native theme notification subscription: full line-height/backing guard.
9. Permit fitting without a current projection: remount-without-Apply guard.

The reduced negative-control runner and all mutated sources were restored to the clean code checkpoint before the aggregate. The original reuse failure and the intermediate font-metric failure remain recorded as failed evidence, not successful validation.

The complete 40-stage aggregate ran continuously on the clean code checkpoint from **08:52:45 to 09:30:55 UTC**. Every stage recorded exit 0. All 26 native scenes passed strict teardown scans for errors, leaks, unhandled exceptions and segmentation faults. No interrupted stage was counted as successful.

- Active-source selection negative checks, formatting, source metadata and four authored-map static validators passed. Metadata remains 167 unique script UIDs and 4,190 intentionally unimported historical audit files.
- Debug game, core-test and ExportRelease compilation passed with zero warnings/errors.
- All 13 core contracts passed, including 16 independent shipped levels, 3,425 legal native actions and exact save/replay round trips. This is not a claim of a complete carried-state campaign victory.
- Full HUD: 510 assertions. Actual idle guidance plus labeled layout/lifetime fixtures: 83. Dossier focus: 73. Complete campaign navigation: 8,908 assertions and zero failed groups, including the unchanged legally played opening victory.
- Animation: 844 assertions. Battle result flow: 371. Campaign UI: 205. Lighting lifetime: 106. Lighting contracts: 1,791. Scenery/resource/lifecycle: 8,753. Existing expected fixture/readiness warnings are not suppressed.

The aggregate records command-stage boundaries, checkpoint, explicit exits, log hashes and script/binary hashes. Each native scene uses fresh isolated save data. The code checkpoint remained unchanged throughout; the following documentation commit records the result only. Rendered GUI/device visual acceptance remains separate and was not performed here.

## Godot references

The change follows [measure-first CPU optimization](https://docs.godotengine.org/en/stable/tutorials/performance/cpu_optimization.html) and [explicit scene dependencies](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html). The metric regression was checked against the actual [Godot 4.6.3 Label implementation](https://github.com/godotengine/godot/blob/4.6.3-stable/scene/gui/label.cpp), especially theme notification and ellipsized minimum-size handling, and the native [Control theme_changed signal](https://docs.godotengine.org/en/stable/classes/class_control.html#class-control-signal-theme-changed).
