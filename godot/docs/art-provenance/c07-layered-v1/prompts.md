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
