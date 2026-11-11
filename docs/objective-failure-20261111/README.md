# Explicit mission failure conditions

## Observed defect

At C05's actual 960×640 battle opening, the objective panel showed the two success tasks (at least two villagers survive; hold seven rounds), but not the canonical immediate loss when Laiya dies. The collapsed summary tooltip and briefing did not supply this constraint either. C01 single-task compaction also dropped its wrapper description. Nested critical objectives in C08 and C15 were similarly flattened away.

Design reference: Nintendo's official *Fire Emblem Awakening* electronic manual distinguishes ordinary unit defeat (p.24) from protagonist defeat causing Game Over (p.25). We adopt only the disclosure principle, not its mechanics or assets:
https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_3ds_2/fire_emblem__awakening_1/ElectronicManual_Nintendo3DS_FireEmblemAwakening_EN.pdf

## Ownership and repair

- Canonical objective handlers and VictorySystem still exclusively own failure evaluation and state transitions. No engine, action, condition, save or replay changes.
- Battle-text schema 4 requires an independent authored `failure: {key|literal}` for every handler-owned `critical` objective. Noncritical failure declarations, generated failure prose, absent translations and schema 3 are rejected. Success/task descriptions remain separate.
- All 21 critical nodes across 16 chapters are covered, including other-player and nested objectives. Contract tests bind each exact canonical condition to its localized wording, so a rule change requires reviewing its explanation.
- The application renders an independent read-only failure row only for visible active objectives with active ancestry. It carries no command, target location or summary hint. Existing task progress and location buttons remain unchanged.

## Verification

- Native build: zero warnings/errors.
- Objective/outcome contract checks: 289 assertions passed, including invalid metadata, all 21 authored guards, literal text, nonmutation and independent success descriptions.
- Negative control: removing only the two-line failure-row projection makes TacticalHudChecks exit 1 at C01's required independent warning-row assertion. Production was restored afterward.
- Native HUD checks: 1,246 assertions passed (strict process/diagnostic wrapper exit 0). Existing duplicate mouse-enter notification warnings remain; no errors or leak diagnostics. Checks cover C01 compaction, C02 escort, C05 protagonist, C08 Tasha and C15 mother-root; hidden/completed guards and hidden/completed ancestry do not disclose warnings. Snapshot identity is unchanged by projection.
- Actual C05 GUI at 960×640: “失败条件：莱娅阵亡” is visible above both existing tasks; 0/2 success progress is unchanged. Text fits the parchment without clipping or overlapping controls. This is a controlled level-entry fixture, not a continuous campaign playthrough. No combat action was issued.
- Full `verify-native.sh` gate on 99326db0: exit 0, zero error/failure diagnostics. All 16 AI_TRACE records and all 16 legal-action/final-state records exactly equal the existing ready-units baseline. CameraIntegration 370, HUD 1,246, CampaignNavigation 9,834 and CampaignUI 209 assertions passed. Final CampaignPlayedFlow passed 22,003 assertions: 415 legal orders across C01/C02 plus 21 in the C03 opening. This is not full-campaign human playthrough, all-chapter visual acceptance or exported-build acceptance. See `full-native.txt` and `baseline-comparison.txt`.

Only small text evidence is included. No previous withheld screenshots, credentials or personal data are included.
