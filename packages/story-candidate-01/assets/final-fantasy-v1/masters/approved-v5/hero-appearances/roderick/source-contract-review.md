# Roderick named mounted appearance: source and idle contract

Approved for an idle pilot by the coordinator on 2026-10-19 at 21:15 UTC. No repository, gameplay or binding changes are made in this shared-source task.

## Named identity authority
The original narrative board is `masters/narrative/characters/character-board-01.png`, 1254×1254. Roderick is top-right, exact native crop `[632,5,1249,622]`, 617×617. Its visible older face has swept-back gray hair, strong dark brows, a full gray beard and moustache. Heavy worn gray steel armor, blue-gray scarf/tabard, brown belt and pouch, broad boots, a plain dark round shield with rim/rivets/boss and a practical straight sword are his anchors. The right anatomical hand (image left) holds the original sword; the left arm (image right) holds the shield. No numerical age was verified. `ART-DIRECTION.md:27` explicitly requires gray beard, old steel, round shield or practical sword and a trustworthy silhouette.

## Rules and personal mount design
`src/units.ts:29–34` establishes `c01.roderick`, mounted movement, heavy armor and cavalry tags, `c01.knight-lance` / `c01.knight-charge`. The novel `deep-novel-v2/01-border-fire-novel.md:113–115` describes arrival on horseback and his dismount to help Torren. The named portrait itself is dismounted and does not establish a personal horse's coat or a lance design.

The existing original `C01-UNIT-LANCE-CAVALRY` board second row supplies horse construction: brown coat, dark mane/tail, pale face/nose marking, bridle, reins, saddle, stirrups, four hooves, plain wood lance with pointed steel head. That topic is a separate catalog unit with no current C01 unit-art consumer; its equipment topic serves knight-charge. Its young helmeted rider, rectangular cream shield and navy/red uniform are not Roderick's identity and must not be copied. The new personal horse is an explicitly approved design completion based on rules and this construction reference, not an assertion that an original named horse already existed.

The approved combat arrangement is Roderick's right hand on the lance, left forearm carrying his original round shield while its hand controls the reins. The portrait's straight sword remains fully sheathed at the hip. No emblem, crown, magical aura, extra weapon hand or additional rider is added.

## Current alias and action contract
Current explicit unit binding is `c01.roderick → C01-UNIT-HEAVY-KNIGHT`; its generic young helmeted foot mace/rectangular-shield soldier remains an independent approved generic source. A named appearance variant will be integrated by the coordinator; canonical count delta is zero. The original named card is `C01-CHAR-RODERICK-01`.

The exact four semantic slots remain idle, walk-a, attack, walk-b. Opposite horse diagonal contacts will distinguish the walk frames. One attack slot supplies both mounted lance and charge; depict a clear forward lance strike with a charge-ready lean and retain the shield/reins. `knight-lance` is pierce; `knight-charge` inherits slash from the weapon helper and adds push1/collision8. This existing rule detail is documented, not rewritten. Independent FX02/FX07 remain separate from authored character art.

## Idle scale proposal
Full native landscape target 1586×992, entire rider, horse, tail, four hooves and lance endpoints inside true transparency. Combined hair-crown-to-hoof height starts at 54–56 logical, with full visible width including lance about72–80. Rider head/hands remain in the same world scale as the approved44-high infantry. Do not scale the rider down independently to fit the horse. Measure the native pilot before locking one whole-canvas shared scale and hoof registration for all four poses. Candidate padded export frame128×80; final geometry awaits idle/native comparison.

Base heavy-knight appearance footprint stays1×1. The reference cavalry's original64×48 frame/2×2 art footprint does not alter Roderick's rules or imply changed gameplay occupancy.

## Actual scene placements
Direct seed/deployment pairing under `levels.ts` place():
- c01-01 `roderick-reserve`, owner1/east, cell(4,11)
- c01-03 `roderick`, owner1/east, cell(3,5)
- c01-10 `roderick`, owner2/west, cell(18,6)
These are current source placements, not a claim that a live scene was reviewed here. The coordinator owns complete native scene review and integration.

All original bytes, native reference crops and hashes are in `references/` and `source-evidence.json`.

## Idle pilot checkpoint, 21:23 UTC
Attempt01 preserved the named face/mount but incorrectly put both hands on the lance. A targeted built-in edit, attempt02, moves the left glove down to the reins; the right glove remains the sole lance grip and the round shield stays on the left arm. Both original native outputs and exact prompts are immutable under idle/attempt-01 and attempt-02. Main idle currently selects attempt02 only as the review candidate.

Attempt02 is1584×993, sha256743cd18a3d2a3ea1affc49ee1a6e21d61e5d2aced5314dd2ea4ae0a004dc88a0. Hair crown14, near-hoof mean978.5, measured height964.5; proposed56/964.5 scale, foot[794,984], visible width71.7636logical. Hoof registration uses four actual sole-band centers, never the rider's stirrup. Whole-canvas 2x comparison against formal44-high swordsman and52-high wolf rider is in idle/native-scale-comparison.png with explicit transform evidence.

Open review issues: the pilot head reads smaller than the most exaggerated approved family and its armor has many small matte wear patches. The native strict perimeter gate also FAILS: four alpha2 pixels at bottom coordinates x899–902,y992; all other perimeter pixels are zero. The visible hoof ends at y984, and rows987–991 contain no A>1 pixels, establishing that no visible hoof is cropped. No alpha cleanup has been applied. An explicit transparent right2-column/bottom1-row common-canvas extension to1586×994 has been proposed for review, preserving every original pixel, but is not yet approved/applied. Remaining poses are held for the coordinator's size/style decision.

## Revised idle checkpoint, 21:27 UTC
Coordinator rejected using padding to accept the raw alpha2 edge and requested one targeted head/material revision. Attempt03 is the new candidate: enlarged older head/beard, clean broad gray armor planes, correct right-lance/left-reins arrangement. Native source1560×1008, sha256d75f827aaff16fa667939d525a853f0d25ba5950db3c3d0a911f9b53f6846a80. Its strict raw perimeter gate PASSES with no A>1 edge pixel; bottom edge is exactlyzero and the bottom29complete rows are alpha0. No programmatic padding, rescale or cleanup was used to attain acceptance.

The tool changed canvas dimensions and redrew the horse/lance projection by roughly2–4% while preserving the pose and anatomy. This is disclosed rather than claimed pixel-identical. The old scale was a proposal, never locked. New measured hair crown22, near-hoof mean969, height947 gives proposed56/947, sourcefoot[782,975], full width70.192logical. Full native comparison uses that proposal against approved52-high wolf rider and44-high swordsman. Await coordinator lock before remaining poses. Attempts01/02, including the rejected alpha2 edge evidence, are retained unchanged.
