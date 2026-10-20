# Native battlefield camera and gestures

The camera owns presentation zoom and scroll only. The battle state, cell geometry,
playability, legal actions, and turn order remain owned by their existing modules.
The screen applies `BattlefieldCamera.Zoom` and `FieldOrigin` to its single world
transform. The board has no additional camera transform.

## Recovered source contracts

- `packages/game-ui/src/ui/battlefield-camera.ts` owns scroll clamping, safe-center
  focus, screen-pixel panning, reveal margins, and resize scroll retention.
- `packages/game-ui/src/ui/game.ts` opens at the living first-human unit centroid,
  falling back to the map center. Later resizes retain scroll pixels rather than
  fitting the map or preserving a logical center. Zoom does preserve the safe
  viewport center, not the mouse location. Pending zoom deltas coalesce per frame.
- `packages/game-ui/src/ui/board.ts` opens at zoom 1.25 and clamps to 0.5–2.2.
  A 32-pixel cell therefore opens at 40 screen pixels even on oversized maps.
- `packages/game-ui/src/art/letterbox.ts` owns one pointer gesture. Primary/left
  and middle buttons drag at Euclidean displacement **at least six screen pixels**.
  Sub-threshold movement does not advance the last pan position. The first pan
  therefore includes the whole displacement from the press. Returning to the
  starting point never converts a drag back into a click.
- Primary tactical clicks occur only on matching release, using the current
  transform. Release-only displacement can start a drag. Middle never clicks;
  secondary/right presses are immediate. Playable-cell validity remains the
  caller's responsibility, including irregular holes and authored scene margins.
- Capture loss/cancel ends a matching gesture without clicking. Foreign pointers,
  mismatched release buttons, and secondary touch starts are ignored. Hover is
  suppressed throughout a held gesture, including before the drag threshold.
- Ctrl or Meta wheel yields `-sign(deltaY)` zoom notches, each worth 0.1 zoom.
  Wheel magnitude is irrelevant. Unmodified wheel remains platform scrolling.
  The original has no independent multi-touch pinch implementation.
- Only AI `command` actions with a path of at least two points and an existing
  pre-action actor follow the final path point. Human moves, stationary commands,
  casts, forced movement, and arbitrary selected-unit changes do not auto-follow.
  Explicit idle-unit cycling/deployment selection use separate source focus calls.

`BattlefieldFocus` reads detached transition facts and an injected cell-to-field
geometry function. The native transition contract additionally suppresses follow
for actors excluded by canonical pre-action fog visibility, preventing a camera
side channel. It never reconstructs movement or visibility rules.

## Integration boundary

`BattlefieldCamera(fieldSize, sceneInsets, initialFocus)` uses unscaled logical
field pixels. Scene insets are authored art outside the tactical field. Viewport
padding is separate: it reserves overlaid HUD bands. A split-panel native layout
already excludes its HUD from the map area and must pass zero padding, not reserve
those bands twice. The original full-window CSS padding (top/right/bottom/left) is
68/302/96/260 above 1180 px, 68/272/96/232 above 900 px, and 68/272/76/28 otherwise.

`Resize` receives usable layout notifications; zero-sized notifications are ignored.
`QueueZoom` accumulates deltas; call `ApplyPendingZoom` once per frame. Its boolean
indicates an actual scale change, including false at a clamp. `CenterOn`, `Pan`,
and `Reveal` all use the same scroll/clamp path. `ToField` only inverts projection;
it does not decide which cell exists or may receive an action.

`BattlefieldGestures` receives explicit projection and output callbacks. Button IDs
are source IDs 0/1/2, not Godot enum values. Its boolean results mean prevent-default,
not whether a callback fired. The screen owns platform input routing and capture,
including continuing an active drag across panel boundaries and canceling on focus
loss, rebind, presentation lock, and unmount. Disposal is terminal.

No new inertia, edge panning, mouse-anchor zoom, fitted view, auto-follow selection,
or direct multitouch semantics are introduced. Source browser smooth scrolling has
no authored duration; this helper supplies exact clamped target geometry only.
Native scroll interpolation, if selected by the screen, is a platform presentation
choice and must not be reported as an exact source timing port. Browser wheel
scroll distance is likewise not a recovered fixed constant.

## Verification

`bash tools/godot/camera-checks/run.sh` executes the original TypeScript camera and
pointer code to generate temporary vectors, then compares the C# helper's state,
ordered callbacks, drag state, and default-prevention result after every operation.
The pure runner also covers initial focus, AI/human/hidden-path distinctions,
inverse transforms, cancellation, and finite-value boundary checks.

`bash tools/godot/camera-checks/run.sh --native` additionally copies the exact helper,
contract, and test files into an isolated temporary Godot project and runs
`res://Scenes/CameraChecks.tscn` headlessly, without building Core or changing shared
project outputs. It checks the actual Godot world scale,
projection/inverse, inset edges, resize/zoom behavior, pointer-to-field mapping,
and AI focus without advancing canonical state. These are automated semantic and
native transform checks, not desktop usability or screenshot evidence.

Add `--verify-guard` to compile a temporary threshold mutant (`> 6` instead of
`>= 6`) and prove the live source vectors reject it. The checkout is not mutated.
