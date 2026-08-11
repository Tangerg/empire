#!/usr/bin/env python3
"""Generate an offline animation lab for runtime-v2 unit sprite sheets."""

from __future__ import annotations

import argparse
import html
import json
import re
import struct
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Any


STORY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = STORY_ROOT / "RUNTIME-V2-UNIT-LAB.html"
CAMPAIGNS = ("candidate-01", "candidate-02", "candidate-03")
CATEGORY_LABELS = {"combat-unit": "战斗单位", "mission-unit": "任务单位"}
VALID_FRAME_WIDTHS = {32, 64, 96}
VALID_FRAME_HEIGHTS = {48, 64}


@dataclass(frozen=True)
class UnitView:
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
    png_src: str
    width: int
    height: int
    frame_width: int
    frame_height: int
    frames: int
    footprint: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument(
        "--check-only",
        action="store_true",
        help="validate manifests and PNG declarations without writing HTML",
    )
    return parser.parse_args()


def png_dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("invalid PNG signature or truncated IHDR")
    return struct.unpack(">II", header[16:24])


def manifest_sort_key(path: Path) -> tuple[int, int, str]:
    if path.name == "manifest-runtime-v2.json":
        return (0, 0, path.name)
    match = re.search(r"-b(\d+)\.json$", path.name, re.IGNORECASE)
    return (1, int(match.group(1)) if match else 9999, path.name)


def manifest_batch(path: Path, manifest: dict[str, Any]) -> str:
    if path.name == "manifest-runtime-v2.json":
        return "primary"
    raw = str(manifest.get("batchId") or "")
    match = re.search(r"(\d+)", raw)
    if not match:
        match = re.search(r"-b(\d+)\.json$", path.name, re.IGNORECASE)
    return f"b{int(match.group(1)):02d}" if match else path.stem


def unit_category(asset_type: object) -> str:
    value = str(asset_type or "").strip().casefold()
    if value.startswith("combat-unit") or value == "unit-sheet":
        return "combat-unit"
    if value.startswith("mission-unit") or value in {"mission-unit-sprite", "task-unit"}:
        return "mission-unit"
    return ""


def positive_int(value: object, field: str, location: str, errors: list[str]) -> int:
    try:
        result = int(value)
    except (TypeError, ValueError):
        errors.append(f"{location}: {field} must be an integer")
        return 0
    if result <= 0:
        errors.append(f"{location}: {field} must be positive")
        return 0
    return result


def footprint_label(value: object, location: str, errors: list[str]) -> str:
    columns: object | None = None
    rows: object | None = None
    if value is None:
        return "未声明"
    if isinstance(value, dict):
        columns = value.get("columns", value.get("width"))
        rows = value.get("rows", value.get("height"))
    elif isinstance(value, (list, tuple)) and len(value) == 2:
        columns, rows = value
    else:
        errors.append(f"{location}: footprint must be [columns, rows] or an object")
        return "无效"
    try:
        col_value, row_value = int(columns), int(rows)
    except (TypeError, ValueError):
        errors.append(f"{location}: footprint dimensions must be integers")
        return "无效"
    if col_value <= 0 or row_value <= 0:
        errors.append(f"{location}: footprint dimensions must be positive")
        return "无效"
    return f"{col_value}×{row_value} 格"


def read_json(path: Path, errors: list[str]) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"{path.relative_to(STORY_ROOT)}: cannot read JSON: {exc}")
        return {}
    if not isinstance(value, dict):
        errors.append(f"{path.relative_to(STORY_ROOT)}: top level must be an object")
        return {}
    return value


def load_units() -> tuple[list[UnitView], list[str]]:
    units: list[UnitView] = []
    manifests_seen: list[str] = []
    errors: list[str] = []
    identities: set[tuple[str, str]] = set()

    for campaign_id in CAMPAIGNS:
        asset_root = STORY_ROOT / campaign_id / "assets"
        complete_path = asset_root / "manifest-complete.json"
        complete = read_json(complete_path, errors)
        campaign_title = str(complete.get("campaignTitle") or complete.get("title") or campaign_id)
        topic_labels = {
            str(topic.get("id")): str(topic.get("label") or topic.get("id"))
            for topic in complete.get("topics", [])
            if isinstance(topic, dict) and topic.get("id")
        }
        manifest_paths = sorted(asset_root.glob("manifest-runtime-v2*.json"), key=manifest_sort_key)
        if not manifest_paths:
            errors.append(f"{campaign_id}: no manifest-runtime-v2*.json")
            continue

        for manifest_path in manifest_paths:
            manifest = read_json(manifest_path, errors)
            manifests_seen.append(manifest_path.relative_to(STORY_ROOT).as_posix())
            batch = manifest_batch(manifest_path, manifest)
            quality = str(manifest.get("qualityTier") or "unknown")
            manifest_ready = manifest.get("runtimeReady") is True
            raw_assets = manifest.get("assets", [])
            if not isinstance(raw_assets, list):
                errors.append(f"{campaign_id}/{manifest_path.name}: assets must be an array")
                continue
            declared_count = manifest.get("assetCount")
            if declared_count is not None:
                try:
                    if int(declared_count) != len(raw_assets):
                        errors.append(
                            f"{campaign_id}/{manifest_path.name}: assetCount {declared_count} != {len(raw_assets)}"
                        )
                except (TypeError, ValueError):
                    errors.append(f"{campaign_id}/{manifest_path.name}: assetCount must be an integer")

            integration = manifest.get("gameIntegration")
            has_ready_list = isinstance(integration, dict) and "runtimeReadyAssetIds" in integration
            raw_ready_ids = integration.get("runtimeReadyAssetIds", []) if has_ready_list else []
            if has_ready_list and not (
                isinstance(raw_ready_ids, list)
                and all(isinstance(value, str) and value for value in raw_ready_ids)
            ):
                errors.append(
                    f"{campaign_id}/{manifest_path.name}: gameIntegration.runtimeReadyAssetIds must be a string array"
                )
                raw_ready_ids = []
            ready_ids = set(raw_ready_ids)

            for index, raw_asset in enumerate(raw_assets):
                if not isinstance(raw_asset, dict):
                    errors.append(f"{campaign_id}/{manifest_path.name}: asset {index} is not an object")
                    continue
                category = unit_category(raw_asset.get("type"))
                if not category:
                    continue
                asset_id = str(raw_asset.get("id") or "")
                content_id = str(raw_asset.get("contentId") or "")
                location = f"{campaign_id}/{manifest_path.name}/{asset_id or index}"
                if not asset_id:
                    errors.append(f"{location}: missing id")
                if not content_id:
                    errors.append(f"{location}: missing contentId")
                identity = (campaign_id, content_id)
                if content_id and identity in identities:
                    errors.append(f"{location}: duplicate campaign contentId {content_id}")
                identities.add(identity)

                png_value = raw_asset.get("png")
                if not isinstance(png_value, str) or not png_value:
                    errors.append(f"{location}: missing PNG path")
                    continue
                png_path = (asset_root / png_value).resolve()
                try:
                    png_path.relative_to(asset_root.resolve())
                except ValueError:
                    errors.append(f"{location}: PNG escapes candidate asset root")
                    continue
                if not png_path.is_file():
                    errors.append(f"{location}: missing PNG {png_value}")
                    continue

                width = positive_int(raw_asset.get("width"), "width", location, errors)
                height = positive_int(raw_asset.get("height"), "height", location, errors)
                frame_width = positive_int(raw_asset.get("frameWidth"), "frameWidth", location, errors)
                frame_height = positive_int(raw_asset.get("frameHeight"), "frameHeight", location, errors)
                frames = positive_int(raw_asset.get("frames"), "frames", location, errors)
                try:
                    actual_size = png_dimensions(png_path)
                except (OSError, ValueError) as exc:
                    errors.append(f"{location}: unreadable PNG: {exc}")
                    continue
                if actual_size != (width, height):
                    errors.append(f"{location}: PNG size {actual_size} != declared {(width, height)}")
                if frame_width not in VALID_FRAME_WIDTHS:
                    errors.append(f"{location}: frameWidth {frame_width} must be one of {sorted(VALID_FRAME_WIDTHS)}")
                if frame_height not in VALID_FRAME_HEIGHTS:
                    errors.append(f"{location}: frameHeight {frame_height} must be one of {sorted(VALID_FRAME_HEIGHTS)}")
                if frames != 4:
                    errors.append(f"{location}: runtime unit contract requires 4 frames, got {frames}")
                if width != frame_width * frames or height != frame_height:
                    errors.append(
                        f"{location}: horizontal sheet must equal frameWidth×frames by frameHeight; "
                        f"got {width}×{height}, expected {frame_width * frames}×{frame_height}"
                    )
                frame_order = raw_asset.get("frameOrder")
                if not isinstance(frame_order, list) or len(frame_order) != frames:
                    errors.append(f"{location}: frameOrder must contain exactly {frames} entries")

                topic_id = str(raw_asset.get("topicId") or "")
                if topic_id and topic_id not in topic_labels:
                    errors.append(f"{location}: unknown topicId {topic_id}")
                footprint = footprint_label(raw_asset.get("footprint"), location, errors)
                runtime_ready = (
                    asset_id in ready_ids
                    if has_ready_list
                    else manifest_ready or raw_asset.get("runtimeReady") is True
                )
                units.append(
                    UnitView(
                        campaign_id=campaign_id,
                        campaign_title=campaign_title,
                        batch=batch,
                        quality=quality,
                        runtime_ready=runtime_ready,
                        asset_id=asset_id,
                        content_id=content_id,
                        topic_id=topic_id,
                        topic_label=str(raw_asset.get("label") or topic_labels.get(topic_id) or content_id or asset_id),
                        category=category,
                        png_src=png_path.relative_to(STORY_ROOT).as_posix(),
                        width=width,
                        height=height,
                        frame_width=frame_width,
                        frame_height=frame_height,
                        frames=frames,
                        footprint=footprint,
                    )
                )

    if errors:
        raise SystemExit("Runtime V2 单位实验室校验失败：\n- " + "\n- ".join(errors))
    return units, manifests_seen


def esc(value: object, *, quote: bool = False) -> str:
    return html.escape(str(value), quote=quote)


def render_card(unit: UnitView) -> str:
    status_key = "ready" if unit.runtime_ready else "candidate"
    status_label = "ready / 实机通过" if unit.runtime_ready else "candidate / 待实机"
    search = " ".join(
        [
            unit.campaign_id,
            unit.campaign_title,
            unit.batch,
            unit.category,
            CATEGORY_LABELS[unit.category],
            unit.asset_id,
            unit.content_id,
            unit.topic_id,
            unit.topic_label,
        ]
    ).casefold()
    duration = max(0.64, unit.frames * 0.18)
    return f'''      <article class="unit-card" data-card data-campaign="{esc(unit.campaign_id, quote=True)}" data-batch="{esc(unit.batch, quote=True)}" data-category="{unit.category}" data-status="{status_key}" data-search="{esc(search, quote=True)}" data-frame-width="{unit.frame_width}" data-frame-height="{unit.frame_height}" data-sheet-width="{unit.width}" data-sheet-height="{unit.height}">
        <div class="card-head"><div><span class="campaign-tag">{esc(unit.campaign_id)} · {esc(unit.batch)}</span><h2>{esc(unit.topic_label)}</h2></div><span class="status {status_key}">{status_label}</span></div>
        <div class="stage" aria-label="{esc(unit.topic_label, quote=True)} 四帧动画预览">
          <div class="sprite-window" style="width:{unit.frame_width}px;height:{unit.frame_height}px">
            <div class="sprite-facing"><img class="sprite-strip" src="{esc(unit.png_src, quote=True)}" alt="" width="{unit.width}" height="{unit.height}" style="--frames:{unit.frames};--travel:-{unit.width}px;--duration:{duration:.2f}s;width:{unit.width}px;height:{unit.height}px"></div>
          </div>
        </div>
        <dl class="metadata">
          <div><dt>contentId</dt><dd>{esc(unit.content_id)}</dd></div>
          <div><dt>topicId</dt><dd>{esc(unit.topic_id or '—')}</dd></div>
          <div><dt>单帧 / 帧数</dt><dd>{unit.frame_width}×{unit.frame_height} / {unit.frames}</dd></div>
          <div><dt>Sheet</dt><dd>{unit.width}×{unit.height}</dd></div>
          <div><dt>Footprint</dt><dd>{esc(unit.footprint)}</dd></div>
          <div><dt>Quality</dt><dd>{esc(unit.quality)}</dd></div>
        </dl>
        <code title="{esc(unit.png_src, quote=True)}">{esc(unit.png_src)}</code>
      </article>'''


def render_lab(units: list[UnitView], manifests: list[str]) -> str:
    campaign_titles = {
        campaign: next((unit.campaign_title for unit in units if unit.campaign_id == campaign), campaign)
        for campaign in CAMPAIGNS
    }
    batches = sorted({unit.batch for unit in units}, key=lambda value: (value != "primary", value))
    combat_count = sum(unit.category == "combat-unit" for unit in units)
    mission_count = sum(unit.category == "mission-unit" for unit in units)
    ready_count = sum(unit.runtime_ready for unit in units)
    campaign_options = "".join(
        f'<option value="{campaign}">{esc(campaign_titles[campaign])}</option>'
        for campaign in CAMPAIGNS
    )
    batch_options = "".join(f'<option value="{esc(batch, quote=True)}">{esc(batch)}</option>' for batch in batches)
    cards = "\n".join(render_card(unit) for unit in units)
    document = '''<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
  <title>Runtime V2 单位动画实验室</title>
  <style>
    :root { color-scheme:dark; --bg:#0b1114; --panel:#151e22; --panel2:#10181b; --line:#314146; --text:#f0ecdf; --muted:#93a4a7; --gold:#e1aa61; --cyan:#70b9ad; --warn:#d8a467; }
    * { box-sizing:border-box } body { margin:0;background:radial-gradient(circle at 88% 0,#213239,transparent 36rem),var(--bg);color:var(--text);font:14px/1.5 system-ui,-apple-system,"PingFang SC",sans-serif }
    header,main,.toolbar { width:min(1600px,calc(100% - 32px));margin-inline:auto } header { padding:38px 0 24px } .eyebrow { color:var(--gold);font-weight:800;letter-spacing:.14em;text-transform:uppercase }
    h1 { margin:5px 0 7px;font-size:clamp(30px,5vw,52px);line-height:1.05 } header p { max-width:900px;margin:0;color:var(--muted) } .summary { display:flex;flex-wrap:wrap;gap:20px;margin-top:16px;color:var(--muted) } .summary strong { color:var(--text) }
    .toolbar-shell { position:sticky;top:0;z-index:5;border-block:1px solid var(--line);background:#0b1114ed;backdrop-filter:blur(12px) } .toolbar { display:grid;grid-template-columns:1fr .8fr .9fr .8fr .8fr .8fr auto 1.5fr auto;gap:8px;padding:10px 0 }
    select,input,button { min-height:40px;border:1px solid var(--line);border-radius:5px;background:var(--panel);color:var(--text);padding:8px 10px;font:inherit } select,input { width:100% } button { cursor:pointer;font-weight:750 } button[aria-pressed="true"] { color:var(--warn);border-color:#795e3c } output { align-self:center;color:var(--cyan);white-space:nowrap }
    main { padding:26px 0 72px }.grid { display:grid;grid-template-columns:repeat(auto-fill,minmax(390px,1fr));gap:13px }.unit-card { min-width:0;border:1px solid var(--line);border-radius:8px;background:linear-gradient(180deg,#182327,var(--panel));padding:13px;overflow:hidden }
    .card-head { display:flex;justify-content:space-between;align-items:start;gap:12px }.campaign-tag { color:var(--gold);font:700 11px ui-monospace,monospace }.card-head h2 { margin:3px 0 0;font-size:17px }.status { flex:none;border:1px solid;border-radius:999px;padding:3px 7px;font-size:10px }.status.ready { color:var(--cyan);border-color:#477c73 }.status.candidate { color:var(--warn);border-color:#735b3c }
    .stage { min-height:218px;margin:12px 0;display:grid;place-items:center;overflow:auto;border:1px solid #28373b;border-radius:6px;background-color:#0f1518;background-image:linear-gradient(45deg,#1a2428 25%,transparent 25%),linear-gradient(-45deg,#1a2428 25%,transparent 25%),linear-gradient(45deg,transparent 75%,#1a2428 75%),linear-gradient(-45deg,transparent 75%,#1a2428 75%);background-size:16px 16px;background-position:0 0,0 8px,8px -8px,-8px 0 }
    .sprite-window { flex:none;overflow:hidden;filter:drop-shadow(0 8px 9px #000a) }.sprite-facing { width:100%;height:100%;transform-origin:center }.sprite-strip { display:block;max-width:none;image-rendering:pixelated;animation:sheet-walk var(--duration) steps(var(--frames),end) infinite }.paused .sprite-strip { animation-play-state:paused } body[data-facing="left"] .sprite-facing { transform:scaleX(-1) }
    @keyframes sheet-walk { from { transform:translateX(0) } to { transform:translateX(var(--travel)) } }
    .metadata { display:grid;grid-template-columns:1fr 1fr;gap:6px 12px;margin:0 }.metadata div { min-width:0 }.metadata dt { color:var(--muted);font-size:10px;text-transform:uppercase;letter-spacing:.05em }.metadata dd { margin:0;font:11px/1.4 ui-monospace,SFMono-Regular,monospace;overflow-wrap:anywhere }.unit-card code { display:block;margin-top:10px;color:var(--muted);font-size:10px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap }
    [hidden] { display:none!important }.empty { display:none;text-align:center;color:var(--muted);padding:80px 0 }
    @media(max-width:1100px) { .toolbar { grid-template-columns:repeat(4,1fr) }.toolbar input { grid-column:span 2 } }
    @media(max-width:680px) { .toolbar { grid-template-columns:1fr 1fr }.toolbar input { grid-column:1/-1 }.grid { grid-template-columns:1fr }.metadata { grid-template-columns:1fr }.stage { place-items:center start } }
  </style>
</head>
<body data-facing="right">
  <header><div class="eyebrow">Runtime sprite inspection</div><h1>Runtime V2 单位动画实验室</h1><p>逐卡播放 manifest 声明的真实运行时 PNG 横向帧带。窗口严格采用 frameWidth × frameHeight，overflow 裁切后以 steps(frames) 推进；母图与概念图不会进入本页。</p><div class="summary"><span><strong>__TOTAL__</strong> 个单位</span><span><strong>__COMBAT__</strong> 战斗</span><span><strong>__MISSION__</strong> 任务</span><span><strong>__READY__</strong> ready</span><span><strong>__MANIFESTS__</strong> 份 manifest</span></div></header>
  <div class="toolbar-shell"><div class="toolbar">
    <select id="campaign" aria-label="剧本筛选"><option value="all">全部剧本</option>__CAMPAIGNS__</select>
    <select id="batch" aria-label="批次筛选"><option value="all">全部批次</option>__BATCHES__</select>
    <select id="category" aria-label="单位类别"><option value="all">战斗 + 任务</option><option value="combat-unit">战斗单位</option><option value="mission-unit">任务单位</option></select>
    <select id="status" aria-label="验收状态"><option value="all">全部状态</option><option value="ready">ready</option><option value="candidate">candidate</option></select>
    <select id="scale" aria-label="显示倍率"><option value="1">1× 原尺寸</option><option value="2">2×</option><option value="3">3×</option></select>
    <select id="facing" aria-label="朝向"><option value="right">朝右</option><option value="left">朝左镜像</option></select>
    <button id="pause" type="button" aria-pressed="false">暂停动画</button>
    <input id="search" type="search" placeholder="搜索名称、contentId、topicId">
    <output id="count">显示 __TOTAL__ / __TOTAL__</output>
  </div></div>
  <main><div class="grid">__CARDS__</div><p class="empty" id="empty">没有符合当前筛选的单位。</p></main>
  <script>
  (() => {
    const cards = [...document.querySelectorAll('[data-card]')];
    const campaign = document.querySelector('#campaign');
    const batch = document.querySelector('#batch');
    const category = document.querySelector('#category');
    const status = document.querySelector('#status');
    const scale = document.querySelector('#scale');
    const facing = document.querySelector('#facing');
    const pause = document.querySelector('#pause');
    const search = document.querySelector('#search');
    const count = document.querySelector('#count');
    const empty = document.querySelector('#empty');

    function applyFilters() {
      const term = search.value.trim().toLocaleLowerCase('zh-CN');
      let visible = 0;
      for (const card of cards) {
        const show = (campaign.value === 'all' || card.dataset.campaign === campaign.value)
          && (batch.value === 'all' || card.dataset.batch === batch.value)
          && (category.value === 'all' || card.dataset.category === category.value)
          && (status.value === 'all' || card.dataset.status === status.value)
          && (!term || card.dataset.search.includes(term));
        card.hidden = !show;
        if (show) visible += 1;
      }
      count.value = `显示 ${visible} / ${cards.length}`;
      empty.style.display = visible ? 'none' : 'block';
    }

    function applyScale() {
      const factor = Number(scale.value);
      for (const card of cards) {
        const frameWidth = Number(card.dataset.frameWidth);
        const frameHeight = Number(card.dataset.frameHeight);
        const sheetWidth = Number(card.dataset.sheetWidth);
        const sheetHeight = Number(card.dataset.sheetHeight);
        const windowElement = card.querySelector('.sprite-window');
        const strip = card.querySelector('.sprite-strip');
        windowElement.style.width = `${frameWidth * factor}px`;
        windowElement.style.height = `${frameHeight * factor}px`;
        strip.style.width = `${sheetWidth * factor}px`;
        strip.style.height = `${sheetHeight * factor}px`;
        strip.style.setProperty('--travel', `${-sheetWidth * factor}px`);
      }
    }

    [campaign, batch, category, status].forEach((control) => control.addEventListener('change', applyFilters));
    search.addEventListener('input', applyFilters);
    scale.addEventListener('change', applyScale);
    facing.addEventListener('change', () => { document.body.dataset.facing = facing.value; });
    pause.addEventListener('click', () => {
      const paused = document.body.classList.toggle('paused');
      pause.setAttribute('aria-pressed', String(paused));
      pause.textContent = paused ? '继续动画' : '暂停动画';
    });
    applyScale();
    applyFilters();
  })();
  </script>
</body>
</html>
'''
    replacements = {
        "__TOTAL__": str(len(units)),
        "__COMBAT__": str(combat_count),
        "__MISSION__": str(mission_count),
        "__READY__": str(ready_count),
        "__MANIFESTS__": str(len(manifests)),
        "__CAMPAIGNS__": campaign_options,
        "__BATCHES__": batch_options,
        "__CARDS__": cards,
    }
    for marker, value in replacements.items():
        document = document.replace(marker, value)
    return document


def main() -> None:
    args = parse_args()
    units, manifests = load_units()
    if not args.check_only:
        args.output.write_text(render_lab(units, manifests), encoding="utf-8")
    counts = Counter(unit.category for unit in units)
    print(
        json.dumps(
            {
                "campaigns": len({unit.campaign_id for unit in units}),
                "manifests": len(manifests),
                "units": len(units),
                "combatUnits": counts["combat-unit"],
                "missionUnits": counts["mission-unit"],
                "runtimeReadyUnits": sum(unit.runtime_ready for unit in units),
                "candidateUnits": sum(not unit.runtime_ready for unit in units),
                "pngFilesChecked": len(units),
                "output": None if args.check_only else str(args.output),
                "passed": True,
            },
            ensure_ascii=False,
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
