# HUD readability audit — 2026-10-29

## Scope and finding

Audit of the existing battle HUD at 1180×760 and 960×640, plus an exploratory
640×480 boundary check. The existing approved layout, artwork and font sizes
remain unchanged. No simulation rules, chapters, global minimum-window policy,
new help feature or new campaign completion were added.

The idle instruction already had a complete native tooltip. Real hovering verified
that tooltip, so ellipsis alone was not the defect. However its Label was not in
the keyboard focus chain, leaving keyboard-only readers without the clipped
instructions. The existing unit-selection test covered only a short instruction.

Non-forecast instructions now wrap completely at the original 13px size. Their
native shaped line heights and theme spacing determine the needed height; only
the existing action tray and its commands move upward by the added text height.
Wrapped text retains an additional bottom inset rather than touching the frame.
Command hit targets, unavailable states, the selected-unit card, forecast layout
and global End Turn position are unchanged. The tooltip remains available.

The native Label minimum size is only `(1, 1)` when ellipsis is enabled and is not
a usable full-text measurement. Regression checks use actual shaped line counts,
visible line counts, material bounds and command rectangles, not a guessed number
of Chinese characters or a reduced font size.

## Actual GUI observations

Evidence root: `empire-c01-evidence-recovery/`.

- `ui7-before/idle-1180.jpg` / `ui7-after/idle-1180.jpg`: same retained QA save and
  viewport; complete keyboard instructions are now visible without hovering.
- `ui7-before/selected-960.jpg`: existing selected actor card and short command
  instruction were already readable. No redesign was necessary.
- `ui7-after/idle-focus-960.jpg`: full instruction beside End Turn/disabled Undo,
  with keyboard navigation exercised; no global order was executed.
- `ui7-after/target-preview-{1180,960}.jpg`: actual border archer targeting the
  opposing legion shield, showing 10 predicted damage, unchanged 78 HP attacker,
  and defender 120→110. Prediction only; no attack was executed.
- At 960, the stationary objective paper can cover a northern map actor after
  resizing. Existing camera drag moved that actor into view; hovering restored
  prediction without executing an action. The HUD does not need a new overlay
  scheme for that normal camera operation.
- `ui7-after/tactic-target-960.jpg`: actual available rally tactic enters target
  selection; its complete two-line instruction and Cancel remain readable.
  Unavailable projected buttons retain their native disabled state and activation
  guards in automated tests. This QA save did not provide an unavailable tactic;
  we did not alter resources/rules merely to manufacture that visual state.
- Both QA save files remained byte-identical. No end-turn, attack, tactic cast or
  save command was performed during this visual audit.

## Known small-window boundary — not passed

`ui7-before/battle-boundary-640.jpg` shows End Turn/Undo overlapping the tactical
command tray at 640×480. Campaign menus support that smaller size, but the battle
HUD had only 960/1180 layout coverage. The practical reviewed battle baseline is
**960×640**; 640 battle support is not claimed. No global minimum window limit was
introduced, because that would regress the already usable small campaign screens.
A future product decision can choose per-battle sizing guidance or a genuinely
responsive small battle layout; neither is silently included in this bounded fix.

## Concentrated validation

- Tactical HUD: **248 assertions**. Added all five long instruction variants at
  960 and 1180; full text, bounded bottom tray, non-overlap and 54px command targets,
  including an unavailable projected command. Existing focus/badge/forecast and
  stale-callback checks remain active.
- Campaign UI: **200**; Camera integration: **277**; real-session BattleScreen
  integration passed. These include the existing busy/idle and input contracts.
- Reinstating one-line truncation is killed by the new idle-instruction assertion:
  `ui7-negative-truncated-hint.log`.
- Build: zero warnings/errors. Formatting, 305 script UID metadata and save-byte
  checks passed. The HUD input harness still emits existing duplicate
  NotifyMouseEntered warnings; no engine errors occurred in the final HUD run.

GitHub remains blocked by the previously reported access environment. This batch
is committed locally and added to the same persistent backup, not claimed pushed.
