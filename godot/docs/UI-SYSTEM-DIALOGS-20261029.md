# System and confirmation UI checkpoint — 2026-10-29

## Scope and observed problems

This continues the approved UI direction from `8bb0770f`, without adding settings,
inventory features, chapters, art assets or battle rules. Native inspection covered
the real system menu, restart confirmation and main campaign replacement flow.
A detached, visibly labelled fixture covers the non-battle storage-failure notice;
no real save permission or contents were deliberately damaged.

- System actions looked available while existing application guards would silently
  refuse them. Save/load could first dismiss the menu, making the refusal look like
  a missing response.
- Root-owned non-battle notices did not inherit the campaign theme and did not set a
  localized close action; Escape could leave a hidden notice attached to the root.
- Campaign replacement did not explicitly choose the safe initial focus, unlike
  battle restart. The two confirmations also had inconsistent sizing.

## Changes

- Existing mutating system actions use the same live busy predicate as dispatch,
  with disabled visuals and a localized explanation. Journal remains available.
  Returning idle restores the actions and exposes existing F5/F9 shortcuts through
  tooltips. Callback admission remains guarded, including deferred/stale callbacks.
- Opening System focuses its visible Cancel/Esc control.
- Shared native dialog configuration supplies campaign materials, wrapped prose,
  104×40 minimum action targets, and a bounded 440px reading width. Destructive
  dialogs initially focus Cancel. Root notices use Chinese Close and retire on
  Escape/cancel as well as explicit close.
- Button minimum size is set through native dialog theme constants, not child size
  overrides that AcceptDialog overwrites. See the [Godot AcceptDialog contract](https://docs.godotengine.org/en/4.5/classes/class_acceptdialog.html).

## Verification

Affected checks pass: Campaign UI 143, Navigation 298, Result flow 154,
and BattleScreen real-session integration. Final logs use `ui4-verified-*` under
`empire-c01-evidence-recovery`; `ui4-final-*` are an earlier build's checks, not the
acceptance evidence. Compilation has zero warnings/errors. No full chapter replay
or fresh victory claim is made.

Actual native screenshots:

- `ui4-before/system-1180.jpg`, `restart-1180.jpg`.
- `ui4-after/system-1180.jpg`, `restart-1180.jpg`, `overwrite-1180.jpg`.
- `ui4-after/notice-640.jpg` is explicitly a UI fixture.

Actual Enter on the focused Cancel in campaign replacement returned to the same
menu; automatic and manual saves remained byte-identical (`ui4-save-cancel-check.log`).
Battle restart likewise cancelled via Enter without advancing the turn. Windows
were closed normally. The notice fixture confirms readable text and a visible,
separate close action in a 640×480 window; native tests also cover 640×360.

Three negative controls were killed: busy controls left enabled, native dialog
minimum-width removed, and unsafe destructive initial focus. Original source was
restored byte-for-byte and rebuilt. Formatting, 305 script UIDs and all 1196
localization messages pass their source guards. Short/long journal reading is the next separate bounded
pass: current actual short journal leaves excessive blank space, and keyboard
reading needs direct native validation rather than assuming default behavior.
