# Current native narrative expectations

`narrative.json` is the reviewed expectation owner for `NarrativeChecks`. It is
checked in as readable JSON, never generated from the running application or PO
catalog during verification. Production structure and Chinese wording remain
owned by `data/grayflag-story.json` and `Localization/zh_CN.po` respectively.

The historical TypeScript oracle, `../oracle/narrative.json.gz`, remains unchanged
(SHA-256 `84c1611d662b8c6e193e073301b56c8f814b145e8c5763534d0075a4fb626654`).
The native fixture was migrated in full, including its definition, document
expectations, states, transitions, lifecycle cases and edge projection inputs.
Inherited section names do not make that historical oracle the native wording
owner. Future intentional native changes require an independently reviewed
fixture change, not a runtime adapter or an exclusion from comparison.

## Reviewed migration

- All 35 authored dates use the native timeline already explicitly asserted in
  `NarrativeChecks` (introduced in `1eb68e53`). The C# date override is removed.
- Laiya and Roderick portrait source paths use the already-declared native
  `/godot/assets/runtime/narrative-static/char-leia-01.png` and
  `/godot/assets/runtime/narrative-static/char-roderick-01.png`. This includes
  matching edge-case presentation inputs; there is no artwork change. The C#
  portrait-path exception is removed. Resource existence and source/export
  SHA-256 verification remain in the standalone runner.
- `35880822261ac1715f2948b63810aacbe80ccd89` intentionally corrected
  `story.c01.brief-01.mission.danger`: Roderick gives ordinary reserve support,
  not a one-shot rescue. See `../../docs/FORMAL-TWIN-HILLS.md`.
- `74226487c4999ed004e831f834866acb258068eb` intentionally changed these five
  keys for the formal Redstone mission and conditional support:
  - `story.c01.brief-03.mission.objective`
  - `story.c01.brief-03.mission.danger`
  - `story.c01.brief-03.mission.lesson`
  - `story.c01.brief-03.beat.rescue-before-judgment.text`
  - `story.c01.brief-03.beat.reinforcement-deadline.text`
  See `../../docs/REDSTONE-CAMPAIGN.md`. Both document expectations and sampled
  rendered expectations contain the reviewed wording.

The initial migration changed exactly 213 existing string leaves:
139 dates, 53 portrait paths and 21 occurrences of those six prose values. No
state, transition, choice, layout, other prose or other artwork value changed.
All existing cases are preserved: 106 story renders, 11 choices, 48 transitions,
11 lifecycle cases and four edge projections. An explicit new coverage assertion
requires the existing normal `reinforcement-deadline` case to reach the widget
loop; the prose negative control targets that exact case. No duplicate render
case is added. Of 35 authored dates, 34 change; campaign-failed remains `未定`.

## Native portrait catalog migration (2026-10-23)

All seven narrative identities now resolve through `data/portrait-catalog.json`,
which also owns Torren's HUD card. The source strings are derived from native
resource paths. The subsequent fixture change updates 48 ordinary story source
expectations and one supplemental expected source, converts 27 portrait entries
across the four supplemental presentation inputs to IDs, and advances those four
inputs to staging schema 3. No other expectation changes. The known-speaker case
without a portrait remains deliberately unbound.

The Mirelle transparent candidate, pending in-scene visual acceptance, replaces only its native PNG. The
catalog owns its new hash; the fixture does not duplicate hashes. Historical
TypeScript portrait bytes and the frozen oracle remain untouched. Current native
build/behavioral/visual verification is separate from authoring this migration.

## Verification

Run `bash tools/godot/narrative-checks/run.sh --verify-guard` for the focused
headless suite. It creates a small temporary native project and fresh
data/config/cache/runtime XDG directories; it verifies all eight catalog portraits against their native byte hashes and
16 scene/chapter references against both source and exported hashes. No live profile is used. `TMPDIR` can place all
output in an external evidence directory.

`bash tools/godot/verify-native.sh` also runs `NarrativeChecks` explicitly, against
this checked-in fixture and with its per-scene isolated `XDG_DATA_HOME`. It does
not substitute the historical TypeScript suite for the current native contract.

Every story projection is compared as a complete JSON object, with no date,
portrait or prose exclusions. Widgets use the same current expected values.
All 91 beat identities and localized speaker projections form an independent
check group. All groups run and aggregate failures, so a prose failure cannot
hide the shared-name projection, and an identity failure cannot produce a pass.

The focused runner's negative controls reject a changed reinforcement-deadline
expectation in both projection and widget groups while the identity group still
runs; reject a changed speaker expectation while the prose projections still
run; and reject an incorrect production next-beat label. These mutations are
confined to the temporary test project/fixtures.
