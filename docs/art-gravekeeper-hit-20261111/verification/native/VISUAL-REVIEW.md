# Actual native image review

The actual full-frame recoil-peak PNG and the keeper pose/transition diagnostic sheets were inspected for both orientations. The right startup capture was also inspected and is the title screen. The review did not substitute artwork previews for native game pixels. The parent operator separately completed and observed both live UI runs.

## Findings

- The keeper is visibly on the battlefield in both recordings. The source archer and surrounding units remain on-screen in the untouched full frames.
- Four distinct recoil poses are visible in the sampled native sequence: onset, peak, recovery and ready, followed by the declared breathing idle pose. The left-facing sequence mirrors the same authored action rather than using another character.
- The keeper's pointed steel shovel and oil lantern remain identifiable. The crop includes the complete keeper silhouette and ground contact. Contact/root registration is also checked against every trace observation, without camera or saved-state edits.
- The original `-33` damage number and impact artwork overlap the keeper's torso during the recoil. They are retained exactly. This overlap is a real effect in the game, not removed for a cleaner art presentation.
- The keeper diagnostic crop is `[393, 208, 454, 282]` in framebuffer coordinates. World-space trace positions are not used as screen crop coordinates. Nearby-unit fragments at the edge of a crop remain authentic native pixels.
- Recovery and the final completed presentation are present in the untouched stills and lossless video. The title-screen startup PNG is not described as a settled battlefield screenshot.

## Verification and limits

For each orientation, all 50 decoded video RGB buffers equal the original native PNG RGB buffers. Selected PNGs preserve original file bytes, every original trace object remains unchanged in JSONL, and every diagnostic image tile matches the declared nearest-neighbor 3× source crop.

The sampled visual review supports the findings above. It does not claim that a contact sheet is a new game rendering, that video preserves PNG alpha, that trace timestamps measure real-time speed, or that traces directly record effect opacity. Exact millisecond contracts come from the frozen source and dedicated authored-art tests, with the sampled timeline checked for compatibility.

Both complete F5 saves independently match the legal settled result except for the two freshly changed writer timestamps. Both native processes exited with code 0, and their unchanged logs contain no error or resource-leak diagnostics. The ordinary software-renderer/V-sync warning and environment-catalog diagnostic are preserved.
