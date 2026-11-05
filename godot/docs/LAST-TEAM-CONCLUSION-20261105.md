# Neutral last-team battle conclusions — 2026-11-05

## Actual defect and boundary

At baseline `cc60df6675899cb082a33afa9edc3f9507aeb5ca`, the formal second battle (`c01-02`) could display **战败 / 敌军已被全歼** after a critical escort failure.

The reproduction resumed an unchanged native autosave produced by an actually played first victory, used natural Tab/Enter through the aftermath and accept-truce choice, and started the second battle normally. The human only ended turns with native E; the normal AI and automatic animation continued without injecting damage, outcomes, or terminal state. On turn 9, `refugee-3` died, the other two refugees and Laiya survived, and nine human units remained. The authored critical objective failed, player 1 was defeated, and the canonical conclusion was `lastTeamRemaining`, team 2. One result view appeared; neither save slot was automatically overwritten.

The first exploratory probe subsequently failed in its own notice-dismissal loop because an awaited operation freed a node in the saved child list. That log remains failed, and it does not prove Retry or clean shutdown. The probe was corrected using `is_instance_valid` before reading a potentially freed node. No production lifecycle/resource code was changed for that probe error.

## Ownership and repair

`VictorySystem` already correctly owns critical-objective failure and undefeated-team counting. `BattleText` projects its semantic conclusion; `BattleEventText` gameOver feedback and `CampaignBattleSummary` use that same projection. The Chinese catalog incorrectly assumed an enemy annihilation from the human perspective.

The only production change replaces `battle.conclusion.last_team_remaining` with **仅剩一个阵营未被击败**. The result heading continues to express victory or defeat. This avoids claiming unit annihilation or guessing a more specific failure cause that the conclusion does not carry. Battle rules, content, map artwork, and save/replay facts are unchanged.

## Regression coverage

- Two native-domain boundary cases each dispatch a legal endTurn, with a critical objective defeating player 1 or player 2 while both teams still have units. They require the failed objective, correct last team, exactly one gameOver, independently expected Chinese wording, and injected alternate-catalog wording without mutating state.
- Existing native victory/defeat result fixtures require their perspective-specific headings and neutral descriptions in the screen and campaign summary. These fixtures remain explicitly synthetic UI checks.
- The existing actually played AI-defeat route additionally checks consistent neutral text on the result screen, final gameOver feedback, and campaign summary.
- The new semantic check fails against the unchanged old catalog with exit 134 at the intended last-team wording assertion, then passes against the corrected catalog: 152 assertions, exit 0. The failure is deliberate test evidence, not a production crash.

## Verification status

Validated source: `615db6db7b2a7e6f0e942eb7d469fdc4cfe6aec4`.

- Repeated the complete actual C02 passive-player route in fresh temporary user data: 32 checks, nine native human end-turn inputs, correct result wording, one result view, exact carried opening after native Retry, and both save slots preserved. Strict native process exit 0. The complete terminal save equals the baseline after excluding exactly the campaign and battle `savedAt` timestamps.
- `bash tools/godot/verify-native.sh`: one uninterrupted run, 2026-11-05 18:58:08–20:02:03 UTC, **3835 seconds, exit 0**. This includes format/source/map checks, main/core builds, 16 core groups, objective/catalog contracts, editor import, and 31 native scenes. All 32 native process logs passed their diagnostic boundary; 40 existing warning headers match the preceding successful gate, with no added warnings.
- All 16 formal levels' 3425 legal actions retain identical ordered action/event/receipt SHA-256 traces and complete terminal state hashes. Only localized presentation prose intentionally changes.
- Real campaign regression: ordinary AI defeat and Retry; actual first victory after 156 legal orders; actual second-battle escort victory after 259; then 21 legal orders in the third opening. The expected carried casualty, resource/growth state, and campaign digests remain unchanged. The newly checked real defeat feedback is neutral across all three shared surfaces.
- `dotnet build godot/Empire.csproj -c ExportRelease --disable-build-servers -m:1 -p:UseSharedCompilation=false`: exit 0, no warnings or errors. This is compilation, not a packaged export.

The supplied first-victory autosave was unchanged (SHA-256 `2d51c3b7eab10fcde8e8447f86e6a8a176b73477f1108a8f1beb85c24e412dea`), produced by the earlier actual first-battle play and native settlement. The C02 probe resumed that outcome; it did not claim to repeat the first battle itself. The full aggregate separately played both battles in one process.

## Limits

Native headless behavior is not rendered visual acceptance. This change does not claim complete-campaign acceptance, packaged export acceptance, or a fix for the previously unexplained intermittent shutdown failures. It does not change or audit unrelated saved-event admission behavior.

## Relevant Godot guidance

- [Internationalization and imported translations](https://docs.godotengine.org/en/stable/tutorials/i18n/internationalizing_games.html): keep translatable wording in the project catalog and reimport changed translations.
- [Object lifetime](https://docs.godotengine.org/en/stable/classes/class_object.html): a reference may remain non-null after an object is freed; the exploratory GDScript uses the documented validity check.
