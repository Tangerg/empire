# Idle instructions without an action tray — 2026-11-02

## Actual integration gap

At the actual C01 opening, cancelling selection left only the roster entry and global orders visible. The existing instruction explaining mouse selection, Space, arrow keys and Enter was entirely absent. The unchanged initial C01 projection has only `crown,objectives,roster`; it still supplies the canonical idle hint. `SupportOrders.Tactics` omits unaffordable tactics, so a main action section is not guaranteed. The view incorrectly made hint visibility conditional on that section.

This is not the previously fixed text-ellipsis issue. The earlier UI7 long-text test supplied a synthetic `tactics` section even for idle wording, so it never covered the missing-section branch. The new regression consumes the real initial C01 projection without removing sections or changing resources/rules. Baseline fails the intended **Opening guidance must remain visible...** assertion after confirming its exact section list.

## Small view-only repair

Nonempty projected instructions obey `ShowHint` regardless of action-section presence. With no main action/forecast, reuse the existing CommandTray material at its existing maximum four-slot width, fitted to the original 13px label's native shaped line height. No full-height empty action panel, new tutorial text, scrolling state, fabricated action, font reduction, or new asset was added. The same slot-limit constant also controls the action grid. Existing command and forecast paths retain their positions; Apply's normal ownership clears the short backing when real actions appear.

Actual images show the restored complete line at 1180×812 and 960×640, with space separating the roster entry, global End Turn/Undo and instruction strip. `selected-960.png` confirms that selecting Laiya replaces the short backing with the original order tray rather than stacking them. The capture stayed at C01 turn 1, with no movement, wait, attack or end turn. Window sizes were normal application launches, not a claim that desktop drag-resize succeeded.

## Verification and bounds

- Fresh final production build: 0 warnings/errors; official import, targeted format and indexed UID metadata checks.
- `IdleGuidanceChecks`: **26 assertions**. Actual C01 initial projection, both supported reviewed sizes, full native text visibility/material bounds/safe areas, longer existing recruitment text, action-tray replacement and return, explicit hidden/empty projections without an empty backing, and unchanged snapshot/revision/events with zero intents.
- `DossierFocusChecks`: **46**; `CommanderNameChecks`: **36**. Both remain isolated presentation/C01 checks; no C06/C07 action suites executed.
- Independent read-only review found no blocking owner/lifecycle issue. Reused the suggested single maximum-slot constant.
- `ShowHint` remains the existing configuration property: changing it then applying an *equivalent* projection still does not trigger a rerender. The hidden-hint test intentionally changes the projection and proves only the rerender contract; this batch does not claim a new reactive property. Production has no dynamic ShowHint setter.
- Short system-menu → journal → Escape → Return actual GUI review also retained safe focus and correct reading layout. Escape first releasing a focused native control, then cancelling map selection on the next press is an existing explicit input contract, not a defect changed here.
- The C06 six paused evidence/save hashes remain unchanged. Official C07, rules, saved battle schemas and 640 battle support policy are unchanged. This is not a new playthrough or a full HUD/campaign acceptance claim.
