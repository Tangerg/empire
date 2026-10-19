import unittest,tempfile
from pathlib import Path
from PIL import Image
from export_registered_sequence import validate
class RegisteredSequenceTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory(); self.addCleanup(self.tmp.cleanup); self.frames={}
  for i,n in enumerate(['idle','walk-a','attack','walk-b']):
   p=Path(self.tmp.name)/f'{n}.png'; im=Image.new('RGBA',(16,16)); im.paste((50+i,80,90,253),(3,3,13,13));im.save(p);self.frames[n]=str(p)
  self.c={'frames':self.frames,'scale':1,'origin':[4,4],'frameSize':[24,24]}
 def test_shared_canvas_valid(self):self.assertEqual(validate(self.c),(16,16))
 def test_duplicate_rejected(self):
  self.frames['attack']=self.frames['idle']
  with self.assertRaises(ValueError):validate(self.c)
 def test_independent_canvas_rejected(self):
  Image.new('RGBA',(17,16)).save(self.frames['attack'])
  with self.assertRaises(ValueError):validate(self.c)
 def test_offset_bounded(self):
  self.c['registrationOffsets']={'walk-a':[0,3]}
  with self.assertRaises(ValueError):validate(self.c)
 def test_offset_no_clip(self):
  self.c['origin']=[0,0];self.c['registrationOffsets']={'walk-a':[0,-1]}
  with self.assertRaises(ValueError):validate(self.c)
 def test_semantic_order_required(self):
  self.c['frames']=dict(reversed(list(self.frames.items())))
  with self.assertRaises(ValueError):validate(self.c)
