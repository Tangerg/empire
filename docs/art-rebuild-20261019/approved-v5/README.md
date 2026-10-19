# Approved-v5 remake

The user approved the concept on 2026-10-19 at 12:19 UTC and the layered direction/full continuation at 12:47 UTC. Earlier bulk exports remain historical candidates, not approved-v5 completions.

## Four independent stages

1. Raw: original generated PNG, exact prompt, reference and alpha QA preserved
2. Export: one registered scale, native transparency/semantic/cohesion review
3. Runtime bound: canonical manifest, texture resolver and consuming UI tested
4. Live game verified: actual gameplay, motion, ownership/damage and occlusion observed

Static Inkscape composites and GIFs are deterministic previews, not live gameplay recordings.

## Reproduce sequence exports

Run from the repository root with Python, Pillow and Inkscape installed:

```sh
python3 tools/art/export_registered_sequence.py docs/art-rebuild-20261019/approved-v5/animation/swordsman/registration.json /tmp/empire-swordsman-export
python3 tools/art/export_registered_sequence.py docs/art-rebuild-20261019/approved-v5/buildings/lorne-keep/registration.json /tmp/empire-keep-export
python3 -m unittest discover -s tools/art -p 'test_*.py'
```

Registration configs reference immutable masters in this repository. Every pose retains its authored canvas at one shared scale; optional bounded translations align planted soles/foundations. Explicitly measured one- or two-pixel image-generation canvas discrepancies may be represented as transparent padding only, never separate pose scaling. Source alpha is not normalized. Runtime sheet publishing must use the canonical manifest and selected HD texture contract.

The swordsman bundle has four authored poses, not a high-frame-count animation. Lorne Keep has three authored states; state reachability depends on existing game bindings and is recorded separately.

## Authored team palette and recovered-pose provenance

`manifest-authored-team-variants.json` maps the existing enemy colour `#a8493f`
to the independently authored red swordsman sequence and its own registered pivot.
Other palettes keep the canonical sequence. This is an art selection only; no
unit definition, ownership rule, or canonical topic count changes. Both PNG
densities are checked against the registered export, and the real unit-picture
resolver has red/blue/unknown-colour regression coverage.

The spearman's three recovered motion outputs were found intact after a worker
interruption. Their intended saved pose briefs are preserved, but the exact
historical tool-call prompts could not be verified. This limitation is recorded
in the raw bundle and is not presented as exact prompt provenance. The native
art was reviewed and retained rather than regenerated solely for bookkeeping.
Its attack support point is about 168 source pixels left of idle. A measured
whole-canvas translation at the unchanged scale, with a wider transparent frame,
aligns the planted support; no body scaling or source-pixel cleanup is applied.
Translations above four logical pixels require explicit measured source feet
for every pose, exact derivation from those feet, and full-canvas bounds checks.

## Formal world ground and full-map evidence

The broad meadow now has one typed source/UV/binding manifest. The selected
1024-logical-unit period uses the unchanged 1024px physical raster: one source
pixel per world unit, not fabricated native2x detail. The preserved512 alternative
shows a real large-plane repeat seam. Full704x448 actual-renderer diagnostics
retain every initial actor/building and expose remaining large-building overlap
and legacy road/water/bridge layers. They are static offline evidence, not live
GPU or browser acceptance. Other material families remain separately tracked.
