/**
 * How this campaign's board art looks, shipped inside the picture.
 *
 * These rules were in the shared base stylesheet, then in this pack's own — which
 * fixed the ownership and left the real problem: a stylesheet is not in the room when
 * markup is rasterised into a texture. A GPU backend would have drawn this campaign
 * with no prop shadows and no terrain grade at all.
 *
 * An SVG document may carry its own style, and it applies both when the document is
 * mounted and when it is turned into an image. So the art carries it. One source of
 * truth, no per-element repetition, and the contextual rules — the same prop wears a
 * different shadow in the foreground than on the ground — keep working as written.
 *
 * The board still overrides all of it while the scale is moving, which is why that
 * rule needs its "important": this arrives later in the document than any stylesheet.
 */
export const CANDIDATE_01_BOARD_STYLE = `<style>
/*
 * Three rules stood here: an isolate on .candidate-map-scenery, a screen blend on
 * .candidate-map-ambient, and pointer-events on the three scene wrappers.
 *
 * The isolate confined that blend to the scenery group. It was the only blend in
 * the repository, and no module emits the class it was declared on. The group it
 * isolated is gone as well: a scene layer is its pieces now, and the renderer's own
 * layer is the group. pointer-events moved to app.css, where every layer that is
 * art rather than interaction is named together.
 */

.candidate-ground-route, .candidate-ground-route-edge {
  filter: drop-shadow(0 1px 0 rgb(255 230 176 / 10%));
}

.candidate-scene-foreground .candidate-environment-prop {
  filter:
    drop-shadow(0 2px 1px rgb(3 9 6 / 78%))
    drop-shadow(0 6px 5px rgb(3 9 6 / 46%));
}

/*
 * Three rules stood below, and all three named a *layer*:
 *
 *   .candidate-map .layer-terrain { filter: saturate(1.08) contrast(1.02) brightness(1.04) }
 *   .candidate-map .layer-ground  { filter: saturate(1.06) contrast(1.025) brightness(1.035) }
 *   .candidate-map .layer-foreground .candidate-environment-prop { filter: … }
 *
 * A layer is the renderer's own, not a picture's: the GPU backend makes it as a
 * container and bakes each picture from its own markup, so no selector that has to
 * reach through a layer can apply there. The first two were a 4% grade on the whole
 * ground that only one of the two backends applied; the third is already covered by
 * the scene-foreground rule above, whose ancestor really is inside the picture it
 * dresses.
 */

.candidate-scene-foreground .is-frame-bottom, .candidate-scene-foreground .is-frame-left, .candidate-scene-foreground .is-frame-right {
  filter:
    drop-shadow(0 3px 1px rgb(3 9 6 / 82%))
    drop-shadow(0 8px 7px rgb(3 9 6 / 52%));
}

.candidate-map .candidate-action-spot, .candidate-map .candidate-selection-ring, .candidate-map .candidate-cursor-ring {
  filter: drop-shadow(0 1px 2px rgb(0 0 0 / 72%));
}

.candidate-map .elevation-badge {
  opacity: 0.82;
}

.candidate-scenery-tree, .candidate-scenery-topic, .candidate-scenery-prop, .candidate-scenery-building, .candidate-environment-prop {
  transform-box: fill-box;
  transform-origin: center bottom;
}

.candidate-scenery-prop image, .candidate-scenery-topic image, .candidate-scenery-building image, .candidate-environment-sprite image {
  image-rendering: auto;
}

.candidate-scenery-prop, .candidate-scenery-topic, .candidate-scenery-building {
  filter: drop-shadow(0 1px 1px rgb(0 0 0 / 42%));
}

.candidate-environment-prop {
  filter:
    drop-shadow(0 1px 0 rgb(5 12 9 / 62%))
    drop-shadow(0 2px 2px rgb(5 12 9 / 34%));
}

/* A thing that stands tall — a tree at a wood's edge, a cliff pillar, a granary. */
.candidate-environment-prop.is-standing {
  filter:
    drop-shadow(0 1px 0 rgb(4 10 7 / 74%))
    drop-shadow(0 3px 3px rgb(4 10 7 / 42%));
}

.candidate-map .runtime-unit-contact-shadow {
  opacity: 0.62;
  filter: blur(0.55px);
}

/*
 * The ring is a hint now, not the answer.
 *
 * It used to carry the whole of "whose unit is this", because the sheets are one
 * grey army — so it was drawn at full strength and a formation read as a row of
 * bright ellipses with figures standing in them. The figures are tinted now, so the
 * ring can go back to what a ring is for: seating a unit on its cell and marking a
 * hair of team colour where the feet are.
 */
.candidate-map .runtime-unit-team-ring {
  stroke-width: 1.45px;
  opacity: 0.72;
}

/*
 * The figure, not the strip.
 *
 * This said ".candidate-map .unit .board-strip", and a board-strip is a nested
 * svg whose viewport is what shows one frame of a spritesheet. A filter on such
 * an element defeats that clip in Chrome: the filter applies to the element's
 * rendered content and the viewport clip goes with it. So every unit in every
 * campaign battle painted its whole four-frame sheet — five soldiers came out as
 * twenty, in rows, with one ring under each fourth of them.
 *
 * Nothing here could see it. The digest pins markup and the markup was right; the
 * tests assert structure and the structure was right; jsdom has no layout, so no
 * test in this repository could have caught it. It took a screenshot of the built
 * app, which is why this is fixed rather than shipped.
 *
 * The figure group holds the body and the strip together — the same element a
 * mirror flips — so the filter lands on the already-clipped result.
 *
 * (And no back-quotes in here. This whole stylesheet is one template literal, so a
 * quoted class name in a comment closes the string and turns the CSS after it into
 * expressions — "map is not defined", thrown when a battle mounts.)
 */
/*
 * A standard reads against a roof, or it is decoration.
 *
 * The pennant is small and lands on top of a lit, detailed building, so it gets the
 * same treatment the figures get: a dark seat underneath so the cloth separates
 * from whatever it flies over.
 */
.candidate-map .candidate-owner-banner {
  filter: drop-shadow(0 1px 1.1px rgb(0 0 0 / 70%));
}

.candidate-map .unit .figure {
  filter:
    drop-shadow(0 0 0.65px rgb(255 246 222 / 45%))
    drop-shadow(0 2px 1.2px rgb(0 0 0 / 75%));
}

.candidate-battle-prop, .candidate-battle-marker, .candidate-portrait-image {
  image-rendering: auto;
}

.candidate-battle-prop {
  filter: drop-shadow(0 2px 2px rgb(0 0 0 / 42%));
}

.candidate-battle-marker {
  opacity: 0.78;
}
</style>`;
