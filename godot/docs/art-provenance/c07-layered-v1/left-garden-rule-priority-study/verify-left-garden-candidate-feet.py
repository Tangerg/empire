from pathlib import Path
import json,sys
p=Path(__file__).resolve().parent;root=p.parent;source=root/'c07-readonly-review/left-garden-source-study/verify-new-continuous-l-wall.py';ns={'__file__':str(source)};exec(compile(source.read_text().split('\npolys=')[0],str(source),'exec'),ns)
layout=json.load(open(p/'left-garden-stone-new-layout.json'));negative='--negative' in sys.argv;results={}
for c in layout['components']:
 poly=c['physicalFootPolygon'];poly=[[x,y+13]for x,y in poly]if negative else poly;r=ns['check'](poly);results[c['id']]=r
passed=all(all(v['terrain']=='#'for v in r['positiveAreaCells'])and r['walk']['intersectingSegments']==0 and r['walkEndpointsInside']==0 and all(v['disjoint']for v in r['relations'].values())for r in results.values())
report={'scope':'Actual generated five-component finite bases; fixed local real Torren walk-a/b sole arrays/actual facing, no commands/AI/save. NEW enclosed stone domains not original hidden-foot claims.','negativeShiftDown13':negative,'soleSegmentCount':len(ns['A']),'results':results,'passed':passed};(p/('left-garden-candidate-feet-negative.json'if negative else'left-garden-candidate-feet-positive.json')).write_text(json.dumps(report,indent=2)+'\n')
assert passed,'Finite left garden stone bases must stay blocked, outside real frozen walk sole corridors and separate from the two house foundations'
print('LEFT_GARDEN_FINITE_BASE_COMPLETE',len(results),len(ns['A']))
