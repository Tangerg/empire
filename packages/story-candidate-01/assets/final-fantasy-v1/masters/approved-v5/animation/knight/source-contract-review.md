# Knight source identity and scale contract investigation

Source investigation completed. Coordinator authorized the original foot knight redraw on2026-10-19 at15:36:35UTC. The mounted/lance gameplay mismatch remains documented and is not fixed by this art task.

## Exact original contract
- Topic: C01-UNIT-KNIGHT; category combat-unit; consumers c01.knight and built-in knight.
- Explicit campaign binding: candidate-01-bindings.ts:15. Built-in binding: same file:55.
- Runtime strip128x48, four32x48 frames; runtime-HD strip256x96, four64x96 frames.
- Order: idle, walk-a, attack, walk-b. Manifest anchor16,47; footprint1x1.
- Original1254x1254 atlas: combat-atlas-ranger-assassin-mage-knight.png, fourth row. Source-row crop[0,930,1254,1254] and firstcell[0,930,314,1254] are preserved, without resampling or alpha edits.

## Actual original visual
Unmounted bearded adult in rounded segmented steel helmet, cream/tan riveted armor with red piping over chainmail, brown gloves and belt, brown cuffed boots with small spurs. Short silver straight sword in screen-left glove, pointed oval olive-brown shield with steel/brass rim and worn gold branching decoration in screen-right arm. No horse, reins or mounted body is present in any knight frame.

## Scene position
In c01-02, bridge-knight is owner1, facing east, cell(3,7). The map is22x14 cells /704x448 world pixels. Its32x32 cell has world bounds[96,224,128,256]. Deployment ordering is explicit in levels.ts:399-403,440,445 and place():822-837; the existing whole-level-layout.json independently records the same position.

## Distinct cavalry identity
C01-UNIT-LANCE-CAVALRY is a separate canonical topic: runtime256x48, four64x48 frames; HD512x96, four128x96 frames; anchor32,47; footprint2x2. Its original source is the second row of combat-atlas-ballista-cavalry-battlemage-eagle.png. Rider is a clean-shaven imperial soldier in navy/red lamellar, angular steel helmet, cream gold-strip shield, and long spear. Mount is a brown horse with dark mane/tail, cream nose marking, bridle/reins, saddle and navy/red barding. No campaign unit-art mapping to this topic is present; knight-charge maps to an EQUIPMENT topic only.

## Semantic mismatch for issue ledger
c01.knight units.ts:67 is tagged mounted/cavalry and has c01.knight-lance + c01.knight-charge, while its explicit stable artwork is the foot sword/shield identity above. This is a gameplay/art semantic mismatch. The available manifest/source/binding evidence does not establish that the mapping is erroneous or authorize remapping to the separate cavalry topic.

## Recommended first-frame contract
Preserve C01-UNIT-KNIGHT original unmounted identity and use the approved chunky human44logical body-height direction. Record the gameplay/art mismatch separately. Do not invent a horse or silently merge the distinct cavalry identity. If actual cavalry is later commissioned, establish a separate rider+mount source-scale/footprint contract rather than using infantry44height for the whole horse.

## Review files
references/source-idle-cell.png: original high-resolution knight first frame
references/runtime-hd-strip.png: exact current four-frame HD strip
references/comparison-cavalry-source-row.png: distinct rider/mount original source row
source-evidence.json: paths, sizes, hashes, frame bounds and source references
