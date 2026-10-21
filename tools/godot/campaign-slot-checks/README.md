# Campaign autosave and title-menu checks

Run from the repository:

    bash tools/godot/campaign-slot-checks/run.sh --native --verify-guard

The runner creates a new `/tmp/empire-campaign-slot.*` directory. The default input is the frozen `godot/tests/oracle/campaign-menu.json.gz` plus current native schema-2 metadata and a disposable check-only catalog projection. There is no live TypeScript regeneration in the native gate.
Filesystem checks refuse to reuse their fixture directory and create seed files
with `FileMode.CreateNew`. No game user-data path or existing player save is used.
Core and test code are copied into this temporary directory before building, so
parallel shared-project builds are not overwritten. Native runtime user data is
also redirected into the same fresh directory.

The frozen storage observations were produced by the original `campaign-storage.ts` against an in-memory
localStorage and actual AST-extracted `titleItem`, `renderMenu` and
`skirmishMarkup` from `apps/game/src/main.ts` in jsdom. Those functions are not
rewritten as test expectations. Non-campaign destinations and other save types
are unused stubs. New/Continue click handling, overwrite confirmation and deletion
are the source's real code. The .NET suite compares canonical state and error classification against those observations. Native menu authoring now uses explicit catalog keys; its fixture preserves original display expectations except for the intentional player-safe invalid-save notice. Detailed stored-document errors remain diagnostics and are logged by the application, not embedded in UI labels. JavaScript JSON parser wording and native OS I/O diagnostics are
platform-specific: those comparisons require rejection with a nonempty message,
not identical error text.

Coverage includes absent/empty/malformed saves, invalid envelopes and state,
active/completed/failed states, battle progress, pending battles, read/write
failures, propagated definition defects, canonical writes, explicit rejection of
obsolete application wrappers (including the former clean legacy wrapper), UTF-16 preservation, non-mutating
reads, failed-write preservation, caller-requested clearing, independent manual
files and concurrent readers during atomic replacement. Progressed/terminal states
are controlled presentation fixtures, not evidence of a victorious playthrough.

`--verify-guard` compiles an isolated mutant that catches all validation exceptions.
It must fail specifically because the original propagates definition defects.
`--native` builds a minimal temporary Godot project and passes the explicit
`--isolated-menu-art-fixture` argument. The missing-art fixture is a detached
metadata clone with a deliberately nonexistent resource path, not an assumption
about what assets happen to be installed. This mode checks actual menu controls:
catalog-resolved text, Continue/New visibility, the safe invalid-save notice, disabled single-use
callbacks, stale/rebound/detached/unmounted controls, and caller-owned new-game
intent. The root application's own confirmation and canonical transitions need
separate integration checks.

In the full project, launch `CampaignMenuChecks.tscn -- <oracle.json>` without the
isolated-fixture argument. It requires `res://data/grayflag-staging.json`, compares
its menu projection against the frozen native expectations, verifies the original menu image's
manifest hash, and checks that the actual TextureRect loads precisely that exported
resource with the source art description and no missing-art diagnostic. Missing
exports fail this mode; they never silently select the isolated boundary.

These are headless contracts, not GUI acceptance, original typography/pixel
equivalence or packaged delivery. Isolated mode does not claim successful exported
art loading; only the full-project mode exercises that integration.

`export-oracle.ts` is retained only for researching original browser behavior. Its literal-wording metadata is not the native schema and must not overwrite the curated native fixture or production documents. Run builds and native checks only in the shared sequential verification lane. Each runner process is time-bounded; a timeout is inconclusive, never a passing result.
