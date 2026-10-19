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

Registration configs reference immutable masters in this repository. Every pose retains its authored canvas at one shared scale; optional bounded translations align planted soles/foundations. One-pixel image-generation canvas discrepancies may be represented as transparent padding only, never separate pose scaling. Source alpha is not normalized. Runtime sheet publishing must use the canonical manifest and selected HD texture contract.

The swordsman bundle has four authored poses, not a high-frame-count animation. Lorne Keep has three authored states; state reachability depends on existing game bindings and is recorded separately.
