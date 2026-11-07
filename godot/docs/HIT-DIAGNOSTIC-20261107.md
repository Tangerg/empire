# C04 hit sampling: new experiment, 2026-11-07

## What was recovered and what was not

The lost hit candidate and original profiling directory were not recovered.
This experiment instead reuses the committed lawful C04 facing fixture: 91 prefix
orders, then gravekeeper 9 attacks swordsman 12. It is not the same as the lost
probe, and it is not a gravekeeper receiving a hit.

The new candidate is an isolated **timing-marker clip**, not new or accepted hit
art: the recipient's existing walk-a, attack and walk-b poses hold for 50 ms each.
The authored total remains 150 ms. Only in-memory presentation bindings inside
a copied diagnostic project change. Rules, source release, movement, damage and
production manifests remain unchanged. The fixed clock is an explicit diagnostic
mode, not a game-clock change in production.

## Verified results

Six serial headless runs passed the native process diagnostic gate. Each retained
all 91 prefix state hashes, before hash `cfc58c1a`, after hash `4ff092cd`, and event
SHA-256 `45e9e44a59c57619fb070c4a684b6091645eee8e335f4b6cac612a45bde182a0`.
All seven presentation receipts completed in their original order. No state was
injected or mutated by the experiment.

- Fixed60, cached markers: selected frames 0 → 1 → 2 → 3 → 0; max delta 16.67 ms.
- Fixed60, uncached markers: the same sequence and delta.
- Realtime, cached markers: selected frames 0 → 1 → 0; max delta 148.50 ms.
- Realtime, uncached markers: selected frames 0 → 1 → 0; max delta 144.05 ms.
- Shipped static-hit controls, fixed60 and realtime: frame 0 throughout.

For both fixed marker runs, the first/last sampled presentation times were:
frame 1: 250.00–283.33 ms; frame 2: 300.00–333.33 ms;
frame 3: 350.00–383.33 ms; idle first sampled at 400.00 ms.
These are sample bounds, not exact pose onset/end or physical display durations.
At fixed60 the change is bounded by adjacent 16.67 ms samples. In each realtime
run, frames 2 and 3 were absent from process samples. Thus coarse sampling can
skip the intermediate markers **in this new headless experiment**. This does not
establish the cause of the lost candidate's behavior.

Single-run wall times, including synchronous transition preparation/settlement,
were cached/uncached 1366.812/3919.001 ms at fixed60 and 1530.516/4631.615 ms with
realtime deltas. These are illustrative CPU-side observations, not repeated
performance statistics, FPS, or a rendered workload benchmark. The cache reduced
wall work here but did not guarantee every 50 ms marker was sampled.

## Limits and failed attempts retained

No graphical display was running. Local Xorg startup failed with
`Cannot establish any listening sockets`; the permission-escalated attempt
failed in the same way. Godot's headless driver is a dummy renderer. Accordingly
all six traces have zero post-draw samples and zero captures. Movie Maker PNGs,
post-draw viewport images and actual screen visibility remain **not verified**.
The opt-in tool supports a future graphical Movie Maker run, but its presence is
not evidence that the graphical path has passed. Offline fixed FPS never proves
real-time smoothness, frame_post_draw never proves monitor presentation, and
neither establishes visual quality.

An early overly broad marker binding failed because a refugee asset has no
attack frame; binding was narrowed to the actual recipient's unit type. An early
headless signal-disconnect error was fixed, and passing runs have no such error.
One later copy exceeded the /tmp tmpfs capacity. The failed log was retained,
temporary copies were moved to disk, and the runner now stages inside its output
directory. No original project or art files were deleted or overwritten.

## Reproduction and evidence

See [the diagnostic guide](../../tools/godot/hit-diagnostic/README.md).
Raw six-run traces, input hashes, build/runtime logs and summary are under
`godot/docs/verification/hit-diagnostic-20261107/`.

The six serial runs used source commit 04a134cb. Its full tracked Git tree is
**identical** to the latest remote parent e6dabd33:
`61ec7062892dad708b54d6661ffa9dfffb47874e`. Publication was prepared in a separate
clean worktree based on e6dabd33; no old commit history was merged or force-pushed.
Final whitespace-only source formatting was followed by another isolated smoke
run. Build, formatting, metadata, shell syntax and summary hash-rejection checks
are recorded separately from runtime/render acceptance. No production source
changed, and this focused diagnostic is not a new full native aggregate pass.
