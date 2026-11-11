# C06 native play review

Reviewed production commit `96c443b2542ae90b0dbdc2ab9d277b20df51ff4a` at the formal minimum client size, 960×640. This review found no newly confirmed defect; no production code or gameplay rules were changed to manufacture a patch.

## Setup and scope

Native WhiteRiverChecks exported a fresh, production-load-validated C06 `people-first` opening into isolated QA storage. Strict `native-process.sh` completion returned exit 0 with 1,765 assertions. Its no-AI route and timing checks are separate from the GUI witness below. The GUI visibly identified itself as a detached chapter-entry fixture, not continuous campaign completion. No ordinary user save was used or overwritten.

## Actual GUI witness

1. Loaded the opening through F9, selected Torren, and zoomed out twice to inspect the wider crossing approach.
2. Ended the human turn with all 13 actors ready. The normal AI completed its actual turn; this was not a no-AI timing probe. A battle mage dealt 35 damage to a GrayFlag guard during the observed playback.
3. At human turn 2, Space selected Laiya and returned the camera to her. It did not accidentally activate the previously clicked end-turn control.
4. Clicked the visible Royal Knight to preview Laiya's move-and-attack. Preview: knight 103→69 (34 main damage), Laiya 116→84 (32 counter damage), and one turn of shaken on the knight.
5. Clicked the knight again to commit the ordinary command. Inspected the settled actors: knight 69/116, Laiya 84/116, both shown acted; the human ready count decreased to 12.
6. Opened the battle log. It showed Laiya's 34 damage, the knight's one-turn/one-stack shaken status, and the knight's 32 counter damage, matching the preview and settled HP. The log remained readable inside the viewport and closed with Escape.
7. Clicked the escort objective. The camera reached the north-bank destination and displayed the six target cells as a clear 3×2 gold outline. The objective remained 0/4 arrived and the ready count remained 12; locating did not issue another battle order.

## Limits and next coverage

This is one opening AI turn and one subsequent move-and-attack, not C06 victory, escort balance, complete route play, or a full 16-chapter regression. The existing WhiteRiver no-AI checks cover route feasibility and event/deadline rules, not normal-play difficulty. No claim is made about every camera position or every attack/counter case. No new screenshots or private fixture saves are committed. Previous withheld evidence is excluded.

The last full native gate remains the separately recorded objective-failure gate; this documentation-only round adds targeted WhiteRiver validation and the actual C06 interaction witness above. A subsequent round should continue another chapter or a full escort route rather than treating this opening sample as complete acceptance.
