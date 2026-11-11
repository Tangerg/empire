# C08 objective wording matches the actual victory rule

## Actual defect

At 960×640, enter C08 through its briefing, choose the ledger contract, and start battle. The visible task read “击退抢粮者并保护三座粮仓 · 剩余敌军 9”. That made protecting three granaries sound like a victory requirement, although the canonical objective is `routEnemies`, wrapped by Laiya and Tasha death conditions. There is no granary-defense objective or granary-loss failure rule. The briefing itself correctly describes protecting Tasha and defeating the raiders.

## Change

The localized leaf task now says “击退全部抢粮敌军”. The canonical remaining-enemy count and both critical-character failure warnings are retained. No combat rules, scene geometry, saves, campaign branches, enemy counts, rewards, or content-oracle baselines change. The obsolete authored source label is not used as a second presentation owner; the supported localization catalog remains the player-facing owner.

## Verification

- Build: zero warnings/errors; native format and diff whitespace checks pass.
- Negative control: with the new test and original translation, strict TacticalHudChecks fails specifically at the C08 task wording assertion. The corrected translation passes 1,256 assertions and the strict process/diagnostic boundary. Existing duplicate mouse-enter warnings remain; no error/leak diagnostic.
- The regression checks the actual `routEnemies` leaf and exact localized wording, the task's canonical progress without invented target cells or commands, Laiya's warning alongside the already-checked Tasha warning, and unchanged session state.
- Catalog ownership boundary: all 76 external ProjectReference assertions pass.
- Native editor import succeeds. The actual application was restarted and the same isolated C08 opening reloaded: “击退全部抢粮敌军 · 剩余敌军 9” and both “失败条件” rows are visible inside the 960×640 HUD.
- A temporary local exporter used the existing campaign construction and unchanged production loader to produce only a detached C08 briefing. Its exporter-only change was restored before testing the final build. In the actual GUI, briefing, ledger choice, staging, and battle entry were performed normally. No earlier campaign victories are claimed. Private fixture saves and screenshots are excluded.

This slice does not claim full C08 combat completion, balance acceptance, or a new full 16-chapter gate. During the same GUI review, a separate existing missing-scene-resource diagnostic was observed wrapping into a narrow vertical column at the left edge; that layout issue is not fixed or hidden by this patch.
