# C01-CH01-S02 — story-grounded semantic reconstruction

## Verified identity and inherited mismatch
- Topic label: 三桥停火 (Three Bridges Ceasefire), original narrative index45
- The exact legacy original maps to scene-board-04.png, bottom-left, inset box[5,632,622,1249], board SHA2567642d75dd7dbd1892a5802cf62657bd9475e5d700ef7b9d177d463c877160670
- The actual617×617 original was inspected. It depicts a dark cobbled ritual gathering with five amber-lit stone pylons and connected fine amber light threads. It has no bridge, river, opposing banks or three-civilian escort. The map is accurate, but its scene semantics conflict with the authored story. It is preserved under original-rejected-for-story-context and explicitly NOT claimed as an accurate recovered bridge scene.
- story.ts:92–104 unambiguously names White River Three Bridges, year0 midsummer, archers stopping fire on the central bridge, and three civilians reaching the east bank. The aftermath describes both sides guarding separate banks and an injured child's transfer. Named speakers are Cain and Laiya. No amber/control-pylon cue appears.
- Original text with line numbers and full source hash is retained in story-source-context.txt and semantic-reconstruction.json. No rules, selectors, story prose or source code was edited.

## Chosen scene and source roles
An explicit story-grounded reconstruction corrects the inherited mismatch. The single chosen moment is three civilians crossing the central bridge under the two commanders' temporary ceasefire. It does not collage the injured-child aftermath into the same time.
- Approved arch-three-bridges/master.png supplies the White River/three-crossings setting and style
- Approved char-leia-01/master.png supplies young Laiya's chestnut braid, face, blue-gray armor and boots, excluding its later gray flag
- Approved char-cain-01/master.png supplies Cain's clean-shaven face, navy/gold helmet and armor, burgundy scarf/cape; generic guard shields use that approved faction equipment
- Approved ch01-s01/master.png supplies early-chapter matte style/scale
- The wrong old pylon plate was not passed to image_gen as a scene reference
- No flag or writing appears; no gray-flag chronology was imported

## Generation and versions
- Built-in image_gen, two attempts, both successful
- V1 had clear opposing banks, exact three-civilian escort, approved identities and three crossings, but mobile center-cover omitted both officers. Its full source, prompts, hash references and QA are archived under rejected-v1
- V2 is a single local regrouping edit. The wide environment remains intact. Officers move closer to the three civilians; no people are added/removed. The compact group remains readable in landscape
- Selected exact request: request-v2.json/prompt-v2.txt/reference-hashes-v2.json. Initial request.json/prompt.txt/reference-hashes.json remains for reproducibility
- Selected whole native source is1672×941 RGB. raw.png, native.png and master.png are byte-identical. SHA25638ff98456bea1116ebcc1a20afeba0d0e35f0a3f723725e7cd18af554bfa7b01
- Explicit formal crop[4,2,1668,938] is1664×936 exact16:9. This tiny edge crop is only for export; the authored master is preserved whole
- No programmatic pixel repaint, alpha extraction, recoloring, sharpening, compositing or artwork change occurred

## Visual QA actually performed
- Native + exact512×288 + canonical256×144: exactly three unarmed travelers cross right/east (elder with stick; cream-hood adult; smaller brown-hood young traveler). Two named officers remain distinct, with separate guarded banks and a clear stone arch/blue river
- Laiya is chestnut-braided and young, with slate-blue practical armor. Cain is clean-shaven in the navy/gold-banded helmet/armor and burgundy scarf. No legacy generic blond/black-hair substitution
- No fighting, flying arrows, gore, injury subplot, oath lights, pylon or invented flag
- Actual desktop, staging inset and mobile staging cover geometries retain the complete five-person main group
- Narrow390×778 centered story cover retains all three civilian silhouettes, the bridge arch and river, and both officers' identifiable faces. Laiya's left-side braid/body edge and Cain's outer cape edge are cropped. This is honestly retained as a mobile limitation rather than claiming all five full silhouettes survive; v2 improves on v1's complete officer omission without sacrificing the wide story scene
- Title-mobile cover is a labeled stress test only; S02 is NOT a title/menu consumer
- The scene is intentionally opaque, not a reusable transparent sprite

## Limits
All QA is offline exact-export/crop inspection. Display filters, story overlay/text and actual browser rasterization are not simulated. No live-game/browser verification, formal binding, exporter run, repository edit or Git operation was performed by this worker. Parent owns selection, integration and final acceptance.
