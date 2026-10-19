import argparse, hashlib, json, subprocess
from pathlib import Path
from PIL import Image
parser=argparse.ArgumentParser(description='Capture current seven-hero source and runtime identities')
parser.add_argument('root',type=Path)
parser.add_argument('output',type=Path)
args=parser.parse_args()
root=args.root.resolve()
out=args.output
pack=Path('packages/story-candidate-01/assets/final-fantasy-v1')
audit=json.loads((out/'inventory.json').read_text())
manifest=json.loads((root/pack/'manifest-final-fantasy-v1.json').read_text())
assets={a['topicId']:a for a in manifest['assets']}
def ref(path):
 p=root/path;raw=p.read_bytes()
 with Image.open(p) as im:
  return {'path':str(path),'sha256':hashlib.sha256(raw).hexdigest(),'size':list(im.size),'mode':im.mode,'alphaExtrema':list(im.getchannel('A').getextrema()) if im.mode=='RGBA' else None}
rows=[]
for unit,card,troop in [('laiya','LEIA','banner-guard'),('roderick','RODERICK','heavy-knight'),('cain','CAIN','legion-shield'),('mirelle','MIREL','gravekeeper'),('bran','BRAN','ranger'),('tasha','TASHA','engineer'),('ivra','IVRA','ivra-growth')]:
 topic='C01-CHAR-'+card+'-01';base='C01-UNIT-'+troop.upper()
 old=next(a for a in audit['assets'] if a['topicId']==topic)
 seq=Path('docs/art-rebuild-20261019/approved-v5/animation')/troop/'export/sequence.json'
 data=json.loads((root/seq).read_text())
 sheet=pack/assets[base]['png'];hd=sheet.with_name(sheet.stem+'@2x.png')
 exported=(root/seq.parent/'sheet@2x.png').read_bytes()
 assert exported==(root/hd).read_bytes(),troop
 rows.append({'unitType':'c01.'+unit,'cardTopicId':topic,'currentBattleTopicId':base,'card':ref(pack/assets[topic]['png']),'originalBoard':ref(Path(old['documentedSource']['board'])),'originalBoardCell':old['documentedSource']['boardCell'],'originalCropBox':old['documentedSource']['cropBox'],'runtimeBattle':ref(sheet),'runtimeBattleHd':ref(hd),'publishedSequence':{'path':str(seq),'sha256':hashlib.sha256((root/seq).read_bytes()).hexdigest(),'matchesCurrentHdBytes':True},'nativeBattleFrames':{state:ref(Path(p)) for state,p in data['config']['frames'].items()}})
report={'checkpoint':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'scope':'read-only named identity references; no source generation or binding mutation','heroes':rows,'contractFiles':{p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in ['packages/story-candidate-01/src/units.ts','packages/story-candidate-01/src/presentation/candidate-01-bindings.ts','packages/story-candidate-01/src/presentation/candidate-01-assets.ts','packages/story-candidate-01/src/presentation/candidate-01-runtime.ts']}}
(out/'hero-references.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print('7 named cards; 7 current battle strips; 28 native frames; all published @2x sheets byte-match current runtime')
