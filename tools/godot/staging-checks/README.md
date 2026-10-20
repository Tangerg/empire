# Native campaign staging parity

Generate current-source expectations and compare the pure native projection:

    bash tools/godot/staging-checks/run.sh /tmp/empire-staging-checks

Check the native screen with the same fixture (no campaign saves or external state are changed):

    source tools/godot/env.sh
    dotnet build godot/Empire.csproj
    "$GODOT_NET" --headless --editor --path godot --import
    "$GODOT_NET" --headless --path godot res://Scenes/CampaignStagingChecks.tscn -- /tmp/empire-staging-checks/oracle.json

## API and ownership

`CampaignStaging.Create(state, battleNode, level, metadata, unitTypeName, hasPendingRequest)` returns an immutable `CampaignStagingView`. Inputs are the canonical campaign snapshot/current battle node, the original catalog level, exported presentation metadata, and the composed engine's name query. It never starts, completes, exits, or resumes a battle.

`CampaignStagingScreen.ShowStaging(view, assets, startBattle, exitCampaign)` renders supplied facts inside the shared `CampaignShell`. Start and the shell's menu exit are one mutually exclusive action per binding. Repeated clicks, detached buttons, callbacks retained from a previous binding, and callbacks after unmount/remount cannot dispatch navigation. Explicit rebinding resets the one-shot action. Canonical request ownership and initial versus reentry behavior remain in the application root.

Original `StoryCampaignController.renderBattleStaging` semantics are preserved:

- `joinAfter` filters roster visibility by completed battle-history count. It does not filter by disposition; fallen/routed/unavailable entries remain visible with their original disposition text.
- Available entries show rounded HP percent and rank. Unit names come from the injected content catalog.
- Actor-key art is used only when the current battle node explicitly binds that campaign unit. Unbound entries use the selected generic unit art.
- Every campaign resource is shown in source property order. Relations show the first five nonzero entries in source order, with the source signed number and clamped relation bar.
- Initial and reentry notes/buttons are distinct. Reentry explicitly says the battle restarts from its opening. It never claims to resume an exited battle.

The application must keep the pending battle request on exit and construct a fresh battle from that same request on reentry, without applying the battle outcome or repeating campaign choices. The view does not enforce or implement that state transition.

## Export contract

The root exporter owns `godot/data/grayflag-staging.json`:

    {
      "schema": 1,
      "title": "source campaign title",
      "battleTotal": 16,
      "joinAfter": { "roster id": 3 },
      "resourceLabels": { "resource id": "source label" },
      "relationLabels": { "relation id": "source label" },
      "portraits": { "roster id": { "source": "original art URL", "resourcePath": null } },
      "levels": {
        "level id": {
          "chapter": 1, "order": 1, "chapterTitle": "source chapter title",
          "briefingId": "source briefing id",
          "chapterArt": { "source": "original art URL", "resourcePath": null },
          "briefingArt": null
        }
      }
    }

All map/method values come from `candidate01CampaignAdapter`; the native helper does not duplicate JOIN_AFTER, character names, labels, chapter mapping, or briefing selection. `briefingArt` is null when the source story intentionally has no scene; otherwise it has the same `{ source, resourcePath }` shape. A non-null resourcePath identifies a copied native resource, not a guessed path.

Source portraits, chapter backgrounds, and briefing art are used when actually exported. Missing source art is exposed through `RenderingDiagnostics`, with the original source reference. The screen uses real selected unit sprites when a portrait is unavailable, and does not substitute geometric placeholders or invented artwork. A missing source scene/backdrop remains an explicit presentation gap, not a claim of visual parity. The diagnostics are for the application/reporting boundary, not new localized source copy.

## Verification

The oracle extracts and executes the actual source `shell` and `renderBattleStaging` methods using the actual candidate adapter, art, and content. It compares visible text, roster rows, resources, relation signs/bars, and original art references, rather than copying the expected formatter.

The 45 source renders cover every battle's initial/reentry screen, JOIN_AFTER boundaries, unavailable-unit status, HP rounding, unknown-label fallback, resource ordering, first-five nonzero relations, empty state, and bound/unbound actors. Native tests verify the original text, actual texture identity, explicit diagnostics for unexported art, one-shot actions, stale callbacks, unmount, and remount.

The final export check independently compares metadata against the live adapter and verifies copied narrative PNG bytes against both the original source and declared SHA256. Native screen checks also run all 45 views with the real exported metadata and require zero missing-art diagnostics.

## Shared metadata ownership

The staging oracle still compares every original staging-owned field exactly: `schema`, `title`, `battleTotal`, `joinAfter`, `resourceLabels`, `relationLabels`, `portraits`, and `levels`. The shared export now also requires five separately owned extensions: `completionLabel`, `menu`, `menuArt`, `chapters`, and `storyScenes`. This suite validates the complete union of field names, rejects unknown or missing fields, and checks the extension value shapes; it does not discard arbitrary extra metadata.

The narrative suite owns completion labels, chapters, story-scene metadata and their original asset contracts. The campaign-slot/menu suite owns menu text and menu-art metadata. Their presence does not relax any staging projection, original asset byte/hash, or real-art zero-diagnostics assertion. The native staging scene also checks shared-shell exit accessibility and both directions of Start/Exit single-use, rebind, detached-button, unmount and remount behavior.
