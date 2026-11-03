# Preserve save/load shortcut boundaries — 2026-11-03

## Confirmed failure

The battle adapter already rejected Ctrl, Alt and Meta chords, but the application
adapter accepted any fresh F5/F9 key event. Thus a modified key could still reach
manual Save or Load after the battle adapter correctly ignored it.

Two independent native campaign-navigation cases reproduced the damage in fresh
isolated storage. Each opened the actual C01 battle, saved its opening A through
ordinary native F5, and committed a real move to obtain unsaved state B:

- Ctrl+F5 sent through the root viewport overwrote the existing manual slot.
- Ctrl+F9 sent through the root viewport replaced the current campaign/session
  and discarded the unsaved move by restoring A.

The original-code run failed exactly these two groups. Its other fifteen
navigation groups passed. This is application input leakage, not a change to the
manual slot, Continue behavior, campaign autosave or load-confirmation policy.

## Repair

`GrayFlagGame._UnhandledKeyInput` now rejects Ctrl/Alt/Meta-modified events before
routing F5/F9. Its existing fresh-key, mounted-node and downstream busy guards
remain intact. Shift behavior remains unchanged, matching the existing battle
shortcut policy.

No additional state, timer or duplicate save/load implementation is introduced.
Ordinary F5/F9 and the visible Save/Load buttons keep the same behavior. No map,
artwork, authored campaign content or saved-document schema changes.

## Regression coverage

Both new groups in `CampaignNavigationChecks` verify:

- The initial native F5 really saved the exact opening battle, not a stale or
  campaign-only fixture.
- A real move produces a different revision/digest and an available undo step.
- All seven nonempty Ctrl/Alt/Meta combinations through both the root viewport
  and application input entry preserve campaign/session/view identity, state,
  revision, undo availability and displayed messages.
- Those chords leave both manual and automatic save bytes and timestamps intact.
- Key release and key-repeat events remain inert.
- Ordinary F5 still saves B and ordinary F9 can restore that exact saved state;
  the independent Load case restores A. Disabling all shortcuts cannot pass.
- Each fixture restores the suite's original temporary manual slot in `finally`;
  the outer navigation suite verifies that slot again.

The native test controls use a real application and session. Movement animation
is disabled in these shortcut fixtures to inspect the settled input boundary;
the separate AI pacing integration exercises actual computer actions and busy
save/load rejection. These are headless native-event checks, not desktop
keyboard or window-manager acceptance.

## Verification boundaries

Completed verification:

- Production build: zero warnings and errors.
- Campaign navigation: all 17 groups passed, including both new native save/load
  flows and the existing actual AI cycles (7,110 assertions in this run;
  frame-sampling assertions vary with runtime).
- AI pacing integration: 20 assertions passed, including busy Save/Load rejection.
- Campaign UI flows: 205 assertions passed.
- All three native processes exited zero without error, leaked-resource,
  segmentation-fault or unhandled-exception diagnostics. Expected injected
  storage/refusal and source-readiness warnings remain visible.
- Changed-source whitespace verification and `git diff --check`: passed.
- Source metadata: 154 unique script UIDs and 14 positive/negative cases passed.
- Independent read-only review: no blockers.
- Accepted C07 scene and selected assembly hashes are unchanged.

The original-code failures are negative evidence, not accepted runs. The full
native format gate still reports the existing failures in 184 untouched files,
with neither changed source among them. This focused repair does not claim the
full native aggregate, TypeScript suite, GUI session or exported package passed.
