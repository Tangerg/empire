# Native popup pointer ownership, 2026-11-05

## Player-visible defect

In the shipped C01 opening, the unit dossier and system menu are dismissible
native PopupPanels. Clicking outside either popup used the same gesture twice:
once to dismiss the popup, then to operate the battlefield underneath it.

The unchanged production build reproduced these real native input paths:

- Dossier and system menu, same-frame and split-frame left press/release over a
  verified opaque actor pixel: each selected Laiya while closing the popup.
- Both popups over EndTurn: the actual button fired and the enemy AI turn began.
- Both popups over enabled Undo: the actual button fired and reverted a preceding
  native Wait action.
- Both popups over Wait: the actual button fired and marked the selected unit done.

Every HUD case used a new isolated opening campaign. Five baseline cases used
one process per case. The dossier/EndTurn witness was the valid first case of
an earlier multi-case process; later setup errors in that process are retained
and not counted as completed cases. No attack or posture-specific command is
claimed to have been reproduced.

The permanent CampaignNavigationChecks regression first ran against unchanged
production code and exited 1 at the dossier outside-dismissal assertion. Its
other existing groups completed. An earlier invocation was rejected before
running by the fixture's required /tmp/empire-* isolation boundary; the boundary
was preserved and the invocation corrected, not weakened.

## Root cause and ownership

Godot forwards embedded-window input before calling the parent window's _Input,
then dispatches to GUI controls if input remains unhandled. Outside dismissal
can transfer focus while the popup remains visible until a deferred hide. The
map consequently started a new gesture, or a HUD button received the press and
subsequent release, after the existing focus-loss handler had cancelled any
older capture.

BattleScreen now consumes pointer input in _Input while one of its actual
owned windows is visible, before either HUD or map GUI dispatch. The map adapter
uses the same computed visibility predicate. Native windows remain the sole
owners of visibility; no modal flag, timer, cooldown, mirror of selection state,
or alternative command path is introduced. Existing native focus cancellation
and keyboard handling are unchanged.

The predicate covers the dossier, system menu, journal, restart confirmation
and camp confirmation. Popup-internal controls are routed to their own window
before this boundary. Outside dismissal remains native, and a subsequent
independent map or HUD click still operates normally.

Official references:
- [Godot input event flow](https://docs.godotengine.org/en/4.6/tutorials/inputs/inputevent.html)
- [Godot 4.6.3 viewport routing](https://github.com/godotengine/godot/blob/4.6.3-stable/scene/main/viewport.cpp)
- [Godot Popup](https://docs.godotengine.org/en/4.6/classes/class_popup.html)

## Regression coverage

The permanent regression uses the actual GrayFlagGame menu/story/choice/staging
route and an unmodified formal C01 battle. It verifies:

- An actual opaque actor pixel is selectable before testing dismissal.
- Independent Idle selection, both popups, same-frame and split-frame native
  outside clicks, including press-time checks.
- The same four timing/window combinations over the real EndTurn button.
- Actual pointer clicks on each popup's internal close control.
- Outside right-click cannot cancel the underlying selection.
- Map-adapter wheel, Ctrl-wheel, pan and touch input remain blocked while a
  popup is visible. These adapter checks are not mislabelled native routing.
- Existing native focus cancellation retires a held map gesture when a popup
  opens and closes. This is integration coverage, not a claim of new cancellation.
- Fresh independent actor selection and EndTurn remain usable after dismissal.
- Complete canonical battle state, revision, event history and camera remain
  unchanged throughout the rejected gestures.

## Verification

Independent same-script candidate verification passed 107 assertions across
eight fresh native processes: six HUD combinations, four map timing/window
cases and two held-pointer interruptions. All exited 0 with no ERROR/WARNING
or shutdown diagnostics. Production source remained unchanged during these runs.

The first candidate permanent navigation run exposed a fixture error: the
unchanged-state assertion compared a Save wrapper whose savedAt is deliberately
current wall time. That failed run is retained. The fixture now compares the
complete canonical Snapshot, exact Events and revision, with camera checked
separately; no production behavior or safety assertion was weakened.

Corrected permanent CampaignNavigationChecks passed 9,289 assertions, zero failed
groups, strict native exit 0. The unchanged final source then passed one complete
`tools/godot/verify-native.sh` run, 06:14:39–06:51:37 UTC (36m 58s), exit 0:

- Source formatting, source companions, static-map validators and their negative
  controls passed.
- Fifteen core groups passed. All 16 shipped levels and 3,425 legal commands
  matched the prior ordered action/event/receipt hashes and final state digests;
  exact save/replay round trips passed. These are independent battles, not a
  claim of carried-state campaign victory.
- Editor import and all 30 native scenes passed the strict process/diagnostic
  boundary. All 31 raw process logs were retained. The 40 warning headers match
  the previous complete gate exactly; no new warning was accepted.
- The new permanent popup group and existing focus, camera, feedback, campaign
  navigation, confirmation and resource-lifecycle regressions passed together.
- ExportRelease compilation passed with zero warnings/errors. This is a release
  compilation, not a packaged export or desktop launch.

Implementation checkpoint: `a3f1a65f08d641476193114139faf3c6e9a8d9c7`.
Verified final code: `a29a95fb14f5e0ef37182ed6673af8a3495444ce`.
BattleScreen.cs SHA256:
`7298eca8bbb04558887493a6af72267d2516fb2ac041aafc351daaff7e53c07b`.
Only this report was added after full validation; no production/test source,
resource, project setting or build configuration changed afterward.

All acceptance in this environment is native headless functional verification,
not rendered pixel/desktop acceptance. No art, scene, map, rule, save format or
product save behavior changes. This is an input-correctness repair, not an FPS
or performance claim.

The earlier intermittent MoraleFeedbackChecks shutdown signal diagnostic is not
claimed fixed by this change; its preserved failure and bounded unchanged-code
recheck remain separate evidence.
