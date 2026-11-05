# Native touch activation and dossier identity

## Reproduced player paths

At baseline `935ceecca`, with the shipped touch-to-mouse emulation enabled, a fresh formal C01 campaign reaches battle through New Campaign, story, the north-hill choice, and staging.

- Native touch on End Turn activates once but produces Godot's `can_process` diagnostic because its button has already left the scene tree. The same-point mouse control is clean. The permanent C01 sequence also exercises Wait, Undo, a dossier facing action, and End Turn; the baseline emits four lifecycle errors.
- Select the real actor with touch, cancel selection with Escape while preserving inspection, then perform a quick touch press/release on the dossier entry. The entry receives its native button signals but the dossier does not open. A deferred map mouse-exit clears incidental terrain and advances the HUD render generation before the queued opener checks that generation.
- The earlier popup-dismissal behavior remains separate: dismissing a dossier or system popup must not activate the battlefield or End Turn beneath it. This work does not treat those successful isolation assertions as evidence that the later positive touch activation was diagnostic-free.

These are native input/scene-tree checks, not screenshots or visual acceptance. No crash, duplicate battle command, or save corruption was established by the lifecycle diagnostic.

## Ownership and repair

Only native button activation is deferred. The button publishes after the current mouse/touch dispatch completes, with coalescing and current lifetime, binding, revision, enabled/busy, and row checks. The session and tactical controller still own transitions synchronously. Removing mouse emulation, consuming raw touches, suppressing diagnostics, or immediately freeing an in-flight control would conceal or distort the failure rather than repair that boundary.

`GrayFlagSession.ProjectHud` now projects the dossier subject from the already visibility-filtered inspected unit, or the site/structure facts it actually renders. `TacticalHud` uses that immutable identity in equivalence, entry retention, and pending opener validity. Incidental hovered terrain is not part of an inspected unit's identity. The widget does not infer identity from localized labels, which can match for distinct units or sites. Null remains valid for a general roster dossier.

Forecast controls retain their existing replacement/lifetime behavior. Forecast-detail row equality is not claimed to establish target identity. No battle rule, map, art, save format, or save meaning changes.

## Regression scope

- Formal native touch: Wait, Undo, dossier stance/facing action, End Turn, quick dossier opening and closing.
- Pending HUD order: duplicate/different-row coalescing, retained unrelated refresh, replaced row, busy state, binding replacement, unmount/remount, freed button, and recovery after rejection.
- Pending dossier read: visually identical different units, different site cells/kinds, canonical revision change, detached opener, retained incidental terrain refresh, and real native opening/disclosure/close lifecycle.
- Existing native signal callers now wait for the intentionally deferred UI boundary. Direct controller/session and keyboard assertions remain synchronous. Stale control tests retain their original rejection semantics rather than dropping assertions.

## Godot basis

[Input event flow](https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html) places GUI dispatch within the viewport's native input processing. [Callable.call_deferred](https://docs.godotengine.org/en/stable/classes/class_callable.html#class-callable-method-call-deferred) provides the engine's idle dispatch boundary. [Scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html) recommends focused scenes with explicit dependencies; here the immutable session projection supplies identity while the HUD owns only control lifetime.

The Godot 4.6.3 source shows touch-to-mouse dispatch before the original touch event in [input.cpp](https://github.com/godotengine/godot/blob/4.6.3-stable/core/input/input.cpp), and touch-focus processing in [viewport.cpp](https://github.com/godotengine/godot/blob/4.6.3-stable/scene/main/viewport.cpp). Runtime trace agrees with that ordering. Source line numbers are not claimed to be a captured native stack for the installed binary.

## Verification

Implementation checkpoint: `ab5f12ae9f6040052f49c1c3998bff1e58c436c8`.

- Test-first navigation on the original production implementation failed as intended: 9,335 assertions, one failed group at the quick dossier assertion, and four native `can_process` errors during the legal touch order sequence. An earlier test compilation error used a nonexistent read-model property; the test was corrected to inspect the authoritative snapshot before this baseline run.
- Candidate navigation passed 9,348 assertions, with strict process exit 0. BattleScreen integration passed. The HUD suite passed 1,041 assertions on an unchanged bounded recheck and again inside the complete gate.
- Eleven unchanged independent native probes passed 60 assertions with process and strict diagnostic exits all 0: eight modal dismissal combinations, unmodal touch End Turn, the mouse control, and quick dossier opening. Assembly and script hashes remained unchanged throughout those probes.
- The original complete `verify-native.sh` command ran uninterrupted from **08:03:08 to 08:44:58 UTC**, 2,510 seconds, exit **0**. All 30 native scenes plus editor import passed strict log checks. All 15 core groups passed. All 16 formal levels' 3,425 commands retained exact ordered action/event/receipt traces and final state digests against the established baseline; save/replay checks passed. All 40 expected warning headers matched the previous complete gate.
- ExportRelease configuration compiled with zero warnings/errors. This is a configuration build, not a packaged export or manual GUI acceptance.

### Retained unresolved shutdown observation

The first candidate HUD run completed its 1,041 assertions but then reported 50 unsafe RefCounted-reference diagnostics during Mono shutdown and exited with SIGSEGV (139). The strict gate correctly failed. One bounded rerun of the unchanged command and identical DLLs passed with exit 0 and no such diagnostics. The later uninterrupted complete gate also passed that scene cleanly.

The first failure is retained, not counted as a pass or claimed fixed. The leaked objects included preexisting badge-test events/themes and art resources; that is not proof that the new activation callback caused the shutdown failure. No production resource-release change, forced garbage collection, log suppression, or retry loop was introduced. This remains an unresolved test-process shutdown reliability observation, separate from the reproducibly fixed native touch defects.

The brief execution-transport disconnect during candidate navigation did not interrupt its process: its completed log and exit 0 were recovered before the full gate began. No interrupted run was reported as a complete pass.
