# Game environment replacement — 2026-11-01 06:14 UTC

Verified remote recovery baseline: d89826d762a273b727640294c0f7afd67754fd43,
Tangerg/empire, feat/godot-grayflag-recovery-20261020.

The old workspace and GUI disappeared during a new command launch. That rejected
launch did not write its proposed CheckLayeredBridgeDepth method. Old local campaign
saves, toolchain, authentication and screenshot/probe files are not present here.
C04 normal-AI T6 completion remains historical actual play: the pushed report and
Library settlement image survive, but its save cannot be claimed available.
Do not replay C04 solely to restore that file, or label a reconstructed C05 entry
fixture as the old exact save.

`reapply-depth-strips.py` reconstructs the runtime change and shared regression test
from tool-call records. It must be freshly built and tested after application.
Pre-reset results (historical only): shared AssembledGround checks 12478, C05
unbound candidate checks 1447; four fixed rear-rail intersections regained correct
Unit selection and draw Z. The pre-reset before/after image files are lost and must
be recaptured, not reused by path.

Depth strips retain one whole transparent independent texture. Contiguous vertical
regions cover the source canvas exactly, with each strip's depth contact on the
actual sloping ground baseline. This changes neither rail height/placement nor
canonical map/movement. One contact shadow per object remains. Tests inspect every
pixel of a known alpha fixture for exactly-once coverage and unchanged pointer
contour, single site ownership, distinct depth, invalid/incomplete partitions and
unchanged battle state. Actual bridge front/back tests still need fresh execution.

The unbound C05 preview remains a candidate. Formal c01-05 is unchanged. Remaining:
- Fixed rear control actor(7,20), source(260,594)/(260,582); actor(9,19),
  source(324,566)/(324,554), actual opaque body in front of rear rail.
- Fixed front control actor(9,20), source(326,598), rail in front of body.
- Retain original near-end unsplit geometry as a negative control.
- NE boulder precise proposed source delta(-2.7,0), not -3; recheck actual eight
  lower stone witnesses and original five-pixel tolerance.
- Cart wheels are on existing walls; shaft source(1078.6,419.32)/(1079.16,421.896)
  and retained low fence(1090,380) must not introduce collision/site hits. Inspect
  actual actor route(33,12)->(33,13)->(33,14).
- Read-only C02 same-type bridge review was not run before reset.
- Seven-house exposed feet do not prove hidden rear collision. Rebuilt readonly
  records must be compared with recovered canonical audits before treating them
  as authoritative. No roof-alpha substitution, no relaxed tolerance.

Private auth data is absent from all recovery artifacts. New login is coordinated
by the root task, not started by this writer.
