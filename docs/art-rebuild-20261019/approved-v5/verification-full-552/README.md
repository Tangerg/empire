# Full regression at 552 canonical bindings

Frozen application/runtime snapshot: 13f82da9e46b1295ebc8a7abbb231ad39984e045, including southern stone crossing integration. Later changes during checks were original-source backups; no runtime, catalog or test changes.

- TypeScript: 133 files, 1892 tests passed, 433.39 seconds.
- Python art pipeline: 344 tests passed, 146.546 seconds.
- All 60 shipped-level AI profile replays match the preserved baseline byte for byte, SHA256 3ebce01a551fe642db90380e9fb774f0682c1d3a546e8ba81bb8fcab4d90e0d9.
- The first Python run was interrupted to stop Git auto-gc filling the disk with a 3.65GB temporary pack. Its partial log is preserved. TypeScript had already completed; Python was restarted in full. Git removed its unfinished temporary pack normally, object connectivity passed, and subsequent commands disable automatic gc per invocation. No original artwork or Git history was deleted.
- Offline complete BoardView and source-alpha crossing tests passed separately. Browser/GPU rendering and actual played interaction remain unverified.
- Existing four application builds and single-HTML original-byte resource audit remain the separately dated 497 milestone, not newly claimed at 552.
