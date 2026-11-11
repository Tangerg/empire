# Successful action notice lifetime — 2026-11-11

Base: `e45ebf483d1024e56a10b8cb0ef619d6a11b6281`.

Observed in the real 960×640 native C02 entry fixture: after moving a civilian,
the 480-pixel-wide `命令完成 · 1 条战斗事件` notice remained over the battlefield
until the next tactical selection. Reading/closing the journal brought the old
notice back. This was a debug-entry fixture, not a continuous campaign victory.

The fix gives accepted command/undo notices three seconds of visible idle reading
time. Busy presentation and modal reading pause the countdown. New persistent
errors/refusals/conclusions replace it; no stale callback can erase a new message.
It changes neither canonical actions nor the engine, save, replay or event rules.

Verification:
- Native camera integration: 370 assertions, exit 0. A negative control disabled
  transient dispatch and failed the exact success-expiry assertion, then the
  restored implementation passed. It covers playback/busy/modal pause, text and
  tooltip clearing, non-mutation, newer transient/persistent messages and rebind.
- BattleScreen real-session integration: exit 0.
- Campaign UI flow: 209 assertions, exit 0.
- Format and build: exit 0, zero warnings/errors; diff check passed.
- Actual 960×640 C02 mouse movement: the civilian arrived, ready count became 11,
  and the success overlay disappeared without another tactical selection.
- This presentation-only change does not modify Core. The complete 16-level gate
  was passed in the preceding objective-location slice; it was not rerun here.

Existing resource readiness diagnostics and expected translated-label warnings
remain. No binary GUI evidence or previously denied artifact is included.
