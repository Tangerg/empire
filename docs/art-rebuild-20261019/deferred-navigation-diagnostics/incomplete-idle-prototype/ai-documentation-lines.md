| 障碍绕行与跨回合接近目标 | 验证中 | `TacticalSpace.navigationDistances` 提供可通行步数；原有战术评分不变，停滞时的跨回合接近规则仍待验证 |
| 障碍之后的目标还隔多少可通行步 | `TacticalSpace.navigationDistances()` → `computeNavigationDistances()`（复用 `Battlefield.traversalCost` 和正式网格；不把几何射程改成路径距离） |
