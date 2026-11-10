# Recorded C01 archer recoil review

Accepted for the visible character poses, both facings, planted contact and
hit → idle → counter → idle handoffs in the southern exchange. All 95 captured
frames were examined for each archer (190 actor crops). No redraw or runtime
workaround was needed. This is frame inspection, not continuous realtime viewing.

## Observation sequence

Indices are zero-based frame filenames, not milliseconds. All records have
stride 1, observedFrame 1–95 and consecutive processFrame 2870–2964.

- Elin 3, unmirrored: idle 000–023; recoil frames 0/1/2/3 at 024–026,
  027–032, 033–038, 039–042; idle 043–048; counter shot 049–085;
  idle again 086–094.
- Enemy 18, mirrored: move 000–004; shot 005–040; idle 041–068;
  recoil 069–070, 071–077, 078–082, 083–087; idle 088–094.
- Final frame 094 has presenting=false.

Both directions visibly lean back at head/shoulders and change defensive arm
pose before recovering. Hood, face orientation, quiver, costume, bow hand and
visible bow outline stay coherent. No visible size pop, hand swap, detached
weapon or foot slide was found. The source review separately inspected the
unobstructed full figures. Impact streaks and rising numbers are not counted as
character frames.

Elin's recorded root/support stays [843.5823, 511.488] in all 95 frames. Enemy
18's stays [939.5823, 500.0313] from 005 onward, after its normal movement.
Both recoils use absolute scale 0.072 and their four declared anchors. Exact
320 ms semantics are established by deterministic timing tests, not inferred
from screenshot wall time.

## Visible limitations

- Elin's -39 covers the central chest/abdomen/defensive arm during 024–048,
  including the complete recoil. Her completion badge covers part of the
  screen-right lower leg and lower bow throughout 000–094.
- Enemy 18's -20 covers the central torso/free arm during 069–093, including
  the complete recoil. Its completion badge only appears at 094.
- Existing impact crosses cover lower body/legs at 024–028 and 069–073;
  bright bursts touch the boot contact at 029–033 and 074–078; fading fragments
  persist below the feet through 048 and 093 respectively.
- Covered chest, buckle, string and hand details cannot be re-approved from
  these native frames alone. This is not an all-body-pixels-unobstructed claim.
- Southern bushes do not materially obscure either silhouette. Another actor's
  lower edge at the top of the enemy crop does not overlap the reviewed archer.

Crops use original viewport boxes Elin (333,257,405,338), enemy
(442,232,540,324), with right/bottom exclusive. The phase sheets magnify by 3×
nearest neighbor; they do not hide, repaint or replace any gameplay pixels.
All original RGB pixels remain in the lossless video.

## Excluded settlement recording

The earlier 99-frame roof route is legal and its official F5 save matches the
independent replay. Key images 075 and 092 show that the roof hides most of unit
9's body and feet. That recording was not given a full art review and contributes
zero frames to the 95-frame visual acceptance count. Its video, hashes and traces
remain only as a diagnostic and input/state witness.

## Scope

This recording does not accept native cancellation, reduced motion, fatal/death
handoff, all overlap layouts, exported performance or a complete campaign.
Those first three contracts have separate synthetic native tests. No UI overlays,
map geometry, camera behavior, game rules or attack-preview logic were changed
for this inspection.
