# Preserve readable battle receipts on completion — 2026-11-11

Base: `a8b5970293e5df2b91e4822097e5000857cbc6cd`.

## Observed problem

A real C01 campaign run reached victory through 189 legal actions. Its last
ranger attack dealt 21 damage and killed the final enemy. The real BattleScreen
retained the localized damage/death receipt, but the normal completion transition
then discarded the screen. At 960×640 the victory page offered only settlement,
retry and camp: the player could not review that final receipt.

The isolated QA harness discovered the terminal action, reloaded the original
campaign opening, independently replayed the exact legal prefix and mounted the
production battle screen before the last action. It did not edit HP, rules,
phase or conclusions. This is a legal terminal replay, not a claim of manually
playing every preceding action.

- Opening hash: `78e7c7b9`; final action index: 188.
- Before: `c001265e`; after: `5faa0763`.
- Ordered final events SHA-256: `74f70f2fc4e8b16865a559ef8ccbf13fbec5902d4b9da69d949f6680d5fb46a5`.

## Change

Before unmounting the battle, the application transfers a detached copy of its
already visibility-filtered `RecentMessages`. Completion displays that snapshot
in a collapsed, read-only section inside the existing ReadingScroll. It never
reconstructs descriptions from raw canonical events or creates another gameplay
transition. The normal 60-run journal bound remains unchanged. Empty snapshots
have no disclosure. Existing settlement, retry and camp confirmation retain their
navigation ownership and guards.

## Verification

- Real 960×640 baseline and fixed GUI: final damage/death can now be reviewed;
  End reaches the final record, collapse restores the compact page, and canceling
  camp confirmation leaves repeated journal reading usable. Footer actions stay
  visible. The same terminal events and hash were preserved.
- A final layout-only fixture using that verified snapshot checked the ordinary
  plus/minus disclosure glyphs, avoiding an emoji-styled triangle.
- BattleResultFlowChecks: 413 assertions, including snapshot isolation, literal
  markup, repeated queued activation, keyboard End, footer geometry, empty data,
  rebind/unmount, camp confirmation, and production screen-to-result handoff.
  Its 81-record input is a layout stress fixture, not the production retention cap.
- Negative control: removing only the application's journal argument fails the
  production handoff check because the completion journal is absent. Restoring
  the argument passes the complete result-flow gate.
- CampaignUiFlowChecks: 209 assertions passed.
- Recovery reran the complete CampaignNavigationChecks against unchanged
  `a8b59702`: 9,559 assertions and the same two failed groups as the feature
  run. Both tests still assumed dossier details start collapsed, despite the
  intentional auto-expansion in `07d53a76`. Keyboard expectations now assert
  the open initial state and then collapse/reopen it. Touch navigation first
  collapses those details through actual native touch before opening the next
  stance section; its visibility and single-order guards remain enforced.
  The interrupted earlier baseline has no claimed result.
- Final complete CampaignNavigationChecks passed after that test correction;
  see `verification/campaign-navigation-final.txt` for the exact assertion count.
- Final build: zero warnings/errors. Repository native whitespace check passed.

The lawful terminal harness and generated campaign saves remain outside the
repository. No GUI images, credentials, user saves or previously withheld evidence
are included. Full Core/16-level parity and the aggregate native suite were not
rerun for this presentation-only change.
