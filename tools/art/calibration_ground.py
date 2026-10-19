"""Ground-only study layers; no production output or creative pixel edits."""
import xml.etree.ElementTree as ET
from PIL import Image

NS = 'http://www.w3.org/2000/svg'
PATCH_PLACEMENTS = ((128, 176, 160), (232, 352, 144))


def element(tag, attrs):
    return ET.Element(f'{{{NS}}}{tag}', {key: str(value) for key, value in attrs.items()})


def append_ground(svg, defs, map_data, base, patch=None, world_period=128):
    if world_period not in (128, 512):
        raise ValueError('ground study period must be 128 or 512 logical pixels')
    with Image.open(base) as image:
        if image.convert('RGBA').getchannel('A').getextrema() != (255, 255):
            raise ValueError('ground base must be fully opaque')
    pattern = element('pattern', {'id': 'study-grass-base', 'patternUnits': 'userSpaceOnUse', 'width': world_period, 'height': world_period})
    pattern.append(element('image', {'href': base.as_uri(), 'width': world_period, 'height': world_period, 'preserveAspectRatio': 'none'}))
    defs.append(pattern)
    svg.append(element('rect', {'x': 0, 'y': 0, 'width': map_data['width'] * 32, 'height': map_data['height'] * 32, 'fill': 'url(#study-grass-base)'}))
    protected = [[i % map_data['width'], i // map_data['width']] for i, value in enumerate(map_data['tiles']) if value in ('water', 'bridge')]
    report = {'base': str(base), 'worldPeriodLogical': world_period, 'pixelOperations': 'resampling/compositing only; no recolor, blur, alpha cleanup or generated scatter', 'protectedWaterBridgeCells': protected, 'patches': []}
    if patch is None:
        return report
    with Image.open(patch) as image:
        rgba = image.convert('RGBA')
        alpha = rgba.getchannel('A')
        if alpha.getextrema()[0] != 0:
            raise ValueError('grass patch needs genuine exterior transparency')
        bbox = alpha.point(lambda value: 255 if value >= 128 else 0).getbbox()
        if bbox is None:
            raise ValueError('grass patch has no visible alpha core')
        width, height = image.size
    clip = element('clipPath', {'id': 'study-land-only', 'clipPathUnits': 'userSpaceOnUse'})
    for index, terrain in enumerate(map_data['tiles']):
        if terrain not in ('water', 'bridge'):
            clip.append(element('rect', {'x': (index % map_data['width']) * 32, 'y': (index // map_data['width']) * 32, 'width': 32, 'height': 32}))
    defs.append(clip)
    group = element('g', {'clip-path': 'url(#study-land-only)'})
    for cx, cy, visible_width in PATCH_PLACEMENTS:
        scale = visible_width / (bbox[2] - bbox[0])
        x = cx - (bbox[0] + bbox[2]) * .5 * scale
        y = cy - (bbox[1] + bbox[3]) * .5 * scale
        group.append(element('image', {'href': patch.as_uri(), 'x': x, 'y': y, 'width': width * scale, 'height': height * scale}))
        report['patches'].append({'source': str(patch), 'visibleCenterWorld': [cx, cy], 'visibleWidthLogical': visible_width, 'visibleSourceBboxAlpha128': list(bbox), 'fullCanvasWorld': [x, y, width * scale, height * scale], 'logicalPerSourcePixel': scale})
    svg.append(group)
    report['layerOrder'] = 'base, grass masses, unchanged terrain blends/routes/water, unchanged registered objects'
    return report
