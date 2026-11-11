# Explain weapon coverage before targeting

## Actual C10 decision gap

At 960×640, enter C10, select Ivra and open “兵种与能力”. The complete “盟约龙息” weapon entry showed damage, range, stock and cooldown, but did not say that its canonical `line` shape covers cells from the attacker to the chosen target. Her unit biography did not explain it either. Players could not discover this positioning property from the weapon dossier before choosing an attack.

## Repair and ownership

- Every weapon now has a directly readable “作用范围” row immediately after its ordinary weapon row. This is not tooltip-only information and is not an actionable command.
- The row reads the weapon definition's canonical `area`. Content text metadata maps registered area identities to localized explanations; no weapon-ID special case or shape-rule switch was added to the HUD.
- Content text metadata is now schema 2, with exact `areaShapes` coverage against the consumed engine registry. Missing/unknown mappings and missing translations fail at composition. Registered extension shapes can supply their own descriptions. This changes presentation metadata, not the game-save schema.
- The line description explicitly stops at the target and excludes the attacker. Grid-neighbor coverage and the authored 3×3 region are described separately; none of these descriptions recomputes affected cells, damage, allegiance or hit results. `WeaponAreaRegistry` remains the gameplay authority.

## Verification

- Build: zero warnings/errors. Native import, native format and diff whitespace checks pass. Catalog ownership: 76 assertions pass.
- Text contracts: 297 assertions, including missing/extra area metadata, missing translations, old metadata schema rejection, custom registered shape support and accurate built-in descriptions.
- Negative control: with the new metadata/API and regression but without the production HUD row, strict TacticalHudChecks exits 1 at the directly readable weapon-shape assertion.
- Final HUD: 1,300 assertions. Actual C10 Ivra, Tasha and Torren selection projections verify line, neighbor-area and single-cell descriptions, row adjacency, no tooltip dependency or order intent, and unchanged battle state.
- DossierFocus: 165 assertions, including native reading/navigation and real-session adjustments. Native BattleScreen real-session integration also passes. All three native scenes finish with strict process/diagnostic exit 0.
- Actual application restarted and the same isolated C10 opening reloaded. The dragon-breath description is fully readable in the 960×640 dossier: “自身至目标的连线地格，不含自身、不越过目标”. No attack was issued for this information-only GUI review.

This is detached chapter-entry evidence, not prior campaign victories or a completed C10 run. No attack preview, coverage algorithm, command, combat state, save contract or oracle baseline changed. The full 16-chapter gate was not rerun. Only small text evidence is committed; private saves/screenshots and previous withheld evidence remain excluded.
