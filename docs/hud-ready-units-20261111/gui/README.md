# Ready-unit HUD: real-window acceptance

Accepted on source commit `ece8b332070ec0b843c0000e9abea502e8774350`
on 2026-11-11. This follow-up used the real Godot 4.6.3 .NET Linux window,
native mouse/keyboard input, and a fresh isolated user-data directory. It did
not inject battle state or drive a synthetic scene. No implementation change
was needed after this pass.

## Played path and observations

1. Start a new campaign from the title screen, read the opening, choose
   **稳步推进**, and enter C01. Keep the client area at **960×640**.
2. Click **待行动 11 · 下个** twice: 莱娅, then 托伦. With that button focused,
   Space selects 艾琳 and Enter selects 守墓人, once each. Another mouse click
   selects 罗德里克. The count stays 11 and the camera reveals each unit.
3. Save with native F5 before and after the last navigation. The complete
   campaign/battle save and event list are identical except the two `savedAt`
   fields. Navigation does not spend an action or create a combat event.
4. Click **待机** for 罗德里克: count 11 → 10. Click **撤销**: count 10 → 11;
   the canonical save returns to the pre-Wait state.
5. Open **系统**, then click the visible navigation button outside the modal.
   This dismisses the menu without selecting a unit. Space on the restored
   System-button focus reopens the menu rather than leaking into the board.
   Escape closes it; a further Escape releases the button focus, after which
   battlefield Space selects 莱娅 normally.
6. Use the actual navigation and Wait buttons to spend all 11 units' actions.
   Observe each decrement down to **暂无可行动单位**. Clicking that disabled
   control does nothing; the game is still in the player's first turn.
7. Undo the final Wait: count 0 → 1. Explicitly click **结束回合**, even with
   that one unit remaining. During the AI turn, Next Unit is absent and the
   turn controls are disabled. On player turn 2, the count returns to 11;
   clicking Next Unit again reveals 莱娅.
8. At minimum size, the portrait, action menu, Next Unit, End Turn, Undo,
   objectives, and system controls remain distinct and readable. Also check
   the wider turn-2 commander-tactics panel. Use the window manager's Resize
   command to request a smaller size: the client remains 960×640. Close the
   test window normally and confirm it exits.

This is an opening-turn HUD acceptance, **not** a C01 victory, full campaign,
touch-device, export-package, audio, or performance certification. The native
automated touch/stale/busy/deployment coverage remains in the parent report.
The cloud renderer is Mesa llvmpipe; the log reports unavailable V-Sync and
ALSA audio and fallback to a dummy audio driver. Those environment limits were
not hidden or treated as HUD failures. No new gameplay exception was logged.

## Four unedited viewport captures

Each image is the application's F12 viewport readback at 960×640, with the
paired existing actor trace. These are a small selection from this new pass,
not reused campaign or motion-capture evidence.

- [Selected unit, turn 1](screenshots/native-003.png): count 11 and action-menu separation.
- [No ready units](screenshots/native-007.png): disabled navigation; End Turn still explicit.
- [AI turn](screenshots/native-009.png): navigation absent and player orders disabled.
- [Player turn 2](screenshots/native-011.png): count 11 restored and navigation working.

## Reproducibility and evidence integrity

From the repository root, after the normal build/import:

```sh
source tools/godot/env.sh
QA_DIR="$(mktemp -d)"
XDG_DATA_HOME="$QA_DIR/userdata" "$GODOT_NET" --path godot --resolution 960x640 \
  res://Scenes/NativeCapture.tscn -- --capture-dir="$QA_DIR/captures" \
  --capture-size=960x640
```

Follow the native UI steps above. F5 uses the application's normal save path
inside that isolated directory; F12 records the native viewport. The committed
saves are snapshots copied after those real inputs, not fixture inputs.

Run `python3 docs/hud-ready-units-20261111/gui/verify.py` to check the recorded
state comparisons, exact PNG dimensions, and SHA-256 manifest. This verifies
the preserved evidence, not a fresh GUI playthrough. The log replaces only the
machine-specific capture directory with `$QA_DIR`. The game source is unchanged
from the previously completed full native gate, so that long gate was not
unnecessarily repeated for this documentation-only follow-up.
