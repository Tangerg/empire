# Native objective and outcome contracts

This focused external-Core check uses one explicitly authored native battle, not a
source fixture, compatibility adapter, or rewritten historical oracle. It keeps
both teams alive while a real `endTurn` command completes a composite objective.

The 43 assertions cover:

- Detached, immutable assessment facts and objective definition/fact/child projections.
- Native default-player normalization followed by actual battle admission, with no synthesized display prose.
- One objective plugin and one registered presentation formatter, including missing-formatter rejection.
- Representative composite/score facts and the four terminal/inactive statuses.
- Actual lifecycle conclusion generation, detached state/event conclusions, and strict admission.
- Campaign admission of an actual bridge result and atomic rejection of obsolete result prose.
- Schema-1 rejection and rejection of prose smuggled into current native saves.
- Full state, event, save, and replay hashes under two injected wording catalogs;
  save/load and replay reproduce the complete concluded state.

The executable takes two arguments: repository root, then `catalog.json` emitted
by the existing PO validator. Test-only wording is overlaid in immutable lookup
catalogs; the PO and production content are not modified.

The project must reference `godot/Empire.Core/Empire.Core.csproj` rather than
compile Core sources into the test assembly. Link these presentation sources:

- `godot/Localization/GameText.cs`
- `godot/Presentation/Feedback/ObjectiveText.cs`
- `godot/Presentation/Feedback/BattleTextDocument.cs`
- `godot/Presentation/Feedback/BattleText.cs`
- `godot/Presentation/Feedback/BattleLogContext.cs` (owns `FeedbackJson`)

Runner and project wiring belong to the native verification tooling owner.
