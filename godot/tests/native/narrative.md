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

## Bell tower choice wording (2026-11-06)

The `joint-control-with-cain` option now promises only that Cain enters as a
temporary ally, rather than his entire legion. The shipped-playable campaign
description explicitly says that he becomes friendly
(`docs/story-candidates/candidate-01/PLAYABLE-CAMPAIGN.md`, choice consequences).
`GrayFlagCampaignPolicy` implements that scope by changing `imperial-cain` only.
The older story draft describes limited squad cooperation, not an opening
conversion of every imperial unit. Neither allegiance rules nor story outcomes
change. Both the document expectation and the rendered choice expectation use
the corrected native PO wording; the historical TypeScript evidence is untouched.

A genuine carried C12 run reached its two-of-three mechanism victory on turn 7:
63 native player orders, 122 total orders and 391 canonical events matched the
legal reference. Settlement reached C13 with 12 victories, 17 supplies and
790 battle funds; an independent automatic restart reconstructed that opening
without overwriting the manual slot. This is headless input/state/lifecycle
acceptance, not graphical acceptance or C13 combat coverage.

The reference also records Cain leaving the battlefield. He is not campaign
roster-bound; this does not establish permanent story death or justify inventing
a revival rule. The unconditional aftermath still needs narrative-intent review
if combat departure is meant to change its dialogue.

The standalone narrative runner now includes `ReadingScroll`, the existing
production dependency of `CampaignShell`. Its prior omission caused CS0246
before the tests could run. The corrected reviewed choice expectations reject
the former legion promise in both projection and native-widget groups, with
independent identity checks still running and clean assertion-failure teardown.

## Ending pagination decision (2026-11-06)

Native endings now present every existing beat in order. This intentional design
change replaces the inherited first-beat-only browser behavior; the frozen
TypeScript oracle remains unchanged. The four-beat chapter-three ending's nine
pre-final render expectations (three cursors times active/completed/failed)
change exactly their primary label and action to `下一段 →` and `nextBeat`.
All narrative text, identities, dates, artwork, final labels and source documents
remain unchanged. All 106 render cases are now exercised as native widgets,
including the nine previously excluded nonzero ending fixtures.

All 15 ending transition cases retain their initial states and cursors. Their
operations are real primary presses: advance locally to the final beat, finish
once if active, then exit. The 40 inherited operations become 38 real primary
presses: inert `nextBeat` probes are removed and sequential paging is covered. Expected pre-final canonical state remains unchanged
and saved state remains null. The existing canonical completion/failure snapshots
are retained; the same-ending completion rebind keeps its final cursor. Terminal
fixtures exit without a save or transition. Case counts remain 106 story renders,
11 choices, 48 transitions, 11 historical lifetime cases and four edge projections.
These are reviewed checked-in expectations, not values captured from production.

Native lifecycle and keyboard-focus checks run for both normal stories and the
multi-beat ending, retaining stale-button, duplicate activation, deferred paging,
rebind, detach/remount and competing-exit guards. Application navigation fixtures
cover active/completed/failed endings, local reading with unchanged save bytes
and timestamps, last-beat completion handoff, immediate single-beat failure,
fresh Continue, exit/re-entry and F9 cursor reset. Ending write-failure checks
now reach the actual last beat before testing persistence failure. These fixtures
are not a claim of a genuinely played campaign victory or graphical acceptance.

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
