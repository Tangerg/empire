# C16 memorial district presentation

This is a presentation-only revision of existing `c01-16`, based on C15 commit
`4813d0720d9bc0f17537544bc3ecf13e73854fa7`. The canonical level, campaign,
combat rules, HUD and camera implementation are unchanged.

The registered ground preserves all 425 cell centers and their terrain semantics.
Low organic forest clusters and muted paving leave room for unit silhouettes.
Nine independently selectable site bodies reuse the C01 faithful west-house asset.
The `(22,8)` house is smaller and offset locally so its facade and the inquisitor
can be selected separately. The site's selection cell and dynamic ownership remain
owned by the canonical battle; its drawing contact is not a new rule location.

Three small blank memorial plaques mark the existing zones `(6,3)`, `(16,8)` and
`(8,13)`. They are static, pointer-pass-through decoration, not switches, capture
meters or independently advancing objective state. The existing HUD owns the
projection of the `true_names` count and locates the actual target cells. Counting
requires a qualifying unit to finish an action, not merely preview movement.

The central cemetery colossus and eastern inquisitor retain their initial enemy
allegiance. Only the inquisitor is the named elimination selector in the main
objective. Laiya's survival guard, the 30-turn limit and generic battle outcomes
are not replaced by the artwork.

## Acceptance scope

Native 960×640 inspection separately selected the inquisitor unit card (88/88)
and its same-cell site. The central colossus card remained distinct. At 1180×812,
all three plaques can be read alongside the units; the existing objective locator
and map panning remain available at narrow widths.

The recovered production entry starts with a detached C15 T13 victory, settles it
through the campaign, and chooses either existing C16 story branch. It does not
prove a continuous run through C01–C14. Both branches share the same battle state
but retain different recognized/protected flags, named-dead relations and restraint.

A 35-command legal replay reaches the third name zone in T2. In the native UI,
unit 13 previewed `(10,13) → (9,13) → (8,13)` at 0/3, then executed wait and
advanced to 1/3. The saved battle, event stream and campaign match the replay;
only save timestamps differ. This is objective-interaction evidence, not a C16
victory. Existing ending checks use fixtures and must not be reported as a played
victory or as two distinct ending nodes.

`MemorialDistrictChecks` covers the registration, canonical source binding, exact
objective markers, all site pointers and unit silhouette picking. Manual visual
acceptance remains separate from those geometry and state checks. The scene is
included in the native aggregate gate; Linux export acceptance is separate.
