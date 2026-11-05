# Normally played campaign opening

## Scope and ownership

This adds a durable native acceptance route for the current authored campaign:
legal AI defeat, normal terminal presentation, retry, two consecutive victories
with their actual carried state, and the third battle's first round. It does not
change production gameplay, artwork, campaign rules or save semantics. The tested
route did not establish a new production defect, so no speculative gameplay fix
was made.

`GrayFlagCampaign` remains the owner of campaign settlement and roster carry;
`BattleSession` owns accepted orders and terminal state; the existing application
AI pacer and board presentation own opponent execution and playback. The test
player only selects legal orders using public movement, command, healing and AI
queries. It does not install another AI policy, edit live state, remove enemies,
change objectives, set a conclusion or settle a battle early.

Campaign navigation, choices, Retry and Settle use native Enter input on visible,
enabled, focused Godot buttons. F5/F9 use native keys. Human tactical orders go
through the authoritative session dispatch with the live revision; this route
does not claim that every tactical command was selected through mouse/touch menus.
Opponent orders and all observed battlefield events use normal application timing
and automatic animation. The approach follows Godot's documented
[native input propagation](https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html)
and [explicit scene dependencies](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html).

## Actual flow

- New, authored opening and `steady-advance` enter the first battle. A separate
  rules session derives the genuine losing checkpoint using human end-turn orders
  and normal opponent decisions. Native F9 restores that exact isolated checkpoint;
  the next normal AI order causes defeat. The 152-order derivation ends at
  `dc37cd6b`; all eight terminal events, including `gameOver`, are presented before
  one result window. The pacer finishes, no victory continuation appears and neither
  save slot changes. Native Retry plus a duplicate callback restores the original
  request and opening `9dae091b`, clears action/event history and preserves both slots.
- From that real retry, the current legal test player wins C01-01, then uses actual
  Settle, Proceed and story/choice controls. The first
  victory uses 156 legal orders on turn 9 and presents all 438 events once before
  its sole result window. Terminal state is `e4ecef0c`, settled campaign `17488f7b`.
- First settlement preserves Torren's actual fallen disposition and omits him from
  the next battle. Elin retains half health (39/78), rank 1 and progress 142. Laiya
  remains at full health. The `accept-truce` choice carries its flag and Cain
  relationship; the no-attacks rule ends on the authored third AI-turn handoff,
  with its signal in that same transition.
- C01-02 completes the actual primary evacuation objective in 259 legal orders:
  all three refugees are alive at 58/58 HP in the authored east-safety zone, the
  ballista is destroyed, and enemies still remain. All 536 events are presented
  once in order before one completion window. The terminal state is `bdca867c`;
  settled campaign state is `d152d095`.
- Second settlement retains Laiya's progress 28 and Elin's 69/78 health, rank 1,
  progress 281. Opening funding is 430, 460 and 490; campaign supplies move 5→6→7
  and treasury stays 5. Actual history, casualty bindings, injury, progression and
  save data are checked against the authoritative state, with independent field
  checks rather than only a second invocation of settlement.
- Native continuation reaches C01-03 and plays its first complete human/AI round:
  21 orders, state `a55b34f3`. The battle remains live and ready for human input;
  campaign state and both save slots are unchanged. This is not a C01-03 victory.

Both slots are compared by bytes and modification time where unchanged behavior
is required. All runs use a fresh guarded `XDG_DATA_HOME` under generated temporary
evidence; pre-existing user data and symlinked data ancestors are rejected.

## Validation and retained failures

The fresh complete aggregate on `c26c74ba49ddc82b53cc38f129c1a6abf224f043`
passed from 13:49:27 to 14:51:51 UTC on 2026-11-05 (3744 seconds), using the
published default budgets without timeout overrides:

- Debug application/core test builds, format/source/metadata/scene gates and
  simulated process checks passed. Application builds had zero warnings/errors.
- All 16 core groups and all 31 native scenes passed, plus editor import, with
  strict process exit and diagnostic inspection. All 32 raw native/editor logs,
  source commit and Debug assembly hashes are retained.
- In the final single played-flow process, both victories total 415 legal orders
  and 974 presented events, followed by the third battle's 21-order first round.
  The complete process exits cleanly. Its 21227 assertions include repeated frame
  deadline checks and are not a fixed count of independent test cases.
- Independent audit confirms that all 16 formal-level replay traces (3425 commands)
  have identical ordered actions, events, receipts and final digests to the
  baseline. All 40 warning headers exactly match the preceding full gate; there
  are no new warning filters or suppressed errors.
- The additional `ExportRelease` application configuration compiled with zero
  warnings and errors. The new test classes are excluded by its existing
  `Presentation/**/*Checks.cs` compile exclusion. This is not a packaged export.

The initial separate defeat run and the normally paced C01-02/C01-03 continuation
passed their strict native exit/diagnostic checks. That continuation used the exact
first-victory autosave created by a previous genuine C01-01 run; its recorded SHA256
is `2d51c3b7eab10fcde8e8447f86e6a8a176b73477f1108a8f1beb85c24e412dea`.
The final aggregate invokes the scene without continuation/derivation arguments,
so its acceptance must cover the entire fresh route in one process.

A deliberate negative removed only the production guard that delays the result
while the AI pacer or board presentation is active. The new test failed precisely
because the result appeared before all terminal events. The source was restored
byte-for-byte before the final normal build; no negative production code is in
this change.

Earlier failures remain classified rather than erased:

- A historical action trace was incompatible with the current expanded maps and
  was rejected on its first path origin. It was removed from the new route; this
  was a test-driver mismatch, not a current gameplay defect.
- The first real C01-01 victory settled successfully, then a test integer reader
  rejected an otherwise valid double-backed campaign reward. The test now reads
  numeric JSON values through the existing canonical JSON representation.
- An initial C01-02 test-player strategy legitimately lost when a refugee died;
  a subsequent naive river-bank approach stalled. Neither is evidence that the
  player's legal game rules are broken. The final test player selects legal
  healing and the live authored bridge crossing, without changing the enemy or
  objectives. Diagnostic derivations are explicitly not playback acceptance.
- The first aggregate stopped before build/native scenes because the gate's
  simulated-process self-test still expected the previous scene count and tail
  positions. The test now verifies 32 calls, 31 unique isolated scenes, exact
  Lighting/Scenery arguments and the played route without shortcut arguments.
  It also verifies separate default and overridden time budgets.

The aggregate's individual scene budget is now 900 seconds, replay 1200 seconds
and the complete normally paced opening 2400 seconds. This reflects observed
native navigation runs above the old 300-second budget, not removal of timeout
or diagnostic checks. Explicit bounds remain configurable and a timeout fails.

## Acceptance boundary

These are actual native headless flow, state, input publication and lifecycle
checks. They do not establish visual composition, typography, shader appearance,
rendered animation quality, desktop touch usability or FPS. No matching platform
package was produced in this round; an ExportRelease configuration build is not
an export. Only one sequence of early choices and carried outcomes is covered,
not the whole campaign or every battle ending. This route has real nonzero growth
and injury/casualty carry, but empty unit resource/career collections and unchanged
full morale do not prove all populated or reduced variants.

The earlier intermittent HUD and morale shutdown diagnostics remain unexplained;
clean exits here do not establish a fix for those separate observations.
