# Inspect a located site from its exposed ground

## Actual defect

In the C07 960×640 native battle, locate the granary task and click the highlighted cell (35,14). The old controller stayed Idle. A temporary site hover appeared, but disappeared on the way to the dossier button, which then opened the unrelated forces list. Clicking the building's separate facade instead pinned the site. The task location and practical inspection path were disconnected.

## Repair and ownership

- In fresh selection, an actual Ground hit on canonical capturable terrain now opens read-only Site inspection. The existing IsSite query remains the sole terrain authority. Ordinary ground does not infer an actor or structure, and existing Unit/Destination/Target command composition remains unchanged.
- Ground never implicitly opens recruitment; an actual Object hit retains the previous recruitment path.
- Selection preserves the original inspection hit kind. HUD projection uses this pinned kind and coordinate, not later incidental hover. This is necessary when capturable terrain shares a cell with a structure: exposed ground must not reveal unhit structure HP/owner. DossierSubject also retains the kind, preserving stale-opener rejection.
- No level, art hitbox, geometry, rule, save or replay facts changed. Capturing and recruiting still require their existing explicit commands.

## Verification

- Build: zero warnings/errors. Native format and diff whitespace checks pass.
- Negative control: remove only the Ground-to-Site selection branch, retaining the new tests. CameraIntegration exits 1 at the actual located-C07-ground inspection assertion. Production restored afterward.
- HUD: 1,253 assertions, including same-cell structure/actor boundaries, pinned-ground later Object hover, no implicit recruitment and preserved explicit building recruitment.
- Strict process gates also passed: CameraIntegration 374 assertions, DossierFocus 165, and BattleScreen real-session integration. Existing duplicate mouse-enter warnings remain, with no error/leak diagnostics.
- Camera integration verifies actual painted C07 ground picking, objective locate, native press/release, pointer departure and dossier opening without a revision or state change.
- Actual C07 960×640 GUI: the same highlighted ground click now shows pinned-site guidance; moving to the dossier entry keeps its identity, and ownership/healing/capture information opens without hunting for the facade. This is a controlled chapter-entry fixture, not continuous campaign play. No battle action was issued.
- Targeted integration is used for this UI selection/projection slice; no full 16-chapter replay/export/visual gate is claimed for this change.

Only small text evidence is included. No earlier withheld evidence, credentials or personal data.
