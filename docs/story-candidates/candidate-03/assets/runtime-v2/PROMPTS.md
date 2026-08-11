# 《布衣定鼎》运行时 V2 首批母图 Prompt

生成模式：Codex 内置 ImageGen。所有母图均为本项目原创生成，完整母图保存在 `masters/`；透明素材先使用纯色色键背景，再由 imagegen 技能随附的 `remove_chroma_key.py` 去背。后续切图、量化、锚点和无缝边缘处理由 `process_runtime_v2.py` 确定性完成。

风格参考（仅用于同项目风格匹配）：

- `../characters/shen-li-22-portrait-hd.png`
- `../units/han-yue-walk-sheet-hd.png`
- `../architecture/huai-right-bank-dike-hd.png`
- `../scenes/rain-night-crossing-hd.png`
- `../props/story-props-sheet-hd.png`

共同美术方向：低奇幻历史题材、东亚古代基层军政与河工语汇、高清像素画；泥土褐、湿靛蓝、暗铁、旧木与粗麻为主色；材质分层、轮廓明确、无现代器物、无文字、无 UI 框、无水印。

## 1. 四兵种四帧母图

Create a clean 4 columns by 4 rows pixel-art sprite atlas on one perfectly flat vivid chroma green background (#00ff00), no grid lines, no labels, no shadows on the background. Low-fantasy historical East Asian county-war setting, high-detail HD pixel art matching grounded late-imperial militia assets. Palette: earth brown, wet indigo, dull iron, aged wood, coarse hemp. Each cell contains one full-body character with generous clear green padding, consistent scale and feet baseline, no cropping. Exactly four distinct unit rows: row 1 county saber-and-wooden-shield infantry; row 2 long-spear line infantry; row 3 rural bowman with bow and quiver; row 4 river engineer/militia laborer with measuring pole and shovel. Exactly four animation columns in each row: idle A, step A, idle B, step B. Same character design across a row, clearly different poses between columns, readable weapons and silhouettes at small size. Three-tone material lighting, crisp hard pixel clusters, no painterly blur, no text, no emblem letters, no magic, no extra people, no decorative border.

## 2. 八类地形纹理母图

Create an exact 4 columns by 2 rows atlas of eight full-bleed square HD pixel-art terrain texture swatches for a grounded historical East Asian river-war tactics game. No characters, buildings, labels, borders, grid lines, text, magic, or modern objects. Every cell is filled edge to edge with one strongly distinct material, viewed top-down/three-quarter game-map style, with crisp pixel clusters and three-tone material lighting. Top row left to right: deep blue-gray flowing river water with ripples; shallow stony ford with visible wet stones; compact earth-and-stone dike crest; violently breached dike with broken soil and rushing muddy water. Bottom row left to right: flooded rice paddy with reflective channels; mature golden-green rice field; muddy rutted road; gray fitted-stone road. Keep a consistent subdued palette of wet indigo, silt brown, moss green, rice gold, and old gray stone. Each swatch must have material detail across the whole cell and enough local texture to derive seamless 32-pixel game tiles.

## 3. 两建筑三状态母图

Create a precise 3 columns by 2 rows HD pixel-art game structure atlas on one perfectly flat vivid chroma green background (#00ff00), no grid lines, no text, no labels, no cast shadows on the green. Grounded historical East Asian county/river defense setting, isometric-ish three-quarter tactics-game view, aged timber, gray tile, old stone, coarse sacks, iron fittings, crisp pixel clusters. Row 1 is the same county granary in three states left to right: normal and stocked; visibly damaged with burned/broken roof and scattered sacks; captured but intact with a separate prominent cloth banner and guards. Row 2 is the same sluice gate and bridgehead in three states: normal working winch and gate; damaged collapsed masonry/timber with turbulent water; captured intact with separate cloth banner and guards. Keep the exact same footprint, camera, scale, and anchor within each row. Every cell must contain a complete uncropped building with generous green padding. Material-rich, strong silhouettes, no fantasy ornament, no modern objects, no decorative border.

## 4. 装备与技能图标母图

Create an exact 4 columns by 2 rows atlas of eight square HD pixel-art game icons on one perfectly flat vivid chroma green background (#00ff00). No borders, no UI frames, no labels, no letters, no shadows on background. Grounded historical East Asian tactics setting, crisp chunky pixels, strong silhouette first, three-tone material lighting, old iron, wood, hemp, leather, silt brown and muted indigo. Top row equipment icons left to right: saber crossed over round wooden shield; long spear with red cloth tie; rural bow with quiver; river engineer kit with measuring pole, shovel, rope. Bottom row skill icons left to right: shield guard shown as braced fighter and shield arc; spear brace shown as crouched spearman and earth impact; arrow volley shown as three flying arrows; dike repair shown as stacked sandbags, wooden stakes and fresh mud. One centered semantic object/group per cell, generous green separation, materially detailed but readable at 32×32, no magic glow, no modern objects.

## 5. 四组连续 FX 母图

Create a precise 4 columns by 4 rows pixel-art FX animation atlas on one perfectly flat vivid chroma magenta background (#ff00ff), no grid, no labels, no shadows on the background. Each row is one continuous four-frame animation progressing left to right, with the same center/ground anchor and generous magenta separation. Grounded historical tactics effects, crisp pixel clusters, no magic symbols. Row 1: iron weapon hit sparks, tiny impact to bright starburst to scattered sparks to fading embers. Row 2: controlled fire attack, small ignition to tall flame and smoke to broken flame tongues to smoke/embers. Row 3: muddy water splash, crown splash to high droplets to falling droplets to low ripple. Row 4: field repair impact on timber and stone, hammer strike/chips to debris burst to falling grit to final glint. High-detail but readable in 32×32 cells, no people, no text, no border, no modern elements.

## 6. 开仓剧情场景母图

Create one cinematic 16:9 HD pixel-art narrative scene for a grounded low-fantasy historical East Asian county tactics game. Nighttime county granary opening during famine relief: large timber granary doors open under warm lanterns, county clerks weigh a visibly limited amount of old/moldy grain, hungry commoners wait in an orderly tense line, militia keep the passage open, damp stone and timber show material texture. The emotional focus is hard governance under scarcity, not celebration. Strong distinct composition with granary doorway as focal light, figures in midground, deep blue-black rainy night around warm amber lamps. Reserve the entire bottom 22% as a dark, low-detail dialogue-safe foreground strip; no key faces, grain evidence, or tools in that strip. Crisp HD pixel clusters, subdued wet indigo, silt brown, rice gold and old gray stone, no UI, no text, no modern objects, no magic, no watermark.
