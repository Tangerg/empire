# Explicit actor presentation context: empty-catalog checkpoint

Baseline: `8981a7cd010241916a39b31b27cba87553d9cc0d`, including the reviewed Laiya,
Roderick and Mirelle appearances. The independent test-only tuple correction
`9b79d506d8e12cc3452a4b2602923eebee937a30` was applied locally as `3da4f86`.
No source PNG, level, rule unit, campaign key, save schema or canonical asset count
changes in this checkpoint. **The production actor catalog is empty.**

## Why an actor context is necessary

All 16 campaign levels place `campaign-torren` with rule type `c01.swordsman`.
Another 29 placed actors use that same rule type. A type-wide named binding would
change those ordinary soldiers and recruitment/type-only illustrations too.
Torren does not have a separate rule unit type.

`UnitArtContext` adds only an optional readonly `actorKey`, passed explicitly as
the last argument through the existing ArtProvider and game-ui wrappers:

- `unitPicture(type, team, context?)`
- `unitIcon(type, team, size, context?)`
- `portraitMarkup(type, team, context?)`

The wrappers retain their existing art/definition arguments and optional size.
BoardView, HUD inspector, initiative icons and placed editor units pass the
existing actor key. Recruitment, codex and editor type palettes remain type-only.
No ambient battle lookup, derived actor name, rule alias or new saved field exists.
`unitIcon`'s fallback forwards the same context to `unitPicture`; `portraitSvg`
forwards it to `portraitMarkup`.

## One pack-owned identity choice

`candidate-01-actor-art.ts` owns the explicit actor declarations and selector.
Known keys must match their declared expected rule type. Registration validates:

1. A nonempty, unpadded actor key
2. A known default rule-type binding and matching canonical topic
3. An explicit, available named appearance with the same canonical topic
4. A real character-card topic when a card is declared

Unknown actor keys and absent context keep the existing type binding. A known
key with the wrong rule type throws. No card on a named actor means its own
runtime frame is used, rather than a type-level face. The existing strict hero
resolver then chooses the named team variant or named base; it never falls from
a named actor to a generic team's different face. The shared selector is also
used by the full-scene and named-consumer review tools.

### Future registration example, not active in this checkpoint

After the independently approved complete stage01 sheet/card are registered,
the exact declaration is:

```ts
export const CANDIDATE_01_ACTOR_ART = readCandidate01ActorArt(CANDIDATE_01_HERO_APPEARANCES, {
  'campaign-torren': {
    expectedUnitType: 'c01.swordsman',
    unitArt: { topicId: 'C01-UNIT-SWORDSMAN', appearanceId: 'torren-stage01' },
    characterTopicId: 'C01-CHAR-TORREN-01',
  },
});
```

The battle identity audit identifies stage01 as the young gray-clad soldier;
the battle sword substitution is explicit. `C01-CHAR-TORREN-02` is the later
officer identity and has no progression owner here. No stage02 choice is inferred.
The campaign roster uses `torren`, not `campaign-torren`; its existing explicit
`CANDIDATE_01_PORTRAITS` bridge must be registered separately with the approved
card. Generic campaign UI must not guess a battle-key prefix.

The named sprite and independent transparent character source/background card
contract remain owned by their existing exporters. The synthetic fixture URLs
in tests are never production sources or completed-art evidence.

## Evidence

- `review.json`: exact validation result and limitations
- `board-before.json` / `board-after.json`: **all 16 complete actual BoardView SVG
  hashes equal**, with actor/building counts and full viewport unchanged
- `boards/*.svg.gz`: the identical complete after projection for every level;
  each decompressed SVG must match the corresponding recorded SVG SHA256
- `contract-before.json` / `contract-after.json`: all 16 level/state, ground,
  under-unit/over-unit and editor-ground contracts equal
- `actor-contract.json`: all **340 actual actors**, including 16 Torren and 29
  other swordsmen, keep identical picture, icon and portrait outputs; every
  before/after save document is equal; 36 actor/card PNG hashes are recorded
- `source-baseline-check.json`: all 36 actor/card PNG hashes also match the
  actual Git baseline bytes, rather than only matching current URLs
- `board-source-baseline-check.json`: all 70 image files referenced by the archived
  complete boards, with current and baseline source hashes
- `replay-before.txt.gz` / `replay-after.txt.gz`: **60 runs, 3,534,788 decompressed
  bytes, exactly equal**, SHA256
  `3ebce01a551fe642db90380e9fb774f0682c1d3a546e8ba81bb8fcab4d90e0d9`

The projection is offline BoardView/SvgBoardSurface evidence, not a live-browser
or GPU screenshot. No new visual acceptance is claimed because no actor art is
activated. Hidden drawings retain the established display-state behavior; a
cached SVG image can exist while its unit is display:none. Hidden selection is
not promoted into the visible selection cue.

## Tests and reproduction

110 focused/consumer/UI/editor/backend/hero tests pass across the recorded runs.
They cover two same-type actors, unknown/known-mismatched keys, invalid binding
registration, named-team fallback, card/frame consistency, actual board/hover/
initiative/editor propagation, generic recruitment/palette calls and save
identity. The activated synthetic fixture also exercises actual BoardView hidden
selection; SVG/Pixi parity separately includes the real campaign Torren.

Architecture: 103/104 pass. The one failed assertion lists 45 unavailable
reference-document/image links in this sparse worktree; its assertion is retained
unchanged in `regression-tests.log.gz`. The full repository suite was not claimed.
Typecheck and all four app builds pass after the independent prerequisite fix.
The original test/build logs are losslessly gzip-compressed; no failure text was removed.

```sh
ACTOR_CONTRACT_OUTPUT=/tmp/actors.json npx vite-node tools/art/review_actor_art_contract.ts
CONTRACT_OUTPUT=/tmp/scenes.json npx vite-node tools/art/review_candidate_scene_contract.ts
HERO_SCENES_OUTPUT=/tmp/boards.json HERO_SCENE_SVG_DIR=/tmp/boards \
  npx vite-node tools/art/review_hero_scene_contract.ts
npm run --silent sweep > /tmp/replay.txt
# Decompress the archived replay and compare it byte-for-byte with /tmp/replay.txt.
```

For a later registered actor's real portrait/icon review, use
`HERO_UNIT_ID=c01.swordsman HERO_ACTOR_KEY=campaign-torren` with
`tools/art/review_named_hero_consumers.ts`; it rejects the current empty catalog
because no explicit Torren appearance/card has been activated.
