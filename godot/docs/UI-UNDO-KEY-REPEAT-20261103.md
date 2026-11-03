# One Undo per fresh U press — 2026-11-03

Implementation update: the [contextual repeat repair](UI-CONTEXTUAL-KEY-REPEAT-20261103.md)
moves repeat policy into the tactical controller while preserving this behavior.
The native-mapping implementation below records the original repair.

## Confirmed failure

An isolated session using the unchanged C01 opening level committed two real
move-and-wait orders through the battle screen. A fresh U press restored the
latest checkpoint and advanced revision from 2 to 3. A subsequent native key
event with `Echo: true` consumed the older checkpoint as well, reaching revision
4. The regression against the original implementation failed at this exact
comparison: `revision 4, expected 3`.

This was not an animation race. Undo settles synchronously, and the current
revision correctly advances after every accepted undo. Revision guards therefore
cannot distinguish a new deliberate press from operating-system key repetition.

## Repair

The native key mapping now maps U to Undo only when `key.Echo` is false. That is
the boundary which still owns native repeat metadata; the shared tactical
controller receives semantic key strings and the session owns undo checkpoints.

No timer, cooldown, held-key latch, undo-stack limit, or new state is added. A
released and freshly pressed U can immediately undo another step. The undo button
and direct session/controller requests keep their existing behavior. Arrow and
WASD navigation repeats, other key mappings, and modifier filtering are unchanged.

No map, artwork, rule, saved-game schema, or authored content changes.

## Regression coverage

The ordinary `BattleScreenChecks` scene now starts with a real two-checkpoint
fixture. It checks:

- Fresh and repeated U during application busy cannot consume history; a repeat
  after busy clears also cannot become a fresh undo.
- The first fresh press restores the exact first-order state and event count.
- Three repeat events through each native entry point leave that state, revision,
  older checkpoint, HUD projection, and visible log unchanged.
- Release is inert; the next fresh press restores the exact original state.
- Repeat, release, and fresh press remain harmless with no checkpoints.
- An echoed arrow and its WASD counterpart still move an interior cursor into a
  legal, uncommitted destination preview.

Each navigation case uses a fresh screen binding over the same unchanged session,
so a previous preview or cursor cannot turn the next letter into a command.

The suite uses the real application session and real screen dispatch. Input
events are supplied to the native adapter entry points directly, not through a
desktop key-hold automation. These results do not claim current graphical or
window-manager acceptance.

## Verification boundaries

Completed checks on the final code:

- Production build: zero warnings and errors.
- `BattleScreenChecks`: full real-session integration passed, including the new
  two-checkpoint repeat checks, held-navigation cases, and existing modifier checks.
- `DossierFocusChecks`: 73 assertions.
- Both native processes exited zero with no error, leaked-resource,
  segmentation-fault, or unhandled-exception diagnostics. Existing source-readiness
  and mouse-notification warnings remain visible.
- Changed-source whitespace verification and `git diff --check`: passed.
- Source metadata: 154 unique script UIDs and 14 positive/negative cases passed.
- Independent read-only review: no remaining blockers.

Checks use fresh temporary user-data directories and a 600-second process budget.
Player saves and the accepted town scene are untouched. The pre-repair failure
is retained as negative evidence, not counted as a passing run.

The full native formatting gate still has the previously documented failures in
184 untouched files. This focused repair does not change those files or claim
the aggregate native or unrelated TypeScript suites pass.
