# GrayFlag desktop review, 2026-10-21

## Scope and actual input

The published `856e5b79` source was restored after a cloud-machine reset and built
with official Godot 4.6.3 .NET / .NET SDK 8.0.426. Actual cloud Linux window input
at 1180 × 812 navigated the title, four opening beats, battle briefing, the
`稳步推进` choice, staging, and the first battle. Selecting the knight and clicking
a highlighted destination moved it; F5 saved and F9 restored the position. After
closing and relaunching the application, campaign continuation and first-level
entry worked, and F9 again restored the earlier moved position. All saves were
isolated verification data outside the repository; no existing user slot changed.

## Demonstrated corrections

- Staging placed roster and campaign facts directly over detailed artwork. A
  padded panel using the existing campaign style now owns the readable content
  surface. The background illustration remains visible around it. Actual desktop
  inspection confirmed readable text, intact roster/status content, and a working
  battle-entry button after the layout change.
- Story dates mixed world year 318 and relative years 0–3 in the original authoring.
  Native date text now explicitly identifies campaign-relative years, and the
  opening retains the world-year anchor. No calendar arithmetic, gameplay state,
  save schema, or historical oracle changed. The final opening was inspected in
  the actual window, without clipping the longer date.

## Focused verification

Production build and import passed; affected C# whitespace checks and diff hygiene
passed. Existing native narrative and staging scenes exited successfully without
error or leak diagnostics. They retain paging/callback and translation coverage,
with explicit current date expectations and a staging readability-surface guard.
No full gameplay matrix was repeated for this presentation-only change.

The cloud renderer is Mesa llvmpipe. Its unsupported VSync warning is an environment
limitation; the cloud has no audio device, so the second inspection explicitly used
Godot's Dummy audio driver. The first-level scene and environment still report
source `runtimeReady=false`; rendering does not constitute authoring approval.

## Remaining acceptance and art issues

- Leia's roster portrait has a baked opaque light background: shipped
  `assets/runtime/narrative-static/char-leia-01.png` is 192 × 224 RGBA with alpha
  255 everywhere. Other roster members use transparent sprites. This asset-family
  inconsistency remains visible and needs deliberate source-art integration, not
  a widget masking workaround.
- This is a bounded first-level desktop flow, not full-campaign manual acceptance.
  Later unauthored scenes and final art/lighting approval remain open.
- No new export package or macOS execution was validated in this batch. Earlier
  Linux export evidence and old untested Mac artifacts are separate checkpoints.

## Character framing follow-up

`CharacterPortrait` now owns thumbnail framing for both dialogue and roster.
It uses the supplied texture's nonzero-alpha bounds, without modifying the source
texture, battle frame, palette, or battle anchor. Opaque artwork retains its full
rectangle. The temporary CPU image is disposed; the resulting atlas only borrows
its source texture. `KeepAspectCentered` preserves the entire visible subject.
There is no color-key removal, background mask, or duplicated portrait catalog.

Actual staging inspection showed the ordinary swordsman and archer at readable
thumbnail scale rather than fitting their large transparent animation margins.
Roderick's story portrait was inspected at 1180 × 812 and a manually resized
689 × 812 window: sword, shield, and feet remained visible. The existing native
narrative/staging scenes passed with exact alpha-region and non-cropping guards,
and their normal teardown emitted no errors. The beige backgrounds are still
present; any generated alpha replacement is a separate art decision.

## Transparent portraits and first-battle spacing

Leia and Roderick's approved generated transparent revisions replace their native
portrait resources. They were inspected at actual roster/dialogue sizes before
integration; they are redraws rather than lossless extraction. Five other opaque
portraits remain unchanged. Provenance is in `data/portrait-revisions.json`.

The first playable scene now has one authoring owner, `data/scenes/c01-01.json`.
The duplicate first-scene file and implicit loader default are removed. Native
`selected-assets.json` and `world-materials.json` own presentation scale/density;
the historical TS exporter writes its corresponding observations only under
`tests/oracle/source-v1`, never over these native documents.

At the same 1180 × 812 window and camera, actors render at 0.62 of their previous
visual scale, preserving frame geometry, contact anchors and tactical footprints.
Forest edge canopy density is 0.52, its placement scale is 0.62, and the first
scene's perimeter clusters fall from 28 to 13. Its road corridor is 20 logical
pixels instead of 32. Canonical level data, collision, deployment and objectives
are unchanged. Actual manual reload restored the same saved battle; selecting a
smaller swordsman still showed its full selection ring and legal-cell overlays.

This is an intermediate composition improvement, not final terrain approval.
Road fills remain too smooth, square forest-floor islands are visible, and the
highlands still read as isolated props. These are the next surface/elevation
work, not hidden by adding foliage or changing camera zoom.

The spacing checkpoint compiled without warnings/errors and passed the existing
native resource contract scene, including all 16 level projections. A manual
swordsman move completed on the revised first scene. Gameplay document bytes and
all sprite fields except presentation scale were compared unchanged. This is
functional verification and a visual candidate, not user acceptance of 0.62 as
the final art direction. No updated export or Mac run is claimed.
