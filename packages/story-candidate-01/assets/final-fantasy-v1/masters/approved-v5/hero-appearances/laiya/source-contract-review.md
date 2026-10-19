# Laiya named battle appearance: source audit and idle proposal

Status: read-only source investigation complete, 2026-10-19 20:45 UTC. No image generation, repository edit, appearance binding, gameplay change or extra canonical topic. Waiting for coordinator appearance-interface contract and equipment staging approval.

## Exact identity references

Repository read: /workspace/scratch/84c9fe948755/empire-art-rebuild-20261019

- Existing named card: C01-CHAR-LEIA-01, runtime/narrative-static/char-leia-01.png, 192x224 RGBA but entirely opaque. SHA256 4ee6c380f5906cf5929d9777672295bed392d33cffac62f7839ed5531b5de0c5
- Higher-resolution identity board: masters/narrative/characters/character-board-01.png, 1254x1254 RGB. SHA256 9d93fcc20b50af8568eeba7803b774bd0475d4c7ea6a7a706241cb5b89290efb
- Exact Laiya cell is top-left, crop box [5,5,622,622], native617x617. Other three people are Roderick/Cain/Mirelle and must never be supplied as unlabeled character identity reference.
- Both identity assets were inspected and their actual hashes match hero-references.json. The opaque source board/card supplies identity only, not final transparency or a backdrop to extract.
- Existing battle topic C01-UNIT-BANNER-GUARD, runtime-approved-v5/combat-unit/unit-banner-guard@2x.png,832x128, SHA256 0bc3c803872b9cc415a20ef4c2b1d4eacd4de4a5472ac2cc1fb6f638e1ace40a. Full strip inspected. This has the older moustached helmeted male generic guard, a marked gray flag, back shield and sword. It supplies approved chunky material/body-mass style only, not Laiya's face, age, hair, symbol or personal kit.
- Existing generic runtime416x64 SHA256 40d5008a83da9c688cbc1d4ca57726402d8f02ea9e91dc924166bcf18bca7211 also matches the audit reference.
- Generic native four sources and published sequence references remain in hero-references.json. No generic art was replaced by this audit.

## Named visual identity observed

18-year-old human woman, brown hair swept over forehead and a thick side braid hanging on image-left, exposed head with no helmet or crown, expressive hesitant but determined face. The braid, facial youth and exposed hair must distinguish her from the generic older guard.

Gray-blue quilted light armor with visible chainmail sleeve/hem edging, a short gray-blue shoulder wrap with a round silver clasp, brown belt, dark brown trousers and broad brown practical boots. Large chunky head/hands/boots and broad matte planes follow the approved family; do not replace her costume with generic male mail/helmet.

The original image-right forearm is her anatomical LEFT forearm and has the prominent steel/riveted wrist guard. That hand is empty/relaxed. The original image-left hand is her anatomical RIGHT hand gripping the wooden banner pole. Preserve this assignment unless a coordinator explicitly approves a different staging.

The gray cloth flag is plain, torn and uneven at the lower edge. It hangs from a lashed wooden crossbar under a steel spear-shaped finial. No white horizontal stroke or three ticks appear on her flag. Do not borrow the generic banner-guard's marks. No named shield, large wooden back shield or pack is visible in this portrait; generic class kit does not establish those as Laiya's equipment.

## Text and rule evidence

- docs/story-candidates/candidate-01/01-border-fire.md:4 explicitly says age18.
- packages/story-candidate-01/assets/final-fantasy-v1/ART-DIRECTION.md:24 specifies gray-blue light armor, first-generation torn flag, LEFT wrist guard and a still-hesitant expression. Later22/27-year crown-band/ledger/cape cues in lines25-26 are separate stages and are not appropriate here.
- 01-border-fire.md:530 states the gray-flag army has a flag without formal heraldry. This supports the plain original cloth; no invented personal emblem.
- packages/story-candidate-01/src/units.ts:22-28 defines c01.laiya, weapons c01.border-blade and c01.gray-oath, infantry/hero/commander, movement4, attack/capture/wait. No mounted reinterpretation.
- src/weapons.ts:4-5 defaults weapons to slash; border-blade at13 has power39/melee. Gray-oath at15-24 has power52, cooldown3, cross1 area, shaken status and melee/signature/command tags. These remain rule data, not grounds to bake an independent special effect into her body sprite.
- src/presentation/candidate-01-bindings.ts:5 currently maps c01.laiya to C01-UNIT-BANNER-GUARD; line90 maps the same unit to C01-CHAR-LEIA-01. Equipment lines100-101 and independent FX lines144-145 remain separate.
- src/levels.ts:347 creates PARTY.laiya from c01.laiya; 402 uses her in the campaign main party. packages/experience-lab/src/index.ts:92 uses campaign-laiya at[2,9], owner1, east, a direct review location. Coordinator owns actual full campaign scene selection and the eventual appearance resolver.

## Proposed first idle composition, awaiting contract

- Independent named hero appearance anchored to existing C01-UNIT-BANNER-GUARD, no new canonical topic or gameplay unit. The exact appearanceId and shared source directory await coordinator contract. Generic c01.banner-guard remains unchanged.
- One full native1586x992 transparent landscape canvas, facing screen-right in the same tactical camera. Body from hair crown to mean planted boot soles targets44 logical pixels, with roughly800-850 native pixels for the first pilot. This is a proposal, not a prematurely fixed numerical scale; measure the generated body and register feet before approval.
- Keep her complete head/braid, large bare hands, left wrist guard, boots and flag finial. Put the compact vertical flag on image-left/upper-left, pole in the original right hand. Set the flag finial above the head with a transparent margin, and keep its torn cloth and pole bottom fully inside the canvas. Expected flag-inclusive height about49-52 logical while body remains44; do not shrink the woman to fit the flag.
- Candidate full-frame104x64/origin[8,4] can be checked after native output; exact full-canvas fit remains evidence-based. Native fullcanvas, one shared scale and measured feet will govern all four poses.
- The named portrait shows no visible sword, while actual rules use a border long sword. Suggested rule-faithful idle addition: a sheathed sword with restrained hilt, leaving the original pole-holding hand and left wrist guard clear. The sheath side and eventual sword hand need coordinator staging approval. Do not silently switch the pole hand or falsely claim the sword is visible in the original card.
- Four inherited slots remain idle/walk-a/attack/walk-b. Once idle and equipment staging pass, opposite boot contacts and a clear drawn-sword action while retaining the gray banner can serve the existing shared attack slot. Gray-oath retains independent FX; one slot still serves both rule weapons, with that limitation explicit.

No imagegen call has been made for Laiya. Next required input is the coordinator's appearance-interface/identity staging contract, not a new user art authorization.

## Coordinator pilot authorization,2026-10-19 20:46 UTC
Generate one independent transparent idle at about44logical body height, full1586x992 canvas. Plain gray flag, brown side braid, woman identity, original right-hand pole and left wrist guard remain. Rule border-blade authorizes adding one understated short straight sword in a complete belt scabbard; this is a reviewed battle-equipment addition absent from original card. Later attack may draw it with the left hand while right hand keeps the banner. Remaining poses await idle and appearance-interface review. Do not derive body scale from whole flag height.
