# 三套剧本题材覆盖原型交付

> **质量状态：`prototype`，不是正式游戏美术。** 这批 atlas 完成了题材 ID、切片、碰撞与引擎接线所需的结构覆盖，但与原有 HD 像素素材在细节、材质和造型上不一致。运行时 V2 正按 [游戏素材契约](./GAME-RUNTIME-ASSET-CONTRACT.md) 重做。

三套剧本的内容结构已扩展为 404 个题材，合计 **1212/1212 个原型槽位**；这个数字不等于 1212 个正式美术已完成。

## 交付数量

| 剧本 | 原型题材槽位 | 既有 / 原型扩展 | delivery | PNG/SVG 原型文件 | 新增 atlas 对 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 《断冠之誓》 | 404 | 22 / 382 | 32 | 64 | 16 |
| 《群星熄灭之前》 | 404 | 22 / 382 | 33 | 66 | 17 |
| 《布衣定鼎》 | 404 | 22 / 382 | 33 | 66 | 17 |
| **合计** | **1212** | **66 / 1146** | **98** | **196** | **50** |

上表的 196 个文件是 404 题材口径下的 98 对 PNG/SVG 交付。原有 18 套实名英雄四帧单位仍保留为基线外额外变体，不重复占用每套 40 个通用战斗单位槽位。

## 每套内容

| 类别 | 每套 | 三套 |
| --- | ---: | ---: |
| 剧情静态 | 80 | 240 |
| 通用战斗单位 | 40 | 120 |
| 任务 / 平民单位 | 24 | 72 |
| 阵营基准套件 | 12 | 36 |
| 地形 | 32 | 96 |
| 交互结构 | 24 | 72 |
| 战场物件 | 32 | 96 |
| 装备 | 48 | 144 |
| 技能 | 48 | 144 |
| 状态 | 24 | 72 |
| FX | 24 | 72 |
| HUD / 地图标记 | 16 | 48 |
| **合计** | **404** | **1212** |

## 接入入口

> 游戏侧不要接入本节的 prototype PNG。可用的运行时候选及其实机验收状态统一见 [Runtime V2 素材画廊](./RUNTIME-V2-ASSET-GALLERY.html) 和 [Runtime V2 覆盖状态](./RUNTIME-V2-STATUS.md)。

- [全局完整 manifest](./COMPLETE-ASSET-MANIFEST.json)：三套总量、分类与单套 manifest 路径。
- [离线素材总画廊](./COMPLETE-ASSET-GALLERY.html)：按剧本、类别和题材名称筛选 98 份 PNG delivery。
- [清单数据契约](./COMPLETE-ASSET-MANIFEST-SCHEMA.md)：题材、atlas、cell 与物理文件的映射规则。
- [C01 manifest](./candidate-01/assets/manifest-complete.json) · [C02 manifest](./candidate-02/assets/manifest-complete.json) · [C03 manifest](./candidate-03/assets/manifest-complete.json)。
- [C01 QA](./candidate-01/assets/qa-complete.json) · [C02 QA](./candidate-02/assets/qa-complete.json) · [C03 QA](./candidate-03/assets/qa-complete.json)。

## 生产与质量规则

- 新增素材不覆盖旧 HD 样包；分别位于 C01 `complete/`、C02 `expansion/`、C03 `expanded/`。
- 运行时默认使用 PNG；SVG 与 PNG 由同一整数像素源生成，用于审阅、重着色和重新导出。
- 同类题材不允许完全重复的 cell；透明素材类别的独立轮廓数不低于 50%。
- C02 另保留 1 张 ImageGen 高概念主视觉母版，仅作原创风格参考；本页所列程序化扩展图仍只算 prototype，不因输出为 PNG/SVG 而晋级为运行时美术。
- C03 完成了官粮封条和无籍灾民临时册两项轻返工，不覆盖旧文件。

## 验收命令

```bash
python3 docs/story-candidates/pixel-master-tools/validate_complete_library.py
python3 docs/story-candidates/pixel-master-tools/build_complete_delivery_manifest.py
python3 docs/story-candidates/pixel-master-tools/generate_complete_gallery.py
```

统一验收检查题材数量、分类、唯一 ID、文件存在性、PNG/SVG 尺寸、SVG `viewBox`、非空 cell、精确重复和透明轮廓多样性。
