# Visible stationary AI attack focus

Base: `9df78ab7`. This concerns already committed enemy actions, not attack
previews or combat rules.

## Real-window baseline

An isolated fixture replayed the formal C14 collision witness's first 17 legal
orders, then loaded the resulting save through the production GrayFlagSession.
Both ChooseAction and AdvanceComputerStep selected the recorded next action:
enemy 20 at (7,10), stationary inquisitor flame against Ivra 7 at (6,10).
The fixture uses the real BattleScreen and AiTurnPacer, pausing only after this
one action for inspection; no campaign victories or user saves were fabricated.

At 1180×812, zoomed in four times and dragged the map away from both participants,
then pressed the fixture's explicit next-enemy-action button. The camera stayed
on unrelated eastern buildings throughout and after the action. The battle log
confirmed Ivra received 20 damage and one-turn silence. The committed attack was
therefore mechanically correct but its performance was entirely offscreen.

The temporary probe was stopped through its launching terminal after inspection
(the stand-alone probe did not initially wire window-close handling). This was
fixture lifecycle setup, not a production game defect. No screenshot, save, or
previously withheld evidence is included.

## 修复与回归设计

- 保留移动命令的原 `CommandDestination` 镜头策略，不放宽 path 长度。
- 仅对已经提交的 AI 单格路径命令，读取匹配命令 actor 的 `attackActivated` 事件 `from/at`；无激活事件的原地等待、人类命令不抢镜。
- 隐藏源无聚焦；迷雾可见集非 null 时，只有 `Before` 中目标格有可见单位才纳入目标端，否则仅聚焦可见源，不泄露隐藏瞄准位置。
- 使用既有 `FrameBounds` 将交战两端框入安全视口，保留 96 屏幕像素演员演出边距，仅必要时缩小。
- C14 正式关卡前 17 条合法动作重放，正常 `GrayFlagSession.FromSave` 与 `AdvanceComputerStep` 选择第 18 条原地攻击；前置 `74663d55`、后置 `fdd2d9da`、完整有序事件 SHA-256 `d5f04e500465e0468e000480a9767964d27d05cf7fe87134e4b1113861468efe` 固定。测试不改变关卡、AI或战役结果。
- 原生集成在 960×640、高缩放、拖离交战的情况下要求播放前镜头移动、两端安全边距、双方完整 Sprite 矩形位于安全视口，演出后 canonical 状态不变。
- 负控：暂时删除 Screen 新聚焦分支，真实前缀精确失败于 `Offscreen stationary AI attack must move the camera before playback`，退出 1；已恢复生产实现。纯相机正控 59 断言通过。完整集成最终结果另记。

最终针对门禁：恢复生产后构建零警告/错误；CameraChecks 59 断言、完整 CameraIntegrationChecks 467 断言均通过严格进程门禁，退出 0。集成包含双方完整 Sprite 边框、固定有序事件 SHA 和演出后状态不变。日志见 verification/。

## Real-window repaired acceptance

Repeated the same zoom-and-pan-away workflow at 1180×812. The normal AI action
returned the camera to both combat participants rather than leaving the attack
offscreen. Repeated with the standard capture wrapper setting the actual native
window to 960×640 (the direct probe's startup size request was overridden before
layout). At this minimum viewport, both participants were framed fully below the
HUD and above the bottom controls after the same real stationary attack.
The windows were closed and the desktop released. Temporary probe source/scene
were moved outside the repository before the final build; no debug entry point,
button, fixture save or screenshot is part of the change.

## Final broad checks and limits

After removing the temporary probe, the complete BattleScreenChecks and complete
AnimationChecks entry points passed their strict native process gates. Animation
reported 8257 assertions. The final native build and CameraChecks were repeated
after the formatting-only test line break correction. Native format and source
metadata checks passed. The production diff has no Core, level, recorded fixture,
asset, or canonical rule changes; original event ordering and state remain pinned
by the real-prefix regression.

No fresh full Core suite, all-native aggregate, full campaign GUI playthrough,
or remote CI pass is claimed. Focus is limited to visible, committed stationary
attacks: hidden attackers do not trigger follow, and fog-filtered aim cells are
not included unless occupied by a visible unit. General stationary healing or
other non-attack abilities are outside this slice.
