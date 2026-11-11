# C14 valve interaction locations

Base: `8559beab`. A real 960×640 detached chapter-entry run confirmed the
briefing asks for two valves, while the battlefield has many similar industrial
props, no task locator, and no arrival/complete-action instruction. Clicking the
plain valve score did nothing.

The existing score targetZones declaration now links valve-a and valve-b. Both
canonical zones contain two valid cells, so all four cells are shown. The task
explicitly says to arrive at both valve areas and complete an action. There is no
new engine rule, coordinate copy, counter, or HUD type branch. Both areas remain
shown until score completion, not an unclosed-only checklist.

Core coverage verifies the formal four-cell declaration, state/save invariance,
immutable detached target data, and fresh canonical updates. Real legal commands
activate a valve, leave the entire two-cell zone, then revisit. Count and trigger
ledger stay at one; full states (except targetZones) and all events match the
no-metadata baseline. Native camera integration adds C14 including complete
marker bounds, keyboard/repeated focus and read-only/busy handling.

The C14 collision witness pins complete state hashes and therefore correctly
failed after this intentional metadata change. Its three hashes were refreshed
only after a paired old/new run: all 85 prefix steps and the final action retain
identical events and full states with only targetZones removed. The old hashes
were reproduced, and the original final events SHA remains unchanged. The prefix,
action and eventsSha256 fields are untouched; production hashing is not weakened.

GUI after repair: both two-cell valve regions are fully visible together, the
instruction fits on one line, repeat locate is stable, and score 0/2, remaining
enemies 11 and ready actors 11 do not change. The window was closed afterward.
This is an explicitly marked isolated entry using the public-records choice,
not a carried-state campaign victory or manual full-chapter completion.

Final check results are recorded below. No screenshots or private saves included.

## Results

- Initial full core run: 16 passed, one failed solely at the old C14 collision
  fixture initial hash. After paired verification and refreshing three hashes,
  that complete counter-effect group passed separately (1 passed, 0 failed).
  All 17 groups therefore have passing coverage; no second full core run is claimed.
- All 16 independent native AI save/replay levels passed. Fifteen trace hashes
  equal the C13 baseline; only C14 metadata changes. Full-level paired replay
  checks events and complete state except the declared metadata.
- Native camera integration: 425 assertions. Objective/outcome: 297. Catalog
  ownership boundary: 76. Strict native process/diagnostic gates passed.
- Builds: zero warnings/errors. Editor import, formatting, source metadata and
  diff checks passed. Removing C14 targetZones in an isolated data fixture fails
  the exact formal-declaration assertion (exit 1).
- No full native aggregate or remote CI success is claimed.
