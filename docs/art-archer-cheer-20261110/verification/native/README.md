# Actual native celebration window

The accepted recording contains 100 consecutive 960×640 native framebuffer
observations at stride 1, from the final enemy's death pose 1 through both
archers' complete six-pose celebration and the first restored idle frame.
It is a selected late-death window, not the whole terminal attack.

`native-cheer-lossless.mp4` is lossless RGB at the engine's configured 60
simulation frames per second. All decoded full-frame RGB bytes were compared
exactly with the original 100 PNGs. The JSONL retains the original actor traces.
`verify-native-evidence.py` rechecks the video, eight archived full PNGs, every
diagnostic crop tile, contiguous render/process observations, stable actor
positions and support planes, six poses and the terminal lifecycle. Three
mutation controls reject pixel-hash, continuity and pose-boundary corruption.
The 3× nearest-neighbor sheets are inspection aids, not new character artwork.

At observation 041 the retired target is absent and Elin (3), generic archer (9)
and Torren (2) start celebrating. Archer frame starts are 041/047/054/064/080/090;
Torren restores idle at 095 and both archers at 099. The final observation has
`presenting=false`. Exact 900/960 ms timing is independently checked by the
controlled-clock tests; sampled frame boundaries alone do not establish exact
sub-frame durations. The separate `native-victory-result.png` records the
result screen after playback. Full output-save equality is in `../legal/`.

The final native route used only normal UI: F9 loads the isolated official save,
five Space presses select ready archer 9, Enter chooses its current cell, and A
commits its sole valid stationary bow attack on enemy 18. No HP, facing, unit,
camera or receipt was patched. Earlier exploratory camera panning was discarded
by reload; the accepted camera is the normal unit-selection camera. F8 was
pressed during death; no animation clock, stride or frame budget was changed.
F5 saved the victory state before clicking any battle-result continuation.

The session log deliberately includes an earlier capture that exceeded the
existing 256 MiB buffer and saved no incomplete sequence. The accepted second
capture completed 100 frames. An earlier unintended zero-event wait and invalid
pointer target were discarded by reloading the unchanged input before the
accepted run. These attempts are not presented as acceptance evidence.

Both visible archers face east/unmirrored in this legal scenario. Mirrored
coverage comes from source registration and controlled-clock checks, not a
second native movie. Trace data does not include opacity or an event list.
Pixel readback changes frame pacing: neither elapsed timestamps nor this video
claim real-time performance. See `../native-review.md` for independent visual
acceptance, including the command toast below the complete silhouettes.
