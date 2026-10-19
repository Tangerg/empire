# Gravekeeper source contract

Canonical topic C01-UNIT-GRAVEKEEPER, category combat-unit. Both c01.mirelle and c01.gravekeeper explicitly bind here in candidate-01-bindings.ts lines 9 and 17. The actor named bridge-cleric in c01-02 is c01.gravekeeper at cell (2,9), owner 1, facing east. Its actor label does not make it the separate built-in cleric/C01-UNIT-CLERIC.

## Original visual identity

Original board: masters/combat-units/cemetery-gravekeeper-source.png, 1672×941. Native first-cell extraction [0,0,418,941] retains the complete original figure. HD strip is 256×96, four 64×96 frames; runtime strip is 128×48, four 32×48 frames. Manifest order is idle, walk-a, attack, walk-b, anchor [16,47], footprint [1,1].

The original is a living older human with swept-back gray hair, short gray beard/mustache, exposed head, hood-down dark tabard, cream sleeves, blue-gray scarf, brown boots, wooden memorial tags, brown pouch and silver bottle. Screen-left bare hand carries a long upright wooden spade; screen-right bare hand carries a separate candle lantern. There are no skeleton, grave-marker or tombstone props to invent.

## Existing action semantics

units.ts lines 47–50 and 69 define heal, attack, capture and wait. weapons.ts lines 64–77 define c01.lantern-staff and c01.oath-dispel as range-2 magic/arcane support actions. The existing attack frame can therefore show a focused forward lantern cast/dispel while retaining the practical shovel. A contained flame and a clear arm/stance gesture convey the action; no external glow haze or additional effect asset is needed. Keep the four-frame contract; no fifth healing frame is added.

## Naming versus visible equipment

The gameplay weapon is called lantern-staff, but the exact original visible equipment is a separate long spade and handheld candle lantern. The remake preserves that source identity and physical hand assignment. This naming/visual discrepancy is documented rather than silently changing the tool into a magical staff. No binding remap or gameplay-semantic fix is performed or claimed.

## Idle review geometry

Native selected idle is 1586×992. Hair crown y=76; exact mean boot soles y=973; proposed shared scale 44/897. Suggested source foot [832,979]. Visible alpha>127 bounds [326,76,1267,980]. Full shovel head/butt, both boots and lantern are retained; zero alpha>1 pixels on the native perimeter. Native alpha=1 specks are preserved unchanged. Raw/master/native bytes agree; exact tool-call prompt and request metadata are saved beside the pose.
