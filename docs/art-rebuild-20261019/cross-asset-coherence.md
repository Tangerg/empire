# Cross-asset cohesion acceptance

## User requirement

The scene materials, characters, buildings, props and interface should feel like
one game when combined. Individual attractive cutouts are insufficient. This
requirement was reinforced on 2026-10-19 after the first batches.

## Shared art direction

- Restrained, matte cartoon rendering with layered, readable material detail
- Muted earth, gray-blue and olive foundations; stronger color reserved for
  meaningful faction, action, resource or state cues
- Consistent readable contour weight at the actual output size; avoid mixing
  heavily outlined sprites with soft painterly cutouts in the same scene
- Compatible elevated three-quarter camera and ground-plane orientation
- One coherent illumination direction and limited highlights; avoid oily
  surfaces, broad exterior glow, and dramatic lighting unique to one asset
- Scale and visual density judged using actual renderer footprints, source
  anchors and native sizes, rather than equalizing every source bounding box
- Ground contact and small contact shadows must agree with neighboring objects;
  an anchor is a pivot, not a requirement that the last opaque pixel touch it
- Source transparency, object identity, state cues and animation contracts
  remain independent mandatory gates

These are visual review criteria, not instructions to recolor or retouch pixels
programmatically. Necessary art corrections go back to image generation. The
separately approved terrain seam operation is restricted to its documented edge
bands and preserves source masters.

## Early composite milestone

After the first meadow materials are accepted, combine the actual exported
meadow, forest modules, a unit and the oath-console structure in a deterministic
preview. Include at least two unit poses and the three structure states in
separate comparison frames. Use source renderer evidence for tile scale,
placement, anchors and y-sort. Record any rule that cannot be reproduced.

The preview must use real asset PNGs, not an image-generated illustration of an
imagined final scene. Keep a machine-readable placement manifest, source hashes,
output dimensions, native-scale view and enlarged inspection view. Preserve raw
masters and existing production contracts.

Review palette, illumination, contour, camera, relative scale, detail density,
contact, silhouettes, occlusion, and gameplay readability together. A mismatch
blocks cohesive-scene acceptance and creates a named correction task, even if
all participating individual assets passed their own checks.

## Evidence boundaries

A deterministic composite is an integration art review, not an observed browser
or gameplay screenshot. Actual rendering, map occlusion, animation playback and
HP/ownership state switching remain pending until exercised in the game. The
current browser socket restriction must remain disclosed in runtime status.

Repeat the composite review when a new major asset family or terrain material
is introduced, and before final acceptance of each campaign's full art set.
