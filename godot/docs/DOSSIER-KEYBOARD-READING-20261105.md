# Continuous dossier keyboard reading — 2026-11-05

## Actual player defect

At baseline `dea926c90ffca7f9678e496e50265d643821a79b`, a native keyboard user could open Laiya's dossier in the genuinely carried C08 opening and expand its unit details, but could not read all the text. The plain `ScrollContainer` only followed focusable section/action controls. At 960×640, Tab jumped from offset 0 to offset 352; the complete armor and movement-type rows fell between those reachable windows. PageDown, Home and End on the disclosure did not provide continuous reading. The observation used the actual application, native F9/Space/Tab/Enter and unchanged campaign saves, without injecting battle outcomes or adjusting units.

The newly added regression fails against the old plain-scroll implementation at its intended first continuous-reading focus assertion. The first version of the subsequent real-session test mistakenly projected hover rather than a pinned unit; that harness error was corrected separately, without treating it as a gameplay defect.

## Ownership and implementation

- Rename the already shared `CampaignScroll` to `ReadingScroll`, preserving its Godot source UID. Campaign prose, menu, results and dossier now use the same native focusable reading surface. Arrow/Page/Home/End input is handled only when the scroll itself owns focus; child buttons keep their own keyboard behavior.
- The session's localized immutable HUD projection supplies the reading instructions. The scroll renders them in a separate sibling surface, outside the prose. Instructions appear only when content needs scrolling, including near-threshold combinations of expanded disclosures.
- The shared parent VBox owns the available space. Hint visibility compares content height against that space minus other visible siblings and their gaps; it does not infer available space from the current hint visibility or a changing scrollbar page. An intermediate deferred-refresh prototype produced a reproducible message-queue loop and failed native exit; it was removed, not masked with a larger queue or weaker diagnostic gate.
- The dossier window owns its 360×420 reading viewport. `WrapControls=false` prevents wrapped text from growing the popup beyond the supported battlefield window; the scroll expands into the remaining space. The old 300-pixel content minimum no longer competes with the hint for popup height.
- Refresh restores either a previously visible child focus or a deliberate reading offset. The previous sequence wrote the old offset before consulting native child coordinates, which remain stale until layout. Instrumented evidence showed the shared component had correctly scrolled to 686, then the HUD overwrote it with 426 and failed to reveal the now-hidden action. The two restoration intents are now mutually exclusive. Focus ownership, generation/lifetime guards and the manual offscreen-reading contract remain intact.

No battle rules, command transitions, maps, artwork, campaign progress or save schema change.

## Verification

Before the aggregate gate:

- Actual carried C08 native route, 960×640 and 1180×760: all 21 Laiya detail rows fully reachable through keyboard reading; PageDown/Home/End and disclosure navigation work; nine actual expansion/collapse transitions remain stable and show instructions whenever content overflows; popup and close control remain in bounds. 119 checks, strict native exit 0, 2026-11-05 23:39:46–23:40:06 UTC. Complete normalized application state is unchanged by reading.
- `DossierFocusChecks`: 142 assertions, strict exit 0. Covers actual opening-session details, continuous reading, updated hint projection, boundary layouts, action focus after growing text, deliberate offscreen reading, resize, real stance/facing commands, close/reopen, revision/binding changes and teardown.
- `CampaignUiFlowChecks`: 209 assertions, strict exit 0, including long/short menu and result reading, action visibility, native navigation and lifecycle boundaries.
- `TacticalHudChecks`: 1163 assertions, strict exit 0. Its eight existing mouse-enter notification warnings remain visible in the log.
- Independent source review and `git diff --check` found no blocking defects. Full repository gate is pending at this checkpoint; no publication is claimed here.

## Limits and guidance

These are native headless input, layout, state and shutdown checks, not pixel-level visual acceptance or screen-reader validation. No new C09 campaign play was performed. Previously unexplained intermittent shutdown diagnostics remain unresolved and are distinct from the rejected layout-loop prototype. Packaged export acceptance is separate.

Relevant Godot guidance:

- [Keyboard/controller UI navigation](https://docs.godotengine.org/en/stable/tutorials/ui/gui_navigation.html): expose deliberate focus stops and preserve child control navigation.
- [Control input](https://docs.godotengine.org/en/stable/classes/class_control.html): use focused `_GuiInput` and accept handled events rather than dispatching gameplay actions.
- [Window sizing](https://docs.godotengine.org/en/stable/classes/class_window.html): `wrap_controls` allows content minimums to resize a window; the dossier instead explicitly owns its viewport.
