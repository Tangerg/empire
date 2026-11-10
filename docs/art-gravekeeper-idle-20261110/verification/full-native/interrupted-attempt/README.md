# Interrupted original aggregate attempt

The original gate started at 2026-11-10T22:44:17Z from clean source
`869078976dd21f5d82932dc4151bdd03fa973c1a`. The execution environment changed and
the original process was lost. The implementation parent confirmed the process
was absent and its exec session returned `Unknown process id`.

This directory preserves the exact 93,327-byte partial log plus original
source/start/empty clean-status records. The last recorded marker is
`Battle result-flow native checks passed: 402 assertions.` There is no exit
record, finish record or original terminal success marker. None was inferred or
created. `interruption-status.json` records byte counts and hashes.

This attempt is not an aggregate pass. The following retry must run the unchanged
original full gate and produce its own real completion evidence. Earlier native
capture evidence can remain applicable only if the tested Godot/tools trees and
actual frozen/live assembly bytes remain identical.
