# C01 faithful independent-scene completion — 2026-10-28

The user approved the west house / small pine / clustered ground sample at 12:33 UTC. This batch extends that exact direction to all four houses and the perimeter, without changing the approved HUD structure or any canonical terrain, elevation, movement, occupancy, building site, actor placement, campaign or save fact. C02–C07 retain their existing renderer and assets.

## Artwork and registration

- Ground and west assets remain the approved `c01-faithful-sample-v4` resources. Ground retains low undergrowth, rocks and cliff lips; it contains no standing houses or conifer crowns/trunks. It is not claimed that every raised stone is an independent object.
- Central, granary and east buildings were reconstructed directly from their original painted-scene counterparts. Each is a full transparent resource, not a UV patch of the ground. `houses.md` and adjacent prompts record provenance.
- All 93 existing decorative contacts remain unchanged. Three conifer silhouettes (approved west tree, tall tree, young tree) replace the old single broad-tree asset; authored world heights vary approximately 67–125 pixels. Low root shrubs move with each object; surrounding low vegetation remains ground dressing. `trees.md` records provenance.
- House sorting front is the doorway/anchor plane, not the front edge of decorative root shrubs. Real movement into the west entrance exposed and verified this fix: the old front coordinate buried Laiya and the nearby flag bearer under the house. Trees retain their physical root-front coordinates.
- Object textures use real alpha and imported mip levels with the existing LinearWithMipmaps renderer. The imported cache was rebuilt and actual loaded image mipmaps are regression checked; no global blur or bitmap cleanup filter was applied.
- One existing Chinese movement-preview hint was shortened to fit the command tray at 960×640; no HUD restructuring.

## Verification and limits

- `dotnet build godot/Empire.csproj --no-restore`: 0 warnings/errors.
- AssembledGroundChecks: 163 assertions, including real resource alpha/mipmap and entrance-plane checks; canonical state unchanged, site picking, pass-through tree coverage, depth, motion and attached-shadow ownership fixtures.
- AnimationChecks: 183 assertions. TacticalHudChecks: 210 assertions. PresentationChecks: resource contracts and authored terrain/connectivity passed.
- Native real battle: isolated existing C01 save, central-house pixel click selected its original entry; Laiya keyboard move to west entrance + wait executed before and after the sorting correction. Three captured frames in each sequence kept every observed actor position exactly on its projected support plane; walk phases and final idle were observed. Capture readback is not a performance benchmark.
- Native 1180×812 and 960×640 selected/preview/cancel states inspected, with readable HP/action controls. 960 camera minus-button zoom inspected.
- Native TerrainPreview at exactly 75%, 100%, 125%, same canonical initial state/camera; independent objects hidden in a separate run show no house/tree/attached-shadow remnants. Preview is not gameplay; low-zoom gray outside the finite ground plate is the preview canvas, not an extended map.
- Did not complete C01 or enter later levels. Did not manually circle every building. Front/back depth includes detached fixtures, not a claim of complete manual traversal.
- Previously known PaintedSurfaceChecks synthetic cliff/cue witness failure remains disclosed and was not suppressed or reported as green. It was not rerun for this asset-only batch. No automated feature-branch CI success is claimed.

Evidence outside the repository: `../empire-final-evidence/c01-faithful-complete-20261028/`. Same-camera original comparison: `../empire-final-evidence/material-cohesion-20261028/baseline/c01-01-001.png` versus `scene125/c01-01-001.png` in the new evidence folder; both TerrainPreview 1180×812, scale1.25/focus13,14. Actual HUD images have separately labeled battle state, not presented as identical preview captures.

Rejected v2/v3 comparison assets were preserved, not deleted or published, under `../empire-final-evidence/c01-assembled-20261028/retained-style-candidates/` with byte sizes and SHA256 manifest. Runtime catalog contains only active faithful assets.
