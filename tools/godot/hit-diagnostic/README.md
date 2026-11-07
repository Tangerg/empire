# C04 hit sampling diagnostic (new experiment)

This is **not restored hit artwork**, not an art release, and not the lost profile.
It uses the existing `c01-04-facing.json` lawful 91-action prefix and final order.
The source is gravekeeper 9; the damaged recipient is **swordsman 12**. It does
not certify a gravekeeper receiving damage. All prefix hashes, final state hash,
event hash and ordered presentation receipts must match the saved fixture.

`markers` changes only in-memory hit bindings for the recipient's unit type to
three already shipped poses: `walk-a`, `attack`, `walk-b`, each 50 ms. They are
**timing markers, not authored recoil art**. Total hit length remains 150 ms;
source release, movement, damage, event order, contacts and rules are untouched.
`shipped` leaves the static hit binding unchanged. No image is generated or
modified, and the production manifest is never edited.

## Run

From the repository root, with the existing official toolchain configured:

```sh
bash tools/godot/hit-diagnostic/run.sh /tmp/my-hit-run cached markers fixed60
bash tools/godot/hit-diagnostic/run.sh /tmp/my-real-run cached markers realtime
bash tools/godot/hit-diagnostic/run.sh /tmp/my-old-run uncached markers realtime
python3 tools/godot/hit-diagnostic/summarize.py /tmp/my-hit-run /tmp/my-real-run
```

Each output must be new. The runner copies `godot/` to a retained staging
project inside the output directory and writes its location in `staging-path.txt`. Nothing is deleted. Allow at least 1.3 GB of free disk space per run. Build
and runtime processes are bounded. Saves have a separate temporary namespace.
`uncached` replaces only the copied `PaintedSurface.cs` with the exact version
from `1d5cbd0a4f0adc84ce71d9fbe05ae8775197bf2a`. Input SHA-256 hashes, source commit,
build log, native log and JSON trace are retained. The engine's existing native
process gate rejects nonzero exits, errors and leaked native objects.

`fixed60` uses Godot `--fixed-fps 60` with the headless dummy renderer: it is an
offline logic diagnostic, not 60 real rendered frames per second. `realtime`
uses ordinary process deltas, with a graphical display when DISPLAY is set,
otherwise headless. The trace records the actual display driver. Do not compare
headless results to prior rendered runs as equivalent workloads.

On a working graphical display, `movie60` runs Movie Maker with
`--fixed-fps 60 --write-movie OUTPUT/movie.png`, also collecting viewport PNGs
after `RenderingServer.FramePostDraw`. This mode is not real-time. `post_draw`
means rendering-server completion, not monitor presentation or visual acceptance.
PNG readback and encoding add cost. Current headless results contain **no**
post-draw samples or images. The graphical path remains unverified until actually
run on a functioning display.

## Reading results

The trace contains process delta, accumulated presentation time, wall time,
process/drawn frame counters, selected recipient sprite frame, impact presence,
and completed receipt count. `start` includes synchronous transition preparation;
the last wall time also includes that preparation and settlement.

The summary's frame runs list **first and last observed process samples**, not
exact pose start/end times or exposure duration. For fixed60, a transition lies
between adjacent samples (16.67 ms apart), rather than being proven to happen at
the reported first sample. Real-time gaps can span entire 50 ms intervals. A
missing sampled frame is not proof that it never reached a real display. These
checks cannot establish art quality, natural gameplay, campaign completion, FPS,
or the cause of the lost candidate's behavior.
