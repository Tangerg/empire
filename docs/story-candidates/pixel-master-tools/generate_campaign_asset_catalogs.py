#!/usr/bin/env python3
"""Generate one self-contained 404-topic catalog inside each campaign."""

from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CAMPAIGNS = {
    "candidate-01": "断冠之誓",
    "candidate-02": "群星熄灭之前",
    "candidate-03": "布衣定鼎",
}
CATEGORIES = (
    ("narrative-static", "叙事静态图", 80),
    ("combat-unit", "战斗单位", 40),
    ("mission-unit", "任务单位", 24),
    ("faction-kit", "阵营套件", 12),
    ("terrain", "地形", 32),
    ("interactive-structure", "交互建筑", 24),
    ("battle-prop", "战场物件", 32),
    ("equipment", "装备", 48),
    ("skill", "技能", 48),
    ("status", "状态", 24),
    ("fx", "特效", 24),
    ("hud", "界面标记", 16),
)


def render(campaign: str, title: str) -> str:
    manifest_path = ROOT / campaign / "assets/manifest-complete.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    topics = manifest["topics"]
    counts = Counter(topic["category"] for topic in topics)
    if len(topics) != 404:
        raise ValueError(f"{campaign}: expected 404 topics, found {len(topics)}")
    lines = [
        f"# 查询《{title}》的完整资源题材",
        "",
        f"本页列出《{title}》全部 404 个资源题材及其稳定 ID。它是策划与美术的完整内容目录，不表示题材已经完成运行时生产或实机验收。",
        "",
        "## 使用题材目录",
        "",
        "策划使用 `topicId` 绑定关卡与剧情需求。美术在生产前锁定题材，程序使用运行时清单中的同名 `topicId` 接线。当前完成状态请查询 [当前库存](./03-runtime-inventory.md)。",
        "",
    ]
    for key, label, expected in CATEGORIES:
        actual = counts[key]
        if actual != expected:
            raise ValueError(f"{campaign}/{key}: expected {expected}, found {actual}")
        lines.extend((f"## {label}，共 {actual} 项", "", f"本节列出《{title}》的全部{label}题材：", ""))
        for topic in (item for item in topics if item["category"] == key):
            lines.append(f"- `{topic['id']}`：{topic['label']}")
        lines.append("")
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check-only", action="store_true")
    args = parser.parse_args()
    for campaign, title in CAMPAIGNS.items():
        output = ROOT / campaign / "art-assets/07-topic-catalog.md"
        content = render(campaign, title)
        if args.check_only:
            if not output.is_file() or output.read_text(encoding="utf-8") != content:
                raise SystemExit(f"stale catalog: {output}")
        else:
            output.write_text(content, encoding="utf-8")
        print(f"{campaign}: 404 topics")


if __name__ == "__main__":
    main()
