# FX09 bounded semantic repair plan — awaiting parent review

No new images have been generated for this study. The current quartet remains an export-verified candidate, not an accepted completed ambient animation.

## Root cause

The current replacement drops first appear with rounded bottoms at source Y 434, 405 and 391. They are already well inside the image, so the dominant upper form can look like an old drop jumping upward. Surviving steps also vary from 32 to 266 source pixels, producing a slow seam beat. Timing correctness does not fix either issue.

## Recommended plan

Redraw all four phases using the current rainfall master as the common style and composition reference. Preserve its muted blue-gray palette, dark matte outline, simple pale inset, approximately 114 × 170 pixel drop bodies, original low beads and shallow crown. Keep the 1254 × 1254 canvas, source pivot (627, 627), and existing whole-canvas export transform unchanged.

Every surviving large drop advances by the same vector: X −30, Y +260 source pixels per frame. This is approximately X −0.67, Y +5.81 logical pixels per frame. New cohorts appear near the top boundary, with a top near Y 80 and rounded bottom at Y 250. No replacement starts in the middle of the image. All complete drawn forms retain clear source margins and zero exterior alpha; no frame is clipped, fitted, translated or synthesized in code.

The previous cohort's last visible rounded bottom is Y 1030. It reaches the fixed water contact level at Y 1075 early in the following 83⅓ ms interval and becomes the authored local splash/beads. Its replacement is a different drop entering at the top. Extrapolating the replacement backward by one step puts its rounded bottom at Y −10, above the source canvas, rather than somewhere visible within the scene. This gives the entry a coherent physical explanation.

The bounding boxes below use 114 pixel width and 170 pixel height for the main drop body. They are generation targets and review landmarks, never export fitting instructions.

| Phase | Left cohort bounds | Center cohort bounds | Right cohort bounds |
|---|---|---|---|
| 1 rainfall | (362, 340)–(476, 510) | (558, 860)–(672, 1030) | (778, 600)–(892, 770) |
| 2 splash-onset | (332, 600)–(446, 770) | (648, 80)–(762, 250), NEW | (748, 860)–(862, 1030) |
| 3 splash-crown | (302, 860)–(416, 1030) | (618, 340)–(732, 510) | (838, 80)–(952, 250), NEW |
| 4 rain-renewal | (392, 80)–(506, 250), NEW | (588, 600)–(702, 770) | (808, 340)–(922, 510) |

## Identity and impacts

- Left: phase 4 at bottom 250 → phase 1 at 510 → phase 2 at 770 → phase 3 at 1030 → impact near X 354, Y 1075; a different left cohort enters in phase 4
- Center: phase 2 at bottom 250 → phase 3 at 510 → phase 4 at 770 → phase 1 at 1030 → impact near X 610, Y 1075; a different center cohort enters in phase 2
- Right: phase 3 at bottom 250 → phase 4 at 510 → phase 1 at 770 → phase 2 at 1030 → impact near X 800, Y 1075; a different right cohort enters in phase 3

Thus all three surviving drops advance down-left by exactly the same vector on 4 → 1. The highest drop still changes identity at a birth event, as it should in continuous rain. The plan does not falsely treat that replacement as the same individual moving upward.

## Ground-water phases

1. Rainfall: four low separate beads, contact bottoms around Y 1075. The central leading drop approaches but does not overlap a bead in the middle gap
2. Splash-onset: the center impact forms a narrow shallow crown around X 610, from Y 1025 to 1075; a fresh center drop enters near the top
3. Splash-crown: central water spreads to roughly X 430–830, with short prongs no higher than Y 970 and small detached beads. The right impact contributes near X 800, but the contact baseline stays at Y 1075
4. Rain-renewal: the broad crown breaks into separate low beads; the left impact contributes near X 354. Three substantial rain drops remain. Phase 1 continues low beads, so no full crown appears suddenly at the seam

## Required redraw subset

All four phases require authoring changes. The current phase 1's center bottom is 895, while the constant-step cycle requires 1030. Retaining it would preserve the uneven cadence that caused the review concern. Phases 2–4 require both near-boundary entries and corrected survivor positions. The current rainfall master remains the common visual reference; the existing accepted-looking materials and identity are not redesigned.

A three-frame repair retaining the current rainfall frame was considered. It could use different constant velocities for each lane, but it would retain unnecessary trajectory differences and would be harder to judge. It is not the recommended plan.

## Acceptance and limits

This plan eliminates middle-of-canvas births and the current large velocity variations. A four-frame animation still has discrete entries; the plan cannot by itself prove that no perceptual pop remains. Approval must inspect actual authored positions, source and native full/seam strips, cohort size consistency and impact/birth correspondence. Unique hashes and APNG timing remain separate mechanical checks.

Before any imagegen calls, the parent should approve or revise this coordinate plan. After approval, generate the first two corrected phases and review their source/native geometry before spending calls on phases 3 and 4. Reject material drift rather than fixing placement or alpha in code. No browser retries are part of this study.
