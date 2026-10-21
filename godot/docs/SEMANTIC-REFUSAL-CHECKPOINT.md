# Native quality checkpoint: semantic refusals and localized application flow

This slice replaces player-facing rule refusal sentences with immutable semantic
facts and connects the application menu and staging views to the native Simplified
Chinese catalog. It builds on the canonical ownership checkpoint f26ee926.

## Contracts and ownership

- `ActionRefusal` owns a scoped semantic code and detached immutable details.
  Refusal queries and command exceptions carry the same facts. The old string
  exception constructor and redundant command wording parameter are removed.
- `ActionRefusalText` formats those facts through an explicitly injected `GameText`
  and open presentation registry. It does not decide legality. Catalog and ability
  names still come from their composed owners; migrating their display metadata
  is a separate remaining content slice.
- Menu and staging metadata contains explicit text keys. Labels, prompts,
  dispositions, resource/relation names, and shared art-failure notices resolve
  through the single PO catalog. Unknown authored labels are errors, not raw-ID
  fallbacks. Expected storage failure diagnostics are logged separately from safe
  player messages.
- The source session fixture now authors valid square-grid facings before executing
  the unchanged TS engine. This corrects invalid test inputs rather than adding
  native aliases or rewriting source results. Its unrelated level, extra cases,
  metadata and custom handoff behavior remain unchanged.

## Verification boundary

The frozen candidate builds with zero warnings/errors and imports successfully.
Actual headless Godot menu, staging and application navigation complete with clean
process exits. Navigation covers story, resume, restart, camp, confirmation dialogs,
settlement, expected storage failures and atomic manual load; malformed saves show
exactly the injected safe notice while preserving their bytes until explicit New.
The focused refusal baseline preserves exact accepted/refused outcomes, semantic
code/details, rollback, events and state hashes. The session flow exercises real
dispatch/undo/save/load and checks that two text
catalogs do not alter refusal facts, revisions, battle state or events.

Deployment, transport, progression and turn/reaction external caller projects
compile against the new API. Their entire runtime matrices were not rerun for this
slice. Earlier complete default battle comparisons belong to f26ee926; they are not
misrepresented as a fresh full-battery run. Manual desktop input remains unverified
because the cloud accessibility route was unavailable; headless scene execution is
not visual acceptance.

## Remaining scope

Objective progress and battle conclusions still require semantic domain contracts.
Built-in content display names, remaining battle HUD/log wording and diagnostic
presentation are not fully localized. Formatting, responsibility-based API naming
and duplicated verification-project cleanup follow the semantic owner work. No
macOS launch, final release package or missing-art acceptance is claimed here.
