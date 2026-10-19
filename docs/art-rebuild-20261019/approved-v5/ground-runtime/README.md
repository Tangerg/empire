# Approved meadow runtime binding: visual gate pending

This is offline evidence from actual runtime `BoardPiece` output, not a live
browser or gameplay capture. The checked-in runtime sampling remains **512×512
logical units from world origin (0,0)**.

## Result

- The approved grass master now resolves through a typed runtime material and
  one formal source/binding/UV/export manifest. It is not an SVG preview-only
  substitution.
- Only base meadow (`surface-meadow`, tone 0) changes. Hill tone 2, hard surfaces,
  forest/shore masks, connected road/water sheets, bridges, scenery positions,
  structure placement, movement and collision remain unchanged.
- The full c01-02 map is shown: 22×14 cells, 704×448 logical units. It includes
  9 terrain structures and 20 initial unit pictures, with all real scene trees.
- The 512-period field shows a real vertical large-plane repeat break at x512.
  Matching the outermost pixels does not make the authored shapes periodic.
  This is **not visually complete**.
- The comparison at 1024 logical units avoids that repeat inside this 704px map
  and reads as quieter broad planes. It is explicitly diagnostic-only, not an
  approved runtime choice. The identical 1024px HD raster is uniformly enlarged
  to a 1024-unit period: its density becomes 1 pixel/unit, and its 2× diagnostic
  is an enlarged view rather than a new native-HD export. Larger maps would
  encounter the repeat again.
- Both complete integration frames expose oversized/overlapping buildings and
  legacy river/bridge/road pieces. Those are separate outstanding art issues;
  none were hidden by crop, deletion or repositioning.

## Files

- `field-full@1x.png`, `field-full@2x.png`: approved 512-period runtime base
- `integration-full@1x.png`, `integration-full@2x.png`: complete actual map
- `field-seam-crop@1x.png`, `field-seam-crop@2x.png`: camera crosses x512
- `integration-seam-crop@1x.png`, `integration-seam-crop@2x.png`: same crop with real layers
- `*-period-1024-diagnostic@1x.png`: same full camera, sampling comparison only
- `*-period-1024-crop-diagnostic@1x.png`: same crop, sampling comparison only
- `export-provenance.json`: immutable source, technical resize/seam and pixel hashes
- `runtime-provenance.json`: actual renderer placement, map hash and comparison declaration
- `raster-validation.json`: exact field crop checks and measured composite differences

SVG files preserve the renderer markup with portable repository-relative asset
URLs. Native PNGs normalize nested SVG viewport clipping only for Inkscape.
Field-only camera crops are byte-exact at both densities. The complete composite
has maximum color-channel differences of 1 at 1× and 2 at 2× in the offline
renderer; its original geometry is identical. This is reported, not called an
exact match. No browser/GPU visual verification or animation playback is claimed.

## Verification

- TypeScript typecheck passed
- All 96 Python art tests passed; field export `--check` passed
- Experience-lab production build passed and contains the new resolved material
- All 104 architecture assertions passed with a 20-second per-test allowance;
  the initial default 5-second import test timed out under concurrent load
- Replay sweep: baseline a2a8bfb and this branch match byte-for-byte across 60
  battles/profiles and 48,029 output lines. SHA256:
  `af965d1720053ac60d409bf960c607714a33665e2f90854da6047e5e7f2b1cd2`
- New guards cover source checksums, full-canvas derivation and unchanged interior,
  stale outputs, old atlas fit/identity, world phase at tile and period boundaries,
  cropped edge blocks, all sixteen chapter topologies and bounded texture sharing
- Final Vitest: all 92 files and 791 assertions passed (`--maxWorkers=2 --minWorkers=2 --testTimeout=20000`). The run is not fully green because it also exposes `container.scrollTo is not a function` in
  the keyboard UI test. The same error was reproduced independently on a2a8bfb;
  it is an existing jsdom test-environment gap, not a grass change

At 81×51 game cells the world base uses 24 blocks and four distinct cached
pictures, all at most 512² logical units. Each references one shared HD URL;
no source image or data URI is embedded separately per tactical cell.

Reproduce using the commands in `tools/art/README.md` under “Approved broad field
material”. Water/road/shore source promotion remains outside this bounded commit.
