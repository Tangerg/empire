# Native tactical HUD — 2026-10-26

## Intent

The painted battlefield remains the dominant surface. The battle UI is a compact,
quiet military command rail, not a dashboard: charcoal/sage surfaces, warm ivory
text, restrained brass emphasis, and original uncropped actor art.

- The rail scales from 300 to 390 logical pixels with the window. Chinese objective
  descriptions wrap; progress is a separate line, never a truncated suffix.
- Mission context comes first, followed by the current forecast and commands.
  Damage, counterattack, predicted HP and death warnings precede modifier details.
- A small aspect-fit portrait sits beside identity, action state and HP. Unit
  capabilities, reaction/facing, formations, terrain and forces use disclosures.
- The current instruction and turn controls remain outside the scrolling rail.
  End turn is distinguished from Undo. Save/load/restart/camp are in a separate
  native system popup; restart retains its confirmation dialog.
- Tab/Shift+Tab traverse native controls. Focused controls own Enter/Space/arrows;
  Escape returns focus to the board. Board input clears stale button focus.

## Interaction contract

Selecting a destination is a preview, including the current cell and a cell whose
only command is Wait. Only choosing an order commits. Cycling available units
also focuses the canonical unit, so Enter cannot reuse a previous cursor. Invalid
movement/targets preserve the current selection and explain the refusal. The
controller still owns all selection transitions; the session and engine own
legality, outcomes and battle state. No campaign rules or save schemas changed.

## C07 scope

Only isolated copies of the already-played C07 saves were used for native visual
acceptance. No C08 continuation. The original completed T13 campaign save is
unchanged. Original T11 `native-015.png` already shows 1/2, and the T11 save records
`granary=completed`; independent reload also shows 1/2. There is no evidence for a
persistent objective-count defect. The previously observed instantaneous 0/2 is
not sufficient evidence to change victory rules.

## Verification

The focused native scene gates cover HUD layout at 300/390 widths, long wrapping
objectives, forecast priority, disclosure/focus persistence, actual session
commands, refusal behavior, stale callbacks, camera/picking and keyboard parity.
Real native screenshots cover C07 inspection, action preview, combat forecast,
system menu and a narrower window. Evidence and a git patch are retained separately
in Library; source artwork is not regenerated or recolored.
