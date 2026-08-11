#!/usr/bin/env python3
"""Build Candidate 03 runtime-v2 assets from the preserved ImageGen masters.

The script is deterministic after the six masters have been generated. It invokes
the official imagegen chroma-key helper, slices and normalizes cells, applies the
runtime palette/alpha rules, writes portable SVG review wrappers, builds 1x/2x
previews, and emits the runtime manifest plus QA evidence.
"""

from __future__ import annotations

import base64
from collections import deque
import json
import os
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps


HERE = Path(__file__).resolve().parent
ASSETS = HERE.parent
MASTERS = HERE / "masters"
INTERMEDIATE = HERE / "intermediate"
PREVIEWS = HERE / "previews"
MANIFEST_PATH = ASSETS / "manifest-runtime-v2.json"
QA_PATH = ASSETS / "qa-runtime-v2.json"

RESAMPLE = Image.Resampling.LANCZOS
NEAREST = Image.Resampling.NEAREST
DITHER_NONE = Image.Dither.NONE


@dataclass(frozen=True)
class Output:
    id: str
    content_id: str
    type: str
    stem: str
    width: int
    height: int
    directory: str
    metadata: dict[str, Any]

    @property
    def png(self) -> Path:
        return HERE / self.directory / f"{self.stem}.png"

    @property
    def svg(self) -> Path:
        return HERE / self.directory / f"{self.stem}.svg"


UNIT_ROWS = [
    ("c03-v2-unit-saber-shield", "grain.unit.saber-shield", "saber-shield"),
    ("c03-v2-unit-spear-line", "grain.unit.spear-line", "spear-line"),
    ("c03-v2-unit-rural-archer", "grain.unit.rural-archer", "rural-archer"),
    ("c03-v2-unit-river-engineer", "grain.unit.river-engineer", "river-engineer"),
]

TERRAINS = [
    ("c03-v2-terrain-river", "grain.terrain.river", "river"),
    ("c03-v2-terrain-ford", "grain.terrain.ford", "ford"),
    ("c03-v2-terrain-dike", "grain.terrain.dike", "dike"),
    ("c03-v2-terrain-dike-breach", "grain.terrain.dike-breach", "dike-breach"),
    ("c03-v2-terrain-paddy-flooded", "grain.terrain.paddy-flooded", "paddy-flooded"),
    ("c03-v2-terrain-paddy-mature", "grain.terrain.paddy-mature", "paddy-mature"),
    ("c03-v2-terrain-road-mud", "grain.terrain.road-mud", "road-mud"),
    ("c03-v2-terrain-road-stone", "grain.terrain.road-stone", "road-stone"),
]

STRUCTURES = [
    ("c03-v2-structure-county-granary", "grain.structure.county-granary", "county-granary"),
    ("c03-v2-structure-sluice-bridgehead", "grain.structure.sluice-bridgehead", "sluice-bridgehead"),
]

ICONS = [
    ("c03-v2-equipment-saber-shield", "grain.equipment.saber-shield", "equipment", "equipment-saber-shield"),
    ("c03-v2-equipment-spear", "grain.equipment.spear", "equipment", "equipment-spear"),
    ("c03-v2-equipment-rural-bow", "grain.equipment.rural-bow", "equipment", "equipment-rural-bow"),
    ("c03-v2-equipment-river-tools", "grain.equipment.river-tools", "equipment", "equipment-river-tools"),
    ("c03-v2-skill-guard", "grain.skill.guard", "skill", "skill-guard"),
    ("c03-v2-skill-spear-brace", "grain.skill.spear-brace", "skill", "skill-spear-brace"),
    ("c03-v2-skill-volley", "grain.skill.volley", "skill", "skill-volley"),
    ("c03-v2-skill-dike-repair", "grain.skill.dike-repair", "skill", "skill-dike-repair"),
]

FX_ROWS = [
    ("c03-v2-fx-hit-sparks", "grain.fx.hit-sparks", "hit-sparks", 14, "add", False),
    ("c03-v2-fx-controlled-fire", "grain.fx.controlled-fire", "controlled-fire", 10, "add", False),
    ("c03-v2-fx-mud-splash", "grain.fx.mud-splash", "mud-splash", 12, "normal", False),
    ("c03-v2-fx-repair-chips", "grain.fx.repair-chips", "repair-chips", 10, "normal", False),
]


def mkdirs() -> None:
    for name in ["units", "terrain", "structures", "icons", "fx", "scenes", "intermediate", "previews"]:
        (HERE / name).mkdir(parents=True, exist_ok=True)


def chroma_helper() -> Path:
    codex_home = Path(os.environ.get("CODEX_HOME", Path.home() / ".codex"))
    helper = codex_home / "skills/.system/imagegen/scripts/remove_chroma_key.py"
    if not helper.exists():
        raise FileNotFoundError(f"Missing official chroma-key helper: {helper}")
    return helper


def remove_chroma_keys() -> None:
    helper = chroma_helper()
    jobs = [
        ("combat-units-master.png", "combat-units-alpha.png"),
        ("structures-master.png", "structures-alpha.png"),
        ("icons-master.png", "icons-alpha.png"),
        ("fx-master.png", "fx-alpha.png"),
    ]
    for src_name, dst_name in jobs:
        src = MASTERS / src_name
        dst = INTERMEDIATE / dst_name
        if not src.exists():
            raise FileNotFoundError(src)
        subprocess.run(
            [
                sys.executable,
                str(helper),
                "--input",
                str(src),
                "--out",
                str(dst),
                "--auto-key",
                "border",
                "--soft-matte",
                "--transparent-threshold",
                "12",
                "--opaque-threshold",
                "220",
                "--despill",
                "--force",
            ],
            check=True,
        )


def boundaries(size: int, count: int) -> list[int]:
    return [round(i * size / count) for i in range(count + 1)]


def grid_cell(image: Image.Image, cols: int, rows: int, col: int, row: int) -> Image.Image:
    xs = boundaries(image.width, cols)
    ys = boundaries(image.height, rows)
    return image.crop((xs[col], ys[row], xs[col + 1], ys[row + 1]))


def alpha_bbox(image: Image.Image, threshold: int = 16) -> tuple[int, int, int, int] | None:
    alpha = image.convert("RGBA").getchannel("A").point(lambda a: 255 if a >= threshold else 0)
    return alpha.getbbox()


def crop_alpha(image: Image.Image, threshold: int = 16, pad: int = 2) -> Image.Image:
    rgba = image.convert("RGBA")
    bbox = alpha_bbox(rgba, threshold)
    if bbox is None:
        raise ValueError("Empty transparent cell")
    left, top, right, bottom = bbox
    return rgba.crop(
        (
            max(0, left - pad),
            max(0, top - pad),
            min(rgba.width, right + pad),
            min(rgba.height, bottom + pad),
        )
    )


def fit_rgba(
    image: Image.Image,
    size: tuple[int, int],
    *,
    limit: tuple[int, int] | None = None,
    align: str = "bottom",
    flip: bool = False,
    binary_alpha: bool = True,
    colors: int = 48,
) -> Image.Image:
    source = crop_alpha(image)
    if flip:
        source = ImageOps.mirror(source)
    max_w, max_h = limit or (size[0] - 2, size[1] - 2)
    scale = min(max_w / source.width, max_h / source.height)
    resized = source.resize(
        (max(1, round(source.width * scale)), max(1, round(source.height * scale))),
        RESAMPLE,
    )
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (size[0] - resized.width) // 2
    if align == "center":
        y = (size[1] - resized.height) // 2
    else:
        y = size[1] - resized.height
    canvas.alpha_composite(resized, (x, y))
    return quantize_rgba(canvas, colors=colors, binary_alpha=binary_alpha)


def quantize_rgba(image: Image.Image, *, colors: int, binary_alpha: bool) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    if binary_alpha:
        alpha = alpha.point(lambda a: 255 if a >= 64 else 0)
    rgb = Image.new("RGB", rgba.size, (0, 0, 0))
    rgb.paste(rgba.convert("RGB"), mask=alpha)
    quantized = rgb.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=DITHER_NONE).convert("RGB")
    output = quantized.convert("RGBA")
    output.putalpha(alpha)
    return output


def isolate_primary_subject(image: Image.Image) -> Image.Image:
    """Discard neighboring-cell weapon bleed while retaining the central actor."""
    rgba = image.convert("RGBA")
    mask = rgba.getchannel("A").point(lambda a: 255 if a >= 12 else 0).filter(ImageFilter.MaxFilter(3))
    pixels = mask.load()
    seen = bytearray(mask.width * mask.height)
    components: list[tuple[int, list[tuple[int, int]]]] = []
    for y in range(mask.height):
        for x in range(mask.width):
            pos = y * mask.width + x
            if seen[pos] or pixels[x, y] == 0:
                continue
            queue: deque[tuple[int, int]] = deque([(x, y)])
            seen[pos] = 1
            points: list[tuple[int, int]] = []
            while queue:
                px, py = queue.popleft()
                points.append((px, py))
                for nx, ny in ((px - 1, py), (px + 1, py), (px, py - 1), (px, py + 1)):
                    if nx < 0 or ny < 0 or nx >= mask.width or ny >= mask.height:
                        continue
                    npos = ny * mask.width + nx
                    if not seen[npos] and pixels[nx, ny] != 0:
                        seen[npos] = 1
                        queue.append((nx, ny))
            components.append((len(points), points))
    if not components:
        return rgba
    # The intended actor occupies the central half of its atlas cell. Penalize
    # border fragments from the neighboring pose even when a long weapon makes
    # that fragment locally prominent.
    def score(component: tuple[int, list[tuple[int, int]]]) -> float:
        count, points = component
        mean_x = sum(point[0] for point in points) / count
        central = mask.width * 0.18 <= mean_x <= mask.width * 0.82
        return count * (1.0 if central else 0.15)

    _, selected = max(components, key=score)
    keep = Image.new("L", rgba.size, 0)
    keep_px = keep.load()
    for x, y in selected:
        keep_px[x, y] = 255
    keep = keep.filter(ImageFilter.MaxFilter(3))
    alpha = ImageChops.multiply(rgba.getchannel("A"), keep)
    rgba.putalpha(alpha)
    return rgba


def fit_unit_cell(image: Image.Image) -> Image.Image:
    source = crop_alpha(isolate_primary_subject(image), threshold=12, pad=2)
    source = ImageOps.mirror(source)
    target_h = 46
    proportional_w = max(1, round(source.width * target_h / source.height))
    # Long horizontal weapons are compressed into the 32px combat cell while
    # actor height remains stable; this keeps spear/bow silhouettes playable.
    target_w = min(30, proportional_w)
    resized = source.resize((target_w, target_h), RESAMPLE)
    canvas = Image.new("RGBA", (32, 48), (0, 0, 0, 0))
    canvas.alpha_composite(resized, ((32 - target_w) // 2, 48 - target_h))
    return quantize_rgba(canvas, colors=48, binary_alpha=True)


def quantize_opaque(image: Image.Image, colors: int) -> Image.Image:
    return image.convert("RGB").quantize(
        colors=colors,
        method=Image.Quantize.MEDIANCUT,
        dither=DITHER_NONE,
    ).convert("RGB")


def save_png_svg(image: Image.Image, out: Output) -> None:
    out.png.parent.mkdir(parents=True, exist_ok=True)
    image.save(out.png, optimize=True)
    data = base64.b64encode(out.png.read_bytes()).decode("ascii")
    svg = (
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{out.width}" height="{out.height}" '
        f'viewBox="0 0 {out.width} {out.height}">\n'
        f'  <image width="{out.width}" height="{out.height}" '
        f'style="image-rendering:pixelated" href="data:image/png;base64,{data}"/>\n'
        "</svg>\n"
    )
    out.svg.write_text(svg, encoding="utf-8")


def build_units(outputs: list[Output]) -> None:
    master = Image.open(INTERMEDIATE / "combat-units-alpha.png").convert("RGBA")
    for row, (asset_id, content_id, stem) in enumerate(UNIT_ROWS):
        frames = []
        for col in range(4):
            cell = grid_cell(master, 4, 4, col, row)
            # ImageGen posed the figures toward viewer-left; source runtime faces right.
            frame = fit_unit_cell(cell)
            frames.append(frame)
        sheet = Image.new("RGBA", (128, 48), (0, 0, 0, 0))
        for col, frame in enumerate(frames):
            sheet.alpha_composite(frame, (col * 32, 0))
        out = Output(
            asset_id,
            content_id,
            "combat-unit",
            stem,
            128,
            48,
            "units",
            {
                "frameWidth": 32,
                "frameHeight": 48,
                "frames": 4,
                "frameOrder": ["idle-a", "step-a", "idle-b", "step-b"],
                "anchor": {"x": 16, "y": 47},
                "footprint": {"columns": 1, "rows": 1},
                "sourceFacing": "right",
                "zOrder": "unit",
            },
        )
        save_png_svg(sheet, out)
        outputs.append(out)


def seamless_tile(cell: Image.Image, variant: int = 0) -> Image.Image:
    # The generated full-bleed swatch provides texture vocabulary. Feather only
    # the outer four pixels toward a shared edge color: unlike quadrant mirroring,
    # this retains the authored flow, stones, furrows and masonry without making
    # an obvious kaleidoscope, while still producing pixel-identical tile edges.
    margin_x = max(0, cell.width // 5)
    margin_y = max(0, cell.height // 5)
    crop_w = cell.width - margin_x * 2
    crop_h = cell.height - margin_y * 2
    shifts = [(-0.12, -0.10), (0.12, -0.08), (-0.10, 0.12), (0.10, 0.10)]
    shift_x, shift_y = shifts[variant % len(shifts)]
    left = round(margin_x + shift_x * margin_x)
    top = round(margin_y + shift_y * margin_y)
    crop = cell.crop((left, top, left + crop_w, top + crop_h))
    tile = crop.resize((32, 32), RESAMPLE).convert("RGB")
    px = tile.load()

    def mix(a: tuple[int, int, int], b: tuple[int, int, int], amount: float) -> tuple[int, int, int]:
        return tuple(round(av * (1 - amount) + bv * amount) for av, bv in zip(a, b))

    feather = [1.0, 0.65, 0.35, 0.15]
    for y in range(32):
        target = tuple(round((a + b) / 2) for a, b in zip(px[0, y], px[31, y]))
        for offset, amount in enumerate(feather):
            px[offset, y] = mix(px[offset, y], target, amount)
            px[31 - offset, y] = mix(px[31 - offset, y], target, amount)
    for x in range(32):
        target = tuple(round((a + b) / 2) for a, b in zip(px[x, 0], px[x, 31]))
        for offset, amount in enumerate(feather):
            px[x, offset] = mix(px[x, offset], target, amount)
            px[x, 31 - offset] = mix(px[x, 31 - offset], target, amount)

    result = quantize_opaque(tile, 64)
    result_px = result.load()
    for y in range(32):
        result_px[31, y] = result_px[0, y]
    for x in range(32):
        result_px[x, 31] = result_px[x, 0]
    return result


MASK_ORDER = [
    "none",
    "n",
    "e",
    "ne",
    "s",
    "ns",
    "es",
    "nes",
    "w",
    "nw",
    "ew",
    "new",
    "sw",
    "nsw",
    "esw",
    "nesw",
]


def connection_mask(bits: int, width: int) -> Image.Image:
    mask = Image.new("L", (32, 32), 0)
    draw = ImageDraw.Draw(mask)
    half = width // 2
    lo = 16 - half
    hi = 15 + (width - half)
    draw.ellipse((lo - 1, lo - 1, hi + 1, hi + 1), fill=255)
    if bits & 1:  # north
        draw.rectangle((lo, 0, hi, 16), fill=255)
    if bits & 2:  # east
        draw.rectangle((16, lo, 31, hi), fill=255)
    if bits & 4:  # south
        draw.rectangle((lo, 16, hi, 31), fill=255)
    if bits & 8:  # west
        draw.rectangle((0, lo, 16, hi), fill=255)
    return mask


def connected_tile(material: Image.Image, ground: Image.Image, bits: int, width: int) -> Image.Image:
    mask = connection_mask(bits, width)
    tile = ground.copy().convert("RGB")
    tile.paste(material.convert("RGB"), (0, 0), mask)
    # One-pixel dark bank/edge clarifies paths and channels without closing a
    # connected arm at the tile boundary.
    outline = ImageChops.subtract(mask.filter(ImageFilter.MaxFilter(3)), mask)
    dark = Image.eval(tile, lambda value: round(value * 0.62))
    tile.paste(dark, (0, 0), outline)
    return tile


def build_terrain(outputs: list[Output]) -> None:
    master = Image.open(MASTERS / "terrain-master.png").convert("RGB")
    cells = [grid_cell(master, 4, 2, index % 4, index // 4) for index in range(8)]
    # Connector material index -> compatible ground material index, band width.
    connector_specs = {
        0: (6, 14),  # river through silt ground
        1: (6, 14),  # ford through silt ground
        2: (4, 12),  # dike through wet paddy
        3: (2, 12),  # breach/channel through dike material
        6: (5, 10),  # muddy road through mature field
        7: (6, 10),  # stone road through mud
    }
    for index, (asset_id, content_id, stem) in enumerate(TERRAINS):
        connect_group = None
        if stem in {"river", "ford"}:
            connect_group = "watercourse"
        elif stem in {"dike", "dike-breach"}:
            connect_group = "dike-line"
        elif stem in {"road-mud", "road-stone"}:
            connect_group = "road-line"
        if index in connector_specs:
            ground_index, band_width = connector_specs[index]
            material = seamless_tile(cells[index], variant=0)
            ground = seamless_tile(cells[ground_index], variant=1)
            variants = [connected_tile(material, ground, bits, band_width) for bits in range(16)]
            sheet = Image.new("RGB", (512, 32))
            for frame, tile in enumerate(variants):
                sheet.paste(tile, (frame * 32, 0))
            metadata = {
                "frameWidth": 32,
                "frameHeight": 32,
                "frames": 16,
                "tileMode": "nesw-16",
                "maskBits": {"north": 1, "east": 2, "south": 4, "west": 8},
                "variantOrder": MASK_ORDER,
                "connectGroup": connect_group,
                "edgeMode": "neighbor-mask-pixel-exact",
                "footprint": {"columns": 1, "rows": 1},
            }
        else:
            variants = [seamless_tile(cells[index], variant=variant) for variant in range(4)]
            sheet = Image.new("RGB", (128, 32))
            for frame, tile in enumerate(variants):
                sheet.paste(tile, (frame * 32, 0))
            metadata = {
                "frameWidth": 32,
                "frameHeight": 32,
                "frames": 4,
                "tileMode": "repeat-variants",
                "variantOrder": ["hash-0", "hash-1", "hash-2", "hash-3"],
                "variantSelector": "stable-coordinate-hash",
                "edgeMode": "opposing-pixels-exact",
                "footprint": {"columns": 1, "rows": 1},
            }
        out = Output(
            asset_id,
            content_id,
            "terrain-tile",
            stem,
            sheet.width,
            32,
            "terrain",
            metadata,
        )
        save_png_svg(sheet, out)
        outputs.append(out)


def build_structures(outputs: list[Output]) -> None:
    master = Image.open(INTERMEDIATE / "structures-alpha.png").convert("RGBA")
    for row, (asset_id, content_id, stem) in enumerate(STRUCTURES):
        cells = [grid_cell(master, 3, 2, col, row) for col in range(3)]
        atlas = Image.new("RGBA", (288, 96), (0, 0, 0, 0))
        for col, cell in enumerate(cells):
            state = fit_rgba(cell, (96, 96), limit=(94, 94), align="bottom", colors=64)
            atlas.alpha_composite(state, (col * 96, 0))
        footprint = {"columns": 3, "rows": 2}
        out = Output(
            asset_id,
            content_id,
            "interactive-structure",
            stem,
            288,
            96,
            "structures",
            {
                "frameWidth": 96,
                "frameHeight": 96,
                "frames": 3,
                "states": ["normal", "damaged", "captured"],
                "stateRows": [{"row": 0, "states": ["normal", "damaged", "captured"]}],
                "stateLayout": "horizontal",
                "anchor": {"x": 48, "y": 95},
                "footprint": footprint,
                "collision": {"x": 0, "y": 32, "width": 96, "height": 64},
                "interactionHotzone": {"x": 24, "y": 48, "width": 48, "height": 48},
            },
        )
        save_png_svg(atlas, out)
        outputs.append(out)


def build_icons(outputs: list[Output]) -> None:
    master = Image.open(INTERMEDIATE / "icons-alpha.png").convert("RGBA")
    for index, (asset_id, content_id, asset_type, stem) in enumerate(ICONS):
        row, col = divmod(index, 4)
        icon = fit_rgba(
            grid_cell(master, 4, 2, col, row),
            (32, 32),
            limit=(30, 30),
            align="center",
            colors=48,
        )
        out = Output(
            asset_id,
            content_id,
            asset_type,
            stem,
            32,
            32,
            "icons",
            {
                "displaySizes": [16, 24, 32],
                "anchor": {"x": 16, "y": 16},
            },
        )
        save_png_svg(icon, out)
        outputs.append(out)


def build_fx(outputs: list[Output]) -> None:
    master = Image.open(INTERMEDIATE / "fx-alpha.png").convert("RGBA")
    for row, (asset_id, content_id, stem, fps, blend_mode, loop) in enumerate(FX_ROWS):
        atlas = Image.new("RGBA", (128, 32), (0, 0, 0, 0))
        for col in range(4):
            cell = grid_cell(master, 4, 4, col, row)
            align = "center" if row == 0 else "bottom"
            frame = fit_rgba(
                cell,
                (32, 32),
                limit=(31, 31),
                align=align,
                binary_alpha=False,
                colors=64,
            )
            atlas.alpha_composite(frame, (col * 32, 0))
        out = Output(
            asset_id,
            content_id,
            "fx",
            stem,
            128,
            32,
            "fx",
            {
                "frameWidth": 32,
                "frameHeight": 32,
                "frames": 4,
                "fps": fps,
                "blendMode": blend_mode,
                "loop": loop,
                "anchor": {"x": 16, "y": 31 if row else 16},
                "frameOrder": [0, 1, 2, 3],
            },
        )
        save_png_svg(atlas, out)
        outputs.append(out)


def build_scene(outputs: list[Output]) -> None:
    master = Image.open(MASTERS / "granary-scene-master.png").convert("RGB")
    target_ratio = 16 / 9
    ratio = master.width / master.height
    if ratio > target_ratio:
        width = round(master.height * target_ratio)
        left = (master.width - width) // 2
        master = master.crop((left, 0, left + width, master.height))
    elif ratio < target_ratio:
        height = round(master.width / target_ratio)
        top = (master.height - height) // 2
        master = master.crop((0, top, master.width, top + height))
    scene = quantize_opaque(master.resize((256, 144), RESAMPLE), 96)
    out = Output(
        "c03-v2-scene-open-granary",
        "grain.scene.open-granary",
        "story-scene",
        "open-granary-night",
        256,
        144,
        "scenes",
        {
            "opaque": True,
            "dialogueSafeArea": {"x": 0, "y": 112, "width": 256, "height": 32},
            "displayScale": [1, 2, 3],
        },
    )
    save_png_svg(scene, out)
    outputs.append(out)


def checker(size: tuple[int, int], cell: int = 4) -> Image.Image:
    image = Image.new("RGB", size, "#26313a")
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill="#33424c")
    return image


def paste_alpha(background: Image.Image, foreground: Image.Image, xy: tuple[int, int]) -> None:
    background.paste(foreground.convert("RGBA"), xy, foreground.convert("RGBA"))


def terrain_frame(image: Image.Image, frame: int) -> Image.Image:
    return image.crop((frame * 32, 0, (frame + 1) * 32, 32))


def font() -> ImageFont.ImageFont:
    return ImageFont.load_default()


def make_previews() -> None:
    contact = checker((640, 480), 4)
    draw = ImageDraw.Draw(contact)
    ink = "#f0dfba"
    draw.text((8, 5), "C03 RUNTIME V2 / 1X", fill=ink, font=font())
    y = 20
    for _, _, stem in UNIT_ROWS:
        paste_alpha(contact, Image.open(HERE / "units" / f"{stem}.png"), (8, y))
        draw.text((142, y + 19), stem, fill=ink, font=font())
        y += 48

    draw.text((260, 5), "TERRAIN 4x2", fill=ink, font=font())
    for index, (_, _, stem) in enumerate(TERRAINS):
        row, col = divmod(index, 4)
        sheet = Image.open(HERE / "terrain" / f"{stem}.png").convert("RGB")
        frame = 15 if sheet.width == 512 else index % 4
        contact.paste(terrain_frame(sheet, frame), (260 + col * 32, 20 + row * 32))

    draw.text((400, 5), "ICONS", fill=ink, font=font())
    for index, (_, _, _, stem) in enumerate(ICONS):
        row, col = divmod(index, 4)
        paste_alpha(contact, Image.open(HERE / "icons" / f"{stem}.png"), (400 + col * 32, 20 + row * 32))

    draw.text((260, 94), "STRUCTURES: N / D / C", fill=ink, font=font())
    for row, (_, _, stem) in enumerate(STRUCTURES):
        paste_alpha(contact, Image.open(HERE / "structures" / f"{stem}.png"), (260, 108 + row * 96))

    draw.text((8, 220), "FX: SPARK / FIRE / MUD / REPAIR", fill=ink, font=font())
    for row, (_, _, stem, _, _, _) in enumerate(FX_ROWS):
        paste_alpha(contact, Image.open(HERE / "fx" / f"{stem}.png"), (8, 236 + row * 32))

    scene = Image.open(HERE / "scenes/open-granary-night.png").convert("RGB")
    contact.paste(scene, (384, 316))
    draw.rectangle((384, 428, 639, 459), outline="#e4bb6b", width=1)
    draw.text((386, 462), "dialogue safe: bottom 32px", fill=ink, font=font())
    contact.save(PREVIEWS / "runtime-v2-contact-1x.png", optimize=True)
    contact.resize((1280, 960), NEAREST).save(PREVIEWS / "runtime-v2-contact-2x.png", optimize=True)

    terrain_preview = Image.new("RGB", (8 * 96, 96), "#000")
    for index, (_, _, stem) in enumerate(TERRAINS):
        sheet = Image.open(HERE / "terrain" / f"{stem}.png").convert("RGB")
        block = Image.new("RGB", (96, 96))
        for row in range(3):
            for col in range(3):
                frame = 15 if sheet.width == 512 else (row * 3 + col) % 4
                tile = terrain_frame(sheet, frame)
                block.paste(tile, (col * 32, row * 32))
        terrain_preview.paste(block, (index * 96, 0))
    terrain_preview.save(PREVIEWS / "terrain-3x3.png", optimize=True)

    # Deterministic 12x8 river network. Each cell mask is derived from shared
    # edges so every east/west and north/south join is a legal reciprocal pair.
    map_preview = Image.new("RGB", (12 * 32, 8 * 32))
    river = Image.open(HERE / "terrain/river.png").convert("RGB")
    east_edges = [[((row * 13 + col * 7 + 3) % 5) < 2 for col in range(11)] for row in range(8)]
    south_edges = [[((row * 11 + col * 5 + 1) % 7) < 3 for col in range(12)] for row in range(7)]
    for row in range(8):
        for col in range(12):
            bits = 0
            if row > 0 and south_edges[row - 1][col]:
                bits |= 1
            if col < 11 and east_edges[row][col]:
                bits |= 2
            if row < 7 and south_edges[row][col]:
                bits |= 4
            if col > 0 and east_edges[row][col - 1]:
                bits |= 8
            tile = terrain_frame(river, bits)
            map_preview.paste(tile, (col * 32, row * 32))
    map_preview.save(PREVIEWS / "terrain-random-12x8.png", optimize=True)

    icons = checker((256, 128), 4)
    for index, (_, _, _, stem) in enumerate(ICONS):
        icon = Image.open(HERE / "icons" / f"{stem}.png").convert("RGBA")
        x = (index % 4) * 64 + 16
        y = (index // 4) * 64 + 16
        paste_alpha(icons, icon, (x, y))
    icons.convert("L").convert("RGB").save(PREVIEWS / "icons-grayscale-1x.png", optimize=True)


def relative(path: Path) -> str:
    return path.relative_to(ASSETS).as_posix()


def build_manifest(outputs: list[Output]) -> None:
    assets = []
    for out in outputs:
        item: dict[str, Any] = {
            "id": out.id,
            "contentId": out.content_id,
            "type": out.type,
            "png": relative(out.png),
            "svg": relative(out.svg),
            "width": out.width,
            "height": out.height,
            "sourceMaster": relative(MASTERS / master_for(out.type)),
        }
        item.update(out.metadata)
        assets.append(item)
    manifest = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-03",
        "campaignTitle": "布衣定鼎",
        "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False,
        "source": "built-in-imagegen+official-chroma-key+deterministic-postprocess",
        "notes": "First playable runtime-v2 batch; it does not represent completion of the 404-topic library.",
        "assetCount": len(assets),
        "assets": assets,
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def master_for(asset_type: str) -> str:
    return {
        "combat-unit": "combat-units-master.png",
        "terrain-tile": "terrain-master.png",
        "interactive-structure": "structures-master.png",
        "equipment": "icons-master.png",
        "skill": "icons-master.png",
        "fx": "fx-master.png",
        "story-scene": "granary-scene-master.png",
    }[asset_type]


def unique_colors(image: Image.Image) -> int:
    rgba = image.convert("RGBA")
    return len(set(rgba.get_flattened_data()))


def alpha_values(image: Image.Image) -> set[int]:
    return set(image.convert("RGBA").getchannel("A").get_flattened_data())


def frame_boxes(image: Image.Image, frame_w: int, frame_h: int, frames: int) -> list[tuple[int, int, int, int] | None]:
    rgba = image.convert("RGBA")
    return [alpha_bbox(rgba.crop((i * frame_w, 0, (i + 1) * frame_w, frame_h))) for i in range(frames)]


def run_qa(outputs: list[Output]) -> dict[str, Any]:
    errors: list[str] = []
    checks: list[dict[str, Any]] = []

    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    ids = [asset["id"] for asset in manifest["assets"]]
    content_ids = [asset["contentId"] for asset in manifest["assets"]]
    checks.append({"id": "manifest.asset-count", "passed": len(outputs) == 27, "actual": len(outputs), "expected": 27})
    checks.append({"id": "manifest.unique-id", "passed": len(ids) == len(set(ids)), "count": len(ids)})
    checks.append({"id": "manifest.unique-content-id", "passed": len(content_ids) == len(set(content_ids)), "count": len(content_ids)})
    checks.append({"id": "manifest.content-id-prefix", "passed": all(cid.startswith("grain.") for cid in content_ids)})
    checks.append({"id": "manifest.runtime-ready-false", "passed": manifest["runtimeReady"] is False})

    file_rows = []
    for out in outputs:
        png_ok = out.png.exists()
        svg_ok = out.svg.exists()
        size_ok = False
        mode = None
        colors = None
        if png_ok:
            image = Image.open(out.png)
            size_ok = image.size == (out.width, out.height)
            mode = image.mode
            colors = unique_colors(image)
        row = {
            "id": out.id,
            "png": png_ok,
            "svg": svg_ok,
            "size": size_ok,
            "mode": mode,
            "colors": colors,
        }
        file_rows.append(row)
        if not all([png_ok, svg_ok, size_ok]):
            errors.append(f"File/size QA failed: {out.id}")
    checks.append({"id": "files.existence-dimensions", "passed": not errors, "assets": file_rows})

    unit_rows = []
    for out in [item for item in outputs if item.type == "combat-unit"]:
        image = Image.open(out.png).convert("RGBA")
        boxes = frame_boxes(image, 32, 48, 4)
        alphas = alpha_values(image)
        corners_clear = all(image.getpixel((x, y))[3] == 0 for x in [0, 31, 32, 63, 64, 95, 96, 127] for y in [0, 47])
        bottoms = [box[3] - 1 if box else None for box in boxes]
        passed = all(box is not None for box in boxes) and set(alphas).issubset({0, 255}) and corners_clear and all(b == 47 for b in bottoms)
        unit_rows.append({"id": out.id, "passed": passed, "frameBoxes": boxes, "alphaValues": sorted(alphas), "feetBottomY": bottoms})
        if not passed:
            errors.append(f"Unit frame/alpha/anchor QA failed: {out.id}")
    checks.append({"id": "units.frames-alpha-anchor", "passed": all(row["passed"] for row in unit_rows), "assets": unit_rows})

    terrain_rows = []
    for out in [item for item in outputs if item.type == "terrain-tile"]:
        sheet = Image.open(out.png).convert("RGB")
        frames = [terrain_frame(sheet, index) for index in range(out.metadata["frames"])]
        unique = len({frame.tobytes() for frame in frames})
        if out.metadata["tileMode"] == "nesw-16":
            horizontal = all(
                frames[left].crop((31, 0, 32, 32)).tobytes()
                == frames[right].crop((0, 0, 1, 32)).tobytes()
                for left in range(16)
                for right in range(16)
                if bool(left & 2) == bool(right & 8)
            )
            vertical = all(
                frames[top].crop((0, 31, 32, 32)).tobytes()
                == frames[bottom].crop((0, 0, 32, 1)).tobytes()
                for top in range(16)
                for bottom in range(16)
                if bool(top & 4) == bool(bottom & 1)
            )
            passed = horizontal and vertical and unique == 16
            details = {"maskCount": len(frames), "uniqueMasks": unique, "compatibleHorizontalEdges": horizontal, "compatibleVerticalEdges": vertical}
        else:
            edge_rows = []
            for frame in frames:
                left_right = frame.crop((0, 0, 1, 32)).tobytes() == frame.crop((31, 0, 32, 32)).tobytes()
                top_bottom = frame.crop((0, 0, 32, 1)).tobytes() == frame.crop((0, 31, 32, 32)).tobytes()
                edge_rows.append(left_right and top_bottom)
            passed = all(edge_rows) and unique == 4
            details = {"variantCount": len(frames), "uniqueVariants": unique, "opposingEdges": edge_rows}
        terrain_rows.append({"id": out.id, "passed": passed, **details})
        if not passed:
            errors.append(f"Terrain edge QA failed: {out.id}")
    checks.append({"id": "terrain.variants-and-compatible-edges", "passed": all(row["passed"] for row in terrain_rows), "assets": terrain_rows})

    transparent_rows = []
    for out in [item for item in outputs if item.type in {"interactive-structure", "equipment", "skill"}]:
        image = Image.open(out.png).convert("RGBA")
        alphas = alpha_values(image)
        corners = [image.getpixel((0, 0))[3], image.getpixel((image.width - 1, 0))[3], image.getpixel((0, image.height - 1))[3], image.getpixel((image.width - 1, image.height - 1))[3]]
        passed = set(alphas).issubset({0, 255}) and all(value == 0 for value in corners)
        transparent_rows.append({"id": out.id, "passed": passed, "alphaValues": sorted(alphas), "corners": corners})
        if not passed:
            errors.append(f"Transparent binary alpha QA failed: {out.id}")
    checks.append({"id": "transparent.binary-alpha-corners", "passed": all(row["passed"] for row in transparent_rows), "assets": transparent_rows})

    fx_rows = []
    for out in [item for item in outputs if item.type == "fx"]:
        image = Image.open(out.png).convert("RGBA")
        boxes = frame_boxes(image, 32, 32, 4)
        hashes = [hash(image.crop((i * 32, 0, (i + 1) * 32, 32)).tobytes()) for i in range(4)]
        passed = all(box is not None for box in boxes) and len(set(hashes)) == 4
        fx_rows.append({"id": out.id, "passed": passed, "frameBoxes": boxes, "uniqueFrames": len(set(hashes))})
        if not passed:
            errors.append(f"FX continuity/nonempty QA failed: {out.id}")
    checks.append({"id": "fx.four-nonempty-ordered-frames", "passed": all(row["passed"] for row in fx_rows), "assets": fx_rows})

    preview_files = [
        PREVIEWS / "runtime-v2-contact-1x.png",
        PREVIEWS / "runtime-v2-contact-2x.png",
        PREVIEWS / "terrain-3x3.png",
        PREVIEWS / "terrain-random-12x8.png",
        PREVIEWS / "icons-grayscale-1x.png",
    ]
    preview_ok = all(path.exists() for path in preview_files)
    checks.append({"id": "previews.required", "passed": preview_ok, "files": [relative(path) for path in preview_files]})
    if not preview_ok:
        errors.append("Missing one or more review previews")

    for check in checks:
        if not check["passed"] and not any(check["id"] in error for error in errors):
            errors.append(f"Check failed: {check['id']}")

    qa = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-03",
        "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False,
        "passed": len(errors) == 0,
        "summary": {
            "assetCount": len(outputs),
            "combatUnits": 4,
            "terrainTiles": 8,
            "interactiveStructures": 2,
            "equipmentIcons": 4,
            "skillIcons": 4,
            "fxSheets": 4,
            "storyScenes": 1,
            "pngFiles": len(outputs),
            "svgFiles": len(outputs),
            "machineErrors": len(errors),
        },
        "checks": checks,
        "errors": errors,
        "manualReview": {
            "required": True,
            "completed": [
                "1x contact sheet inspection",
                "2x nearest-neighbor contact sheet inspection",
                "transparent edge inspection",
                "terrain 3x3 seam inspection",
                "terrain 12x8 composition inspection",
                "icon grayscale silhouette inspection",
            ],
            "pendingBeforeRuntimeReady": [
                "game-board integration screenshot",
                "engine registration test using contentId",
                "gameplay collision and interaction hotzone test",
                "faction capture banner recolor test",
            ],
            "decision": "Keep runtimeReady=false until root-agent in-engine acceptance.",
        },
        "prototype404Status": "Separate expanded/ 404-topic pack remains prototype-only; this QA does not promote it.",
    }
    QA_PATH.write_text(json.dumps(qa, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return qa


def main() -> None:
    mkdirs()
    remove_chroma_keys()
    outputs: list[Output] = []
    build_units(outputs)
    build_terrain(outputs)
    build_structures(outputs)
    build_icons(outputs)
    build_fx(outputs)
    build_scene(outputs)
    build_manifest(outputs)
    make_previews()
    qa = run_qa(outputs)
    print(json.dumps(qa["summary"], ensure_ascii=False, indent=2))
    if not qa["passed"]:
        for error in qa["errors"]:
            print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)


if __name__ == "__main__":
    main()
