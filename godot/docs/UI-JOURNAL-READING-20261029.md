# Journal reading checkpoint — 2026-10-29

## Product changes

The real short journal previously occupied the same 420px-height region as a long
journal. It now measures native wrapped content and stays compact for short receipts,
while long history remains bounded. The existing journal opens with reading focus,
shows a restrained keyboard hint and visible focus outline, and returns focus to its
visible top-level entry when closed. From the system submenu it deliberately returns
to this visible entry, not the now-hidden submenu button.

RichTextLabel already supports Home/End, arrows and PgUp/PgDn, and selection already
enables focus. No duplicate key handler was added; see the [Godot 4.6.3 implementation](https://raw.githubusercontent.com/godotengine/godot/4.6.3-stable/scene/gui/rich_text_label.cpp).

A native-input regression reproduced a real bug at both 960 and 1180 widths:
replacing Text on every new receipt reset follow mode and pulled historical reading
to the end. Append now samples whether the reader was at the end before refreshing
text. Readers at the end continue following. Readers above it retain their anchor.
When the 60-run bounded journal evicts its first run, the exact native paragraph
height (including wrapping and embedded line breaks) is subtracted, not a fixed
line estimate. If the anchored oldest record is itself evicted, the position clamps
to the new first retained record. The existing bounded/compressed journal stays the
single content owner; no extra event log or gameplay cache is introduced.

The hint and prose are siblings in one VBox content region. AcceptDialog owns that
region's geometry, so independently anchored direct children would overlap. A native
layout assertion explicitly prevents the hint from covering any receipt or close
control. Deferred focus restoration uses the binding captured at opening; rebind or
unmount cannot revive an old focus request.

## Evidence and boundaries

Evidence prefix: `empire-c01-evidence-recovery/ui5-`.

- `ui5-before-reading.log`: original implementation fails actual Home/PgDn/append
  preservation checks, not a synthetic source-string assertion.
- Final affected checks: Campaign UI **179**, Camera integration **277**, Navigation
  **298**, Result flow **154**. UI includes 640×360, 960×640, 1180×760; unequal wrapped
  receipt heights, anchored oldest eviction, end-following, stale-binding focus,
  and unchanged battle revision.
- `ui5-after/journal-short-960.jpg`: real retained manual-save load receipt.
- `journal-reading-before-append-960.jpg` and `journal-reading-after-append-960.jpg`:
  actual GUI Home/PgDn then F6 append while inspecting the clearly marked synthetic
  journal fixture. Receipt 06 remains at the same visible reading position after
  oldest-run eviction.
- `journal-follow-latest-960.jpg`: native End then append shows receipt 62.
- `journal-return-focus-960.jpg`: Tab to Close, Enter closes and restores entry focus.
- `ui5-save-check.log`: both manual and automatic save bytes remain unchanged. No
  turn, move, attack, chapter or real outcome was executed.

The optional `NativeCapture --journal-ui-fixture` only injects labelled presentation
receipts (F7 fills, F6 appends, including inside the native journal Window). It neither
changes canonical events nor writes saves. The marker and window title explicitly
identify synthetic records; these images are not real battle-history evidence.

## Existing busy-state assertion alignment

The expanded camera gate found an old screen-only expectation that a busy Save click
still forwards its callback. Both production application owners already reject busy
save/load. The previous UI checkpoint now visibly disables those actions, so the old
presentation assertion is aligned to that existing application contract: no callback
is dispatched while busy. The application guards, unchanged-save/revision checks,
and positive restoration after the timeline settles remain in place. This is not a
new save rule or a removed failure check.

Negative controls disable eviction compensation and force a 420px native minimum
height for short journals. A requested-height-only mutant was not observable in the
headless layout (native sizing could reduce it), so it is not claimed as a killed
mutant. The final size negative control changes the actual minimum constraint. Both final
negative controls were killed.
The detached fixture explicitly sizes its unparented Control and asserts all three
content dimensions; Window.Size alone is not evidence of Control resizing.
Source is restored before the final build. Native titlebar/material consistency is
the next separately approved batch, not part of this checkpoint.
