# Partial-capture feedback — 2026-11-04

## Evidence and scope

Baseline: `adbdd17d1e8cd24f311ac0cbd221663751b32bfc`.

The unchanged C01-04 legal replay reaches three captures at (24,15):

- Command 46: `39462b0e` → `ecee9856`, progress 42%, owner remains player 2.
- Command 69: progress 84%, state `9bb03e7a`, owner remains player 2.
- Command 87: completed capture, state `af6d2285`, owner becomes player 1 and
  the map's accumulated capture counter resets to zero.

Indices are zero-based. The first command moves human archer 7, with 33 HP,
from (23,17) through (23,16) and (23,15) to the site. The existing site dossier
already correctly shows 42/100, current ownership and the continuation/reset
rules. The defect is narrower: each incomplete receipt becomes only
“占领进度提升” in the battle journal, losing its committed location and progress.
The generic capture command hint does not explicitly promise immediate ownership.

## Ownership and disclosure

The core capture event already owns `at`, normalized `progress` and `captured`.
The presentation now formats those facts, without recomputing HP contribution,
adding a capture predictor, changing gameplay, or inferring a result from later
map state. Completed capture keeps its existing announcement.

Only location and progress are added to the incomplete message. Both are existing
public site-inspection facts. The capturing faction is not necessarily the site's
current owner, so partial capture must not add a player or hidden unit identity.
The formal witness itself has fog disabled. This is not a change to fog rules.

## Verification status

The native regression replays the original 88 legal commands and mounts three
separate saved checkpoints from that replay. Each uses the actual BattleScreen,
keyboard destination selection when movement is needed, and the visible enabled
capture button's Pressed signal. It checks the exact original action, event hash,
final state, event-time toast, opened journal, refresh and save/rebind lifetime.
These are separate native checkpoints, not a whole naturally played campaign or
physical mouse-input/rendered GUI acceptance.

The first two test-setup attempts opened the journal with Godot's initial 64px
headless window. Waiting for layout alone did not establish a supported viewport;
both failures are retained. The fixture now explicitly selects 1280×720 before
layout and restores the previous size afterwards. No production journal bounds
were changed to accommodate an unsupported test window.

With the supported viewport, the original implementation exits 1 at the intended
feedback assertion after proving the exact 42% command, event hash and state.
The fixed implementation reaches all three native checkpoints with the expected
42%, 84% and unchanged completion messages. An additional synthetic boundary
uses a fractional event and identity resolvers that throw, proving that neither
later map state nor a hidden participant's name supplies the partial message.
The complete `BattleScreenChecks` process exits 0, including every existing
battle test and strict shutdown checks. Two deliberate production mutations
compile successfully and exit 1 at their intended assertions: a fixed 42%
percentage fails the real 84% checkpoint; resolving the capturing player
identity fails the synthetic private-identity boundary. Both are restored to
the exact committed production source before subsequent verification.

An environment restart interrupted the first mutation build before any exit
code was recorded. Its source and partial log were archived, production restored
and verified against Git, and both original mutation commands rerun. That
interrupted attempt is not counted as passing.

The subsequent, uninterrupted canonical `tools/godot/verify-native.sh` run at
`e46cefcbe87f2bdf6880203b90a66f8b3a7a07cd` completed from 18:44:36 to 19:38:43 UTC:
54m07s, exit 0. It used the existing 900-second native-process and 1200-second
replay budgets, without removing or replacing any stage:

- All eight preparation/format/metadata/map-contract checks and both Debug builds
  passed, including 173 C# source UID companions.
- All 14 core groups passed. All 16 formal levels' 3425 legal commands retained
  exactly the prior ordered action/event/receipt trace hashes and final digests.
- Objective/catalog boundary checks, editor import and all 30 native scenes passed.
  The new capture checks and all previous BattleScreen checks passed together.
- All 31 editor/native process logs were independently retained and audited: no
  error, leak, crash or unhandled-exception diagnostic. The 30 expected warning
  headers match the preceding complete gate exactly, rather than being hidden.
- The final `ExportRelease` build exited 0 with zero warnings/errors. It did not
  change the Debug assemblies exercised by the complete native gate.

Only this report was updated after that verified source checkpoint. No campaign
art, map, core rule, save schema, capture contribution or ownership transition was
changed. Headless native controls and release compilation are not rendered GUI
or exported-package acceptance; those remain separate.

## Other measured work, unchanged

A separate read-only audit measured public enemy-click selection and target
hover/HUD/overlay on legal C01/C07/C09 checkpoints, verifying every prefix hash,
unchanged state/catalog and stable selected actions. At the C07 checkpoint,
initial warmed samples measured roughly 6.2 ms and 4.38 MB per enemy-click;
hover/HUD about 6.5 ms and 3.98 MB, and the overlay about 3.3 ms and 1.79 MB.
Separate tiering-disabled measurements confirmed repeatable allocation but
showed substantial shared-host timing noise. Do not compare timings across modes.

Both HUD and overlay independently preview the same valid target. The preview's
complete clone provides necessary hypothetical placement/facing and failure
isolation. Unchanged targets do not repeat this work every frame. This establishes
an interaction cost, not rendered frame loss or a perceptible stall. No forecast
cache or isolation shortcut is introduced. Revisit only with representative native
interaction evidence that justifies a bounded internal reuse mechanism.
