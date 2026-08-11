# C01 游戏运行时 V2 首批 ImageGen 提示词

生成方式：OpenAI 内置 ImageGen。输入图只作为 C01 正式 HD 像素素材的风格参考；所有输出均为原创母图。透明资产先生成在平面 `#ff00ff` 色键底上，再用 ImageGen 技能自带的 `remove_chroma_key.py` 清除背景。

## 剧情场景

母图：`masters/gray-banner-dawn-council-master.png`

```text
Use case: illustration-story
Asset type: high-detail source master for a 16-bit tactical RPG story scene, final crop 256×144
Input images: the five supplied C01 HD pixel-art images are style references only; do not edit or copy their composition
Primary request: original scene titled “灰旗营黎明议事”. A young uncrowned female gray-banner officer, an old armored mentor, a burgundy imperial shield captain, and a hooded oath-lantern keeper study a weathered river map across a rough campaign table inside an open command tent. Through the tent opening, muddy fortifications, a damaged red-stone oath tower, river mist, and soldiers repairing palisades are visible.
Style/medium: exceptionally detailed late-16-bit / 32-bit era pixel art, hand-placed clusters, crisp pixel edges, dense readable material texture, no painterly blur, matching the supplied HD C01 assets
Composition/framing: cinematic 16:9 wide shot; figures and table in foreground, tent structure framing both sides, tower and misty camp in background; keep essential subjects inside central 90% safe area for crop
Lighting/mood: cold blue-gray dawn, wet atmosphere, restrained amber oath-lantern and brazier highlights; somber strategic tension
Color palette: slate blue-gray, soot black, weathered brown, muted burgundy, small amber accents
Materials/textures: wet wool, old steel, smoke-darkened red stone, mud, charred timber, worn parchment
Constraints: original characters; no crown; no text or symbols; no UI; no watermark; no smooth vector shapes; no photorealism; no anime cel shading; do not imitate any named commercial game
```

## 四个战斗单位

共同约束：

```text
Use case: stylized-concept
Asset type: high-detail source master for one 16-bit tactical RPG battle unit, to be reduced into a 128×48 four-frame sheet (4 cells, each 32×48)
Input images: five supplied C01 HD assets are style references only
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local removal; absolutely uniform with no texture, gradient, floor, shadow, or reflection
Style/medium: hand-crafted late-16-bit / 32-bit pixel art, crisp square pixel clusters, dense readable old-steel and worn-cloth texture, subdued C01 blue-gray world palette, not smooth illustration
Composition/framing: exactly four equal-width animation poses in one horizontal row, no dividers; same character identity, scale, equipment, lighting and three-quarter side-facing direction in all four; pose sequence idle A, step A, idle B, step B; full body and equipment visible; feet share one baseline; generous magenta padding
Lighting/mood: cool overcast light, small restrained amber specular accents
Constraints: no text; no labels; no border; no UI; no watermark; do not use #ff00ff in the subject; no cast shadow; no blurred or anti-aliased outer edge; exactly four characters total; no extra objects; distinct silhouette remains readable at 32×48
```

各单位主请求：

| 母图 | `contentId` | Primary request |
| --- | --- | --- |
| `masters/units/gray-banner-soldier-master.png` | `soldier` | Gray Banner spear-guard, practical blue-gray brigandine, kettle helm, long ash spear, narrow heater shield strapped on left arm, gray torn shoulder pennant, disciplined veteran stance. Keep spear fully inside each cell. |
| `masters/units/silverwood-archer-master.png` | `archer` | Silverwood longbow ranger, dark moss-green layered cloth and brown leather, ash-gray hood, tall recurved longbow, short quiver, light boots, lean scouting walk. No leaves or scenery; bow stays fully inside each cell. |
| `masters/units/burgundy-knight-master.png` | `knight` | Burgundy imperial shield veteran, muted wine-red lamellar coat over dark plate, closed iron helm, large squared burgundy shield with plain face, short sword at hip, heavy defensive walk. Shield and helmet make a broad unmistakable silhouette. |
| `masters/units/forge-cleric-master.png` | `cleric` | Mountain-forge oath hammer infantry, soot-black mail and weathered bronze plates, compact hornless forge helm, two-handed square war hammer, thick leather apron split for movement, stocky silhouette, tiny amber oath-metal rivets. Hammer stays fully inside each cell. |

## 地形材料板

母图：`masters/terrain/c01-terrain-material-board-master.png`

```text
Use case: stylized-concept
Asset type: high-detail source master for a tactical RPG terrain tileset
Input images: the five supplied C01 HD pixel-art assets are style references only; preserve their palette, crisp pixel density, worn materials, and subdued lighting without copying designs
Primary request: exactly eight distinct seamless top-down terrain material swatches arranged as a strict 4 columns × 2 rows board, equal square cells, no labels. Reading order: muddy grass plain; rutted dirt road; wet timber-and-stone bridge deck; dense dark silverwood forest floor; rocky scrub hill; soot-gray mountain stone; cold dark river water; smoke-darkened red-stone wall-walk.
Style/medium: hand-crafted late-16-bit / 32-bit pixel-art terrain, orthographic top-down, crisp square pixel clusters, dense but readable material detail
Composition/framing: straight top-down, no perspective and no horizon; each swatch fills its square cell edge to edge; one-pixel-equivalent dark separators; no objects crossing cell boundaries
Lighting/mood: neutral cold overcast light, consistent direction across all cells
Color palette: desaturated blue-gray, brown, dark moss, soot stone, muted red stone; no saturated greens
Materials/textures: mud, sparse grass, wheel ruts, water-wet planks, roots and leaf litter, stratified rock, broken ripples, chipped masonry
Constraints: exactly eight cells; no text; no characters; no buildings; no UI; no watermark; tiles must not contain large unique focal objects; each texture must support seamless processing; no smooth vector art; no painterly blur
```

## 两个交互建筑

### 赤石誓约烽塔

母图：`masters/structures/redstone-oath-tower-states-master.png`

```text
Use case: stylized-concept
Asset type: high-detail source master for one interactive tactical RPG building with three states
Input images: the supplied C01 HD assets are style references only; preserve their palette, crisp pixel density, worn materials and subdued lighting without copying a specific building
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background; uniform, no shadow, gradient, floor or reflection
Primary request: original red-stone oath signal tower shown exactly three times in one horizontal row: normal, damaged, captured. Same isometric three-quarter camera, identical scale and footprint in all states. Normal has intact battlements and small amber brazier; damaged has cracked upper masonry, scorched timbers and extinguished brazier but remains standing; captured has intact geometry plus a plain removable gray-blue banner and active amber beacon.
Style/medium: exceptionally detailed late-16-bit / 32-bit pixel-art building, crisp clusters, chipped smoke-darkened red stone, old iron, charred wood
Composition/framing: three equal cells, no dividers; building bases share one baseline; generous magenta padding; no cropped roof or base
Constraints: exactly three towers; no text; no emblem; no UI; no watermark; do not use #ff00ff in subject; no cast shadow; no scenery; states keep identical camera and anchor
```

### 灰旗军需站

母图：`masters/structures/gray-banner-depot-states-master.png`

```text
Use case: stylized-concept
Asset type: high-detail source master for one interactive tactical RPG building with three states
Input images: the supplied C01 HD assets are style references only; preserve their palette, crisp pixel density, worn materials and subdued lighting without copying a specific building
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background; uniform, no shadow, gradient, floor or reflection
Primary request: original gray-banner field supply depot shown exactly three times in one horizontal row: normal, damaged, captured. Same isometric three-quarter camera, identical scale and footprint. It is a sturdy low timber command-and-ration depot with wet gray canvas, stacked crates, ledger table, palisade corners and a plain gray flag. Damaged state has torn canvas, broken crate and scorched palisade; captured state is repaired enough to function and carries a removable muted burgundy side pennant without changing the structure.
Style/medium: exceptionally detailed late-16-bit / 32-bit pixel-art building, crisp clusters, wet wool canvas, worn wood, iron straps, muddy supplies
Composition/framing: three equal cells, no dividers; bases share one baseline; generous magenta padding; no cropped roof or base
Constraints: exactly three depots; no text; no emblem; no UI; no watermark; do not use #ff00ff in subject; no cast shadow; no scenery; states keep identical camera and anchor
```

## 装备与技能图标

母图：`masters/icons/runtime-icons-master.png`

```text
Use case: stylized-concept
Asset type: high-detail source master for eight tactical RPG equipment and skill icons
Input images: the supplied C01 HD pixel-art assets are style references only; preserve their restrained palette, crisp clusters and worn material highlights
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background; uniform, no shadow, gradient, texture or reflection
Primary request: exactly eight independent square pixel-art icons arranged in a strict 4 columns × 2 rows grid with generous padding, no labels. Reading order: ash spear; burgundy square shield; silverwood longbow; forge oath hammer; amber healing lantern; torn gray rally banner; bridge repair toolkit with hammer and iron clamp; broken oath shackle dispel icon.
Style/medium: detailed late-16-bit / 32-bit painted pixel icons, crisp silhouette first, three-tone materials plus tight highlights
Composition/framing: one centered object or compact object pair per cell, consistent scale, nothing touches cell edge, thin dark outline, no frames
Color palette: old steel, brown leather, muted burgundy, gray-blue cloth, tiny amber accents
Constraints: exactly eight icons; no text; no letters; no numbers; no UI frame; no watermark; do not use #ff00ff in icons; no cast shadow; no extra objects; each icon readable after reduction to 32×32
```

## 四组 FX

母图：`masters/fx/runtime-fx-master.png`

```text
Use case: stylized-concept
Asset type: high-detail source master for four tactical RPG FX animations, four frames each
Input images: the supplied C01 HD pixel-art assets are style references only; preserve their restrained palette and crisp hand-placed clusters
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background; uniform, no shadow, gradient, texture or reflection
Primary request: strict 4 columns × 4 rows sprite board, exactly sixteen cells with one centered effect per cell and no dividers. Each row is a four-frame progression, left to right. Row 1: steel blade hit, thin arc to bright cross-spark to shards to fade. Row 2: compact amber oath-flame ignition, rise, flare, ember collapse. Row 3: healing lantern motes, three motes gather, ring bloom, upward sparks, vanish. Row 4: masonry impact debris, crack flash, stone chips burst, dust pixels settle.
Style/medium: hand-placed late-16-bit / 32-bit pixel FX, crisp hard-edged clusters, readable at 32×32, no soft airbrush
Composition/framing: equal square cells, effect centered on identical anchor, safe padding, no cell overlap
Constraints: exactly sixteen effects in the specified grid; no text; no characters; no weapons or scenery; no UI; no watermark; do not use #ff00ff inside effects; no cast shadow; avoid large opaque discs; effects must not obscure a unit for more than two frames
```

## 第二轮 1×1 地图建筑

三张母图均用于替换真实关卡截图中带亮绿色底板的旧回退图。输入图 1 为 C01 首关实机截图；输入图 2–5 为 C01 正式 HD 建筑参考。

### 村庄

母图：`masters/structures/village-states-master.png`

```text
Use case: stylized-concept
Asset type: high-detail source master for a 1×1 tactical RPG map building, final state frame 32×64
Input images: Image 1 is the real C01 in-game screenshot showing a bright-green placeholder village to replace; Images 2–5 are C01 formal HD architecture style references only
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local removal; uniform with no shadow, gradient, floor, texture or reflection
Primary request: an original compact frontier village marker shown exactly three times in one horizontal row: normal, damaged, captured. Same isometric three-quarter camera, identical scale and ground footprint. The neutral village is a tight cluster of two smoke-darkened timber-and-stone cottages with wet brown thatch, a tiny central well and a short rough fence. Damaged has one collapsed roof corner, charred beams and scattered broken planks but remains recognizable. Captured keeps the neutral buildings and adds one small plain gray-blue removable banner on a narrow pole.
Style/medium: exceptionally detailed late-16-bit / 32-bit pixel-art map building, crisp square clusters, readable silhouette at 32 px wide, matching the supplied C01 wet-stone and worn-wood materials
Composition/framing: exactly three equal cells, no dividers; each village fully isolated; bases share one baseline; compact vertical silhouette fitting a 1×1 tile with upward overflow; generous magenta padding
Lighting/mood: cool overcast light with restrained warm window highlights
Constraints: exactly three village states; no people; no trees; no grass tile; no bright green; no text; no emblem; no UI; no watermark; do not use #ff00ff in subject; no cast shadow; state camera and anchor must match
```

### 兵营

母图：`masters/structures/barracks-states-master.png`

```text
Use case: stylized-concept
Asset type: high-detail source master for a 1×1 tactical RPG map building, final state frame 32×64
Input images: Image 1 is the real C01 in-game screenshot showing a bright-green placeholder barracks to replace; Images 2–5 are C01 formal HD architecture style references only
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local removal; uniform with no shadow, gradient, floor, texture or reflection
Primary request: an original compact neutral frontier barracks shown exactly three times in one horizontal row: normal, damaged, captured. Same isometric three-quarter camera, identical scale and ground footprint. It is a sturdy smoke-darkened timber drill hall with a steep wet gray roof, reinforced stone corners, narrow weapon rack, small side awning and plain closed gate. Damaged has a broken roof section, scorched beam and splintered rack. Captured keeps the neutral structure and adds one small plain gray-blue removable pennant on a short pole.
Style/medium: exceptionally detailed late-16-bit / 32-bit pixel-art map building, crisp square clusters, strong blocky military silhouette readable at 32 px wide, matching the supplied C01 wet wool, worn wood, old iron and stone
Composition/framing: exactly three equal cells, no dividers; each barracks isolated; bases share one baseline; compact vertical silhouette fitting one 1×1 tile with upward overflow; generous magenta padding
Lighting/mood: cool overcast light, restrained warm doorway glint
Constraints: exactly three barracks states; no soldiers; no grass tile; no bright green; no text; no crossed-sword emblem; no UI; no watermark; do not use #ff00ff in subject; no cast shadow; state camera and anchor must match
```

### 城堡

母图：`masters/structures/castle-states-master.png`

```text
Use case: stylized-concept
Asset type: high-detail source master for a 1×1 tactical RPG map building, final state frame 32×64
Input images: Image 1 is the real C01 in-game screenshot showing a bright-green placeholder castle to replace; Images 2–5 are C01 formal HD architecture style references only
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local removal; uniform with no shadow, gradient, floor, texture or reflection
Primary request: an original compact neutral border castle shown exactly three times in one horizontal row: normal, damaged, captured. Same isometric three-quarter camera, identical scale and ground footprint. It is a tall but narrow smoke-darkened red-stone gate keep with two square corner turrets, iron-banded central gate, shallow battlements and a tiny unlit beacon bowl. Damaged has one cracked turret crown, broken battlement and blackened gate while staying upright. Captured keeps the neutral masonry and adds one small plain gray-blue removable banner above the gate.
Style/medium: exceptionally detailed late-16-bit / 32-bit pixel-art map building, crisp square clusters, strong fortress silhouette readable at 32 px wide, matching the supplied C01 chipped red stone and old iron
Composition/framing: exactly three equal cells, no dividers; each castle isolated; bases share one baseline; narrow vertical silhouette fitting one 1×1 tile with upward overflow; generous magenta padding
Lighting/mood: cool overcast light with restrained amber slit-window glints
Constraints: exactly three castle states; no soldiers; no terrain tile; no bright green; no text; no crown emblem; no UI; no watermark; do not use #ff00ff in subject; no cast shadow; state camera and anchor must match
```
