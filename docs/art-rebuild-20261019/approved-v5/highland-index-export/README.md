# Highland catalog-only index export

This extends the existing deterministic environment exporter only for the known
`cliff-modules-highland` sheet. It does not register semantic cells or select the
sheet in a level. The atlas remains `cells: []`, with the original 16 `cellOrder`
labels, 128/256 cell geometry, default anchor `[64,127]`, ramp slots `[5,6,7]`,
height range, blocking default and y-sort contract. No footprint, obstruction
height, deployment, navigation or level metadata is created.

## Source registration

`catalog-example-first-four.json` shows the exact schema and verified source hashes
for original slots 0–3. Keys are original slot labels, not file aliases or guessed
semantic IDs. The integrator must back up the original 1x/2x atlas to the listed
immutable baseline paths before merging this example into
`tools/art/environment-overrides.json`. The example does not perform registration.
The baseline hashes were read from main before the first highland export.

For the 1254-square masters, use `scale: 0.09888357256778309` (`124/1254`),
`origin: [2,2]`, and the recorded `sourceFoot` estimate. Full canvas is uniformly
resampled to 248-square HD pixels and pasted at `[4,4]` inside the 256-square cell.
Logical 128-square output is downsampled from HD. There is no crop, rotation,
recolor, alpha clamp or new painted texture. The existing alpha gate admits at
most alpha 1 native perimeter residue; full native bytes remain unchanged.

`sourceFootLogical` in the export report is QA only. It does not update the atlas
anchor. `logicalAnchor` remains `[64,127]`, with `contactCalibrated: false`. For
slots 0–3 the measured source-contact estimates project respectively to Y
107.8054226475, 112.5518341308, 99.8947368421, and 96.5326953748. These differ
from the default Y127; catalog export is not scene contact calibration or actual
level integration. No assertion of aligned cliff seams is made.

Reports use `label` and `index` for highland slots and explicitly state
`cellAddressing: original-slot-label-and-index`,
`anchorPolicy: atlas-default-preserved`, `integration: catalog-only`. Other
existing atlas report and semantic-anchor behavior remains unchanged.

## Verification

```sh
python3 -m unittest discover -s tools/art -p test_environment_overrides.py -v
# After the integrator registers backed-up sources:
python3 tools/art/export_environment_overrides.py
python3 tools/art/export_environment_overrides.py --check
```

The 39 environment exporter tests pass, including two-slot and four-slot
replacements with respectively 14 and 12 untouched slots identical at both
densities. They independently reconstruct the complete-canvas raster, compare
manifest bytes and native source bytes, require QA-only foot changes to leave
exports unchanged, reject changed slot order/semantic metadata, reject aliases
and numeric slot keys, and ensure a bad later source writes no output. Empty
semantic cells remain invalid for other supported atlases. A deliberate in-memory
one-slot destination shift produced the two expected new regression failures;
no production code was changed by the mutation check.

The original runtime atlas, source registration catalog and manifest are not
changed in this branch. The real-source proof below runs in an isolated temporary
fixture; integrating the registered atlas is owned by the main integrator.

## Actual original-slot 0–3 export proof

`actual-first-four-export.json` records the four original master hashes, both
original-atlas hashes, full manifest hash, all 16 before/after cell pixel hashes
at both densities, native alpha QA and transformed source-contact estimates.
`after@1x.png` and `after@2x.png` are the actual isolated exported atlases.
`full-atlas-before-after.png` shows all 16 complete cells at logical density; it
is a catalog diagnostic, not a cropped game scene or a level integration claim.
The source registration example is separate from the measured export report.

Result: only 0–3 changed. All other 12 slots are identical at both densities.
The complete manifest and native source bytes are unchanged, and exact repeated
export/check passes. Foot estimates remain above the preserved default anchor
by 19.1946, 14.4482, 27.1053 and 30.4673 logical pixels respectively. Sources and
alpha are not adjusted to conceal that missing scene calibration.

To reproduce after the integrator has created immutable baseline backups:

```sh
python3 docs/art-rebuild-20261019/approved-v5/highland-index-export/verify_first_four.py \
  --tool-root "$PWD" --source-root "$PWD" --output /tmp/highland-first-four-proof
```

The initial proof used `--original-runtime-baseline` before the first highland
export, reading original runtime pixels into the isolated fixture. That option
requires the exact baseline hash and intentionally fails after runtime pixels
change; normal reproduction reads the immutable backup paths from the example.
Only six image files and the manifest are copied, not a repository or worktree.
The source tree is read-only. The diagnostic assembles approved sprites and text
labels; it creates no painted source material.

The full art Python sweep was also attempted in the sparse worktree: 183 tests,
17 errors (including one class setup error), no assertion failures. These errors
are missing sparse-checkout support: environment-builder tool (11), old edge
runtime provenance (4), old water runtime provenance (1), and the stone-wall
material for wall-pilot setup (1). `all-art-tests.log` preserves the failures.
This is not a full-suite pass; the integrator validates the complete main tree.
The focused 39-test pass and deliberately detected wrong-slot mutation are also
retained as logs. No production renderer, map, or rules changed here, so no new
mission replay or live GPU check is claimed.
