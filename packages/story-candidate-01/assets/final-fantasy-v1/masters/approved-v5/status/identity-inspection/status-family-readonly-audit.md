# C01 status source-slot and semantic ledger

Native runtime: 32 × 32, anchor [16,16]. Actual HUD default display: 28 × 28. Full source canvas fits 28 × 28 with 2-pixel runtime padding, without alpha-bbox trimming.

Original boards are byte-identical to baseline 2276a89. Authored catalog order, exact manifest IDs, board visuals and baseline runtime assets establish slots.

| Topic | Label | Board / row / col | Visible source identity | Direct binding |
|---|---|---|---|---|
| C01-STATUS-01 | 中毒 | 01 / 1 / 1 | Folded off-white cloth/bandage with one olive-green poison droplet. | poisoned |
| C01-STATUS-02 | 沉默 | 01 / 1 / 2 | One plain gray metal disc with three curved sound/wave runs on each side, six total; not a mouth or crossed-out letter. | silenced |
| C01-STATUS-03 | 护卫 | 01 / 1 / 3 | One blue-gray shield with cream upward chevron and one short three-link gray chain beside it. | No direct mapping found |
| C01-STATUS-04 | 誓文受控 | 01 / 1 / 4 | One clenched tan hand/forearm wearing an ornate gold cuff with original scrolling line motif and tied brown cord/tails; no readable letters. | c01.oath-controlled |
| C01-STATUS-05 | 溃退 | 01 / 2 / 1 | One tilted fallen chevron shield on bounded dirt/stones plus two separated black bootprints. | shaken |
| C01-STATUS-06 | 隐蔽 | 01 / 2 / 2 | One deep olive hood, a fern/leaf branch and one brown boot sole. | No direct mapping found |
| C01-STATUS-07 | 风暴暴露 | 01 / 2 / 3 | One gray pawn beneath three broad curled wind assemblies and three small accent arcs, with eleven diagonal rain strokes: six left and five right. Bounded stroke shapes, no added fog. | No direct mapping found |
| C01-STATUS-08 | 受伤 | 01 / 2 / 4 | One folded off-white blood-stained bandage tied with brown cord; object-only injury cue. | armor_down |
| C01-STATUS-09 | 资金 | 01 / 3 / 1 | Four coin groups: rear-left four bronze coins, front-left one bronze coin, rear-right six visibly layered silver coins, front-right two silver coins. Coins have concentric rims and blank faces; partially overlapping stack edges are visible, not separately inferred hidden coins. | No direct mapping found |
| C01-STATUS-10 | 粮秣 | 01 / 3 / 2 | One open grain sack with visible grain and one scored brown loaf. | No direct mapping found |
| C01-STATUS-11 | 弹药 | 01 / 3 / 3 | One wooden quiver holding two visible arrow shafts, each with two pale fletching faces (four feather faces total), beside one low powder container with one metal measuring dipper holding dark powder. | No direct mapping found |
| C01-STATUS-12 | 士气 | 01 / 3 / 4 | One plain gray forked hanging banner on spear-topped wooden pole with gold ties/tassels. | inspired |
| C01-STATUS-13 | 誓约权限 | 01 / 4 / 1 | One bronze circular permission-ring motif with three attached hollow loop lobes; preserve open top/loop structure. | No direct mapping found |
| C01-STATUS-14 | 工程材料 | 01 / 4 / 2 | Timber plank bundle, one perforated L-shaped iron bracket, and one rope coil with loose tail. | No direct mapping found |
| C01-STATUS-15 | 补给 | 01 / 4 / 3 | One wooden supply crate with one corked blue-gray bottle and visible bread forms. | No direct mapping found |
| C01-STATUS-16 | 地方支持 | 01 / 4 / 4 | One small cottage surrounded by six wooden pawn figures. | No direct mapping found |
| C01-STATUS-17 | 占领目标 | 02 / 1 / 1 | One gray forked flag on a concentric round stone occupation platform. | No direct mapping found |
| C01-STATUS-18 | 护送目标 | 02 / 1 / 2 | Two tall blue-gray cream-chevron shields flank one wooden wheel. | No direct mapping found |
| C01-STATUS-19 | 撤离目标 | 02 / 1 / 3 | Two opened wooden palisade gate leaves frame seven receding black bootprints; each print contains sole and heel shapes. Original wooden uprights, iron straps and rope lashings remain functional parts. | No direct mapping found |
| C01-STATUS-20 | 救援目标 | 02 / 1 / 4 | Two bracered human hands protect one lit lantern above broken iron-bar fragments. | No direct mapping found |
| C01-STATUS-21 | 维修目标 | 02 / 2 / 1 | One stone-headed hammer against block-stone wall pieces reinforced with iron plates. | No direct mapping found |
| C01-STATUS-22 | 破坏目标 | 02 / 2 / 2 | One broken wooden/bronze gear with missing rim section and one separate gray shaft. | No direct mapping found |
| C01-STATUS-23 | 守时目标 | 02 / 2 / 3 | One hourglass tied beside one ragged gray flag on a wooden pole. | No direct mapping found |
| C01-STATUS-24 | 证据送达 | 02 / 2 / 4 | One sealed glass document tube with original wax seal, one right arrow and three linked blank identity tags each bearing one person-bust silhouette. | No direct mapping found |

Boundary corrections: Atlas01 white grid excluded from all complete-subject references. Atlas02 status23 nominal cell includes part of status24 document tube; complete23 x889..1291 and complete24 x1294..1772 preserve each actual subject without contamination.

Semantic mismatches: C01-STATUS-08 authored label is injured/blood-stained bandage, while actual armor_down binding uses it. Preserve canonical original art identity unless parent explicitly changes contract. C01-STATUS-12 authored morale banner is used for inspired. C01-STATUS-03 guard/shield-chain is cataloged but not directly bound by CANDIDATE_01_STATUS_ART; do not infer it replaces guard mechanics.

No direct mapping found is not a claim of absolute non-use. Generic lookup availability does not establish an actual consumer.
