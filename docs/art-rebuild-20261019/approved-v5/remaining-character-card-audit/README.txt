Remaining C01 character-card audit, 2026-10-20 UTC

Result
15 cards remain after7 approved named stage01 portraits. All15 exact original617x617 cells reproduce their current192x224 runtime pixels. Only IVRA01 currently has dialogue/roster/HUD selectors; the other14 are catalog-only. No remaining card has runtime HD. All83 checked source files and23 checked contract files stayed stable. No generation, production edit, exporter run, consumer change or Git command was performed.

Next safe assignments
1. LEIA02: character-board-02 BL, zero-based catalog slot28, crop[5,632,622,1249]. Same approved Laiya face at22; gray-queen stage, explicit shoulder/multipeople-clasp/dragon-wing-emblem brief. Avoid invented royal blood or a tall crown.
2. LEIA03: character-board-02 BR, slot29, crop[632,632,1249,1249]. Same approved Laiya face at27; no crown, multi-flag cloak, protective command clothing, old left wrist guard.
3. RODERICK02: character-board-03 TL, slot30, crop[5,5,622,622]. Same approved older mentor, same equipment aged, Crown Army allegiance. No evil black-armor transformation. Standing sword/shield portrait does not change mounted/lance combat.

The exact original and approved named portrait/idle/four-pose paths, SHA256s, dimensions, native alpha contracts and lore quotes are in next-safe-assignments.json. All three use independent native1024x1536 RGBA character masters, with an intentionally separate background for the existing192x224 UI composite. Current shared starting placement is renderWidth144, origin[24,4]; it still needs native and real UI crop review. Keep current contract; future384x448 validation is documented separately and not implemented.

Conflicts needing explicit reconciliation
- ADA01: original toy boat/cloth vs later bilingual-medicine brief; creative bible introduces bilingual labels in chapter2 after the age8 bridge event. ADA02 is explicitly17, with tower-healing-glove/label detail absent or unclear in original. No generic doctor or refugee source establishes Ada identity.
- IVRA01: original cute, round, huge-eyed uninjured-looking baby vs explicit injured/wary/non-pet brief. Approved IVRA-GROWTH combat image is more mature and not a reviewed juvenile named stage. IVRA02/03 are distinct age stages; adult must remain riderless, reinless and independent.
- NOBANNER01/02: unstable plural aggregate versus negotiated coordinator. Both keep multiple individuals; neither is a generic ghost or skeleton monarch. No live card consumer exists.
- SEVERIN: only one unsuffixed canonical card. Original ornate armored regent vs explicit plain/tired political-clothes brief. He is not Roderick even though the original faces resemble each other.
- BRAN02: original weathered/gray-forelocked man must maintain plausible continuity with approved young Bran; village strips/survivor-list pouch needs explicit detail review.
- MIREL02: female named keeper must stay female; later public-tools purification seal is not clearly depicted in original.
- TORREN02: approved young Torren continuity, later Old Flag role and Roderick-glove custody; original scroll/shield does not show the glove. No stage02 actor selection exists.

Authority
The current transparent-master requirement, named identity, exact age/role and explicit stage brief take priority. The old fullboard records provenance and framing; a known incorrect original is not an eternal constraint. Generic soldier aliases never redefine named faces, gender or age. No automatic stage/age selector is inferred or changed.

Files
- audit.json: full15-card machine-readable audit,83 source hashes, current source/consumer evidence, visual identity/emotion/pose/framing, literal lore quotes, alpha/HD contracts
- next-safe-assignments.json: three ready-to-scope recommendations with exact image references
- source-mismatches.json: per-card discrepancies and source-language quotes
- future-card-hd-validation.json: bounded future384x448 checks only
- original-cells/:15 exact617x617 provenance crops; inspection derivatives only
- remaining-original-cells-contact-sheet.png: all15 original cards, labeled by topic/board/cell/slot
- approved-named-reference-sheet.png: seven approved named portrait/idle pairs; inspection derivative only
- build_audit.py: read-only reproducer; reruns assert the same pending-topic checkpoint

Verification limits
Source/code audit only. No browser/GPU or live-game acceptance, no new artwork quality claim, no gameplay tests or output exports. The prior narrative-family-audit is a historical checkpoint; its old approved-card hashes and Torren consumer status must not be reused as current evidence.
