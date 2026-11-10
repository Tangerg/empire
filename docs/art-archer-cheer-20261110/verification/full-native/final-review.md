# Independent final aggregate review

## Verdict

PASS for the original native aggregate and its declared headless scope.
Independently compared archived full-native.txt byte-for-byte with the original
QA/verification/full-native.txt. They are identical, SHA256
727765291d562311f4f65bbcbaf0f7267a77b3aaf548aca89aa3bc46f2cc0df3.
The real archived exit is 0, finish record is 2026-11-10T22:24:01Z, and the log
ends with the original gate's terminal success marker. This is actual completed
evidence, not a synthetic comparator fixture or partial-progress inference.

## Checks independently performed

- Portable archive comparator returned 0 with all required checks passing.
- Comparator with actual frozen and current runtime DLL directories passed;
  all three DLL byte hashes match the recorded tested-build hashes.
- All eight isolated comparator controls passed, including pending/no-exit,
  missing terminal, changed trace, nonzero exit, wrong UID, source and hash
  record rejection. No Godot test or rebuild was launched during this audit.
- Source record is 6fa6f8aa12cf4586967955c6898b6dd1409d16ad. Current HEAD and that
  tested commit both have Godot tree 5023aa4d61b633c0d59c2e15e0e4057505fcf557
  and tools tree afff6c24fcf143fdff3a5a4facf2f373d1ad3290.
- All 45 original stages have ordered coverage. There are 6669 animation
  assertions, 22496 played-flow assertions, 213 unique script UIDs and four
  build summaries with zero warnings/errors. Assertion totals include wait
  observations and are not counts of unique scenarios.
- All 16 ordered AI traces, totaling 3425 independent legal orders, and all six
  played-flow markers match both the immediate accepted fall aggregate and
  the separately provenance-verified staged hit reference.
- The normalized warning multiset remains 41, unchanged from both references.
  No error or leak diagnostics were detected. Existing environment readiness
  diagnostics are not relabeled as newly solved conditions.

The immediate fall baseline remains correctly identified as a single original
aggregate tested at 90952ac7a0d74f3f43b7dbeb0971a27df29b6dac. The older hit
reference remains explicitly staged coverage evidence; neither is relabeled
as the other's execution mode.

## Acceptance limits

This completes the pending aggregate evidence noted in earlier source/native
reviews. Independent native image/package and complete saved-state comparison
are recorded separately in ../native-review.md. Native imagery covers the
accepted unmirrored scenario; mirrored evidence is source registration plus
controlled-clock checks. No standalone export, complete-campaign playthrough
or real-time performance acceptance is claimed. No production edits or commit
were made by this review.
