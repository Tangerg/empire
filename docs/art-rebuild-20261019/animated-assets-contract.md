# Animated unit and building export design

Design only, 2026-10-19. No generation, implementation, or animation acceptance implied.

## Verified source contracts

Truth: each pack's manifest-final-*.json; C01 HD manifest overlays pixel source but logical anchors remain unchanged. Preserve each topic's actual frameWidth/frameHeight, footprint and order; do not derive dimensions from category alone.

- Combat: 40 topics per campaign, four horizontal frames [idle, walk-a, attack, walk-b]. All anchors [frameWidth/2, frameHeight-1].
  - C01:28 at32×48,6 at64×48,6 at64×64
  - C02:30 at32×48,3 at64×48,6 at64×64,1 at96×64
  - C03:30 at32×48,7 at64×48,3 at96×64
- Mission:24 per campaign, four horizontal frames [idle, walk-a, carry-or-work, walk-b]. Same anchor formula.
  - C01:24 at32×48
  - C02:23 at32×48,1 at64×48
  - C03:18 at32×48,5 at64×48,1 at96×64
- Interactive structure:24 per campaign, three horizontal states [normal, damaged, captured], each64×64, sheet192×64, anchor[32,63], footprint[2,2]. These are states, not an animation loop.
- C01 HD must verify actual manifest records and use2× frame dimensions and sheet dimensions. C02/C03 have no assumed HD. Fail if record deviates rather than inventing one.

packages/game-ui/src/art/runtime-raster.ts lines106–108 uses one idle frame, two walk frames at6.25fps, and one nonloop attack frame. This contract cannot produce a fluid multiframe attack animation. Structure occupants painted into a state image cannot move independently; separate character sprites/layers would be a new renderer feature, not achieved by regenerating structure states.

## Recommended source format

Prefer one separately generated PNG per semantic pose/state, all sharing exact authored canvas dimensions, camera, facing, character scale, costume/equipment, lighting, and a marked conceptual foot contact coordinate. Use first accepted idle/normal as reference for all later poses. Four independent prompts without that reference risk identity drift.

Proposed new typed override (do not overload existing static source record):

```json
{
  "kind":"frames-v1",
  "frames": {
    "idle":{"source":".../idle.png","sha256":"..."},
    "walk-a":{"source":".../walk-a.png","sha256":"..."},
    "attack":{"source":".../attack.png","sha256":"..."},
    "walk-b":{"source":".../walk-b.png","sha256":"..."}
  },
  "placement":{"sourceCanvas":[512,768],"sourceContact":[256,752],"scale":0.05859375}
}
```

Numbers are illustrative; final per-topic scale/contact must be reviewed and recorded, not inferred from bbox. Scale is one shared scalar for all frames. Translation maps sourceContact onto declared logical anchor, retaining whole canvas inside target. Reject any transform whose canvas falls outside target; choose a smaller common scale or explicitly fix generated framing. Never auto-fit each pose independently: that makes body size pulse and feet jitter. If a full source canvas cannot map foot contact to anchor with a transparent margin, adjust explicit source framing/scale or use imagegen to correct it, not crop feet.

Alternative verified sheet source: one RGBA PNG plus explicit named integer rectangles per pose and sha256; rectangles must be within source bounds, nonoverlapping, same size, exactly cover required semantics, and individually pass alpha boundary tests. Do not assume imagegen respected equal panel boundaries. Mechanically extracting declared rectangles is okay; regenerating crossed boundaries requires imagegen. Prefer per-frame files for the pilot.

For structures use the same format with normal/damaged/captured keys. Same walls/foundation/perspective must register across states; damage changes meaningful parts, captured changes readable allegiance, not random layout. No duplication to pretend three states exist.

## Deterministic packing

1. Resolve stable topic metadata and strict frameOrder/stateOrder from manifest; exact key set required, no missing/extra/duplicate labels.
2. Validate all hashes, pack-owned source paths, PNG RGBA, identical authored canvas sizes, transparent boundary (existing <=1 raw alpha residue rule), near-opaque subject, no clipped limbs/weapons.
3. Transform all frames with shared reviewed scale/contact mapping. Preserve alpha, no chromakey or generated artistic changes. Use immutable masters and explicit transforms.
4. Pack row in manifest order with unmasked paste. Logical sheet is frameWidth×N by frameHeight; HD if declared is exactly2×. No resizing whole sheet across frame boundaries.
5. Gate each exported frame independently for boundary and contact; full-sheet perimeter alone misses bleeding between frames.
6. Export/check only registered topic, leaving manifests, gameplay and other files unchanged. Report hashes, ordered frame IDs, frame dimensions, actual alpha bboxes and placement.

## Acceptance gates

Machine gates: exact semantic set/order, frame/strip dimensions, source hashes, genuine alpha, bounded placement, no crossings, no duplicate decoded frame pixels, reproducibility, unchanged untouched assets. Identical frame rejection is only a cheap failure detector. Different pixels are NOT proof of valid animation.

Human visual gates at native resolution and2×: identity/costume stable; same facing/camera; feet and root contact stable; walk-a/b visibly opposite meaningful leg/arm positions; attack reads as attack with weapon/limbs intact; idle distinct where appropriate; structure states preserve geometry with clear damage/ownership differences. Inspect an alternating6.25fps walk preview for popping/sliding and static contact overlay. Do not claim smooth animation from two poses or classify arbitrary noise differences as valid movement.

Pilot recommendation: one common32×48 humanoid combat unit plus one64×64 structure only after icon and forest batches are integrated. One accepted4-frame unit is four source poses but one canonical completed topic; one accepted3-state structure is three source images but one topic. Do not count unreviewed generated poses as completed assets.
