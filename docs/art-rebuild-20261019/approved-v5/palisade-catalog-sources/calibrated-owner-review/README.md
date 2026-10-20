# Corrected ordinary-cell contact contract

The initial audit and source handoff conflated the immutable atlas default with mutable derived per-cell contact metadata. That incorrectly imposed [48,91] on each new source, forcing tiny previews. This was our audit error, not defective source layout or an exporter limitation.

`export_environment_overrides.render_cell` lines80–103 already fits the complete authored canvas and derives contact from the actual rounded HD transform. `plan` lines199–215 writes that derived contact to ordinary cell metadata. Existing registered stone-wall-straight already has [48,64.3604465709729], demonstrating this is established behavior rather than a newly relaxed rule. Atlas [48,91], dimensions96/192, footprints, obstruction height and slot order remain fixed.

Using the same measured source feet and full-canvas92/1254 scale at[2,2], straight/corner/breach regain readable native silhouettes. The integration owner inspected full sources, native96/192 against white/black/olive and original catalog identity. Eight straight stakes, joined V corner, and broken open gap with rubble/fallen timber are retained. Wood planes and braided ropes remain more detailed than a strict two-tone ideal, but the broad matte silhouette is readable. Source alpha is predominantly near-opaque, not literally all255, as documented in the original review.

Selected three cells are catalog-only. No gameplay placement, passability, scene topology, renderer behavior, registration guard or source pixels were changed. Only slots6,7,9 differ from the prior runtime atlas at both densities; all other13 slots, including gate and bridges and existing stone cells, remain pixel-identical. Both exports reproduce exactly in check mode. A focused test verifies derived per-cell anchors directly from the unchanged transform owner while protecting atlas defaults and footprints.

Gate remains unbound because its front-facing camera differs from the original oblique subject. Its size/anchor is not a blocker. A future correction should address camera only, not the withdrawn groundline requirement. No palisade regeneration was executed.
