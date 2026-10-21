# Native campaign staging checks

Run `bash tools/godot/staging-checks/run.sh`. The default gate uses frozen original rendered expectations and current native schema-2 metadata, not live TypeScript regeneration. It compares 45 views, immutable rows, explicit actor bindings and original PNG hashes. The check-only catalog JSON is disposable; the shipped runtime loads native PO.

`CampaignStaging.Create(text, state, battleNode, level, metadata, unitTypeName, hasPendingRequest)` returns an immutable view. The explicit `GameText` service localizes shared campaign/chapter titles and shell action/accessibility labels. The chapter table is their single owner; levels contain chapter IDs, not repeated chapter titles. The root still owns canonical request/start/reentry/outcome transitions.

Staging-specific roster/status/resource/relation/level labels and action notes are not yet migrated to PO. Neither this suite nor the narrative slice claims full staging or game internationalization. Genuine user-authored names remain literal data; built-in content names will be resolved at their presentation boundary rather than stored as translated core facts.

`CampaignStagingScreen.ShowStaging(view, assets, startBattle, exitCampaign)` renders those facts inside the shared shell. Start and Exit are mutually exclusive per binding. Stale, double, detached, unmounted and remounted callbacks cannot navigate without an explicit new binding. Reentry still means restart from the same pending request, not resume.

Native source documents own presentation metadata. `export-grayflag.ts` no longer writes them; it only copies declared, hash-verified narrative artwork. The shared schema has exactly `schema`, `titleKey`, `completionLabelKey`, `menu`, `menuArt`, `chapters`, `storyScenes`, `battleTotal`, `joinAfter`, `relationLabels`, `resourceLabels`, `portraits` and `levels`. `validate_narrative.py` rejects missing/extra fields and obsolete duplicated chapter labels. There is no schema-1 fallback.

The frozen oracle still checks original roster filtering, HP rounding, disposition text, resource ordering, first-five nonzero relations, signs/bars, actor bindings and artwork. Native tests separately clone schema-2 metadata with unavailable art to check diagnostics and real unit-sprite fallback, then run all 45 cases against actual exported art. Missing-art diagnostics remain English diagnostic data and are still displayed by the existing missing-art widgets. A localized, player-safe artwork-error message is outside this slice.

Latest pure run passed 491 comparisons. Native scene is `res://Scenes/CampaignStagingChecks.tscn`, taking a decompressed `godot/tests/oracle/staging.json.gz` path after `--`. Run native builds/checks in the shared sequential verification lane with fresh user-data directories. Headless rendering is not physical GUI acceptance.
