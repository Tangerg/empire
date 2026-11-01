# C02 independent-material candidates — 2026-11-01

**Resource preparation only; NOT active runtime art or completed level acceptance.**
The production `c01-02.json` still selects its original painted registration.
No terrain, movement, roster, objective, reinforcement, bridge promise or save rule
is modified by these candidates. Registration and actual rendered placement must
be verified before adoption; generated similarity is not geometry proof.

Generated with the built-in image-generation tool, using the original
`assets/native/scenes/three-bridges.png` as the approved perspective/layout/style
reference. Full original alpha is preserved; no bitmap cropping, resampling,
background-removal script or texture-atlas rectangle is used. Hashes and dimensions
are in `manifest.json`; generation instructions are in `prompts.md`.

- `ground-candidate.png`: 1672×941 clean ground candidate. Removed standing houses,
  upright trees and bridge front rails. Fixed bridge decks, arches, far rails,
  roads, water, low shrubs, cliffs and rocks remain ground geometry. It is not
  claimed that the entire bridge is independent. The road/river/bridge landmarks
  still require alignment checks against the unchanged canonical registration.
- `stone-front-segment-candidate.png`: independent transparent short front parapet,
  one left post and an open right join, intended for repeated depth-separated
  sections. Perspective, join continuity and scale must be checked in engine.
- `wood-front-segment-candidate.png`: transparent timber rail, two beams with one
  left post. Selected cleanup revision retained original generated alpha. Dark
  RGB outside the silhouette can exist at alpha 0; it must not be confused with
  opaque ground or a painted black background. Visual halo inspection remains
  part of the engine material acceptance.
- `broadleaf-candidate.png`: independent round broadleaf tree. This preserves the
  existing broadleaf type rather than silently swapping it for a reused pine.

Four buildings and peripheral pines should first reuse C01 faithful materials
where their silhouettes/doors actually match. C02 needs its own pointer geometry:
opaque roofs still occlude rear actors but must not redirect every roof click to a
capture cell. Existing bridge rails have three depth segments per bridge. A whole
bridge sprite at one depth is not an acceptable replacement.

Source names for generated originals (outside repo):
- ground: `exec-0dc00ea0-5d2a-4472-8078-119e40f0b12b.png`
- stone: `exec-54fa4188-04af-4ddd-bebb-90de33ecc234.png`
- wood first version: `exec-f20b4dc7-9898-43d5-bd04-741a07705467.png`
- wood cleanup: `exec-cf0316f9-cea1-449b-8374-8200b0ae0214.png`
- tree: `exec-5fee475c-a209-43cc-b15f-a7b6134d30e8.png`

Before production adoption: validate all four building contacts and selection
cells, every bridge deck/front-rail depth transition, hidden actor cues and clicks,
normal pan/zoom, save/load, and the normal-AI escort. Resource preparation alone
passes none of those gameplay or complete-scene gates.
