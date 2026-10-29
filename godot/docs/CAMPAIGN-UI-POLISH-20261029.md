# Campaign UI consistency checkpoint — 2026-10-29

## Scope and reproduced defects

This follows the tactical HUD checkpoint. Existing approved illustrations, materials,
portraits and campaign/battle rules are unchanged. No C01 replay or new chapter is
claimed. `CampaignUiPreview.tscn` is an opt-in detached visual fixture: its title and
window explicitly identify QA, callbacks only print a receipt, and it has no engine,
save service or campaign transitions. F1/F2/F3/F4 select menu/staging/summary/completion; F5 opens an existing story
with detached callbacks (its text is source content, not a new story).

The actual 960×640 long-summary baseline focused Continue below the viewport, with
no visible action on screen. The old UI test called `EnsureControlVisible` itself,
which verified a scrollable container rather than the player's focus path. That
helper has been removed from action-reachability acceptance.

At 640×480, the fixed two-column staging hero squeezed its title into an extra
single-character line and cropped its briefing illustration to a nearly square
slice; the roster/status region followed below that tall header. The narrow layout
now prioritizes mission text, follows it with a compact illustration, and stacks
the roster/status panels and action note/button. Wide windows
return to columns, without replacing art or changing roster facts.

## Ownership and interaction

- Summary Continue and completion Settle/Retry/Camp live in a fixed action rail,
  outside scrollable long prose. The existing one-shot/generation guards remain.
  Initial summary reading stays at the top rather than jumping past casualties.
- Result metric labels wrap; numeric values retain their intrinsic width.
- Campaign/menu/result reading shares `CampaignScroll`: native Tab can focus the
  reading region, arrows/page keys/Home/End scroll it, and a restrained gold outline
  identifies reading focus. Keys are handled only when the scroll itself owns focus;
  child action controls retain their own behavior. Focused child buttons follow into
  view. No timer or forced per-refresh scroll is added.
- Local narrative beat updates must still preserve scroll position, and new
  narrative bindings must still start at the top. These existing contracts are
  included in the affected integration gate. The narrative primary action and
  beat indicators now use the same fixed footer boundary, so restoring their
  focus cannot drag the reading region to an unrelated offset.
- Staging resource amounts align to the edge instead of taking an equal expanding
  text column. Numeric amounts explicitly keep intrinsic unwrapped width (a
  narrow-window GUI check caught and corrected digit-by-digit wrapping during
  development). The footer remains independent of a long roster.

## Verification and evidence

The focused native gate passed: narrative 3306 assertions (35 documents / 91 beats /
11 choices), result flow 154, campaign navigation 289, staging 155, menu 105 and
campaign UI 122. All six processes exited 0 without error/leak teardown diagnostics.
Build finished with zero warnings/errors; formatter and 305-script UID metadata
checks passed. Evidence is kept
outside the repository in `empire-c01-evidence-recovery`; final logs use `ui2-final-`.
The visual baseline is `ui2-before/result-960.jpg` and `ui2-before/staging-640.jpg`.
These are original CUA window screenshots, not composited or browser captures.
After images in `ui2-after/` include `result-960.jpg`, `result-end-960.jpg`,
`staging-640.jpg`, `staging-resources-640.jpg`, `completion-640.jpg`, `menu-640.jpg` and `story-beat-640.jpg`.
Real mouse-wheel scrolling and keyboard reading both worked. Tab/End reading reached the long summary's final line while its action stayed
fixed. Menu Tab kept the focused action visible. The existing story's next beat
retained reading position and a visible primary action. No fixture callback was
invoked except local story beat advance; no campaign outcome or save was created.

Two deliberate negative controls were rejected: returning Proceed to the long
scrolling result body, and disabling focused keyboard reading. Working source was
restored byte-for-byte before the final build. The result-body mutant is rejected
by fixed-action/reading-start contracts even though focus-follow can make its
initial button visible; this distinction prevents a superficial fix from passing.

## Boundaries

The fixture's example turns, casualties, signals and resources are synthetic UI
facts, never actual campaign outcomes. Real settlement/one-shot persistence remains
covered separately by native result/navigation tests. Linux cloud-window evidence
does not constitute macOS/Windows or audio acceptance. Publication still depends on
restored GitHub write access; local checkpoints and private bundles are not a push.
