# Campaign dispatch surface

Built-in imagegen, 2026-10-27. New art, not copied from a reference game.
Source: `assets/native/hud/dispatch-board-v1.png`, 1536×1024 RGBA.
Runtime renders the [34,65]–[1502,935] board with a nine-slice source corner of 120 px and destination corner of 24 px. Text uses a separate native layout safe area. No raster filtering/editing was applied to the generated source.

Prompt: one empty rectangular campaign dispatch board, 3:2 landscape, straight-on orthographic view, transparent outside; hand-painted medieval strategy, restrained thin walnut border, four broad matte aged-bronze corners, dark warm charcoal leather writing surface. Calm nearly uniform interior, no text/icons/buttons. Broad coherent brush shapes, upper-left warm light. No glitter, speckles, dense scratches, bright dots, halos, bloom, thick ornament or isolated alpha particles. This is a reusable nine-slice material, not a mockup.

Visual review: source material is coherent with the accepted tactical wooden command rail; the new large surface reduces fine border ornament. Only the board rectangle is rendered; external generated transparent fringe is outside its source rectangle. Native-size UI and safe-area checks are required before acceptance.
