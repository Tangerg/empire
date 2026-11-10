# Independent final native-gate review

**Result: passed.** Independently reran the archived comparator with both optional
live DLL checks and all four comparator controls; each passed. No gate process
was restarted, duplicated, or changed by this review.

- The archived log, exit, start, finish and source records byte-match the live
  originals. Exit is `0`, and the original gate's terminal success marker is
  present. Recorded interval: **2026-11-10 20:55:18–21:28:28 UTC**.
- Log SHA-256: `26af520e9ff722c79a182cfacf83e185c19aaf7cb2c56c9a7a949af36bd852e2`.
- All **45 original stages** have coverage evidence. Silent stages are supported
  by successful completion of the original fail-fast aggregate script.
- **6,115 animation assertions** and **22,121 played-flow assertions** are
  recorded. Counts include frame waits and are not unique scenario counts.
- All **16 ordered AI trace digests and order counts**, totaling **3,425 legal
  orders**, match the accepted archer-hit baseline. All **six played-opening
  terminal/carry markers** match exactly.
- All **41 warnings** match the baseline multiset after temporary save-root
  normalization. No error/leak diagnostics were found. All **four observed
  build summaries** report zero warnings and zero errors. Metadata reports
  **212 unique script UIDs**.
- Baseline aggregate hash, plan/completion provenance, and all 45 stage result
  and log hashes verify. That baseline remains a staged recovery; the current
  evidence is the completed original aggregate run.

## Source and binary identity

Tested build source: `90952ac7a0d74f3f43b7dbeb0971a27df29b6dac`.
Review HEAD: `179463a28042e6897a54b96c497334586585c01d` (documentation only).
Both Godot trees are `9d962e7735bedd02698701bf3b08cb17d632e4c8`.
The working-tree diff against the tested build is empty for `godot` and
`tools/godot`; no production changes were made by this review.

Frozen and current runtime DLL bytes independently match their recorded hashes:

- Empire.Core.dll: `4b9fdbcdd66dde4e9bc898539ee03266ac5cf997b2ed740b61bb0639c7b983c8`
- Empire.dll: `097bd10747f9710295d064a7549b6d2e1b42bd30503b74fc32cdb64e7cd6016a`
- GodotSharp.dll: `2142e62894176d7340f50581f8b2fe84e36ae64e1a85f1b2bd7737a7e8a5c8ba`

This establishes headless native contracts, deterministic independent battles,
and the recorded played opening. It does not establish GUI, export or complete
campaign acceptance. Comparator controls are synthetic admission tests, not
additional native gameplay runs.
