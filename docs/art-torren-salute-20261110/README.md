# Torren's authored victory salute

This slice replaces only `campaign-torren`'s celebration binding. Six original
painted poses lift his sword, hold a proud salute, lower it and return to ready.
The spans are 100/120/160/240/140/140 ms, totaling 900 ms. The existing receipt
clock plays the winning cohort together and restores each actor's own idle.
Torren's breathing, thrust, recoil and fall, other actors, attack preview and
combat rules remain unchanged. Production integration is data-only.

## Source and extraction

The built-in image tool generated `source-row.png` from `prompt-v1.txt`, using the
official original Torren and accepted breathing row as references. This adapts
[sprite-gen's component-row method](https://github.com/aldegad/sprite-gen), not
its CLI or Grok pipeline. No separate provider or API credential was used.

Independent source/native-size review accepted identity, equipment ownership,
anatomy and fixed elevated camera. The high and hold drawings deliberately
form a 400 ms salute, not two different large movements. Both boots retain their
footprint. Native observations below verify the blade clearance and idle
transitions in one actual victory.

`extraction-review.json` records source/component hashes, geometry, support
anchors, intervals and one uniform 0.09 scale. Significant alpha components
(>16/255, >1000 pixels) identify six figures. Original nonzero RGBA within three
pixels is preserved with four transparent padding pixels. The extraction removes
109,225 detached pixels of at most 7/255 alpha, without repainting, interpolation
or per-picture resizing. The runtime PNG contains the six original components.
`verify-extraction.py` reproduces this exactly and checks live manifest values.
A one-channel in-memory RGBA mutation is rejected; disk files are untouched.

The first contact-sheet column is accepted breathing idle, followed by the six
salute poses, shown in both directions. `anchored-salute.gif` is a single-play
offline diagnostic with idle at both ends, not captured gameplay.

## Ownership and acceptance

Only the ordered committed `gameOver` event starts celebration. Positive-HP
actors must belong to the winning After-state team, remain drawn and be visible
both before and after. Neutral outcomes, dead/absent units and hidden units cannot
acquire this action. The longest eligible clip overlaps the original 220 ms
result beat, so the result panel waits for Torren's entire 900 ms action.
No extra event owner, timer, result flag or gameplay decision is introduced.

The unchanged generic fixtures still test concurrent 128/384 ms clips. New
production-art checks cover exact pose intervals and padding, mirrors, root,
contact, shadow, RGBA, winner eligibility, completion and breathing restoration,
plus cancellation, rebind, tree exit, hidden-art lookup and reduced motion.

## Actual victory playback

The [legal victory package](verification/legal-victory/README.md) reproduces the
official campaign opening and 214 legal actions, with independent core receipt
comparison and save/reload validation. Torren survives with 98 HP on open ground
at (21,18). Archer 9 attacks enemy 18 from (27,18), without moving, to produce
the terminal victory. The input save has SHA-256
`4b4da235f178826876b6bfde6b1922d43c62013024d23673aacc8e86e874df77`.
An earlier lawful candidate put Torren behind a roof; ordinary legal movement
created this unobstructed checkpoint instead. No HP, map, receipt or rule edits
were used to obtain the result.

The normal Godot window loaded that exact save and accepted the final attack
through the existing UI. `verification/native-salute-lossless.mp4` contains all
100 consecutive 960×640 native frames, stride one, at fixed simulation 60 fps.
The salute starts at observations 45/51/58/68/82/90, then observation 99 restores
breathing frame zero and reports `presenting=false`. Root/support position stays
constant and every salute pose retains uniform 0.09 scale. A decoded RGB
comparison verifies every video frame exactly against its native PNG source.
The raw traces, PNG/RGB hashes, input/assembly hashes and transitions are recorded
in `native-salute-frames.jsonl` and `native-salute-proof.json`.

`native-salute-phases-3x.png` is a nearest-neighbor diagnostic crop of those
actual frames. Full viewport start, raised sword and restored idle captures are
also retained. The separate `native-victory-result.png` is a native F12 capture
after the sequence, not part of the video. The 900 ms result gate is also checked
with the controlled receipt clock. Readback affects wall-clock pacing; these
observations do not establish real-time frame rate or exported-build performance.

Independent native PNG/trace review accepted all six poses, consistent identity,
equipment and sword length, stable feet, unobstructed raised blade, and both idle
handoffs. The normal foot status marker overlaps part of the boots without
obscuring the action. Breathing uses its existing 0.093 scale and salute 0.09;
their visible body sizes join without a conspicuous pop. This is frame-by-frame
native review, not direct continuous-video or performance acceptance.

## Verification status

- Native build: 0 warnings, 0 errors. Recorded and tested assembly SHA-256:
  `d53dc0e8938c2697330cabd15e761f98376690f50a983502fdbcbf5e4f545b4b`.
- Animation checks: 4055 assertions passed. Generic concurrent 128/384 ms clips,
  other actors' 220 ms beat and dead-Torren result timing remain covered.
- Two isolated-data negative controls were rejected: changing the first two
  spans by +1/−1 ms while retaining 900 ms, and restoring `static.celebrate`.
  Exact-RGBA extraction mutation was also rejected.
- Data comparison against `62a2a07c` confirms that removing the added asset/clip
  and restoring Torren's single celebration binding recovers the baseline data.
- Initial new-test coordinate and formatting errors were corrected. An old
  reattached-board check awaited completion at 221 ms; its run was stopped, and
  the expectation now explicitly verifies pending at 220 ms and completion at
  900 ms. No production timing, valid assertion or timeout was weakened.
- The full native aggregate is running and is not yet claimed as passed.

Only one native facing and one legal victory are captured here. The other facing
has static image review and controlled-clock geometry coverage. Other characters'
genuine actions, exported builds and complete-campaign acceptance remain separate
work. Feature-branch remote CI is not implied by local verification.
