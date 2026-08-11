#!/usr/bin/env python3
"""Build an offline gallery containing only runtime-v2 game assets."""

from __future__ import annotations

import argparse
import html
import json
import struct
from dataclasses import dataclass
from pathlib import Path


STORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_OUTPUT = STORY_ROOT / "RUNTIME-V2-ASSET-GALLERY.html"
CAMPAIGNS = ("candidate-01", "candidate-02", "candidate-03")
CATEGORY_ORDER = (
    "combat-unit",
    "mission-unit",
    "terrain",
    "interactive-structure",
    "battle-prop",
    "equipment",
    "skill",
    "status",
    "fx",
    "hud",
    "narrative-static",
)
CATEGORY_LABELS = {
    "combat-unit": "战斗单位",
    "mission-unit": "任务单位",
    "terrain": "地形",
    "interactive-structure": "交互建筑",
    "battle-prop": "战场物件",
    "equipment": "装备",
    "skill": "技能",
    "status": "状态",
    "fx": "FX",
    "hud": "HUD",
    "narrative-static": "叙事场景",
}


@dataclass(frozen=True)
class AssetView:
    campaign_id: str
    campaign_title: str
    batch: str
    quality: str
    runtime_ready: bool
    asset_id: str
    content_id: str
    topic_id: str
    topic_label: str
    category: str
    asset_type: str
    png_src: str
    width: int
    height: int


def png_dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"不是有效 PNG：{path}")
    return struct.unpack(">II", header[16:24])


def normalize_category(asset_type: str) -> str:
    value = asset_type.casefold()
    if value.startswith("combat-unit"):
        return "combat-unit"
    if value.startswith("mission-unit"):
        return "mission-unit"
    if value.startswith("terrain"):
        return "terrain"
    if "structure" in value or "facility" in value:
        return "interactive-structure"
    if value in {"map-object", "battle-prop"} or "prop" in value:
        return "battle-prop"
    if "equipment" in value:
        return "equipment"
    if "skill" in value:
        return "skill"
    if "status" in value:
        return "status"
    if value.startswith("fx"):
        return "fx"
    if "hud" in value:
        return "hud"
    if "scene" in value or "narrative" in value:
        return "narrative-static"
    raise ValueError(f"无法识别运行时资产类别：{asset_type}")


def manifest_sort_key(path: Path) -> tuple[int, str]:
    return (0 if path.name == "manifest-runtime-v2.json" else 1, path.name)


def load_assets() -> tuple[list[AssetView], list[str]]:
    views: list[AssetView] = []
    manifests: list[str] = []
    errors: list[str] = []
    seen_ids: set[tuple[str, str]] = set()
    seen_paths: set[Path] = set()

    for campaign_id in CAMPAIGNS:
        asset_root = STORY_ROOT / campaign_id / "assets"
        topic_manifest_path = asset_root / "manifest-complete.json"
        topic_labels: dict[str, str] = {}
        canonical_title = campaign_id
        if topic_manifest_path.is_file():
            topic_manifest = json.loads(topic_manifest_path.read_text(encoding="utf-8"))
            canonical_title = str(
                topic_manifest.get("campaignTitle")
                or topic_manifest.get("title")
                or campaign_id
            )
            topic_labels = {
                str(topic["id"]): str(topic.get("label", topic["id"]))
                for topic in topic_manifest.get("topics", [])
            }

        manifest_paths = sorted(asset_root.glob("manifest-runtime-v2*.json"), key=manifest_sort_key)
        if not manifest_paths:
            errors.append(f"{campaign_id} 缺少 runtime-v2 manifest")
            continue

        for manifest_path in manifest_paths:
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            quality = str(manifest.get("qualityTier", "unknown"))
            if quality == "prototype":
                errors.append(f"运行时画廊拒绝 prototype 清单：{manifest_path}")
                continue
            campaign_title = canonical_title
            batch = str(manifest.get("batchId") or "primary")
            manifest_runtime_ready = bool(manifest.get("runtimeReady", False))
            manifests.append(manifest_path.relative_to(STORY_ROOT).as_posix())

            declared_count = manifest.get("assetCount")
            assets = manifest.get("assets", [])
            if declared_count is not None and int(declared_count) != len(assets):
                errors.append(
                    f"{manifest_path.name} assetCount={declared_count}，实际={len(assets)}"
                )
            asset_ids = {str(asset.get("id", "")) for asset in assets}
            game_integration = manifest.get("gameIntegration")
            has_asset_ready_list = (
                isinstance(game_integration, dict)
                and "runtimeReadyAssetIds" in game_integration
            )
            partial_ready_ids: set[str] = set()
            if has_asset_ready_list:
                ready_value = game_integration.get("runtimeReadyAssetIds")
                if not isinstance(ready_value, list) or not all(
                    isinstance(value, str) and value for value in ready_value
                ):
                    errors.append(
                        f"{manifest_path.name} 的 gameIntegration.runtimeReadyAssetIds "
                        "必须是非空资产 ID 数组"
                    )
                    ready_value = []
                unknown_ready_ids = sorted(set(ready_value) - asset_ids)
                if unknown_ready_ids:
                    errors.append(
                        f"{manifest_path.name} 的 runtimeReadyAssetIds 不属于 assets："
                        f"{unknown_ready_ids}"
                    )
                partial_ready_ids = set(ready_value) & asset_ids
            for asset in assets:
                asset_id = str(asset.get("id", ""))
                content_id = str(asset.get("contentId", ""))
                identity = (campaign_id, content_id)
                if not asset_id or not content_id:
                    errors.append(f"{manifest_path.name} 存在缺 id/contentId 的资产")
                    continue
                if identity in seen_ids:
                    errors.append(f"{campaign_id} contentId 重复：{content_id}")
                    continue
                seen_ids.add(identity)

                png_value = asset.get("png")
                if not png_value:
                    errors.append(f"{campaign_id}/{asset_id} 缺少 PNG 路径")
                    continue
                png_path = asset_root / str(png_value)
                if not png_path.is_file():
                    errors.append(f"缺少 PNG：{png_path}")
                    continue
                if png_path in seen_paths:
                    errors.append(f"PNG 被多个资产重复声明：{png_path}")
                    continue
                seen_paths.add(png_path)

                actual_size = png_dimensions(png_path)
                declared_size = (int(asset["width"]), int(asset["height"]))
                if actual_size != declared_size:
                    errors.append(
                        f"{campaign_id}/{asset_id} 尺寸不一致：PNG={actual_size}，清单={declared_size}"
                    )
                    continue
                topic_id = str(asset.get("topicId", ""))
                if topic_id and topic_id not in topic_labels:
                    errors.append(f"{campaign_id}/{asset_id} 引用了未知 topicId：{topic_id}")
                    continue
                asset_type = str(asset.get("type", "asset"))
                try:
                    category = normalize_category(asset_type)
                except ValueError as exc:
                    errors.append(f"{campaign_id}/{asset_id}: {exc}")
                    continue

                views.append(
                    AssetView(
                        campaign_id=campaign_id,
                        campaign_title=campaign_title,
                        batch=batch,
                        quality=quality,
                        runtime_ready=(
                            asset_id in partial_ready_ids
                            if has_asset_ready_list
                            else manifest_runtime_ready
                        ),
                        asset_id=asset_id,
                        content_id=content_id,
                        topic_id=topic_id,
                        topic_label=topic_labels.get(topic_id, content_id),
                        category=category,
                        asset_type=asset_type,
                        png_src=png_path.relative_to(STORY_ROOT).as_posix(),
                        width=actual_size[0],
                        height=actual_size[1],
                    )
                )

    if errors:
        raise SystemExit("运行时画廊生成失败：\n- " + "\n- ".join(errors))
    return views, manifests


def esc(value: object, *, quote: bool = False) -> str:
    return html.escape(str(value), quote=quote)


def render_card(asset: AssetView) -> str:
    status = "实机通过" if asset.runtime_ready else "候选待实机"
    search = " ".join(
        (
            asset.campaign_id,
            asset.campaign_title,
            asset.batch,
            asset.category,
            CATEGORY_LABELS[asset.category],
            asset.asset_id,
            asset.content_id,
            asset.topic_id,
            asset.topic_label,
            asset.asset_type,
            asset.png_src,
        )
    ).casefold()
    return f'''        <article class="card" data-card data-campaign="{esc(asset.campaign_id, quote=True)}" data-category="{esc(asset.category, quote=True)}" data-status="{'ready' if asset.runtime_ready else 'candidate'}" data-search="{esc(search, quote=True)}">
          <div class="preview"><img src="{esc(asset.png_src, quote=True)}" alt="{esc(asset.topic_label, quote=True)}" loading="lazy" decoding="async"></div>
          <div class="copy">
            <div class="badges"><span class="batch">{esc(asset.batch)}</span><span class="{'ready' if asset.runtime_ready else 'candidate'}">{status}</span></div>
            <h3>{esc(asset.topic_label)}</h3>
            <p>{esc(asset.content_id)}</p>
            <div class="meta"><span>{asset.width} × {asset.height}</span><span>{esc(asset.asset_type)}</span></div>
            <code title="{esc(asset.png_src, quote=True)}">{esc(asset.png_src)}</code>
          </div>
        </article>'''


def render_gallery(assets: list[AssetView], manifests: list[str]) -> str:
    titles = {
        campaign: next(asset.campaign_title for asset in assets if asset.campaign_id == campaign)
        for campaign in CAMPAIGNS
    }
    sections: list[str] = []
    for campaign in CAMPAIGNS:
        campaign_assets = [asset for asset in assets if asset.campaign_id == campaign]
        categories: list[str] = []
        for category in CATEGORY_ORDER:
            items = [asset for asset in campaign_assets if asset.category == category]
            if not items:
                continue
            categories.append(
                f'''      <section class="category" data-category-section>
        <div class="section-head"><h2>{CATEGORY_LABELS[category]}</h2><span>{len(items)} 项</span></div>
        <div class="grid">{''.join(render_card(item) for item in items)}</div>
      </section>'''
            )
        ready_count = sum(asset.runtime_ready for asset in campaign_assets)
        sections.append(
            f'''    <section class="campaign" data-campaign-section>
      <header class="campaign-head"><div><span>{campaign}</span><h2>{esc(titles[campaign])}</h2></div><p>{len(campaign_assets)} 项 · {ready_count} 项来自实机通过包</p></header>
{''.join(categories)}
    </section>'''
        )

    options_campaign = "".join(
        f'<option value="{campaign}">{esc(titles[campaign])}</option>' for campaign in CAMPAIGNS
    )
    options_category = "".join(
        f'<option value="{category}">{label}</option>'
        for category, label in CATEGORY_LABELS.items()
    )
    ready_count = sum(asset.runtime_ready for asset in assets)
    return f'''<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
  <title>三剧本 Runtime V2 游戏素材</title>
  <style>
    :root {{ color-scheme:dark; --bg:#0d1315; --panel:#172023; --line:#334347; --text:#eeeade; --muted:#96a5a5; --gold:#dba65e; --cyan:#72b6ad; --warn:#d9a665; }}
    * {{ box-sizing:border-box }} body {{ margin:0;background:radial-gradient(circle at 90% 0,#233237,transparent 36rem),var(--bg);color:var(--text);font:14px/1.5 system-ui,-apple-system,"PingFang SC",sans-serif }}
    .page-head,main {{ width:min(1520px,calc(100% - 32px));margin:auto }} .page-head {{ padding:40px 0 24px }} .eyebrow {{ color:var(--gold);font-weight:800;letter-spacing:.14em;text-transform:uppercase }}
    h1 {{ margin:5px 0 8px;font-size:clamp(30px,5vw,54px);line-height:1.05 }} .page-head p {{ max-width:820px;color:var(--muted);margin:0 }} .summary {{ display:flex;gap:22px;flex-wrap:wrap;margin-top:18px;color:var(--muted) }} .summary strong {{ color:var(--text) }}
    .toolbar-wrap {{ position:sticky;top:0;z-index:4;border-block:1px solid var(--line);background:#0d1315e8;backdrop-filter:blur(12px) }} .toolbar {{ width:min(1520px,calc(100% - 32px));margin:auto;display:grid;grid-template-columns:1fr 1fr 1fr 2fr auto;gap:9px;padding:11px 0 }}
    select,input {{ width:100%;min-height:40px;border:1px solid var(--line);border-radius:5px;background:var(--panel);color:var(--text);padding:8px 10px }} output {{ align-self:center;color:var(--cyan);white-space:nowrap }} main {{ padding:28px 0 70px }}
    .campaign {{ margin-bottom:52px }} .campaign-head {{ display:flex;justify-content:space-between;align-items:end;border-bottom:1px solid var(--line);padding-bottom:12px;margin-bottom:25px }} .campaign-head span {{ color:var(--gold);font:700 12px monospace }} .campaign-head h2 {{ margin:3px 0 0;font-size:27px }} .campaign-head p {{ color:var(--muted);margin:0 }}
    .category {{ margin-bottom:34px }} .section-head {{ display:flex;justify-content:space-between;align-items:baseline;margin-bottom:11px }} .section-head h2 {{ margin:0;font-size:19px }} .section-head span {{ color:var(--muted) }} .grid {{ display:grid;grid-template-columns:repeat(auto-fill,minmax(245px,1fr));gap:12px }}
    .card {{ min-width:0;border:1px solid var(--line);border-radius:7px;overflow:hidden;background:var(--panel) }} .preview {{ height:178px;display:grid;place-items:center;padding:10px;background-color:#111719;background-image:linear-gradient(45deg,#1a2427 25%,transparent 25%),linear-gradient(-45deg,#1a2427 25%,transparent 25%),linear-gradient(45deg,transparent 75%,#1a2427 75%),linear-gradient(-45deg,transparent 75%,#1a2427 75%);background-size:16px 16px;background-position:0 0,0 8px,8px -8px,-8px 0 }}
    .preview img {{ display:block;max-width:100%;max-height:100%;object-fit:contain;image-rendering:pixelated;filter:drop-shadow(0 7px 11px #0008) }} .copy {{ padding:12px }} .badges,.meta {{ display:flex;justify-content:space-between;gap:8px;color:var(--muted);font-size:11px }} .badges span {{ padding:2px 6px;border:1px solid var(--line);border-radius:999px }} .badges .ready {{ color:var(--cyan);border-color:#41766e }} .badges .candidate {{ color:var(--warn);border-color:#755d3f }} .copy h3 {{ margin:9px 0 1px;font-size:15px }} .copy p {{ margin:0 0 8px;color:var(--muted);font:12px monospace;overflow-wrap:anywhere }} code {{ display:block;margin-top:8px;color:var(--muted);font-size:10px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap }} [hidden] {{ display:none!important }} .empty {{ display:none;text-align:center;color:var(--muted);padding:60px }}
    @media(max-width:800px) {{ .toolbar {{ grid-template-columns:1fr 1fr }} #search {{ grid-column:1/-1 }} .campaign-head {{ align-items:start;flex-direction:column }} }}
  </style>
</head>
<body>
  <header class="page-head"><div class="eyebrow">Runtime asset library</div><h1>三剧本 Runtime V2 游戏素材</h1><p>本页只展示按 32px 网格、动画帧、锚点、透明通道与状态切片契约生产的运行时素材。标为“候选待实机”的包已通过机器 QA，但仍需进入真实关卡截图后才能晋级。</p><div class="summary"><span><strong>3</strong> 个剧本</span><span><strong>{len(assets)}</strong> 项运行时资产</span><span><strong>{ready_count}</strong> 项来自实机通过包</span><span><strong>{len(manifests)}</strong> 份 manifest</span></div></header>
  <div class="toolbar-wrap"><div class="toolbar"><select id="campaign"><option value="all">全部剧本</option>{options_campaign}</select><select id="category"><option value="all">全部类别</option>{options_category}</select><select id="status"><option value="all">全部状态</option><option value="ready">实机通过</option><option value="candidate">候选待实机</option></select><input id="search" type="search" placeholder="搜索题材、contentId、topicId 或路径"><output id="count">显示 {len(assets)} / {len(assets)}</output></div></div>
  <main>{''.join(sections)}<p class="empty" id="empty">没有符合条件的素材。</p></main>
  <script>(()=>{{const cs=[...document.querySelectorAll('[data-card]')],c=document.querySelector('#campaign'),g=document.querySelector('#category'),s=document.querySelector('#status'),q=document.querySelector('#search'),o=document.querySelector('#count'),e=document.querySelector('#empty');function f(){{let n=0,t=q.value.trim().toLocaleLowerCase('zh-CN');for(const x of cs){{const v=(c.value==='all'||x.dataset.campaign===c.value)&&(g.value==='all'||x.dataset.category===g.value)&&(s.value==='all'||x.dataset.status===s.value)&&(!t||x.dataset.search.includes(t));x.hidden=!v;if(v)n++}}for(const x of document.querySelectorAll('[data-category-section]'))x.hidden=!x.querySelector('[data-card]:not([hidden])');for(const x of document.querySelectorAll('[data-campaign-section]'))x.hidden=!x.querySelector('[data-category-section]:not([hidden])');o.value=`显示 ${{n}} / ${{cs.length}}`;e.style.display=n?'none':'block'}}[c,g,s].forEach(x=>x.addEventListener('change',f));q.addEventListener('input',f)}})()</script>
</body></html>'''


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--check-only", action="store_true")
    args = parser.parse_args()
    assets, manifests = load_assets()
    if not args.check_only:
        args.output.write_text(render_gallery(assets, manifests), encoding="utf-8")
    print(
        json.dumps(
            {
                "campaigns": len(CAMPAIGNS),
                "manifests": len(manifests),
                "assets": len(assets),
                "runtimeReadyAssets": sum(asset.runtime_ready for asset in assets),
                "pngFilesChecked": len(assets),
                "output": None if args.check_only else str(args.output),
                "passed": True,
            },
            ensure_ascii=False,
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
