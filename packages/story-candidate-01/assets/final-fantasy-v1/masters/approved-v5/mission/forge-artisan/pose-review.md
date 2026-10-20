# Forge artisan source quartet

Four native built-in imagegen poses for C01-MISSION-FORGE-ARTISAN. Order: idle, walk-a, carry-or-work, walk-b.

## Identity and consumers
idle/walk-a/carry-or-work/walk-b. Zero current actors or scene placements. Carry-or-work is noncombat workpiece inspection. Do not rebind c01-14 forge-laborer, which uses BRIDGE-LABORER.
Preserve original bronze skin, charcoal gray-streaked hair/shirt, rust-red apron, brown gloves/boots/belt, silver earring, blue-gray vessel and orange round workpiece. No faction recolor.

## Shared geometry
Accepted idle bun crown y38 to supporting lowest sole y968 gives930source pixels and one fixed44/930scale. Canonical source foot805.25,968. Proposed whole-canvas export104x64 at8,4. Native canvas variations1584–1586 by992–993 are preserved; common1586x993 requires at most2transparent right columns/1bottom row during export only. Maximum whole-canvas registration translation1.041logical pixels. No per-pose scale fitting.

## idle
Bronze-skinned woman with gray-streaked charcoal bun, round silver earring, rolled charcoal sleeves, rust-red apron, brown gloves and huge boots. Blue-gray vessel in screen-left hand and tongs with orange round workpiece in screen-right. Both boots planted.
Native size[1585, 992]; measured foot[805.25, 968]; whole-canvas logical translation[0.0, 0.0].

## walk-a
Screen-right boot extended heel-first with toe raised and dark underside visible; screen-left boot flat and weight-bearing. Vessel swings backward in its same screen-left hand. Tongs remain screen-right.
Native size[1586, 992]; measured foot[783.25, 954]; whole-canvas logical translation[1.0408602150537634, 0.6623655913978495].

## carry-or-work
Meaningful compact workpiece inspection: screen-right arm lifts tongs and orange workpiece toward eye level; gaze follows it. Screen-left hand lifts blue-gray vessel to waist height. Both boots planted; no pouring, combat, prop drop or swapped hands.
Native size[1584, 993]; measured foot[803.25, 965]; whole-canvas logical translation[0.09462365591397849, 0.14193548387096774].

## walk-b
Opposite walking contacts: screen-right boot flat and weight-bearing; screen-left heel raised with bent knee and downward-pointing toe. Vessel swings forward in its same screen-left hand. Tongs remain screen-right. Same face orientation, not mirrored.
Native size[1585, 992]; measured foot[798.5, 947]; whole-canvas logical translation[0.3193548387096774, 0.9935483870967742].

## Provenance and QA
All4native attempts succeeded. Every selected raw/master pair is byte-identical to its immutable generated output. Exact prompts, request JSON, request/prompt/reference SHA256 hashes, source paths and actual native dimensions are retained. Exact original board/row/idle reference pixels verified. No rejected or failed attempts occurred.
Full-native and registered44logical black/white/scene QA inspected. Distinct opposite foot contacts and meaningful inspection work pose; original prop hands retained. No haze or light halo. QA composites/export padding only; no source pixel edits.
Coordinator owns runtime export, repository integration, tests and Git. No repository writes performed.
