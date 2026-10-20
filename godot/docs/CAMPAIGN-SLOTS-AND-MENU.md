# Campaign-only autosave and title menu

## Owners and integration

`CampaignSlotStorage` is the native filesystem boundary for the original browser
campaign slot. Every operation requires a caller-supplied absolute filesystem path:

- `Load(definition, path)` returns `LoadedCampaign(State, Rejected, LegacyApplicationEnvelope)`.
  A missing file and the source's empty-string slot mean absent. Unreadable,
  malformed, invalid-UTF-8 and invalid documents return a nonempty rejection; a pending battle
  has the original explicit Chinese rejection. Loading never deletes or rewrites.
- `Save(definition, state, path)` refuses pending battles and delegates schema and
  semantic validation to `CampaignSave.Create`, serialization to `EcmaJson`, and
  replacement to a same-directory temporary write, flushed before rename. Failed
  validation leaves the previous save intact. Write failures propagate, and a
  failed temporary-file cleanup is reported together with the original failure.
- `ClearForNewCampaign(path)` implements the source's explicit New action only.
  It is not rejection cleanup. The caller owns confirmation before replacing a
  usable saved campaign. Clear failures propagate and must prevent entering a
  supposedly fresh campaign as if clearing succeeded.

`CampaignSave`, `CampaignValidation`, `StoredDocumentException` and `EcmaJson`
remain the only owners of the core document contract. The adapter does not catch
`CampaignInvariantException` from definition defects. No rule is recomputed by the
menu. Core document creation timestamps remain owned by `CampaignSave`.

The application root must keep two independent paths:

- Campaign autosave: source-compatible `CampaignSave` envelope only, saved at
  source-defined clean campaign boundaries, especially before `BeginBattle`.
- Manual application slot: `GrayFlagCampaign.Save`, including the request, battle,
  event history and `atCamp`. Autosave and New never target that path.

Neither this helper nor the native root discovers or imports browser localStorage.
Schema compatibility is not browser-profile access or automatic import.

## Narrow checkpoint compatibility

The e6 checkpoint wrote a schema-1 application wrapper to its autosave path. A read
accepts that wrapper only with explicit null `request` and `battle`, false `atCamp`,
empty `events` and a validated inner campaign with null `pendingBattle`. Recognition
requires an inner campaign save with `state`, not a canonical campaign identity.
Full manual saves and inconsistent application wrappers are rejected, never reduced
to a seemingly valid campaign-only save. This adapter is only for the actual e6
checkpoint consumer, not a general application-save importer. A later authorized
save writes the canonical envelope. It can be removed when support for resuming
that checkpoint is explicitly dropped; reads do not erase the original data.

## Original menu, with an explicit native layout mapping

`CampaignMenu.Create(LoadedCampaign, metadata)` receives the source-exported menu
prose, title art and battle count. `CampaignMenuScreen.ShowMenu(view, onContinue,
onNewRequested)` draws that projection and signals intent. It never confirms,
clears, starts, advances or loads a campaign itself.

The original source does not have different title-menu labels for active,
completed and failed campaigns. Every usable state shows `继续战役`, completed
battle count / total, and `重新开始战役`; an absent or unusable save offers
`开始战役`. The title prose, menu labels and replacement prompt are exported from the
original `apps/game/src/main.ts`, not a second C# content table.

The source's full rejection sentence is under its single-battle overlay, announced
by a note on the title. This campaign-only native menu shows that same sentence
directly because it has no single-battle overlay. This is an intentional placement
mapping, not evidence that skirmish/codex/editor/demo screens have been ported.
No invented scope warning or status badge appears in gameplay UI.

Every accepted menu intent disables both actions for that binding. The application
root must rebind after declining confirmation or handling an error. Old, detached,
unmounted or remounted-but-not-rebound callbacks cannot act on campaign state.
Confirmation, overwrite policy, state assignment and menu-return transitions remain
root-owned, with additional integration tests required there.

## Evidence boundary

Verified on 2026-10-20: 299 isolated .NET assertions against 27 original storage
reads, three writes and six original title renders; 95 native Godot control
assertions with an explicit missing-art fixture, and 102 with the actual exported
menu metadata/title image copied into the isolated project and imported. The latter
checks source identity, manifest hash and the TextureRect's loaded resource path.
It is focused art integration, not a whole-project GUI acceptance claim.
The broad-catch masking mutant was rejected. The original storage/main
Vitest suites also passed all four tests.

See `tools/godot/campaign-slot-checks/README.md` for the live-source oracle, isolated
filesystem tests, negative control and native control checks. These establish
storage/menu contracts, not the root's complete application flow, a successful
campaign playthrough, visual acceptance, platform-specific power-loss durability
or packaged export. Same-directory rename avoids partial documents for normal
readers; no filesystem transaction or directory-fsync crash guarantee is claimed.
