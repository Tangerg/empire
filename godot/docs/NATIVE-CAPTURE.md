# Native viewport observation

`res://Scenes/NativeCapture.tscn` wraps the real application scene. It requires a
real display and an absolute `--capture-dir`. F12 saves one viewport and its
actor geometry. Without idle options, F8 retains its original behavior: arm or
disarm capture of the next committed transition, including its settled frame.
The harness is excluded from `ExportRelease`.

## Idle sequences

Pass `--capture-idle-frames=<positive integer>` to make F8 immediately observe
that many frames from a visible settled board. Optional
`--capture-idle-stride=<positive integer>` saves every Nth rendered frame; its
default is 1, and it requires the frame-count option. Missing values, zero,
negative values, nonintegers, overflow and duplicate options fail startup rather
than silently selecting a default. F8 on a hidden, absent or busy board declines
the request; it does not arm a later capture.

For example, after building/importing normally, use a fresh isolated save
namespace and the real application:

```sh
source tools/godot/env.sh
XDG_DATA_HOME=/absolute/isolated-save-namespace \
  "$GODOT_NET" --path godot --audio-driver Dummy --fixed-fps 60 \
  res://Scenes/NativeCapture.tscn -- \
  --capture-dir=/absolute/capture-directory \
  --capture-idle-frames=60 --capture-idle-stride=4
```

Enter or load the intended battle normally, then press F8 while it is settled.
This observes 240 rendered frames and retains frames 4, 8, ..., 240: approximately
four simulated seconds at fixed 60 Hz, with 15 saved observations per simulated
second. Alternatively, fixed 15 Hz with stride 1 retains the same observation
cadence but changes the application's simulation step. Neither mode establishes
actual rendering performance. `elapsedMs` is observed wall time, not clip time.
Encode the example's saved sequence at 15 fps, not 60 fps, for simulation-time
playback. Inspect `processFrame` deltas as well as the rendered-frame indices.

The same observer handles finite and idle sequences. It awaits `FramePostDraw`,
reads the actual viewport and records asset identity, frame, mirror, scale,
position, support plane and offset. It never advances or resets the battle,
animation phase or presentation clock. Idle capture rejects a finite transition,
hidden board, board replacement, or replacement of its canonical projection,
including between saved frames. Rejection saves no buffered sequence.

Images are held in memory until observation finishes, then written to
`idle-<timestamp>-<sequence>/frame-NNN.png` with matching JSON. The existing
256 MiB budget and guaranteed disposal remain in force. At 1180×812, 60 RGBA
images require 229,958,400 bytes and fit; larger viewports can exceed the cap.
An aborted observation can leave an empty output directory. A filesystem error
during writing can leave partial output, which is not an accepted recording.

## Verification checklist

- No idle options: F8 still waits for a committed transition and captures its
  settled frame; F12 remains a single screenshot.
- Valid idle options: exact saved count, expected observed-frame stride, asset
  identity and mirrored/contact data in each trace; all idle traces have
  `presenting: false`. Verify an actual pose wrap rather than inferring it from
  elapsed time alone.
- Invalid/duplicate options and stride without frame count: explicit startup
  failure, not a fallback capture mode.
- Start a transition, hide the board, or replace/reload the board during idle
  observation: reject the sequence, dispose images, and allow a later F8 request.
- Repeated F8 during capture does not start another observer. Budget exhaustion
  rejects the sequence without saving buffered frames.
- Complete normal native build/format checks and actual-display checks before
  claiming acceptance; headless animation checks alone do not verify readback.
