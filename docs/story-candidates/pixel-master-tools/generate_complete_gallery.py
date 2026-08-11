#!/usr/bin/env python3
"""Generate the offline gallery for topic-coverage prototype manifests."""

from __future__ import annotations

import argparse
import html
import json
import struct
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path


STORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_OUTPUT = STORY_ROOT / "COMPLETE-ASSET-GALLERY.html"
CAMPAIGNS = ("candidate-01", "candidate-02", "candidate-03")
CATEGORY_ORDER = (
    "narrative-static",
    "combat-unit",
    "mission-unit",
    "faction-kit",
    "terrain",
    "interactive-structure",
    "battle-prop",
    "equipment",
    "skill",
    "status",
    "fx",
    "hud",
)
CATEGORY_LABELS = {
    "narrative-static": "叙事静态",
    "combat-unit": "战斗单位",
    "mission-unit": "任务单位",
    "faction-kit": "阵营套件",
    "terrain": "地形",
    "interactive-structure": "交互建筑",
    "battle-prop": "战场物件",
    "equipment": "装备",
    "skill": "技能",
    "status": "状态",
    "fx": "FX",
    "hud": "HUD",
}


@dataclass(frozen=True)
class DeliveryView:
    campaign_id: str
    campaign_title: str
    category: str
    delivery_id: str
    delivery_type: str
    png_src: str
    png_path: Path
    width: int
    height: int
    topic_ids: tuple[str, ...]
    topic_labels: tuple[str, ...]


def png_dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"不是有效 PNG：{path}")
    return struct.unpack(">II", header[16:24])


def load_deliveries() -> tuple[list[DeliveryView], int]:
    deliveries: list[DeliveryView] = []
    total_topics = 0
    errors: list[str] = []

    for campaign_id in CAMPAIGNS:
        manifest_path = STORY_ROOT / campaign_id / "assets" / "manifest-complete.json"
        if not manifest_path.is_file():
            errors.append(f"缺少清单：{manifest_path}")
            continue

        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        campaign_title = (
            manifest.get("title")
            or manifest.get("campaignTitle")
            or campaign_id
        )
        topics = {topic["id"]: topic for topic in manifest.get("topics", [])}
        total_topics += len(topics)

        for delivery in manifest.get("deliveries", []):
            delivery_id = delivery["id"]
            topic_ids = tuple(delivery.get("topicIds", ()))
            unknown = [topic_id for topic_id in topic_ids if topic_id not in topics]
            if unknown:
                errors.append(f"{campaign_id}/{delivery_id} 引用了未知题材：{unknown}")
                continue
            categories = {topics[topic_id]["category"] for topic_id in topic_ids}
            if len(categories) != 1:
                errors.append(
                    f"{campaign_id}/{delivery_id} 必须且只能属于一个类别，实际为 {sorted(categories)}"
                )
                continue
            category = next(iter(categories))
            if category not in CATEGORY_LABELS:
                errors.append(f"{campaign_id}/{delivery_id} 使用未知类别：{category}")
                continue

            png_path = manifest_path.parent / delivery["png"]
            if not png_path.is_file():
                errors.append(f"缺少 PNG：{png_path}")
                continue
            actual_size = png_dimensions(png_path)
            declared_size = (int(delivery["width"]), int(delivery["height"]))
            if actual_size != declared_size:
                errors.append(
                    f"{campaign_id}/{delivery_id} 尺寸不一致：PNG={actual_size}，清单={declared_size}"
                )
                continue

            deliveries.append(
                DeliveryView(
                    campaign_id=campaign_id,
                    campaign_title=str(campaign_title),
                    category=category,
                    delivery_id=delivery_id,
                    delivery_type=str(delivery.get("type", "asset")),
                    png_src=png_path.relative_to(STORY_ROOT).as_posix(),
                    png_path=png_path,
                    width=actual_size[0],
                    height=actual_size[1],
                    topic_ids=topic_ids,
                    topic_labels=tuple(topics[topic_id]["label"] for topic_id in topic_ids),
                )
            )

    if errors:
        raise SystemExit("画廊生成失败：\n- " + "\n- ".join(errors))
    return deliveries, total_topics


def esc(value: object, *, quote: bool = False) -> str:
    return html.escape(str(value), quote=quote)


def render_card(item: DeliveryView) -> str:
    search_text = " ".join(
        (
            item.campaign_id,
            item.campaign_title,
            item.category,
            CATEGORY_LABELS[item.category],
            item.delivery_id,
            item.delivery_type,
            item.png_src,
            *item.topic_ids,
            *item.topic_labels,
        )
    ).casefold()
    alt = f"{item.campaign_title} {CATEGORY_LABELS[item.category]} {item.delivery_id}"
    return f"""        <article class="asset-card" data-card data-campaign="{esc(item.campaign_id, quote=True)}" data-category="{esc(item.category, quote=True)}" data-search="{esc(search_text, quote=True)}">
          <div class="asset-preview"><img src="{esc(item.png_src, quote=True)}" alt="{esc(alt, quote=True)}" loading="lazy" decoding="async"></div>
          <div class="asset-copy">
            <div class="asset-kicker"><span>{esc(item.delivery_type)}</span><span>{len(item.topic_ids)} 题材</span></div>
            <h3>{esc(item.delivery_id)}</h3>
            <div class="asset-meta"><span>{item.width} × {item.height}</span><span>PNG</span></div>
            <code title="{esc(item.png_src, quote=True)}">{esc(item.png_src)}</code>
          </div>
        </article>"""


def render_gallery(deliveries: list[DeliveryView], total_topics: int) -> str:
    grouped: dict[str, dict[str, list[DeliveryView]]] = defaultdict(lambda: defaultdict(list))
    titles: dict[str, str] = {}
    for item in deliveries:
        grouped[item.campaign_id][item.category].append(item)
        titles[item.campaign_id] = item.campaign_title

    campaign_options = "\n".join(
        f'          <option value="{esc(campaign, quote=True)}">{esc(titles[campaign])}</option>'
        for campaign in CAMPAIGNS
    )
    category_options = "\n".join(
        f'          <option value="{esc(category, quote=True)}">{esc(CATEGORY_LABELS[category])}</option>'
        for category in CATEGORY_ORDER
    )

    campaign_sections: list[str] = []
    for campaign in CAMPAIGNS:
        category_sections: list[str] = []
        for category in CATEGORY_ORDER:
            items = grouped[campaign].get(category, [])
            if not items:
                continue
            cards = "\n".join(render_card(item) for item in items)
            topic_count = sum(len(item.topic_ids) for item in items)
            category_sections.append(
                f"""      <section class="category-section" data-category-section data-campaign="{esc(campaign, quote=True)}" data-category="{esc(category, quote=True)}">
        <div class="section-heading">
          <h2>{esc(CATEGORY_LABELS[category])}</h2>
          <span>{topic_count} 题材 · {len(items)} 份 PNG</span>
        </div>
        <div class="asset-grid">
{cards}
        </div>
      </section>"""
            )
        campaign_topic_count = sum(len(item.topic_ids) for item in deliveries if item.campaign_id == campaign)
        campaign_delivery_count = sum(1 for item in deliveries if item.campaign_id == campaign)
        campaign_sections.append(
            f"""    <section class="campaign-section" data-campaign-section data-campaign="{esc(campaign, quote=True)}">
      <header class="campaign-heading">
        <div><span>{esc(campaign)}</span><h2>{esc(titles[campaign])}</h2></div>
        <p>{campaign_topic_count} 题材 · {campaign_delivery_count} 份 PNG</p>
      </header>
{chr(10).join(category_sections)}
    </section>"""
        )

    return f"""<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>三剧本题材覆盖原型总览</title>
  <style>
    :root {{ color-scheme: dark; --bg:#111719; --surface:#182124; --surface-2:#202c2f; --line:#354548; --text:#f2eee4; --muted:#9eaaa8; --accent:#e0ad62; --accent-2:#7eb2a2; }}
    * {{ box-sizing:border-box; }}
    body {{ margin:0; background:radial-gradient(circle at 85% 0,#243234 0,transparent 34rem),var(--bg); color:var(--text); font:15px/1.5 ui-sans-serif,system-ui,-apple-system,"PingFang SC","Microsoft YaHei",sans-serif; }}
    header,main {{ width:min(1500px,calc(100% - 32px)); margin-inline:auto; }}
    .page-head {{ padding:44px 0 24px; }}
    .eyebrow {{ color:var(--accent); font-size:12px; font-weight:800; letter-spacing:.16em; text-transform:uppercase; }}
    h1 {{ margin:6px 0 8px; font-size:clamp(30px,5vw,58px); line-height:1.05; letter-spacing:-.04em; }}
    .page-head p {{ margin:0; max-width:760px; color:var(--muted); }}
    .summary {{ display:flex; gap:22px; margin-top:20px; color:var(--muted); flex-wrap:wrap; }}
    .summary strong {{ color:var(--text); font-variant-numeric:tabular-nums; }}
    .toolbar-wrap {{ position:sticky; top:0; z-index:10; border-block:1px solid var(--line); background:color-mix(in srgb,var(--bg) 88%,transparent); backdrop-filter:blur(14px); }}
    .toolbar {{ width:min(1500px,calc(100% - 32px)); margin:auto; display:grid; grid-template-columns:minmax(180px,1fr) minmax(180px,1fr) minmax(240px,2fr) auto; gap:10px; padding:12px 0; align-items:center; }}
    select,input {{ width:100%; min-height:42px; border:1px solid var(--line); border-radius:6px; background:var(--surface); color:var(--text); padding:9px 12px; font:inherit; }}
    input::placeholder {{ color:var(--muted); }}
    #visible-count {{ color:var(--accent-2); font-variant-numeric:tabular-nums; white-space:nowrap; text-align:right; }}
    main {{ padding:30px 0 72px; }}
    .campaign-section {{ margin-bottom:54px; }}
    .campaign-heading {{ display:flex; justify-content:space-between; gap:20px; align-items:end; border-bottom:1px solid var(--line); padding-bottom:14px; margin-bottom:28px; }}
    .campaign-heading span {{ color:var(--accent); font:700 12px/1.2 ui-monospace,SFMono-Regular,Menlo,monospace; }}
    .campaign-heading h2 {{ margin:3px 0 0; font-size:28px; }}
    .campaign-heading p {{ margin:0; color:var(--muted); }}
    .category-section {{ margin:0 0 38px; }}
    .section-heading {{ display:flex; align-items:baseline; justify-content:space-between; gap:16px; margin-bottom:12px; }}
    .section-heading h2 {{ margin:0; font-size:20px; }}
    .section-heading span {{ color:var(--muted); font-size:13px; }}
    .asset-grid {{ display:grid; grid-template-columns:repeat(auto-fill,minmax(260px,1fr)); gap:14px; }}
    .asset-card {{ min-width:0; border:1px solid var(--line); background:var(--surface); border-radius:8px; overflow:hidden; }}
    .asset-preview {{ height:210px; display:grid; place-items:center; padding:14px; background-color:#0d1214; background-image:linear-gradient(45deg,#172022 25%,transparent 25%),linear-gradient(-45deg,#172022 25%,transparent 25%),linear-gradient(45deg,transparent 75%,#172022 75%),linear-gradient(-45deg,transparent 75%,#172022 75%); background-size:20px 20px; background-position:0 0,0 10px,10px -10px,-10px 0; }}
    .asset-preview img {{ display:block; max-width:100%; max-height:100%; image-rendering:pixelated; object-fit:contain; filter:drop-shadow(0 8px 16px rgba(0,0,0,.28)); }}
    .asset-copy {{ padding:13px 14px 15px; }}
    .asset-kicker,.asset-meta {{ display:flex; justify-content:space-between; gap:12px; color:var(--muted); font-size:12px; }}
    .asset-kicker span:first-child {{ color:var(--accent-2); text-transform:uppercase; letter-spacing:.06em; }}
    .asset-copy h3 {{ overflow-wrap:anywhere; margin:7px 0; font:700 14px/1.35 ui-monospace,SFMono-Regular,Menlo,monospace; }}
    code {{ display:block; margin-top:9px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; color:var(--muted); font-size:11px; }}
    .empty {{ display:none; padding:64px 0; text-align:center; color:var(--muted); }}
    [hidden] {{ display:none !important; }}
    @media (max-width:760px) {{ .toolbar {{ grid-template-columns:1fr 1fr; }} #asset-search {{ grid-column:1/-1; }} #visible-count {{ text-align:left; }} .campaign-heading {{ align-items:start; flex-direction:column; }} .asset-grid {{ grid-template-columns:1fr; }} }}
  </style>
</head>
<body>
  <header class="page-head">
    <div class="eyebrow">Prototype coverage library</div>
    <h1>三剧本题材覆盖原型</h1>
    <p><strong>非正式游戏美术。</strong>本页用于检查 1212 个题材的 ID、切片和引擎接线；程序化扩展图已标记为 prototype，正由 runtime-v2 逐批替换。</p>
    <div class="summary"><span><strong>3</strong> 个剧本</span><span><strong>{total_topics}</strong> 个题材</span><span><strong>{len(deliveries)}</strong> 份 PNG</span><span><strong>12</strong> 个类别</span></div>
  </header>
  <div class="toolbar-wrap">
    <div class="toolbar" role="search">
      <select id="campaign-filter" aria-label="筛选剧本"><option value="all">全部剧本</option>
{campaign_options}
      </select>
      <select id="category-filter" aria-label="筛选类别"><option value="all">全部类别</option>
{category_options}
      </select>
      <input id="asset-search" type="search" placeholder="搜索交付 ID、路径、题材 ID 或名称" autocomplete="off">
      <output id="visible-count" aria-live="polite">显示 {len(deliveries)} / {len(deliveries)}</output>
    </div>
  </div>
  <main>
{chr(10).join(campaign_sections)}
    <p class="empty" id="empty-state">没有符合当前筛选条件的素材。</p>
  </main>
  <script>
    (() => {{
      const campaign = document.getElementById('campaign-filter');
      const category = document.getElementById('category-filter');
      const search = document.getElementById('asset-search');
      const count = document.getElementById('visible-count');
      const empty = document.getElementById('empty-state');
      const cards = [...document.querySelectorAll('[data-card]')];
      const categorySections = [...document.querySelectorAll('[data-category-section]')];
      const campaignSections = [...document.querySelectorAll('[data-campaign-section]')];

      function applyFilters() {{
        const campaignValue = campaign.value;
        const categoryValue = category.value;
        const query = search.value.trim().toLocaleLowerCase('zh-CN');
        let visible = 0;
        for (const card of cards) {{
          const matches = (campaignValue === 'all' || card.dataset.campaign === campaignValue)
            && (categoryValue === 'all' || card.dataset.category === categoryValue)
            && (!query || card.dataset.search.includes(query));
          card.hidden = !matches;
          if (matches) visible += 1;
        }}
        for (const section of categorySections) {{
          section.hidden = !section.querySelector('[data-card]:not([hidden])');
        }}
        for (const section of campaignSections) {{
          section.hidden = !section.querySelector('[data-category-section]:not([hidden])');
        }}
        count.value = `显示 ${{visible}} / ${{cards.length}}`;
        empty.style.display = visible ? 'none' : 'block';
      }}

      campaign.addEventListener('change', applyFilters);
      category.addEventListener('change', applyFilters);
      search.addEventListener('input', applyFilters);
    }})();
  </script>
</body>
</html>
"""


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument(
        "--check-only",
        action="store_true",
        help="只校验三个清单及其 PNG，不写入 HTML",
    )
    args = parser.parse_args()

    deliveries, total_topics = load_deliveries()
    if not args.check_only:
        args.output.write_text(render_gallery(deliveries, total_topics), encoding="utf-8")
    print(
        json.dumps(
            {
                "campaigns": len(CAMPAIGNS),
                "topics": total_topics,
                "deliveries": len(deliveries),
                "pngFilesChecked": len(deliveries),
                "output": None if args.check_only else str(args.output),
                "passed": True,
            },
            ensure_ascii=False,
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
