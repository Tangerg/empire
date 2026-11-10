# Native gate comparison: aggregate passed

The original unchanged `tools/godot/verify-native.sh` completed with exit 0 and
its final success marker. This archive contains the exact real log, source,
start, finish and exit records. Both archive-only and live-DLL comparisons pass.
All 45 stages, 6,669 animation assertions and 22,496 played-flow assertions are
present. All 16 AI traces (3,425 legal orders), six played-flow markers and 41
expected warnings match the immediate accepted fall aggregate. Four builds
report zero warnings/errors; no unexpected error/leak diagnostics occur.

Tested source: `6fa6f8aa12cf4586967955c6898b6dd1409d16ad`.
Tested Godot tree: `5023aa4d61b633c0d59c2e15e0e4057505fcf557`.
The source/start files were copied from the real launch records. The three DLL
hashes come from the frozen tested assemblies; their paths are reduced to
filenames so this archive remains portable. A recorded hash is not a live byte
check. Optional `--frozen-dir` and `--runtime-dir` check actual DLL bytes.

## Comparison and provenance

`compare-gate.py` reads evidence only. No build, Godot invocation or test launch
occurs. Default paths are relative to this archive. The immediate accepted
baseline is the fall aggregate archived with d76f6fff, whose actual tested build
was `90952ac7a0d74f3f43b7dbeb0971a27df29b6dac`. Its full log is pinned to SHA256
`26af520e9ff722c79a182cfacf83e185c19aaf7cb2c56c9a7a949af36bd852e2`.
The comparator verifies its exit0, terminal marker, source/status provenance and
45 completed stages, then compares all 16 ordered AI traces, six played-flow
markers and normalized warning multiset directly against it.

The earlier archer-hit full-native-staged archive remains the ordered 45-stage
coverage reference. It is explicitly a staged recovery, not a single-process
aggregate. Its log/plan hashes, runner exit, completion/source record and each
individual stage result/log hash are verified without changing their provenance.
Current traces, played outcomes and warnings are also compared against that
reference. Silent format/import completion requires actual successful aggregate
termination, never a partial log.

Current success requires its own exit0 and original terminal marker, all ordered
stage coverage, 213 script UIDs, zero-warning/error build summaries, no error or
leak diagnostics, exact source identity and the three expected assembly hashes.
Only assertion totals and UID counts in stage coverage marker strings are
normalized; the actual expected UID213 has a separate check. Assertion totals
include process-frame waits and are not unique scenario counts. AI order counts,
digests, played outcomes and warning multiplicities are never normalized.

From the repository root:

```sh
python docs/art-archer-cheer-20261110/verification/full-native/compare-gate.py --output /tmp/cheer-gate-comparison.json
python docs/art-archer-cheer-20261110/verification/full-native/test-compare-gate.py
```

For externally stored evidence, supply `--log`, `--exit` and `--source`.
`--hashes` overrides the recorded hash manifest; `--baseline` overrides only the
staged coverage reference, not the pinned immediate fall aggregate.

Return0 means complete and all requested checks passed. Return1 means completed
but mismatched. Return2 means no final exit file, regardless of intermediate
markers. Missing/malformed required evidence raises an error, not a pass.

Eight synthetic controls pass: equivalent complete fixture, changed trace
rejection, pending without exit, missing terminal rejection, nonzero exit
rejection, old UID count rejection, wrong tested source rejection and wrong
assembly-record rejection. These temporary synthetic inputs are not archived
as native gate logs and do not establish actual runtime success.

Headless coverage and played opening do not establish GUI, exports, complete
campaign or real-time performance acceptance. Native visual review is separate.
