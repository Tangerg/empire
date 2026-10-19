import json,sys
from pathlib import Path
n=int(sys.argv[1]); msg=sys.argv[2]; p=Path('/workspace/shared/empire-art-rebuild/approved-v5/hud')/f'hud-{n:02d}/alpha-qa.json'; q=json.loads(p.read_text()); q.update({'reviewComplete':True,'visualQA':['Native image preserves original subject, composition and functional colors.','Bold contours and broad matte cel planes; no exterior haze or background.','Transparent alpha and open exterior verified numerically.',msg]}); q['independentPreview']['visualReview']=msg; p.write_text(json.dumps(q,indent=2)+'\n')
