# Stable 453-topic regression

Tested commit:0967e0d5a4ce08ba096fd027206e089b262bdf26. Production files and catalogs remained unchanged for this run.

The archive discovery/link defects are fixed: no archived proposal or duplicate harness was collected. The run executed1696 tests across129 files:1695 passed, one field-post BoardView test exceeded its existing15000ms budget. This is not recorded as an all-green full run.

The unchanged field-post suite was retried alone with one worker:9/9 passed, including hidden Mirelle selection/movement and standing-placement restoration. No timeout, assertion, visibility contract or production code was weakened. The isolated retry lasted32.21s for the entire suite. The full concurrent-run timing limit remains a reproducibility caveat; exact failed and successful logs are retained.

Architecture-boundary tests passed104/104 after correcting archival links; scene binding tests passed33/33 after both ending exports existed. Typecheck passed. Browser/GPU gameplay validation remains unavailable; these DOM/engine checks are not screenshots of a running game.
