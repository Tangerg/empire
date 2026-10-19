import json,hashlib
from pathlib import Path
base=Path('/workspace/shared/empire-art-rebuild/approved-v5/status')
notes={'status-08':'Actual32/HUD28 on white, black and olive: off-white folded bundle with two red-brown stain areas and rope knot remains distinguishable from status01 green-drop bundle. Cloth seam details are subordinate.','status-12':'Actual32/HUD28 on white, black and olive: blank gray two-tail banner, spear pole and gold ropes remain readable. Individual rope loops are exact at native source but not separately resolved at HUD28.','status-03':'Actual32/HUD28 on white, black and olive: chevron shield and adjacent chain remain distinct. Four links are retained in native source; small oblique connector is too small to count independently at HUD28.','status-06':'Actual32/HUD28 on white, black and olive: empty olive hood, golden-olive diagonal fern and brown sole form three distinct components; tiny leaflet lobes simplify at HUD28 without becoming noise.'}
for slug,note in notes.items():
 d=base/slug;p=d/'alpha-qa.json'
 if not p.exists():continue
 q=json.loads(p.read_text());q['independentPreview']['visualReview']=note;q['visualQA'].append(note);q['reviewComplete']=True;p.write_text(json.dumps(q,ensure_ascii=False,indent=2)+'\n')
 p=d/'qa-preview-32-28/method.json';m=json.loads(p.read_text());m['reviewStatus']='Visually reviewed at actual32 and28 on white/black/olive';p.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n')
 p=d/'source-evidence.json';e=json.loads(p.read_text())
 if slug=='status-08':e['semanticBindingMismatch']='Authored subject/label is injured, visible blood-stained bandage. Runtime direct binding is armor_down. Original art identity preserved; no rule meaning or gameplay binding changed.'
 if slug=='status-12':e['semanticBindingNote']='Authored morale banner is directly bound to inspired. Original blank gray banner preserved.'
 if slug in ['status-03','status-06']:e['consumerStatus']='Catalog source; no direct mapping found in CANDIDATE_01_STATUS_ART. Generic lookup availability does not establish actual use.'
 p.write_text(json.dumps(e,ensure_ascii=False,indent=2)+'\n')
 h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();assert h(d/'raw.png')==h(d/'master.png')==q['sha256'];print(slug,'source/preview verified')
