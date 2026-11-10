# Native gate comparison

`compare-gate.py` only reads evidence. It does not launch Godot, rebuild, restart,
resume or alter the native gate. Its default paths are relative to this archive,
not to a machine-specific workspace.

The original gate finished with exit 0 at 21:28:28 UTC on 2026-11-10. Its exact
`full-native.txt`, `full-native.exit`, start and finish records are archived here.
Both archive-only and live-binary comparisons pass. The tested build source is
`90952ac7a0d74f3f43b7dbeb0971a27df29b6dac`, recorded in
`full-native-source-commit.txt`. The later `179463a2` documentation commit has the
same Godot source tree; it is not substituted for the tested build identity.
The passing outcome is established by those recorded results, not by the
presence of this script.

From the repository root, run:

```sh
python3 docs/art-archer-fall-20261110/verification/full-native/compare-gate.py --output /tmp/gate-comparison.json
python3 docs/art-archer-fall-20261110/verification/full-native/test-compare-gate.py
```

For a still-running or separately stored run, pass `--log`, `--exit`, and
`--source` explicitly. `--baseline` can select an equivalent complete baseline
archive; the default is the checked-in archer-hit
`verification/full-native-staged` package. `--hashes` selects the recorded DLL
hash manifest. No uncommitted DLLs or installed engine are needed for archive
comparison or its controls.

The comparator verifies the baseline aggregate log hash, runner exit, plan hash,
completion/source record, and all 45 individual stage result/log hashes and
source records. That baseline is a complete staged recovery, **not** a
single-process aggregate run. Current success requires its own exit file `0`
and the original gate's terminal success line, plus:

- All 16 ordered AI trace level/order-count/digest records match the baseline.
- All six played-opening terminal and carry markers match exactly.
- Warning multisets match after normalizing only temporary save-root names.
- All 45 original stages have ordered coverage evidence. Silent format/import
  stages require aggregate completion under the original `set -e` gate.
- Metadata reports 212 UIDs, observed build summaries have zero warnings and
  errors, and no error/leak diagnostics are present.
- Source identity and the three archived assembly hashes match the tested build.

Assertion counts in stage success markers are normalized because some count
frame waits. They are not unique scenario totals. AI order counts and digests,
played-flow facts, and warning multiplicities are not normalized.

## Optional live binary check

`--frozen-dir /path/to/final-assemblies` and/or
`--runtime-dir /path/to/godot/.godot/mono/temp/bin/Debug` enable separately named
live binary checks. Their absence means **not performed**, not a byte-identity
claim. The default check verifies the archived hash record only. If supplied,
each directory must contain all three DLLs and their bytes must match the
record. This distinction keeps archive verification reproducible after an
environment reset.

## Return codes and controls

- `0`: complete original run and all requested comparisons pass.
- `1`: a completed run fails at least one required comparison.
- `2`: no final exit record yet; pending, regardless of intermediate markers.
- Missing/malformed required evidence raises an error; it is not accepted.

The four controls use temporary synthetic logs only: equivalent completed
fixture returns `0`; changed AI digest returns `1`; missing exit returns `2`;
missing terminal marker despite exit `0` returns `1`. They do not manufacture or
replace native test evidence. Headless checks and the played opening do not
establish GUI, export or complete-campaign acceptance.
