#!/usr/bin/env python3
"""Build small runtime-spec samples from the approved Campaign 1 source boards."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent
INTERMEDIATE = ROOT / "intermediate"
RUNTIME = ROOT / "runtime"
PREVIEWS = ROOT / "previews"


def occupied_spans(alpha: Image.Image, axis: str) -> list[tuple[int, int]]:
    occupied: list[int] = []
    if axis == "x":
        for x in range(alpha.width):
            if alpha.crop((x, 0, x + 1, alpha.height)).getbbox():
                occupied.append(x)
    else:
        for y in range(alpha.height):
            if alpha.crop((0, y, alpha.width, y + 1)).getbbox():
                occupied.append(y)

    spans: list[tuple[int, int]] = []
    if not occupied:
        return spans
    start = previous = occupied[0]
    for value in occupied[1:]:
        if value > previous + 1:
            spans.append((start, previous + 1))
            start = value
        previous = value
    spans.append((start, previous + 1))
    return [span for span in spans if span[1] - span[0] > 20]


def alpha_crop(image: Image.Image) -> Image.Image:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("source cell has no visible pixels")
    return image.crop(bbox)


def fit_bottom(image: Image.Image, size: tuple[int, int], padding: int = 1) -> Image.Image:
    image = alpha_crop(image)
    max_w, max_h = size[0] - padding * 2, size[1] - padding * 2
    scale = min(max_w / image.width, max_h / image.height)
    resized = image.resize(
        (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", size)
    x = (size[0] - resized.width) // 2
    y = size[1] - padding - resized.height
    canvas.alpha_composite(resized, (x, y))
    return canvas


def center_square(image: Image.Image) -> Image.Image:
    side = min(image.width, image.height)
    left = (image.width - side) // 2
    top = (image.height - side) // 2
    return image.crop((left, top, left + side, top + side))


def split_row(path: Path, count: int) -> list[Image.Image]:
    image = Image.open(path).convert("RGBA")
    spans = occupied_spans(image.getchannel("A"), "x")
    if len(spans) != count:
        raise ValueError(f"{path.name}: expected {count} columns, found {spans}")
    return [image.crop((x0, 0, x1, image.height)) for x0, x1 in spans]


def split_grid(path: Path, columns: int, rows: int) -> list[Image.Image]:
    image = Image.open(path).convert("RGBA")
    alpha = image.getchannel("A")
    x_spans = occupied_spans(alpha, "x")
    y_spans = occupied_spans(alpha, "y")
    if len(x_spans) != columns or len(y_spans) != rows:
        raise ValueError(
            f"{path.name}: expected {columns}x{rows}, found {x_spans} / {y_spans}"
        )
    cells: list[Image.Image] = []
    for y0, y1 in y_spans:
        for x0, x1 in x_spans:
            cells.append(image.crop((x0, y0, x1, y1)))
    return cells


def checker(size: tuple[int, int], block: int = 8) -> Image.Image:
    image = Image.new("RGBA", size, "#394044")
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], block):
        for x in range(0, size[0], block):
            if (x // block + y // block) % 2:
                draw.rectangle((x, y, x + block - 1, y + block - 1), fill="#303639")
    return image


def paste_alpha(canvas: Image.Image, image: Image.Image, xy: tuple[int, int]) -> None:
    canvas.alpha_composite(image, xy)


def load_font(size: int) -> ImageFont.ImageFont:
    candidates = [
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
        "/System/Library/Fonts/Supplemental/Arial.ttf",
    ]
    for candidate in candidates:
        path = Path(candidate)
        if path.exists():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


def build_preview(
    unit: Image.Image,
    village: Image.Image,
    tower: Image.Image,
    terrain: Image.Image,
    icons: Image.Image,
) -> Image.Image:
    preview = Image.new("RGBA", (640, 360), "#171b1d")
    draw = ImageDraw.Draw(preview)
    title_font = load_font(14)
    label_font = load_font(11)
    draw.text((16, 12), "CAMPAIGN 1 · RUNTIME-SCALE SAMPLE", fill="#d9d2be", font=title_font)

    panels = [
        (12, 36, 268, 136),
        (280, 36, 628, 244),
        (12, 146, 268, 348),
        (280, 254, 628, 348),
    ]
    for box in panels:
        draw.rounded_rectangle(box, radius=5, fill="#242a2d", outline="#596064")

    draw.text((22, 44), "GRAY BANNER SPEARMAN · 4 × 32×48", fill="#bfc6c8", font=label_font)
    unit_bg = checker((136, 56), 8)
    paste_alpha(preview, unit_bg, (22, 64))
    paste_alpha(preview, unit, (26, 68))

    draw.text((290, 44), "BUILDINGS · 3 STATES · 64×64", fill="#bfc6c8", font=label_font)
    for state in range(3):
        village_cell = village.crop((0, state * 64, 64, state * 64 + 64))
        tower_cell = tower.crop((0, state * 64, 64, state * 64 + 64))
        paste_alpha(preview, checker((64, 64), 8), (294 + state * 108, 66))
        paste_alpha(preview, village_cell, (294 + state * 108, 66))
        paste_alpha(preview, checker((64, 64), 8), (294 + state * 108, 150))
        paste_alpha(preview, tower_cell, (294 + state * 108, 150))

    draw.text((22, 154), "TERRAIN SWATCHES · 8 × 32×32", fill="#bfc6c8", font=label_font)
    paste_alpha(preview, terrain, (22, 180))
    # Repeat once to expose how the small swatches behave as a tile palette.
    paste_alpha(preview, terrain, (22, 256))

    draw.text((290, 264), "EQUIPMENT / SKILLS · 8 × 32×32", fill="#bfc6c8", font=label_font)
    paste_alpha(preview, icons, (294, 286))
    return preview.convert("RGB")


def main() -> None:
    RUNTIME.mkdir(parents=True, exist_ok=True)
    PREVIEWS.mkdir(parents=True, exist_ok=True)

    unit_cells = split_row(INTERMEDIATE / "gray-banner-spearman-alpha.png", 4)
    unit = Image.new("RGBA", (128, 48))
    for index, cell in enumerate(unit_cells):
        unit.alpha_composite(fit_bottom(cell, (32, 48), 1), (index * 32, 0))
    unit.save(RUNTIME / "gray-banner-spearman-walk.png")

    def building_sheet(name: str) -> Image.Image:
        cells = split_row(INTERMEDIATE / f"{name}-alpha.png", 3)
        sheet = Image.new("RGBA", (64, 192))
        for index, cell in enumerate(cells):
            sheet.alpha_composite(fit_bottom(cell, (64, 64), 1), (0, index * 64))
        sheet.save(RUNTIME / f"{name}.png")
        return sheet

    village = building_sheet("frontier-village-states")
    tower = building_sheet("gray-banner-watchtower-states")

    terrain_cells = split_grid(INTERMEDIATE / "terrain-swatches-alpha.png", 4, 2)
    terrain = Image.new("RGBA", (128, 64))
    for index, cell in enumerate(terrain_cells):
        cell = center_square(alpha_crop(cell)).resize((32, 32), Image.Resampling.LANCZOS)
        terrain.alpha_composite(cell, ((index % 4) * 32, (index // 4) * 32))
    terrain.save(RUNTIME / "terrain-swatches.png")

    icon_cells = split_grid(INTERMEDIATE / "equipment-skills-alpha.png", 4, 2)
    icons = Image.new("RGBA", (128, 64))
    for index, cell in enumerate(icon_cells):
        cell = center_square(alpha_crop(cell)).resize((32, 32), Image.Resampling.LANCZOS)
        icons.alpha_composite(cell, ((index % 4) * 32, (index // 4) * 32))
    icons.save(RUNTIME / "equipment-skills.png")

    preview_1x = build_preview(unit, village, tower, terrain, icons)
    preview_1x.save(PREVIEWS / "campaign-01-resource-samples-1x.png")
    preview_1x.resize((2560, 1440), Image.Resampling.NEAREST).save(
        PREVIEWS / "campaign-01-resource-samples-4x.png"
    )


if __name__ == "__main__":
    main()
