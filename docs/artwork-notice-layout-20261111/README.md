# Readable native artwork diagnostic

## Reproduced issue and ownership

At the formal 960×640 minimum, C08's actual missing-artwork notice appeared as a single-character-wide vertical column along the left edge. `BattleScreen` created a wrapping Label directly under the screen, but never assigned it a rectangle. Unlike a container child, this direct overlay had no parent layout supplying a reading width.

The notice is now explicitly anchored below the existing event-log area at x=28..324, y=152..190. It uses the same overlay depth as other HUD feedback, ignores pointer input, and has a dark text shadow for board contrast. No max-line clipping, hidden diagnostic, raw resource path, replacement count, gameplay change, or scene-resource readiness change was introduced. Full localized text and the existing visibility condition remain the authority.

## Verification

- Build: zero warnings/errors. Native format and diff whitespace checks pass.
- Negative control: the native BattleScreen test with the original unpositioned label fails at the readable bounded-width assertion. This failure is from the real mounted screen, not a mock.
- Strict native BattleScreen real-session integration exits 0, including its process/diagnostic gate.
- Regression exercises the artwork and surface-pending wordings at 960×640, 1280×720, 1920×1080 and back to 960×640, with two injected localization variants. It verifies readable width, no line clipping, screen containment, separation from the event log and visible actionable buttons, overlay depth and pointer pass-through. The existing localized interaction test continues to compare state, events and saves across variants.
- Actual C08 GUI: loaded the same isolated ledger-branch opening. The complete localized “部分场景资源未能显示（1 项）。” now appears horizontally beneath the event area. Enlarging the native window and restoring 960×640 retained the layout. Space selected Laiya and displayed her unit/action cards without overlap. This is a detached chapter-entry fixture, not continuous campaign completion.

This patch improves reporting of an existing incomplete-art condition; it does not claim that C08 art is complete. Native viewport sizes above the available desktop were checked through mounted-control layout, not actual full-screen visual acceptance. Full 16-chapter replay/export gates were not rerun for this layout-only change. No private saves, screenshots, or previously withheld evidence are committed.
