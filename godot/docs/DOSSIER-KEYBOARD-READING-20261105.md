# Continuous dossier keyboard reading — 2026-11-05

## Actual player defect

At baseline `dea926c90ffca7f9678e496e50265d643821a79b`, a native keyboard user could open Laiya's dossier in the genuinely carried C08 opening and expand its unit details, but could not read all the text. The plain `ScrollContainer` only followed focusable section/action controls. At 960×640, Tab jumped from offset 0 to offset 352; the complete armor and movement-type rows fell between those reachable windows. PageDown, Home and End on the disclosure did not provide continuous reading. The observation used the actual application, native F9/Space/Tab/Enter and unchanged campaign saves, without injecting battle outcomes or adjusting units.

The newly added regression fails against the old plain-scroll implementation at its intended first continuous-reading focus assertion. The first version of the subsequent real-session test mistakenly projected hover rather than a pinned unit; that harness error was corrected separately, without treating it as a gameplay defect.

## Ownership and implementation

- Rename the already shared `CampaignScroll` to `ReadingScroll`, preserving its Godot source UID. Campaign prose, menu, results and dossier now use the same native focusable reading surface. Arrow/Page/Home/End input is handled only when the scroll itself owns focus; child buttons keep their own keyboard behavior.
- The session's localized immutable HUD projection supplies the reading instructions. The scroll renders them in a separate sibling surface, outside the prose. Instructions appear only when content needs scrolling, including near-threshold combinations of expanded disclosures.
- The shared parent VBox owns the available space. Hint visibility compares content height against that space minus other visible siblings and their gaps; it does not infer available space from the current hint visibility or a changing scrollbar page. An intermediate deferred-refresh prototype produced a reproducible message-queue loop and failed native exit; it was removed, not masked with a larger queue or weaker diagnostic gate.
- The scrollbar track theme owns its minimum width. The first aggregate caught a 4-pixel overlap: content reserved the tactical track's native 6-pixel minimum while `CustomMinimumSize` enlarged the bar to 10. Copying the current track style and increasing its effective horizontal content margins provides the same minimum to native layout and rendering, without changing its colors or mutating a shared theme resource. The real clipping assertion was preserved; continuous reading now also excludes the scrollbar rectangle.
- The dossier window owns its 360×420 reading viewport. `WrapControls=false` prevents wrapped text from growing the popup beyond the supported battlefield window; the scroll expands into the remaining space. The old 300-pixel content minimum no longer competes with the hint for popup height.
- Refresh restores either a previously visible child focus or a deliberate reading offset. The previous sequence wrote the old offset before consulting native child coordinates, which remain stale until layout. Instrumented evidence showed the shared component had correctly scrolled to 686, then the HUD overwrote it with 426 and failed to reveal the now-hidden action. The two restoration intents are now mutually exclusive. Focus ownership, generation/lifetime guards and the manual offscreen-reading contract remain intact.

No battle rules, command transitions, maps, artwork, campaign progress or save schema change.

## Verification

Validated production source: `b38ad78555f39fa5592d1ca7f6dd1606c61823af`.

- Actual carried C08 native route, 960×640 and 1180×760: all 21 Laiya detail rows fully reachable through keyboard reading; PageDown/Home/End and disclosure navigation work; nine actual expansion/collapse transitions remain stable and show instructions whenever content overflows; popup and close control remain in bounds. 121 checks, strict native exit 0, 2026-11-06 00:11:59–00:12:20 UTC. The reading proof excludes the actual scrollbar width. Complete normalized application state is unchanged by reading.
- `DossierFocusChecks`: 143 assertions, strict exit 0. Covers actual opening-session details, continuous reading, updated hint projection, boundary layouts, action focus after growing text, deliberate offscreen reading, resize, real stance/facing commands, close/reopen, revision/binding changes and teardown.
- `CampaignUiFlowChecks`: 209 assertions, strict exit 0, including long/short menu and result reading, action visibility, native navigation and lifecycle boundaries.
- `TacticalHudChecks`: 1163 assertions, strict exit 0. Its eight existing mouse-enter notification warnings remain visible in the log.
- `MoraleFeedbackChecks`: 74 assertions, strict exit 0 after the track-width repair, including real C05 civilian/commander dossier clipping at 1280×720 and 960×640.
- The first aggregate on `326b664da` stopped at the preserved morale-clipping assertion, exit 1, after 22 successful native processes. Its 16 core groups and all 3425 legal actions had exact baseline traces and terminal state hashes, but the failed aggregate is not counted as acceptance. The corrected candidate received the new full run below.
- `bash tools/godot/verify-native.sh`: one uninterrupted run, 2026-11-06 00:14:06–01:05:04 UTC, **3058 seconds, exit 0**. Format/source/map checks, main/core builds, all 16 core groups, objective/catalog contracts, editor import and 31 native scenes pass. All 32 native process diagnostic boundaries pass; 40 existing warning headers exactly match the preceding successful gate, with no additions.
- All 16 formal levels' 3425 legal actions preserve the ordered action/event/receipt SHA-256 traces and complete terminal state hashes. These are independent replay fixtures, not newly played carried campaign chapters.
- The aggregate's real continuous route retains normal AI defeat and Retry with both save slots unchanged, first victory after 156 legal orders, second escort victory after 259, then 21 legal orders in the third opening. Casualties, growth, resources, outcome/state digests, single result views and carried progress match the baseline. No victory or state was injected.
- `dotnet build godot/Empire.csproj -c ExportRelease --disable-build-servers -m:1 -p:UseSharedCompilation=false`: exit 0, zero warnings/errors. This is compilation, not a packaged export.
- Independent source review and `git diff --check` found no blocking defects. Temporary diagnostic printing was removed; failure messages retain useful geometry.

## Limits and guidance

These are native headless input, layout, state and shutdown checks, not pixel-level visual acceptance or screen-reader validation. No new C09 campaign play was performed. Previously unexplained intermittent shutdown diagnostics remain unresolved and are distinct from the rejected layout-loop prototype. Packaged export acceptance is separate.

Relevant Godot guidance:

- [Keyboard/controller UI navigation](https://docs.godotengine.org/en/stable/tutorials/ui/gui_navigation.html): expose deliberate focus stops and preserve child control navigation.
- [Control input](https://docs.godotengine.org/en/stable/classes/class_control.html): use focused `_GuiInput` and accept handled events rather than dispatching gameplay actions.
- [Window sizing](https://docs.godotengine.org/en/stable/classes/class_window.html): `wrap_controls` allows content minimums to resize a window; the dossier instead explicitly owns its viewport.

The track sizing follows [Godot 4.6 ScrollBar minimum-size ownership](https://github.com/godotengine/godot/blob/4.6-stable/scene/gui/scroll_bar.cpp#L486-L512); effective style margins use `StyleBox.GetMargin`, including the documented negative-margin fallback.
