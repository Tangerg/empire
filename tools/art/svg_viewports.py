"""Preserve browser viewport clipping in offline SVG renderers."""
import xml.etree.ElementTree as ET

NS = 'http://www.w3.org/2000/svg'


def flatten_viewports(svg, defs):
    # Inkscape does not reliably apply nested SVG overflow clipping. Spell the
    # same viewport and viewBox transform explicitly; never crop the source PNG.
    index = 0
    for parent in list(svg.iter()):
        for child in list(parent):
            if child.tag != f'{{{NS}}}svg':
                continue
            index += 1
            vx, vy, vw, vh = map(float, child.get('viewBox').split())
            x, y = float(child.get('x', '0')), float(child.get('y', '0'))
            width = float(child.get('width', str(vw)))
            height = float(child.get('height', str(vh)))
            clip = ET.SubElement(defs, f'{{{NS}}}clipPath', {
                'id': f'viewport-{index}', 'clipPathUnits': 'userSpaceOnUse',
            })
            ET.SubElement(clip, f'{{{NS}}}rect', dict(zip(
                ('x', 'y', 'width', 'height'), map(str, (vx, vy, vw, vh)),
            )))
            group = ET.Element(f'{{{NS}}}g', {
                'transform': f'translate({x} {y}) scale({width / vw} {height / vh}) translate({-vx} {-vy})',
            })
            inside = ET.SubElement(group, f'{{{NS}}}g', {'clip-path': f'url(#viewport-{index})'})
            inside.extend(list(child))
            parent.insert(list(parent).index(child), group)
            parent.remove(child)
