# Native UI cohesion verification — 2026-10-27

The accepted tactical rail layout is retained. Campaign menu, story, staging, results and system popup share warm leather/wood/metal materials, text and native control states. Staging uses the same registered portrait crop owner as tactical UI, while retaining explicitly authored narrative image sources. Its footer lives outside the scroll viewport.

Input lifetime changes: story/dossier retain generation checks; initial campaign focus cannot survive removal or steal established focus. System popup actions hide immediately but dispatch after the native Window's input callback, with binding/session/idle checks and one pending action per binding. Restart retains its confirmation and defaults to Cancel. No battle rules or campaign/save facts changed.

Verified final build: zero warnings/errors. Focused checks: CampaignUiFlowChecks 76 assertions; TacticalHudChecks 203; AnimationChecks 183; CampaignStagingChecks 155; native BattleScreen integration; narrative isolated runner 3306; campaign-slot isolated runner 228; narrative validator 37 unit tests plus 431 keys / 35 stories / 91 beats / 11 choices. Source-resource presentation validation passed. HUD fixture emits pre-existing duplicate mouse-entry warnings; BattleScreen fixture intentionally emits its missing-art diagnostic. These are not a claim that the aggregate repository suite passed.

Native C07 verification at 1180x812 and 960x640: menu/story/staging/system, selected HP, restart confirmation and cancellation, popup-to-camp navigation, completed battle and scrollable summary. Only copies of the existing T7/T13 saves were used. Summary shows 1/16 because this isolated campaign evidence contains one committed victory; no C08 was entered. At 100% and 125% camera zoom the three targeted source cleanups remain legible. Actual guard movement frames retain the support plane. Evidence outside repo: `empire-final-evidence/ui-cohesion-20261027/`.

Performance group is separately verified in `PaintedSurfaceBroadPhaseChecks`: 8129 exact original/optimized comparisons, unchanged clip/order/UV/picking output. Real C07 diagnostic surface work fell from 307/356 ms to 117/97 ms in the recorded pairs; this is not an FPS benchmark. Existing broad aggregate surface-cliff and Oath walking-bob fixture failures were reproduced on original code, not concealed as new passes.

Feature branch has no automatic CI trigger (workflow is main/PR/manual); local checks are not CI results. No package delivery is needed.
