# 《布衣定鼎》Runtime V2 Batch 03 战斗单位锁表

目标：将 C03 战斗单位从 Primary 4 个与 Batch 02 8 个补齐至 `40/40`。本表的 28 个 `topicId` 来自 `manifest-complete.json` 的 `combat-unit` 类别，均未被前两批占用；`contentId` 全部为新的稳定 `grain.unit.*` ID。

Primary 已占用语义：`c03-unit-4c02a584` 刀盾卒、`c03-unit-9ad7ca0f` 长枪阵、`c03-unit-915fc486` 乡弓手、`c03-unit-79f06a6f` 河工。

Batch 02 已占用：`c03-unit-ff5e54ed`、`c03-unit-6fa9292e`、`c03-unit-15c058cc`、`c03-unit-0137a889`、`c03-unit-c11ad030`、`c03-unit-58b98436`、`c03-unit-222078e0`、`c03-unit-25f91749`。

| topicId | 题材 | contentId | 单帧 |
| --- | --- | --- | --- |
| `c03-unit-fd4c21da` | 火船队 | `grain.unit.fire-ship` | `96×64` |
| `c03-unit-2be16bd2` | 架桥营 | `grain.unit.bridge-corps` | `64×48` |
| `c03-unit-4dad36ef` | 云梯队 | `grain.unit.ladder-corps` | `64×48` |
| `c03-unit-0609e6e6` | 炮石营 | `grain.unit.catapult-crew` | `64×48` |
| `c03-unit-3d86046f` | 火铳手 | `grain.unit.musketeer` | `32×48` |
| `c03-unit-5e64c3a6` | 火箭营 | `grain.unit.fire-arrow-corps` | `32×48` |
| `c03-unit-9881f2a3` | 震天雷队 | `grain.unit.thunder-bomb-corps` | `32×48` |
| `c03-unit-4566788b` | 军医 | `grain.unit.field-medic` | `32×48` |
| `c03-unit-b59c3f96` | 旗鼓手 | `grain.unit.drummer-standard` | `32×48` |
| `c03-unit-a53b725f` | 粮秣官 | `grain.unit.quartermaster` | `32×48` |
| `c03-unit-8f49e6ef` | 军师 | `grain.unit.strategist` | `32×48` |
| `c03-unit-8842d111` | 斥候司 | `grain.unit.scout-office-agent` | `32×48` |
| `c03-unit-75ee37ba` | 说客 | `grain.unit.envoy` | `32×48` |
| `c03-unit-f0a4ddaa` | 乡勇 | `grain.unit.village-militia` | `32×48` |
| `c03-unit-76cd6822` | 城门盾兵 | `grain.unit.gate-shield-guard` | `32×48` |
| `c03-unit-73d2bcb4` | 将领亲兵 | `grain.unit.commander-retinue` | `32×48` |
| `c03-unit-6a62b51e` | 铁锤破阵手 | `grain.unit.warhammer-breaker` | `32×48` |
| `c03-unit-4377f3e4` | 筑城守备 | `grain.unit.fortification-guard` | `32×48` |
| `c03-unit-3f6fea2f` | 潜渡凿船手 | `grain.unit.covert-hull-saboteur` | `32×48` |
| `c03-unit-6d03b909` | 漕运护军 | `grain.unit.canal-escort` | `32×48` |
| `c03-unit-c30bf360` | 盐仓护卫 | `grain.unit.salt-store-guard` | `32×48` |
| `c03-unit-747800db` | 雪原骑射 | `grain.unit.snowfield-horse-archer` | `64×48` |
| `c03-unit-118c7423` | 互市护卫 | `grain.unit.frontier-market-guard` | `32×48` |
| `c03-unit-4619e546` | 宁朝禁军 | `grain.unit.ning-imperial-guard` | `32×48` |
| `c03-unit-3f4a6e1d` | 密察司缉事 | `grain.unit.secret-inspector` | `32×48` |
| `c03-unit-0c42f6d3` | 地道营 | `grain.unit.tunnel-corps` | `32×48` |
| `c03-unit-2bc4ddf7` | 火油防备队 | `grain.unit.fire-oil-defense` | `32×48` |
| `c03-unit-cecc448d` | 降兵整编队 | `grain.unit.reorganized-surrendered` | `32×48` |
