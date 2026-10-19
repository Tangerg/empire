import unittest,tempfile,json
from pathlib import Path
from PIL import Image
from calibrate_connectors import builder,geometry,compose,edge,build
ROOT=Path(__file__).resolve().parents[2]
class Connectors(unittest.TestCase):
 def test_all_ports_and_interior_continuity(self):
  mod,_=builder(ROOT)
  for water in (False,True):
   shapes=[geometry(mod,m,water) for m in range(16)]
   for m,im in enumerate(shapes):
    for bit,(x,y,dx,dy) in enumerate([(32,0,0,1),(63,32,-1,0),(32,63,0,-1),(0,32,1,0)]):
     self.assertEqual(im.getpixel((x,y))>128,bool(m&(1<<bit)))
     if m&(1<<bit):
      self.assertTrue(all(im.getpixel((x+dx*k,y+dy*k))==255 for k in range(32)))
   for a in range(16):
    for b in range(16):
     if a&2 and b&8:self.assertEqual(shapes[a].crop((63,0,64,64)).tobytes(),shapes[b].crop((0,0,1,64)).tobytes())
     if a&4 and b&1:self.assertEqual(shapes[a].crop((0,63,64,64)).tobytes(),shapes[b].crop((0,0,64,1)).tobytes())
 def test_composed_and_shoulder_joins(self):
  mod,_=builder(ROOT);tex=Image.new('RGB',(64,64),(100,120,140))
  for water in (False,True):
   imgs=[compose(tex,geometry(mod,m,water),(72,61,45)) for m in range(16)]
   rings=[edge(geometry(mod,m,water)) for m in range(16)]
   for group in (imgs,rings):
    for a in range(16):
     for b in range(16):
      if a&2 and b&8:self.assertEqual(group[a].crop((63,0,64,64)).tobytes(),group[b].crop((0,0,1,64)).tobytes())
 def test_outputs_and_provenance(self):
  with tempfile.TemporaryDirectory() as tmp:
   r=build(ROOT,Path(tmp));self.assertEqual(len(r['outputs']),6)
   for name,record in r['outputs'].items():
    with Image.open(Path(tmp)/name) as im:self.assertEqual(im.size,tuple(record['dimensions']))
    self.assertEqual(record['cells'],64)
   self.assertFalse(r['productionMutation']);self.assertEqual(len(r['sources']),2)
 def test_reject_package_output(self):
  with self.assertRaises(ValueError):build(ROOT,ROOT/'packages'/'calibration-output')

 def test_explicit_generated_material(self):
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp)/'water.png';Image.new('RGB',(64,64),(60,90,110)).save(p)
   r=build(ROOT,Path(tmp)/'output',p)
   self.assertEqual(r['sources']['water-slow-river']['path'],str(p))
   Image.new('RGBA',(64,64),(60,90,110,128)).save(p)
   with self.assertRaises(ValueError):build(ROOT,Path(tmp)/'bad',p)
