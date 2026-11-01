# C04 normal-AI route validation — 2026-11-01

## Result and boundary

The independent scene at `ead3f129df8916141ad907087a40f9929888529c` was played
through the real native GUI: **T6 primary-objective victory**, all **9** entering
allies alive, **4** enemies remaining, **24** engagements shown in settlement.
This is the three-clue/withdrawal route, not the alternate enemy-elimination win.
All five enemy turns T1–T5 used normal AI. No synthetic commands, combat reloads,
damage avoidance, rule edits, or modified battle states were used.

Entry continued the actual C03 aftermath and chose normal `分兵取证` preparation.
C01 prerequisites still originate from the clearly labeled entry fixture; actual
C02 T11 and C03 T4 results were carried forward. This does not certify a continuous
C01–C04 campaign. Elin was already fallen and remains fallen; there were no new
friendly deaths in C04.

## Objective and interaction evidence

- T5 Torren: (20,12) → (21,12), clue A / `found_prisoners`. His visible feet in the
  blue opening selected the correct unit. At the preceding curtain-covered square,
  normal Space unit cycling selected him; a hidden body was not falsely claimed
  mouse-visible or made clickable through the roof.
- T5 reserve guard: (23,15) → (24,15), clue B / `found_oath_template`. Front-pole
  ordering and desk/character foot separation were visible in actual play.
- T6 banner guard: (27,18) → (28,18), clue C / `saved_witnesses`; clicking his
  visible opening body selected him. Three clues completed before withdrawal.
- T6 Laiya: (9,20) → (9,21), `escort-west-exit`, sequence and primary completion.
  She deliberately waited outside after an earlier legal visit, so this proves
  the post-evidence arrival path rather than depending on a pre-positioned hero.

Normal combat also exercised support interception, retaliation and healing:
Mirelle fell to 19 HP and retreated without reloading, then healed to 49; the
cleric survived at 44, Laiya 78, Torren 75. Settlement advanced to `aftermath-04`;
carried health ratios are Laiya 67%, Torren 76%, Mirelle 63%. C05 was not entered.

## Local evidence and hashes

Root: `/workspace/scratch/84c9fe948755/empire-evidence-20261101/`.

- `c04-manual-play-evidence-index.json`: complete capture/checkpoint index and hashes.
- `c04-formal-play-v1/native-003.png`: actual entry; 005 front-pole interaction;
  007 blue opening selection; 008/009 red arrival; 010 white arrival;
  011 legal withdrawal preview; **012 victory; 013 settlement**.
- `c04-terminal-FIXTURE.json`, SHA-256
  `c1284038c3e051a72cdccc756bc92b1fad49740bf24a93deba83b0ed387feec5`.
- `c04-settled-autosave-FIXTURE.json`, SHA-256
  `a9ab771039e48443acf903b324f059b20175aa48644a5d873384194aa4a12dd7`.
- `c04-terminal-runtime-FIXTURE.log`, SHA-256
  `eff9190898b553ecdd7915a582bbbdf609eefcdb9a3732a53646e35f6d1b3a26`.

After settlement, the unchanged formal `BlackFlagCampChecks` suite was rerun:
**332 assertions pass** (`c04-terminal-checks.log`). This rerun uses the already
built ead3 runtime; later commits only added unbound C05 images/documentation.
It is independent synthetic coverage, not part of the actual victory claim.

## Remaining boundaries

The far eastern cart and every possible furniture/roof pixel were not visited
manually. Existing physical/input probes cover the cart neighbor and audited feet;
they are not a substitute for exhaustive manual interaction. Incidental native
captures reported about 27–42 FPS, not a controlled performance benchmark.
Dense/soft ground versus crisper independent props remains a cross-C03/C04 visual
coherence item. No claim that every art detail is finally polished follows from
the successful route. No gameplay or canonical source changes were needed here.
