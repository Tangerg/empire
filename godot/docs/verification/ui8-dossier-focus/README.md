# Dossier keyboard visibility — 2026-11-02

## Proven defect and minimal repair

In the actual C01 turn-1 application, select Laiya, open **兵种与能力**, expand the ability description, then press Tab twice. Before the fix, focus moved to **阵形** below the visible scroll area; only manually scrolling revealed its gold outline. `before-tab-hidden-1180.png` and `before-scrolled-bottom-1180.png` preserve that failure. `after-tab-visible-1180.png` shows the same 1180×812 window/character: the focused disclosure is now automatically visible. No font, artwork, window layout, rules, or gameplay action changed.

The ScrollContainer now enables native `FollowFocus`. Existing projection refresh preserved the old scroll offset *after* restoring focus, which could undo native following when preceding content grew. A local snapshot records whether the old focus was fully visible; after the existing guarded deferred offset restoration, `EnsureControlVisible` preserves that visibility. If the user had deliberately scrolled away from the focused control, the chosen reading offset remains unchanged. There is no new persistent focus/scroll state or custom scrolling implementation.

Godot 4.6 native contract: https://docs.godotengine.org/en/4.6/classes/class_scrollcontainer.html (`follow_focus`, `ensure_control_visible`). Generation, live-instance, and mounted-tree guards remain in the existing owner.

## Verification

- Fresh production build: 0 warnings/errors; targeted dotnet format and source metadata checks.
- `res://Scenes/DossierFocusChecks.tscn`: **46 assertions**, presentation projections only, **zero gameplay intents**, no battle state/engine/action dispatcher. Tests actual Tab/Shift+Tab input, long preceding-content refresh, intentional off-focus reading, 960×640 and 1180×760, in-process resize, close/reopen/native hide, and unload with a queued refresh callback.
- Original runtime negative control fails exactly `Tab beyond long description: focused control must remain inside...` (`baseline-focus.log`). FollowFocus-only repair fails exactly `Refresh with longer preceding content...` (`follow-focus.log`). Both reached the intended behavior assertion, not fixture initialization.
- Real application C01 T1 at 960×640: Tab scrolls to the next disclosure, Shift+Tab returns to the ability header; Escape closes and returns to the opener, Return reopens safely on Close; clicking visible Close also returns to the opener. Images preserve those states. At 1180×812, two Tabs reveal the formations disclosure with its full gold focus outline.
- The two real window sizes were obtained by normal application restart. Attempted window-manager dragging did **not** resize this desktop window; no real-drag success is claimed. In-process resize is covered by the presentation check, not a substitute claim about the desktop window manager.
- A preliminary headless `Popup.PushInput(Escape)` check did not simulate the native window-manager dismissal path. It was withdrawn, not treated as a production defect; automated native-hide coverage and the real GUI Escape check are reported separately.
- Independent read-only review found no blocking ownership/lifecycle issue. Suggested clearer fixture label and queued-unload check were incorporated before the final run.

## Boundaries

This is a keyboard-reading bug fix, not a new campaign/playthrough result. Actual GUI inspection stayed at C01 turn 1 with no move, attack, wait, or end-turn. The six paused C06 evidence/save files retain their recorded SHA-256 values (private recovery evidence); neither C06 nor C07 action progression was run. The aggregate `TacticalHudChecks` was intentionally not executed: its primary-order checks dispatch actions in C01–C07. The new focused suite isolates this presentation contract instead. Official C07 scene and the unselected high-cost contact-band study remain unchanged. Long-content refresh uses clearly synthetic presentation rows; screenshots use the actual C01 character data.
