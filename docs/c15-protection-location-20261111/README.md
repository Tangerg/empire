# C15 protected structure location

Base: `f022b802`. Real 960×640 chapter-entry review (immediate-stop branch)
showed dense forest and many similar props. The critical HUD warning named the
mother root but could not locate it; clicking the warning did nothing. Zooming
out still did not identify which object had to survive. This is a targeting aid,
not a claim that the mother root was initially outside the viewport.

The condition handler owns optional public spatial targets. The structure
condition resolves its canonical living structure by ID; absent/dead structures
have no target, while disabled or ownership-changed living structures retain
their position. Other conditions default to no positions: unit selectors, chance
and composed conditions are not inferred or evaluated to produce targets.
The failOn objective delegates this projection. HUD attaches its ordinary locate
intent to the active/public failure row, with matching failure-prefixed row key.
No UI structure type switch, authored coordinate copy, or gameplay rule change.

The existing hidden/inactive ancestor checks remain authoritative. Navigation
only moves the camera and paints a cell outline; it does not issue orders or
reveal fogged units. Existing structure inspection remains the source of HP.

## Verification notes

Core: 17 groups passed, including all 16 independent native AI save/replay levels;
all sixteen action/event/receipt trace hashes equal the C14 baseline. There is no
content or state-schema change. Disabling failOn's spatial delegation fails the
formal C15 assertion (exit 1).

Native HUD: 1320 assertions, including C15 active/public location, matching
failure row key, hidden/completed/inactive/failed condition and parent rejection.
The HUD snapshot test previously assumed every non-Action intent binds selection;
it now explicitly recognizes existing read-only FocusObjective as selection-independent.
Native camera hidden-target fixtures now map failure-row keys back to objective
IDs instead of treating a presentation row key as a canonical objective ID.

GUI at 960×640: locating the mother root frames its canonical marked cell near the
center, the failure warning stays readable on one line, repeated navigation is
stable, and ready actors 10 / remaining enemies 11 do not change. The scene used
an explicitly marked detached immediate-stop entry, not a continuous victory.
Manual structure dossier/HP access was not verified in this run.

Final additional native results follow below. No screenshots or private saves
are included. No full native aggregate or remote CI success is claimed.

Final camera integration: 436 assertions passed, including the C15 failure-row
keyboard locator, exact canonical marker, safe viewport, preserved selection and
state, repeated focus, busy/rebind cleanup and hidden ancestors. Objective/outcome
297 and catalog ownership boundary 76 passed. Strict process/diagnostic checks,
formatting, metadata, diff checks and zero-warning builds passed.
