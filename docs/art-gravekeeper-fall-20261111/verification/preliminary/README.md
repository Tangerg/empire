# Preliminary verification, not final acceptance

Both builds, editor import, formatter and source UID check pass. The first
focused AnimationChecks run failed an adapted test at nominal975ms: the
seconds-based timeline yielded439.99999999999994ms and correctly held frame4.
The corrected integrated checks bracket the transition at974.999/975.001ms;
exact direct ActorClip half-open440ms checks remain unchanged.

The second run passed the new fall section but was terminated by the focused
wrapper's default300-second process budget. Its exit124 and shutdown diagnostics
are retained; this is not a passing suite. The full repository gate already uses
900seconds per native process. A new focused run uses that same existing budget,
without changing source timing, gate script, assertions or failure detection.
Its outcome is pending at this implementation checkpoint.
