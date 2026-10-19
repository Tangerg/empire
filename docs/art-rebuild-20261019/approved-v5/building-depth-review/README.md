# Full-field integration checkpoint

All 22×14 cells, nine actual buildings and twenty initial actors are retained. This is offline rendering of the real presentation markup, not browser/GPU gameplay.

Canonical presentationScale now keeps the original registered footing while displaying Keep at0.65, granary/registry at0.6 and shelter at0.65. Source images, gameplay footprints and level positions are unchanged. Old full-scale overlap evidence remains in formal-map-current.

The SVG and Pixi unit layers now sort standing positions by y, then x and stable ID, including movement. Strike nudges do not change standing order. A two-backend regression covers crossing neighbours. The offline review was corrected to follow the existing terrain-before-scenery contract; previously it put buildings over scenery and hid some real overlap.

Remaining blocker: standing settlement props still paint above all terrain buildings because static scenery and terrain have separate global layers. This can put haystacks over a doorway despite different world feet. A shared standing-object depth contract is being audited. c01-02 has zero overUnits pieces; the apparent tree/actor problem must not be falsely attributed to foreground tree layering. Dense adjacent actors and mixed old/new sprites remain visible. Top-edge structures may extend outside the battlefield, which the actual wider scene frame can display; this field-only view does not add white overscan.

Water now uses512 logical world sampling and1024physical native2x material with unchanged28cell occupancy. Legacy road/bridge/shore art remains pending formal integration.
