import unittest
import xml.etree.ElementTree as ET
from svg_viewports import NS, flatten_viewports


class ViewportTests(unittest.TestCase):
    def test_atlas_viewport_clips_without_cropping_or_repositioning_image(self):
        svg = ET.fromstring(f'''<svg xmlns="{NS}"><defs/>
          <g transform="translate(-53.855 -92.382) scale(1.102)">
            <svg width="96" height="96" viewBox="0 0 96 96" overflow="hidden">
              <image href="forest.png" x="-192" y="0" width="384" height="384"/>
            </svg>
          </g></svg>''')
        defs = svg.find(f'{{{NS}}}defs')
        image = svg.find(f'.//{{{NS}}}image')
        original = dict(image.attrib)
        flatten_viewports(svg, defs)
        self.assertEqual(list(svg.iter(f'{{{NS}}}svg')), [svg])
        self.assertEqual(image.attrib, original)
        self.assertEqual(svg[1].get('transform'), 'translate(-53.855 -92.382) scale(1.102)')
        self.assertEqual(defs[0][0].attrib, {'x': '0.0', 'y': '0.0', 'width': '96.0', 'height': '96.0'})
        self.assertEqual(svg[1][0][0].get('clip-path'), 'url(#viewport-1)')

    def test_offset_viewbox_keeps_source_coordinates_and_destination_size(self):
        svg = ET.fromstring(f'''<svg xmlns="{NS}"><defs/>
          <svg x="4" y="8" width="48" height="32" viewBox="10 20 96 64">
            <image href="unit.png" x="-96" y="0" width="384" height="64"/>
          </svg></svg>''')
        defs = svg.find(f'{{{NS}}}defs')
        flatten_viewports(svg, defs)
        self.assertEqual(svg[1].get('transform'), 'translate(4.0 8.0) scale(0.5 0.5) translate(-10.0 -20.0)')
        self.assertEqual(defs[0][0].attrib, {'x': '10.0', 'y': '20.0', 'width': '96.0', 'height': '64.0'})
        self.assertEqual(svg[1][0][0].get('x'), '-96')


if __name__ == '__main__':
    unittest.main()
