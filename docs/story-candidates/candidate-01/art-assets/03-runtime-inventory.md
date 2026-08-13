# 查询《断冠之誓》的当前素材

当前唯一正式运行时包是 [`final-fantasy-v1`](../../../../packages/story-candidate-01/assets/final-fantasy-v1/README.md)。它覆盖全部 404 个题材，Manifest 是程序索引真值；游戏代码只维护领域 ID 到 `topicId` 的语义绑定，不保存素材文件路径。

## 当前覆盖

| 类别 | 运行时数量 | 当前接入方式 |
| --- | ---: | --- |
| 剧情静态图 | 80 | 剧情场景、角色卡、菜单主视觉 |
| 战斗单位 | 40 | 四帧语义动画与职业单位绑定 |
| 任务单位 | 24 | 四帧语义动画与护送单位绑定 |
| 阵营基准 | 12 | 内容制作参考，暂不直接绘制战场 |
| 地形 | 32 | 四变体或 N/E/S/W 16-mask |
| 交互建筑 | 24 | 正常、受损、占领三态 |
| 战场物件 | 32 | 掩体、尸体和语义战场标记 |
| 装备 | 48 | 武器与指令界面图标 |
| 技能 | 48 | 技能与战术界面图标 |
| 状态 | 24 | 单位状态图标 |
| 特效 | 24 | 武器、治疗等四帧一次性特效 |
| HUD | 16 | 目标等战斗界面语义图标 |
| **合计** | **404** | **404/404 已进入素材目录** |

## 地图环境库存

正式题材包之外，当前另有 [`environment-builder-v1`](../../../../packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/README.md)：36 套 1×/2× 图集、1188 个可拼装部件。它覆盖完整过渡、四变体道路与水体、正式路肩、高差、坡道、断崖、森林、地基、工事、地标、贴花和农村生活件，专门承担地图编辑与复用。

该环境包已通过机器素材 QA，但尚未完成真实关卡渲染替换，因此其 Manifest 保持 `runtimeReady=false`，不能把“文件齐全”写成“实机已接入”。

## 程序入口

- 素材目录与 Manifest 校验：`packages/story-candidate-01/src/presentation/candidate-01-assets.ts`
- 领域 ID 到题材 ID 的映射：`packages/story-candidate-01/src/presentation/candidate-01-bindings.ts`
- 战场渲染适配：`packages/story-candidate-01/src/presentation/candidate-01-runtime.ts`
- 剧情图解析：`packages/story-candidate-01/src/presentation/candidate-01-story.ts`
- 正式清单：[`manifest-final-fantasy-v1.json`](../../../../packages/story-candidate-01/assets/final-fantasy-v1/manifest-final-fantasy-v1.json)

未被当前前三章使用的素材仍可通过 `topicId` 查询，不应为了“提前接线”把剧情名或文件路径写入战斗内核。

## 历史包

旧 `runtime-v2` 已归档至 [`assets/archive/runtime-v2/`](../assets/archive/runtime-v2/README.md)。归档可读、未删除，但不再参与构建或运行时加载。
