# Full regression at 510 formal topics

Frozen production source/runtime/catalog/test snapshot: 054a222d41b99835bc1174e285a4cbc64dc59435. Only independent raw-source backups and review evidence were added while these suites ran; no formal binding or application code changed.

- `npm test -- --maxWorkers=1 --minWorkers=1`: 131 files, 1808/1808 tests pass, 347.13 seconds, exit0.
- `PYTHONPATH=tools/art python3 -m unittest discover -s tools/art -p 'test_*.py'`: 340/340 tests pass, 157.47 seconds, exit0.
- Prior475 snapshot's three UI timeouts were not hidden or edited. Those tests pass here under their unchanged original deadlines. Prior failure and isolated rerun logs remain in verification-475.
- The new heal500ms versus hit420ms adapter regression and late resource-inlining fixture tests are part of this full run.
- This is headless logic/adapter/export testing. Live GPU/browser rendering and observed animation remain unverified.
- Four application builds and original-byte resource-integrity audit passed at497 and remain separately identified, rather than being silently relabeled as510 builds.
