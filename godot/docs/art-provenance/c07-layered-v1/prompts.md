# C07 initial independent materials — built-in imagegen, unbound

All three are early candidates, not production scene changes or a completed city.
Input style/reference: original native/scenes/luojia.png and exact source crops.
No bitmap edits were performed with Python; crops/grids are inspection only.

- Blue stand (`exec-85df8cf8-6f64-4cf7-ab12-0e2513552ff8`): isolate the exact
  compact blue hipped-roof wooden object; retain solid golden lower panel and
  dark upper opening, warm city wood, two distinct visible front feet. Explicitly
  not a four-post walk-through shelter. Real transparency, no paving, foliage,
  surrounding house or new ornaments. Actual1073x1465 output.
- Training fence (`exec-d58e65b0-c106-462a-a036-aada99bf9db1`): three square wooden
  posts/two X-braced short panels, genuine transparent gaps, elevated original
  camera and sloping foot line, no weapons/background posts/stone plinth. Actual
  output1187x1325. **Rejected:** generated near-equal bays and added dark foot
  collars fail original material/gap witnesses. Do not use a positional tolerance
  to turn opaque legal gaps into a pass. One targeted structure revision pending.
- Clean ground (`exec-3871db08-a1dd-4bec-bc19-4112b5617d0b`): remove all tall city
  objects to a registered1536x1024 ground-only layer, preserve source streets,
  circular plaza, courtyard surfaces and dirt training yard. Output is not yet
  road/footprint validated; flat reconstructed building footprints are not new
  walkable rules. Independent buildings/walls/trees/props remain incomplete.

Blue front feet lie in canonical paved cells, but that alone is not a collision
failure: reviewed actual Torren sole points at the two nearby cells do not land
on the fixed wood-foot witnesses. Hidden rear base and all motions are not
certified. No terrain, site selection or current painted C07 contracts change.

## militia-barracks-core-v1-candidate — exec-801381e5-4f99-4fbf-90b1-f16274faff63
Built-in imagegen, transparent background. Reference1 is original C07 main-whole-raw crop (890,30),305x185. Reference2 is approved faithful-v4 west-house for material/outline only, not front-facing camera or orange roof. Requested one independent blue-gray tiled main barracks core, exact original camera/proportions/arched entrance, warm masonry/timber and dormer. Remove attached lower lean-to complex and all cargo/pots/racks/fence/kiosk/trees/walls/paving. Reconstruct only hidden structural wall, not extra doors or extensions. Preserve source-crop composition; requested4x1220x740 transparent canvas with visible structural contacts near452628(left timber),656572(door right jamb),804568(front wing timber); actual output dimensions must be measured, never assumed. True RGBA silhouette, no shadow backdrop/halo/text/characters. This is an unbound candidate; scalar fit, hidden completion footprint and actual depth are pending.

## militia-barracks-core-v2-candidate — exec-ef781f41-36ae-4670-83cd-5948045d1291
Built-in imagegen precise local edit of v1, transparent preserved. Requested only three existing stone-foot bottom edges: left-front573928→about575936, right door902843→899846, wing1134851→1135840; preserve camera/roof/windows/body/scale, no whole-image warp, no new slab/pavement/shadow. Actual output1609x977 and actual contacts must be remeasured, not taken from request. One bounded correction; no further subpixel generation. Original source localization uncertainty approximately1pixel remains explicit; source left witness is antialiased A61 rather than solid.

## 2026-11-01 Front single-span lean-to candidate v1
Built-in imagegen, transparent_background=true. Raw exec-1621b21d-bae0-49dc-bceb-92c41c824194.png. References: original crop938120..1008198, core-v2 for material only. Request: independent blue slate single-plane roof/eave with two slender timber posts, no main wall/cargo/fence/pavement/ground shadow; transparent below except structural wood. Conceptual70x78 geometry roof(9,16),(33,5),(60,15),(36,32), near post(35,34) to(35,72), left post(8,22) to(8,54); left hidden toe expressly new completion, not source-certified. No rear posts or solid dark recess. This is an unbound candidate pending uniform registration and actual material negative probes; post-to-roof proportions require checking.
