# Torren's authored breathing idle

This slice changes only `campaign-torren`'s idle binding. It adds **four different
painted pictures**, played as six intervals (0, 1, 2, 3, 2, 1) over 1800 ms. The
chest, shoulders, elbows and equipment breathe while the boots stay planted.
It does not replace other actors' artwork or change battle rules, movement,
damage, saves, attack previews, the existing thrust, hit, death or celebration.

## Source and art decisions

The original identity/equipment reference is
`godot/assets/painted-units-v1/torren-stage01-v2.png`, especially its leftmost idle
figure. The accepted thrust source supplied material/style continuity. The
built-in image generation tool produced `source-row.png` from `prompt-v1.txt`.
This adapts [sprite-gen's component-row method](https://github.com/aldegad/sprite-gen);
it is not a run of that repository's CLI or Grok video pipeline. No separate
API credential/provider was used.

The six-picture generation preserved identity and articulated breathing, but
picture 5 dropped too abruptly from the peak to rest. `prompt-v2.txt` attempted
a targeted repair; `rejected-v2.png` did not reliably improve it and is not used.
Instead, the runtime selects the first four original pictures and retraces their
anatomical poses for exhalation. This is **four drawings, not six unique frames**.
There is no image interpolation, whole-sprite bob, rotation or stretch.
Independent art review approved this selection for native testing; final motion
acceptance is recorded with the verification evidence below.

The six interval lengths are 440/220/240/440/240/220 ms. Equal peak/low holds make
the direction changes deliberate. The relatively larger middle pose change and
the held endpoints must be judged in the actual small-size loop, not solely from
the source illustration.

## Extraction and registration

`extraction-review.json` freezes the source hash, selected components, original
RGBA hashes, rectangles, anchors, scale and reviewed playback order. Substantial
alpha components (>16/255, >1000 pixels) identify the six figures. Nearby original
nonzero-alpha pixels within three pixels of those components are preserved,
including small detached painted details. Only 13,791 distant pixels with alpha
at most 6/255 are removed. The two unselected complete figures remain in the
source document and never enter the runtime sheet.

Each selected component receives four transparent padding pixels. Retained RGBA
pixels are neither repainted nor rescaled in the atlas. A single 0.093 presentation
scale yields approximately 36-pixel low-breath body height, with natural upper-body
expansion. Contact anchors were registered to the same planted-foot baseline and
central support position; no independent per-frame scale fitting is used.

`verify-extraction.py` reproduces the selection and exact runtime pixels, verifies
the atlas hash, live rectangles/anchors, uniform scale and six playback spans.
It requires Pillow, NumPy and SciPy. A negative control changed one RGB channel
only in memory; the oracle rejected it. The native manifest remains the only
owner of live geometry/timing; this review record is historical evidence.

`native-size-contact.png` compares the original pose with the four selected poses
at their intended size. Its 3× version uses nearest-neighbor magnification only.
`anchored-loop.gif` is an offline diagnostic with the exact six intervals, not a
screenshot, captured gameplay or an interpolation/smoothness claim.

## Runtime and checks

Production integration is data-only. Existing `BoardView.Idle` owns modulo phase,
membership, shadow/depth refresh, hide/reduced-motion behavior and finite-action
suspension/resume. `ActorClip` and the receipt clock are unchanged. The original
base appearance remains the explicit finite hit/death/movement artwork; successful
actions and cancellation return to the new declared first idle pose.

Production-art checks cover four unique padded pictures, six half-open intervals,
wrap, large advances, both mirrors, fixed root/contact, held textures, hidden and
reduced motion, input/event/state isolation, and move/hit/death handoffs. Existing
Torren thrust checks now assert the declared idle on recovery/cancellation.
Historical synthetic fixtures pin their named static idle so they retain their
original test meaning rather than inheriting this new production art.

The verification-only `NativeCapture` observer now supports an explicit bounded
idle recording, sharing its existing `FramePostDraw` capture path. It does not
advance/reset the game clock or synthesize receipts. See
[`godot/docs/NATIVE-CAPTURE.md`](../../godot/docs/NATIVE-CAPTURE.md) for options,
buffer limits, interruption rejection and the distinction between saved frame
cadence, simulation time and real performance.

## Verification status

The reviewed native build completed with zero warnings/errors. Focused
AnimationChecks completed with 3,352 assertions and no error/leak diagnostics,
then the movement witness was strengthened from one cell to three cells and
450 ms so it crosses the first idle boundary. The final full aggregate has now completed against that strengthened source:
`bash tools/godot/verify-native.sh` exited 0 with no error/leak diagnostics,
including **3,353 animation assertions** and **20,359 played-flow assertions**.
`verification/full-native-passed.txt` and `full-native-status.json` retain the
complete result. The tested assembly hash matches both real-render recordings.
The played-flow count includes process-frame waiting checks; it is not a stable
count of unique gameplay scenarios.

All 16 independent-level action/event/receipt hashes, covering 3,425 legal
orders, match baseline `e0d50d13`. All six played-flow result markers also match:
C01 and C02 victories after 415 legal orders, then 21 orders in C03, plus the
opponent-caused defeat/retry probe. This is not C03 victory or full-campaign
acceptance. All 41 warning lines match the baseline multiset after normalizing
isolated temporary paths, including intentional save-rejection probes and
existing import/viewport warnings. No check was disabled or weakened.

GitHub reports no workflow run or status context for source commit `8211e683`.
The existing workflow triggers on main-branch pushes, pull requests or manual
dispatch, not this feature-branch push. This local native gate and captured
native artwork are not being presented as a remote CI result.

### Actual rendered idle and recovery

The real application loaded the isolated C01 turn-four checkpoint. A four-second
idle observation retained 60 full-viewport images across 240 rendered frames
(stride 4, fixed 60 Hz simulation). It contains two complete 0→1→2→3→2→1→0 wraps.
Torren's support point, actor position and scale are constant in every trace;
all other actors remain unchanged. Independent small-size review found no
blocking identity, equipment, groundedness or wrap defect. The raised chest and
shoulder silhouettes are visible without a whole-body floating effect.

`verification/native-idle-lossless.mp4` encodes those observations at **15 fps**.
Every decoded RGB byte matches all 60 original PNGs. `native-idle-proof.json`
records original PNG/RGB/video hashes, pose order, sampling and assembly identity;
`native-idle-frames.jsonl` preserves every raw state observation. Full unmodified
low/high viewport PNGs and the labeled 2× phase crop are adjacent to them.

A separate normal committed attack retains the default F8 capture mode. All 45
frames were saved at stride 1: the original walk pose, six thrust poses, then
`torren-breathe-v1` frame 0 from observation 31 onward. The game reports nine
damage and six battle events. `native-recovery-lossless.mp4` is lossless RGB at
60 fps; its proof and JSONL retain exact artwork/frame changes. This is a single
captured recovery, not a new attack implementation or complete gameplay claim.

Both videos reproduce simulation-time observations, not measured real-time
rendering performance. The normal idle and recovery sessions closed with no
ERROR/leaked-object diagnostic. Their logs disclose the graphics driver's
unsupported V-Sync warning and existing `runtimeReady=false` scene diagnostic.

### Observation failure boundaries

During idle recording, F9 replaced the board. The observer rejected the mixed
sequence with its explicit expected error and saved zero buffered frames. This
negative result is kept separately in `native-interruption-negative.txt`, not
counted as a clean positive session. Seven actual-display startup probes also
rejected zero, negative, text, overflow, missing/duplicate frame counts and stride
without a frame count, each with exit 1 and its expected diagnostic. Their logs
and historical invocation are under `verification/negative-options/`.

Budget exhaustion, hiding the board and repeated F8 during capture were reviewed
but not separately exercised in this manual run. Broader character animations,
complete-campaign gameplay and exported-build acceptance remain separate work.
