# Replaceable spatial contract checks

This isolated migration slice composes the source ten-method spatial policy around the existing C# movement and vision owners. It does not introduce another implementation of movement, target eligibility, or fog rules. It does not by itself finish the engine composition-root migration or establish arbitrary plugin parity.

Run with the existing official .NET toolchain, without TypeScript or Godot:

```sh
bash tools/godot/spatial-extension-checks/run.sh
bash tools/godot/spatial-extension-checks/run.sh --verify-guard
```

`EMPIRE_FIXTURE_ROOT` and `EMPIRE_ENV_FILE` permit testing an isolated source copy against the unchanged fixture repository and existing toolchain. Builds and mutations are confined to new temporary workspaces. No application saves, installs, network calls, or GUI are used.

## Contract and rule ownership

| Original TacticalSpace | Native ITacticalSpace | Default rule owner |
| --- | --- | --- |
| moveField | ComputeMoveField | MovementRules.ComputeMoveField |
| pathTo | PathTo | MovementRules.PathTo |
| threatOf | ThreatTiles | MovementRules.ThreatTiles |
| controlZoneAgainst | HostileControlZone | MovementRules.HostileControlZone |
| attackTargets | AttackTargetCoords | MovementRules.AttackTargetCoords |
| healTargets | HealTargetsFrom | MovementRules.HealTargetsFrom |
| board | Board | MovementRules.Board |
| visibleTiles | VisibleTiles | BattleVision.VisibleTiles |
| isUnitVisible | IsUnitVisible | BattleVision.IsUnitVisible |
| visibleUnits | VisibleUnits | BattleVision.VisibleUnits |

DefaultTacticalSpace delegates each method directly to those owners. Its inherited threat does not call a subclass's move-field override, inherited visibility does not call overridden sibling queries, and inherited targeting does not use an overridden board. These are intentional source semantics, not missing propagation. A policy that changes several operations must override those operations coherently.

MoveField is extension-constructible plain mutable data, as in the source. Tiles, tile objects, and an explicitly supplied stop set retain identity; a custom stop set is not silently derived from tile occupancy. The default computation still owns its ordinary field/stop construction.

`TacticalCommandRules` names the command consumer's distinct catalog, grid, and spatial collaborators. Authoritative movement asks the resolved space for its field and canonical path; command facing uses the separately resolved grid. Attack/heal menus and accepted command validation ask the same ability policy. Battle remains the sole transactional writer.

Transport, recruitment, forced movement, and parting-shot geometry use their explicit catalog/grids; scenario battlefield reads use their existing catalog without an unused grid dependency, matching the source's ordinary `GridRules` dependencies. They do not grow the spatial interface with content, battlefield, or control-radius helpers. The existing control-radius arithmetic remains one static MovementRules owner shared by default movement and parting shots.

Presentation board/cursor geometry uses the spatial board. Selection/HUD visibility calls the explicit spatial IsUnitVisible predicate, while overlays share VisibleTiles with that predicate, matching game-ui/game.ts rather than accidentally bypassing an override through inherited VisibleUnits. HUD rule queries (commander aura and facing metadata/choices) use resolved grids, matching game-ui/hud.ts. Incoming-cast overlays use its board, as `packages/game-ui/src/ui/game.ts` does; authoritative combat geometry and AI board geometry use resolved grids. The tactical threat overlay forwards its computed movement field into the threat query, matching GameSession.threatOf. AI threat calls the spatial threat query without a supplied field, matching ai/threat.ts.

## Source evidence and coverage

Source contracts: `packages/battle-engine/src/tactical-space.ts`, `movement.ts`, `vision.ts`, `actions.ts`, `abilities.ts`, `zone-of-control.ts`, `plugins/default.ts`, `session.ts`, `ai/default-intents.ts`, `ai/threat.ts`, and `packages/game-ui/src/ui/game.ts`. The pacifist menu/authoritative-refusal case ports `engine-extension-seams.test.ts`.

Runtime checks cover:

- All ten public methods, subclass replacements, and a replacement implementing only ITacticalSpace.
- Attack/heal menus, direct attack query, authoritative rejection, and rollback.
- Accepted custom movement, canonical-path forwarding, and independently authored stopping restrictions.
- Default sibling independence for movement/threat, visibility, and board/targeting.
- Mutable field, tile, and stop-set identity.
- A kernel install-time consumer receives the final replacement.
- Separate producer and spatial consumer installers declare the grid dependency. The spatial installer receives the final registry replacement, preserving exact identity and live registry semantics before engine sealing. The stale TypeScript local capture is an intentional native delta, covered in the engine-composition suite.
- 36 committed original-source movement records, 72 retained source visibility records, and 12 retained source incoming-cast records.
- Application/AI interface edges and source threat-field forwarding.

Seven compiled behavioral mutants must fail (virtual sibling calls, discarded canonical path, bypassed target policy, copied field identity, recomputed stops, and resolved-registry snapshot). Three deliberately broken application edges (threat-field forwarding, explicit visibility predicate, and HUD resolved-grid queries) must fail their architecture guards.

## Frozen visibility provenance

`godot/tests/oracle/spatial-visibility.json.gz` contains only the `cases` and `casts` projections extracted from three byte-identical retained outputs of the existing `vision-checks/oracle.ts`. Their complete original SHA-256 is `faafe48b9e9bacd0c49157eb675750ed53179fa15a10f0c643b86ff8ea78b98b`. Original generation commit metadata was not embedded in those outputs; this is recorded evidence, not newly regenerated evidence.

At audit, both source files were unchanged at repository HEAD `16e05c4d`; their most recent relevant commit was `a68c5737043e8c9b9cc2bd6a3869040c67343682`:

- `packages/battle-engine/src/vision.ts`: SHA-256 `58179a25e5772410f1a9622c3d9ec41e0a15e32d2ab77e68ea24eb3bd6995275`.
- `tools/godot/vision-checks/oracle.ts`: SHA-256 `2ec9a373acd95c77d072f5dc7e822e9d4bcc4c3e05a9bd13a514cfe4243658ab`.

The retained catalog equals the committed tactical-query fixture catalog. The retained schema matches the current generator: three grids × two fog settings × four variations × three viewers = 72 cases, and three grids × four cast clocks = 12 cast cases. TypeScript was not executed while preparing this slice.

## Remaining integration boundary

The coordinated AI planning-port patch must supersede the provisional AI edits: source AI traces parent links directly from the returned MoveField instead of calling the overridable spatial PathTo. That shared trace extraction and its override-independence check belong to the AI slice.

The production engine root must install this contract through the migrated kernel and provide its final resolved instance to every consumer. The isolated constructor migration prepares those edges; it is not a second engine assembly path or a compatibility alias. Full native/GUI checks and all-level replay checks must be rerun after final root integration. Existing native integer-domain and malformed-document restrictions still apply.

## Isolated verification, 2026-10-20

The final spatial implementation applied without conflict to a current foundation snapshot and passed pure-.NET checks, with no TypeScript execution or Godot/native build:

- 456 spatial checks and all 10 deliberate regression guards.
- 14 core checks, including three recorded replays totaling 396 actions.
- 161 tactical checks.
- 2,398 application tactical-adapter assertions and 7,429 HUD assertions against recorded source fixtures.
- All 16 independent GreyFlag battles, totaling 2,983 actions, with exact chosen actions, ordered events, and state hashes.

These results precede the coordinated engine-root, AI-port, and combat-policy integration. Re-run affected checks on the combined tree rather than treating isolated passes as combined coverage.
