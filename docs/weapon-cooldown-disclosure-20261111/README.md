# Disclose weapon cooldown before first use

## Actual C09 decision gap

The C09 briefing explicitly asks the player to plan explosive cooldowns. In the actual 960×640 battle, select Tasha and open “兵种与能力”, then scroll to her weapons. Before first use, the original “破门火药” line showed blunt power 56, range 1–2 and unlimited stock, but no cooldown. The canonical `c01.satchel-charge` definition has cooldown 3. The old projection only displayed a positive runtime cooldown after it had already been incurred.

## Repair

Every weapon row can now show two separate localized facts:

- “使用后冷却”: the positive cooldown from its weapon definition.
- “当前剩余冷却”: the positive current value from that actor's weapon runtime state.

Ready zero-cooldown weapons remain uncluttered. Existing stock, requirements, costs, damage, range and icons are unchanged. This is read-only disclosure: no cooldown schedule, combat preview, command eligibility, save schema, campaign rule or engine ownership changed.

## Verification

- Build succeeds with zero warnings/errors; native format and diff whitespace checks pass. Catalog ownership: 76 assertions pass. Editor import succeeds.
- Negative control retains the new tests and localized strings with the original production projection. It exits 1 specifically at the missing fixed three-turn cooldown assertion. An earlier fixture-setup failure was corrected to use the real selection controller and is not counted as the negative control.
- Final TacticalHudChecks: 1,272 assertions, strict process/diagnostic exit 0. Existing mouse-enter warnings remain, with no error/leak diagnostics.
- C09 Tasha read-model probes inspect remaining values 0, 3, 1 and 0. They require the definition's fixed three-turn cooldown throughout, separate current remaining values only when positive, no cooldown wording on the ready hammer, and an unchanged session snapshot. These are isolated projection probes, not a claim of a played four-stage cooldown sequence.
- Actual application restarted, same isolated C09 opening reloaded. Tasha's weapon line now visibly includes the complete “使用后冷却 3 回合” before any attack. It wraps readably inside the 960×640 dossier; the hammer remains without a cooldown warning.

The GUI entered C09 from a production-loader-validated detached briefing, selected the engineering branch and began battle normally. No prior victories, full chapter completion, or full 16-chapter regression are claimed. The temporary local export-only harness was restored before final builds. No private saves/screenshots or previously withheld evidence are committed.
