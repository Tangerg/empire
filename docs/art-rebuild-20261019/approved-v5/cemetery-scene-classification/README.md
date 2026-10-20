# Cemetery square-scene classification correction

Only C01-CH03-S11 canonical kind changes, key-prop → scene. Its exact 128×128 record, path, legacy quality tier and opaque PNG bytes are preserved. Existing 41 wide scenes, 22 cards and remaining 16 key-props are untouched. No art, override, HD registration or completed topic is added.

The original scene-board-07 top-left source and deep-customized/09-static-assets.md section 3 plus deep-customized/03-elder-peoples.md section 1 agree on the cemetery scene. Source hashes and exact canonical invariant are in evidence.json. Tools documentation carries the durable rationale.

Opaque scene export and HD validation now accept only declared 256×144 or 128×128 unframed, unanchored narrative scenes. Crops must exactly match canonical aspect; optional HD must use exact declared density and never exceed source pixels. CH01-S00 stays held. Reusable props remain transparent; card rules are unchanged.

Passed: 89 Python tests, 330 TypeScript tests, typecheck, four-application build, and read-only registered exporter check for 362 outputs across 320 topics. Two new square-source tests were verified to fail against the old renderer in memory. Full repository tests and browser visual acceptance are not claimed.

The initial sparse worktree omitted native source/output files. They were materialized from the branch's Git objects for integrated verification; the resulting 80 initial missing-output failures were resolved without code changes, and the full 330-test scoped run passed. Historical inventory reconciliation remains with the parent integrator. No remote push.
