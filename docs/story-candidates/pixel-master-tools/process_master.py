#!/usr/bin/env python3
"""Turn a high-detail chroma-key pixel master into game-ready PNG and SVG."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def extract_alpha(source: Image.Image) -> Image.Image:
    rgba = source.convert("RGBA")
    processed: list[tuple[int, int, int, int]] = []
    for red, green, blue, _alpha in rgba.get_flattened_data():
        distance_sq = (red - 255) ** 2 + green**2 + (blue - 255) ** 2
        keyish = (
            red > 115
            and blue > 115
            and green < min(red, blue) * 0.62
            and abs(red - blue) < 145
        )
        processed.append((0, 0, 0, 0) if distance_sq < 145**2 or keyish else (red, green, blue, 255))
    result = Image.new("RGBA", rgba.size)
    result.putdata(processed)
    return result


def crop_content(image: Image.Image, padding: int = 0) -> Image.Image:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("No non-key pixels found")
    left, top, right, bottom = bbox
    return image.crop(
        (
            max(0, left - padding),
            max(0, top - padding),
            min(image.width, right + padding),
            min(image.height, bottom + padding),
        )
    )


def fit(image: Image.Image, width: int, height: int, padding: int, anchor: str) -> Image.Image:
    inner_w = max(1, width - padding * 2)
    inner_h = max(1, height - padding * 2)
    scale = min(inner_w / image.width, inner_h / image.height)
    size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
    resized = image.resize(size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    x = (width - size[0]) // 2
    y = padding if anchor == "top" else height - padding - size[1]
    canvas.alpha_composite(resized, (x, y))
    return canvas


def quantize(image: Image.Image, colors: int) -> Image.Image:
    alpha = image.getchannel("A").point(lambda value: 255 if value >= 112 else 0)
    backdrop = Image.new("RGBA", image.size, (18, 18, 22, 255))
    backdrop.alpha_composite(image)
    indexed = backdrop.convert("RGB").quantize(
        colors=colors,
        method=Image.Quantize.MEDIANCUT,
        dither=Image.Dither.NONE,
    )
    result = indexed.convert("RGBA")
    result.putalpha(alpha)
    cleaned = [pixel if pixel[3] else (0, 0, 0, 0) for pixel in result.get_flattened_data()]
    result.putdata(cleaned)
    return result


def make_single(source: Image.Image, width: int, height: int, colors: int, anchor: str) -> Image.Image:
    extracted = extract_alpha(source)
    padding = max(2, min(source.size) // 150)
    cropped = crop_content(extracted, padding)
    return quantize(fit(cropped, width, height, max(1, width // 48), anchor), colors)


def make_sheet(source: Image.Image, width: int, height: int, frames: int, colors: int) -> Image.Image:
    extracted = extract_alpha(source)
    frame_w = width // frames
    sheet = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    source_frame_w = source.width / frames
    for frame in range(frames):
        left = round(frame * source_frame_w)
        right = round((frame + 1) * source_frame_w)
        panel = extracted.crop((left, 0, right, source.height))
        panel = crop_content(panel, max(1, source.width // 1000))
        fitted = fit(panel, frame_w, height, max(1, frame_w // 16), "bottom")
        sheet.alpha_composite(fitted, (frame * frame_w, 0))
    return quantize(sheet, colors)


def make_scene(source: Image.Image, width: int, height: int, colors: int) -> Image.Image:
    image = source.convert("RGBA")
    target_ratio = width / height
    source_ratio = image.width / image.height
    if source_ratio > target_ratio:
        crop_width = round(image.height * target_ratio)
        left = (image.width - crop_width) // 2
        image = image.crop((left, 0, left + crop_width, image.height))
    elif source_ratio < target_ratio:
        crop_height = round(image.width / target_ratio)
        top = (image.height - crop_height) // 2
        image = image.crop((0, top, image.width, top + crop_height))
    image = image.resize((width, height), Image.Resampling.LANCZOS)
    return quantize(image, colors)


def svg_color(pixel: tuple[int, int, int, int]) -> tuple[str, str]:
    red, green, blue, alpha = pixel
    fill = f"#{red:02x}{green:02x}{blue:02x}"
    opacity = "" if alpha == 255 else f' opacity="{alpha / 255:.3f}"'
    return fill, opacity


def vectorize(image: Image.Image, output: Path, label: str) -> None:
    rgba = image.convert("RGBA")
    rows: list[str] = []
    for y in range(rgba.height):
        x = 0
        while x < rgba.width:
            pixel = rgba.getpixel((x, y))
            if pixel[3] == 0:
                x += 1
                continue
            run = 1
            while x + run < rgba.width and rgba.getpixel((x + run, y)) == pixel:
                run += 1
            fill, opacity = svg_color(pixel)
            rows.append(
                f'<rect x="{x}" y="{y}" width="{run}" height="1" fill="{fill}"{opacity}/>'
            )
            x += run
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{rgba.width}" height="{rgba.height}" '
        f'viewBox="0 0 {rgba.width} {rgba.height}" role="img" aria-label="{label}" '
        f'shape-rendering="crispEdges" style="image-rendering:pixelated">\n'
        + "\n".join(rows)
        + "\n</svg>\n",
        encoding="utf-8",
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--png-out", required=True, type=Path)
    parser.add_argument("--svg-out", required=True, type=Path)
    parser.add_argument("--mode", choices=("single", "sheet", "scene"), default="single")
    parser.add_argument("--width", required=True, type=int)
    parser.add_argument("--height", required=True, type=int)
    parser.add_argument("--frames", type=int, default=1)
    parser.add_argument("--colors", type=int, default=48)
    parser.add_argument("--anchor", choices=("top", "bottom"), default="bottom")
    parser.add_argument("--label", required=True)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    source = Image.open(args.input)
    if args.mode == "sheet":
        result = make_sheet(source, args.width, args.height, args.frames, args.colors)
    elif args.mode == "scene":
        result = make_scene(source, args.width, args.height, args.colors)
    else:
        result = make_single(source, args.width, args.height, args.colors, args.anchor)
    args.png_out.parent.mkdir(parents=True, exist_ok=True)
    result.save(args.png_out, optimize=True)
    vectorize(result, args.svg_out, args.label)


if __name__ == "__main__":
    main()
