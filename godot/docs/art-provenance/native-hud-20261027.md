# Native HUD material artwork, 2026-10-27

Original Empire assets generated with the built-in image generation tool, transparent PNG pixels preserved. Runtime atlas/nine-patch regions are authored in TacticalHudStyle, while controls and text remain native Godot nodes. No external game's art has been copied.

- `assets/native/hud/battle-banner-v1.png`: torn dark gray-blue military cloth pennant, restrained worn copper fastenings, empty dark center for readable battle text.
- `assets/native/hud/objectives-paper-v1.png`: warm aged parchment objective strip, irregular paper edge and restrained corner fittings, empty light center.
- `assets/native/hud/command-tray-v1.png`: low dark timber command tray with old bronze/iron rim, wide empty dark interior, transparent exterior.
- `assets/painted-units-v1/aftermath-equipment.png`: compact discarded helmet, faded cloth, broken spear and ochre wooden shield, 45-degree elevated painterly view, no person/gore/ground patch/shadow. It replaces only the visual for the existing fallen-location equipment marker, not the marker's canonical fallen unit or any revival rules. The old blue shape was discarded equipment, not a fallen mounted-unit type.

Generation briefs requested original warm painterly strategy-game materials, broad readable shapes, no baked text/UI icons, no halo, genuine transparency and complete margins. These are brief summaries; hero/infantry full prompts are retained in painted-units-v1/.

Layout references personally viewed: Ironhide's Legends of Kingdom Rush official combat screenshot (map-dominant composition and independent command symbols), and Battle Brothers developer UI article (material hierarchy). Borrowed design principles only, not assets or mechanics:

- https://files.ironhidegames.com/ironhidegames-www-upfiles/original/Games/d3585c59-3387-4ef9-9f37-c42739cb37aa.jpg
- https://battlebrothersgame.com/dev-blog-75-progress-update-reworked-ui/

Readability priorities: main selected unit / current-max HP / action availability precede phase/faction detail; commands use short semantic labels; target prediction is contextual; low-priority text stays in details or battle log. Independent bottom silhouettes leave map visible between them. Safe text bounds are smaller than artwork's external rectangle.
