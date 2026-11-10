# Native gate comparison: pending

The original `tools/godot/verify-native.sh` was launched at
`2026-11-10T22:44:17Z` from clean tested source
`869078976dd21f5d82932dc4151bdd03fa973c1a`. The source/start/empty clean-status
files here are copied byte-for-byte from the actual launch records. No finish,
exit or completed aggregate log has been created by this comparator task.
The gate remains owned by the implementation task.

Tested Godot tree: `cfe370846c78c10100fbcfc8d19965efd96be697`.
Tested tools tree: `afff6c24fcf143fdff3a5a4facf2f373d1ad3290`.
The exact frozen assembly hashes from the primary gate builds are now pinned.
`frozen-assemblies-sha256.txt` preserves the actual digest/name pairs with only
absolute directory prefixes removed for portability. No earlier slice's DLL
hashes are accepted as a substitute. A read-only pending comparison also
verified the live frozen assembly bytes against those recorded hashes.

## Comparison and provenance

`compare-gate.py` is a portable, read-only evidence comparator. It launches no
Godot process, import, build or game test. Default paths are relative to this
archive and its sibling accepted evidence archives.

The immediate accepted behavioral baseline is the **cheer aggregate**, tested
source `6fa6f8aa12cf4586967955c6898b6dd1409d16ad`, with exact log SHA256
`727765291d562311f4f65bbcbaf0f7267a77b3aaf548aca89aa3bc46f2cc0df3`.
Its archived exit0, source record, terminal marker, successful archive comparison
and all 45 ordered stage markers must verify. Current 16 ordered AI traces,
six played-flow markers and normalized warning multiset are compared directly
against this cheer log, not against fall or the older staged recovery.

The archer-hit `full-native-staged` archive is used **only** as ordered 45-stage
coverage provenance. Its log/plan hashes, runner exit, completion/source record
and each individual stage result/log hash must verify. This archive remains a
staged recovery and is never represented as a single-process aggregate.
Only assertion totals and UID counts in coverage marker strings are normalized.
Current metadata must independently report exactly 214 unique script UIDs.
AI order counts, digests, played outcomes and warning multiplicities are never
normalized. Warning normalization is limited to isolated temporary save roots.

Current success requires actual exit0 and the original terminal marker, all
45 ordered stage markers, clean launch source, exact tested commit, the 214-UID
marker, zero-warning/error build summaries, no error/leak diagnostics and the
three pinned assembly hashes. A recorded hash is not a live byte verification;
`--frozen-dir` and `--runtime-dir` optionally verify actual assembly bytes.
Silent import/format stage completion requires successful aggregate termination
and is not inferred from a partial log.

From the repository root after the actual run is archived:

```sh
python docs/art-gravekeeper-idle-20261110/verification/full-native/compare-gate.py --output /tmp/gravekeeper-idle-gate-comparison.json
python docs/art-gravekeeper-idle-20261110/verification/full-native/test-compare-gate.py
```

For external evidence, use `--log`, `--exit`, `--source`, `--source-status` and
`--hashes` as appropriate. `--baseline` changes only the staged coverage
reference, never the pinned immediate cheer aggregate.

Return0 means complete and all requested checks passed. Return1 means completed
but mismatched. Return2 means no final exit file, regardless of intermediate
markers. Missing/malformed required completed evidence raises an error, never
a pass. Missing frozen hashes may be reported as pending while the real gate is
still running; completed evidence cannot omit them.

The eight isolated synthetic controls cover an equivalent complete fixture,
changed trace rejection, pending without exit, missing terminal rejection,
nonzero exit rejection, old UID count rejection, wrong tested source rejection
and wrong assembly-record rejection. They use temporary explicitly synthetic
logs derived from the accepted cheer log. They do not establish runtime success.
All eight synthetic controls pass; their actual unittest output is retained in
`comparison-controls.txt`. These controls do not change the live gate's pending
status.

Headless contracts and played opening do not establish GUI, export, complete
campaign or realtime-performance acceptance. Native visual review is separate.
