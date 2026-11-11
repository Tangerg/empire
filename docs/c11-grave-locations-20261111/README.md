# C11 grave-reading locations

Base: `34b38cc0`. At 960×640 the C11 chapter-entry fixture showed many decorative
graves but no way to distinguish the three actual reading cells. Its score task
was plain text; clicking it did not locate anything. The briefing said to read
three names, but did not explain that completing an action at the location is the
canonical trigger.

## Change and ownership

C11's score objective now declares `targetZones` referencing `name-a`, `name-b`
and `name-c`. The score handler validates zone references and projects canonical
coordinates through the existing ObjectiveHandler.TargetCells API. Neither the
HUD nor content copies coordinates or infers trigger relationships. Scenario still
owns afterAction conditions, score grants, signals and once-only trigger state.

The objective text now says to arrive at the three graves and complete an action.
The existing locator shows all three authored locations until the score is
completed, **not an unread-grave checklist**. No gameplay action or rule changed.
The new author declaration is serialized with the objective; the C11 state digest
therefore intentionally changes, without weakening full-field digest validation.

## Manual witness

In the unchanged 960×640 production UI, the new two-line task button stays inside
the task panel. Clicking it centers the camera and exposes all three gold target
outlines. Ready actors remain 10 and names remain 0/3. The game was closed after
verification. This is a detached, production-loader-validated chapter entry, not
continuous campaign progress or a C11 victory. No binary screenshots or saves are
included.

## Automated verification

Core checks cover default non-spatial scores, canonical zone updates, duplicates,
immutable projections, unknown-zone admission, score completion, and legal
move-and-wait/leave/revisit behavior in an isolated near-grave fixture. Every
movement/turn event and complete state matches the no-locator baseline after
removing only the new score targetZones declaration; a grave grants its name once.

Full shipped-level replay additionally runs the locator-bearing C11 alongside its
no-declaration baseline using the same AI actions, comparing all events and all
state fields except that declaration after every action. Other levels retain their
usual save/replay and full-field digest checks.

CameraIntegration includes C11: keyboard activation, exact target projection,
selection/state preservation, repeated activation, refresh focus, busy state,
rebind, and hidden parent/leaf boundaries. Results: build 0 warnings/errors; core 17 groups including all 16 shipped level
replays pass; CameraIntegration 385 assertions; objective/outcome 297 assertions;
catalog boundary 76 assertions; source metadata and formatting pass. Returning
no score target cells deliberately fails the exact three-location assertion;
restoring the implementation passes the selected core suite again. Existing
native mouse-exit warnings are recorded, with no ERROR/FAIL in the positive gate.

Against the objective-failure baseline, 15 action/event/receipt SHA256 traces are
identical. Only C11 differs because its serialized objective gains targetZones;
its side-by-side no-declaration replay proves identical events and otherwise
identical state after every action. No digest fields were excluded from production
hashing. The full native aggregate and remote CI were not run.
