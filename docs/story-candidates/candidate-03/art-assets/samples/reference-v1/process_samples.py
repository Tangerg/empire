#!/usr/bin/env python3
"""Build runtime-scale samples for the Campaign 3 approved art direction."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent
INTERMEDIATE = ROOT / "intermediate"
RUNTIME = ROOT / "runtime"
PREVIEWS = ROOT / "previews"


def spans(alpha: Image.Image, axis: str) -> list[tuple[int, int]]:
    values: list[int] = []
    if axis == "x":
        for x in range(alpha.width):
            if alpha.crop((x, 0, x + 1, alpha.height)).getbbox():
                values.append(x)
    else:
        for y in range(alpha.height):
            if alpha.crop((0, y, alpha.width, y + 1)).getbbox():
                values.append(y)
    if not values:
        return []
    result: list[tuple[int, int]] = []
    start = previous = values[0]
    for value in values[1:]:
        if value > previous + 1:
            result.append((start, previous + 1))
            start = value
        previous = value
    result.append((start, previous + 1))
    return [item for item in result if item[1] - item[0] > 20]


def alpha_crop(image: Image.Image) -> Image.Image:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("empty source cell")
    return image.crop(bbox)


def fit_bottom(image: Image.Image, size: tuple[int, int], padding: int = 1) -> Image.Image:
    image = alpha_crop(image)
    scale = min(
        (size[0] - padding * 2) / image.width,
        (size[1] - padding * 2) / image.height,
    )
    image = image.resize(
        (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
        Image.Resampling.LANCZOS,
    )
    result = Image.new("RGBA", size)
    result.alpha_composite(image, ((size[0] - image.width) // 2, size[1] - padding - image.height))
    return result


def clean_key_residue(image: Image.Image) -> Image.Image:
    """Drop negligible magenta key pixels left by antialiased resampling."""
    image = image.copy()
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if a <= 8 and r > 220 and b > 220 and g < 50:
                pixels[x, y] = (0, 0, 0, 0)
    return image


def center_square(image: Image.Image) -> Image.Image:
    image = alpha_crop(image)
    side = min(image.width, image.height)
    return image.crop(
        (
            (image.width - side) // 2,
            (image.height - side) // 2,
            (image.width + side) // 2,
            (image.height + side) // 2,
        )
    )


def split_row(path: Path, count: int) -> list[Image.Image]:
    image = Image.open(path).convert("RGBA")
    columns = spans(image.getchannel("A"), "x")
    if len(columns) != count:
        raise ValueError(f"{path.name}: expected {count} columns, got {columns}")
    return [image.crop((x0, 0, x1, image.height)) for x0, x1 in columns]


def split_grid(path: Path, columns: int, rows: int) -> list[Image.Image]:
    image = Image.open(path).convert("RGBA")
    alpha = image.getchannel("A")
    x_spans = spans(alpha, "x")
    y_spans = spans(alpha, "y")
    if len(x_spans) != columns or len(y_spans) != rows:
        raise ValueError(f"{path.name}: expected {columns}x{rows}, got {x_spans} / {y_spans}")
    return [
        image.crop((x0, y0, x1, y1))
        for y0, y1 in y_spans
        for x0, x1 in x_spans
    ]


def checker(size: tuple[int, int], block: int = 8) -> Image.Image:
    image = Image.new("RGBA", size, "#424541")
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], block):
        for x in range(0, size[0], block):
            if (x // block + y // block) % 2:
                draw.rectangle((x, y, x + block - 1, y + block - 1), fill="#353936")
    return image


def font(size: int) -> ImageFont.ImageFont:
    for path in (
        Path("/System/Library/Fonts/Supplemental/Arial Bold.ttf"),
        Path("/System/Library/Fonts/Supplemental/Arial.ttf"),
    ):
        if path.exists():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


def preview(
    unit: Image.Image,
    sluice: Image.Image,
    depot: Image.Image,
    terrain: Image.Image,
    icons: Image.Image,
) -> Image.Image:
    result = Image.new("RGBA", (720, 408), "#1b1d1a")
    draw = ImageDraw.Draw(result)
    title = font(14)
    label = font(11)
    draw.text((16, 12), "CAMPAIGN 3 · ANCIENT CHINESE RUNTIME SAMPLE", fill="#ded5bc", font=title)
    boxes = ((12, 36, 292, 146), (304, 36, 708, 246), (12, 158, 292, 396), (304, 258, 708, 396))
    for box in boxes:
        draw.rounded_rectangle(box, radius=5, fill="#292c27", outline="#676b61")

    draw.text((22, 46), "SWORD + RATTAN SHIELD · 4 × 32×48", fill="#c9c8bb", font=label)
    result.alpha_composite(checker((136, 56)), (22, 70))
    result.alpha_composite(unit, (26, 74))

    draw.text((314, 46), "BUILDINGS · NORMAL / DAMAGED / CAPTURED", fill="#c9c8bb", font=label)
    for state in range(3):
        x = 318 + state * 128
        sluice_cell = sluice.crop((0, state * 64, 96, state * 64 + 64))
        depot_cell = depot.crop((0, state * 64, 64, state * 64 + 64))
        result.alpha_composite(checker((96, 64)), (x, 70))
        result.alpha_composite(sluice_cell, (x, 70))
        result.alpha_composite(checker((64, 64)), (x + 16, 158))
        result.alpha_composite(depot_cell, (x + 16, 158))

    draw.text((22, 168), "PADDY / CANAL / ROAD TILES · 8 × 32×32", fill="#c9c8bb", font=label)
    result.alpha_composite(terrain, (22, 196))
    result.alpha_composite(terrain, (22, 276))

    draw.text((314, 268), "EQUIPMENT / COMMAND · 8 × 32×32", fill="#c9c8bb", font=label)
    result.alpha_composite(icons, (318, 302))
    return result.convert("RGB")


def main() -> None:
    RUNTIME.mkdir(parents=True, exist_ok=True)
    PREVIEWS.mkdir(parents=True, exist_ok=True)

    unit = Image.new("RGBA", (128, 48))
    for index, cell in enumerate(split_row(INTERMEDIATE / "sword-shield-infantry-alpha.png", 4)):
        unit.alpha_composite(fit_bottom(cell, (32, 48)), (index * 32, 0))
    unit = clean_key_residue(unit)
    unit.save(RUNTIME / "sword-shield-infantry-walk.png")

    def states(source: str, name: str, frame_size: tuple[int, int]) -> Image.Image:
        sheet = Image.new("RGBA", (frame_size[0], frame_size[1] * 3))
        for index, cell in enumerate(split_row(INTERMEDIATE / source, 3)):
            sheet.alpha_composite(fit_bottom(cell, frame_size), (0, index * frame_size[1]))
        sheet = clean_key_residue(sheet)
        sheet.save(RUNTIME / name)
        return sheet

    sluice = states("river-sluice-bridge-states-alpha.png", "river-sluice-bridge-states.png", (96, 64))
    depot = states("public-grain-depot-states-alpha.png", "public-grain-depot-states.png", (64, 64))

    terrain = Image.new("RGBA", (128, 64))
    for index, cell in enumerate(split_grid(INTERMEDIATE / "terrain-swatches-alpha.png", 4, 2)):
        cell = center_square(cell).resize((32, 32), Image.Resampling.LANCZOS)
        terrain.alpha_composite(cell, ((index % 4) * 32, (index // 4) * 32))
    terrain = clean_key_residue(terrain)
    terrain.save(RUNTIME / "terrain-swatches.png")

    icons = Image.new("RGBA", (128, 64))
    for index, cell in enumerate(split_grid(INTERMEDIATE / "equipment-command-alpha.png", 4, 2)):
        cell = center_square(cell).resize((32, 32), Image.Resampling.LANCZOS)
        icons.alpha_composite(cell, ((index % 4) * 32, (index // 4) * 32))
    icons = clean_key_residue(icons)
    icons.save(RUNTIME / "equipment-command.png")

    one_x = preview(unit, sluice, depot, terrain, icons)
    one_x.save(PREVIEWS / "campaign-03-resource-samples-1x.png")
    one_x.resize((2880, 1632), Image.Resampling.NEAREST).save(
        PREVIEWS / "campaign-03-resource-samples-4x.png"
    )


if __name__ == "__main__":
    main()
