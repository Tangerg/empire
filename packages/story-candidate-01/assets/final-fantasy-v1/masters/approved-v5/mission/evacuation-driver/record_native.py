from pathlib import Path
import json,hashlib,shutil,sys
b=Path(__file__).parent;pose,src=sys.argv[1:];d=b/pose;d.mkdir(parents=True,exist_ok=True);s=Path(src);h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for n in ['raw.png','master.png']:shutil.copyfile(s,d/n)
(d/'generation-result.json').write_text(json.dumps({'nativeOutputPath':str(s),'nativeOutputSha256':h(s),'selectedRawSha256':h(d/'raw.png'),'rawMatchesMaster':h(d/'raw.png')==h(d/'master.png'),'nativeOutputRetainedUnchanged':True},indent=2)+'\n')
