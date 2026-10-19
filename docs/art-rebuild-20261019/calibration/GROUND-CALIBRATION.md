# Ground-only study after Legends of Kingdom Rush reference review

The user requested more living ground and explicitly prioritized Legends of Kingdom Rush. Reference: official Steam screenshots at https://store.steampowered.com/app/1904860/Legends_of_Kingdom_Rush/ . The observed direction is broad calm grass/earth fields, irregular grass edges with localized detail, and readable figures. No official artwork was copied into the game.

Two new imagegen source identities are preserved: opaque ground-base and transparent grass-mass. These are candidates, not accepted production art. All source pixels are unchanged. The original 128-logical repeat visibly produced camouflage rhythm and is retained as failed evidence. The 512-logical source sampling changes display scale only; no blanket recolor, blur, or alpha cleanup is applied.

`ground-ab-large-uv.png` compares A greener ground and B earth clearings. B uses the previously generated muted road material as a proposed broad ground surface, without changing terrain rules. Both use two identical grass masses centered at world [128,176] and [232,352], visible widths160 and144. Raw canvas and aspect ratio are retained; actual water and bridge cells are protected by an explicit presentation mask. Roads, water, bridges, buildings, trees, people, camera, and all object registrations are byte-for-byte equivalent in their provenance to the prior final selected study.

These are art-study composites, not live gameplay captures. No shadows were added in this ground-only comparison. Existing omitted legacy layers and deliberate identity/state substitutions remain explicit in each provenance JSON. Gameplay, production atlases and bulk remake state are unchanged.

A is substantially quieter at512 sampling; B remains earth-dominant and has more pebble detail than the official clearing reference. Neither is a full scene-style acceptance. Further changes remain bounded by the current style review.

## Revised broad-mass comparison

`ground-ab-broad.png` is the current A/B review board. Both now share masses centered [128,176] and [232,352] at visible width288, plus a peripheral mass at[48,384] width160. The first two alone covered38.85% of reviewed land by alpha>=128; the third raises coverage to44.04% before road overlays. A conservative lower bound treating every road cell as completely uncovered is39.43%. This measures actual alpha, not rectangular bounds, inside crop cells[0,3,14,10], excluding water/bridge cells.

B uses an explicitly authored renderer background fill RGB[174,149,118], based on the measured muted earth material mean. This removes the pebble texture without altering any generated image. A retains the512-logical green source. The comparison preserves exactly the prior object's source registrations and camera. Both remain style candidates; this is no claim of final target acceptance or gameplay verification.
