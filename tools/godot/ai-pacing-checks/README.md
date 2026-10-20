# AI loop pacing and refusal checks

```sh
bash tools/godot/ai-pacing-checks/run.sh
bash tools/godot/ai-pacing-checks/mutants.sh
```

This test-only oracle extracts and executes the actual original `GameController.runAiTurns` method with controlled march/settle promises and a fake sleep clock. It compares native execution with seven source scenarios and 26 observations: immediate initial action, blocked settlement, exact 89/90 and 119/120ms boundaries, terminal pause, refused normal/handoff attempts, refused fallback followed by recovery, and invariant propagation. It also executes the source's 2000-attempt guard and disposal during sleep. No source code is loaded by the native runtime.

Native tests exercise the real `ComputerActionExecution` and `AiTurnPacer`, including fallback invariant propagation, cancellation/rebinding and no commits while presenting. The pacer uses a monotonic millisecond clock supplied by `GrayFlagGame`; it owns no battle data. A zero-animation action starts its pause immediately, not on the following frame. Animated actions start the pause on observed settlement. Busy lasts through the final action's pause even if it hands control to a human or ends the battle.

`GrayFlagSession.AdvanceComputerStep` returns `ComputerStepResult` with `Committed` and `AttemptedHandsOffTurn`; its original bool `AdvanceComputer` entry point calls the same executor. Only `IllegalActionException` is recovered. One refused suggestion appends the original `AI 的行动被拒绝：…` message, tries `endTurn` once, then uses the attempted order's handler metadata for pacing. A refused fallback is a no-event step, not a second message; domain invariant errors propagate.

`GrayFlagGame` cancels its pacer on view replacement/unmount, gates Save/Load while the loop is running or the screen is input-blocked, and identity-binds application callbacks. `BattleScreen.IsPresenting` remains animation-only; `SetApplicationBusy` covers the whole AI loop. The frame-driven scheduler latches guard exhaustion or an invariant failure until binding replacement or a human turn, preventing a new 2000-step loop or repeated defect every frame. Cancellation drops the obsolete view's busy/deadline immediately; source disposal waits out its existing sleep before finally clearing busy, but neither commits another action.

These are headless timing and rule-adapter tests, not real-window interaction acceptance. `GrayFlagGame` integration must also build and run with the current screen adapter.

`mutants.sh` compiles isolated copies with five deliberate defects: 89ms normal delay, 119ms handoff delay, an initial pre-delay, fallback rather than attempted-action metadata, and swallowing invariant errors. All must fail executable assertions, not merely compilation.

`res://Scenes/AiPacingIntegrationChecks.tscn` mounts the actual Game/Screen/campaign, runs actual AI-selected orders and native animation settlement, then advances the Game clock explicitly. It checks direct, keyboard and button Save/Load refusal during the non-animation pause, file/session immutability, exact deadline commit, screen/session replacement, stale controls and unmount cancellation. It requires fresh isolated `XDG_DATA_HOME` under `/tmp/empire-*`; campaign entry and clock are fixtures, AI selection/dispatch/presentation are real.
