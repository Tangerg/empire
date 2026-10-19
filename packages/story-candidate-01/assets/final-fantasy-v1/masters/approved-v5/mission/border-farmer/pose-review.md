# Border farmer source quartet

Four native imagegen poses for C01-MISSION-BORDER-FARMER. Exact order: idle, walk-a, carry-or-work, walk-b.

## Identity and consumer contract
idle/walk-a/carry-or-work/walk-b. Existing ambient scene consumers draw frame0 only. Carry-or-work is seed-pouch handling, never combat. Source completion does not make animation reachable.
Preserve original warm-tan skin, taupe/olive scarf, beige dress/tunic, brown trousers/boots/seed pouch/wood handle and gray bent iron blade; no faction recolor.
Existing c01-01 authored ambient placement scale/opacity/flip/layer are separate from44logical body scale and must remain unchanged. No domain bindings or behavior edits are requested.

## Shared geometry
Approved idle scarf crown36 to lowest boot sole975 yields939source pixels and fixed44/939scale. Canonical sourceFoot780.75,975. Whole-canvas export proposal104x64 at8,4. Common source1586x993 uses at most2transparent right columns and1bottom row during export only. Selected native images remain unmodified.

## idle
Older worried warm-tan woman with taupe chin-tied headscarf, beige patched dress/tunic, brown waist tie, trousers and huge boots. Screen-left patched-sleeve hand holds tied seed pouch; screen-right plain-sleeve hand holds short wooden-handled bent-blade hoe. Both boots planted.
Native size[1585, 992]; measured foot[780.75, 975]; whole-canvas logical translation[0.0, 0.0].

## walk-a
Front screen-right boot heel-first with toe raised and dark sole visible; rear screen-left boot flat. Seed pouch swings back while staying in the original patched-sleeve hand; hoe stays in original opposite hand.
Native size[1584, 993]; measured foot[766.5, 958]; whole-canvas logical translation[0.6677316293929713, 0.7965921192758254].

## carry-or-work
Distinct noncombat seed-pouch check: patched-sleeve screen-left hand lifts pouch near chest/waist; eyes glance down toward it. Plain-sleeve screen-right hand lowers the same hoe outward, blade safely down. Both boots planted. Corrected generation preserves source physical hands.
Native size[1585, 992]; measured foot[773.0, 946]; whole-canvas logical translation[0.36315228966986157, 1.3588924387646433].

## walk-b
Opposite contacts from walk-a: front screen-right foot flat and weight-bearing; rear screen-left heel raised and knee bent, toe contacting behind her. Pouch swings forward. Clothing, tool, hand assignment and camera remain unchanged.
Native size[1586, 992]; measured foot[754.0, 967]; whole-canvas logical translation[1.25346112886049, 0.37486687965921195].

## Provenance and QA
Five generation attempts preserved: four selected native outputs and one rejected swapped-hand carry/work output. Every selected raw/master pair is byte-identical to tool output. All exact prompts, requests, native-source paths, SHA256 hashes, original board/row/idle references and rejected-candidate rationale are retained. No alpha cleanup, recolor, image painting, cropping or per-pose scale fitting. Full-size black/white/scene composites and native44logical three-background comparison are QA only.
Runtime export, tests, integration and Git are owned by the coordinator; no repository writes performed.
