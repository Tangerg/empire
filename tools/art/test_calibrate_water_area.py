import unittest,tempfile
from pathlib import Path
from PIL import Image
from calibrate_water_area import water_area_mask,render_water_area
class WaterArea(unittest.TestCase):
 def test_all_masks_dimensions(self):
  for size in (32,64):
   for m in range(256):self.assertEqual(water_area_mask(size,m).size,(size,size))
 def test_full_interior_and_shore(self):
  self.assertEqual(water_area_mask(64,255).getextrema(),(255,255))
  isolated=water_area_mask(64,0);self.assertEqual(isolated.getpixel((0,32)),0);self.assertEqual(isolated.getpixel((32,32)),255)
 def test_two_by_n_has_no_internal_holes(self):
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp)/'material.png';Image.new('RGB',(64,64),(70,100,130)).save(p)
   for size in (32,64):
    im,r=render_water_area(p,[(x,y) for y in range(7) for x in range(2)],size);a=im.getchannel('A')
    self.assertEqual(a.crop((size-3,size,size+3,6*size)).getextrema(),(255,255))
    self.assertEqual(a.getpixel((0,3*size)),0);self.assertEqual(r['worldOriginCells'],[0,0])
 def test_concave_land_is_not_filled(self):
  m=(1<<0)|(1<<6);a=water_area_mask(64,m)
  self.assertEqual(a.getpixel((0,0)),0)
  self.assertEqual(water_area_mask(64,m|(1<<7)).getpixel((0,0)),255)
 def test_outside_cells_remain_clear(self):
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp)/'material.png';Image.new('RGB',(64,64),(70,100,130)).save(p)
   im,r=render_water_area(p,[(3,4),(4,4),(3,5)],64)
   self.assertEqual(im.getchannel('A').crop((64,64,128,128)).getextrema(),(0,0))
   self.assertEqual(r['worldOriginCells'],[3,4])
