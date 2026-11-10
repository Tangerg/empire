"""Data-only negative controls in an isolated project with private runtime cache."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import subprocess

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--repo',type=Path,required=True)
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
repo=args.repo.resolve()
out=args.output.resolve()
assert repo not in out.parents and out != repo, 'Output must be outside the repository.'
assert not out.exists(), 'Output must be new; preserving previous controls.'
stage=out/'project'
out.mkdir(parents=True,exist_ok=True)
assert not stage.exists(), 'Refusing to overwrite previous controls.'
stage.mkdir()
for path in (repo/'godot').iterdir():
    destination=stage/path.name
    if path.name in ('data','.godot'):
        shutil.copytree(path,destination)
    else:
        destination.symlink_to(path,target_is_directory=path.is_dir())
paths=[stage/'data/art-bindings.json',stage/'data/selected-assets.json']
original={p:p.read_bytes() for p in paths}
production_paths=[repo/'godot/data/art-bindings.json',repo/'godot/data/selected-assets.json',repo/'godot/Presentation/BoardView.Animation.cs']
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
production_before={str(p.relative_to(repo)):sha(p) for p in production_paths}
summary={}

def run(name,expected=None):
    env=os.environ.copy();env['XDG_DATA_HOME']=str(out/('xdg-'+name))
    logfile=out/(name+'.txt')
    with (out/(name+'-wrapper.txt')).open('w') as output:
        result=subprocess.run(['bash',str(repo/'tools/godot/native-process.sh'),str(logfile),name,
          env['GODOT_NET'],'--headless','--path',str(stage),'res://Scenes/AnimationChecks.tscn'],
          env=env,stdout=output,stderr=subprocess.STDOUT)
    text=logfile.read_text()
    summary[name]={'gateExit':result.returncode,'logSha256':sha(logfile),
      'failure':next((line for line in text.splitlines() if line.startswith('ERROR:')),None)}
    if expected:
        assert result.returncode==1 and expected in text,(name,summary[name])
    else:
        assert result.returncode==0 and 'Animation presentation checks passed:' in text
    return text

try:
    binding=json.loads(original[paths[0]])
    binding['CANDIDATE_01_UNIT_ART']['c01.archer']['actionClips']['death']='static.death'
    paths[0].write_text(json.dumps(binding))
    run('static-death-binding', 'Shooting does not replace')
    paths[0].write_bytes(original[paths[0]])

    manifest=json.loads(original[paths[1]])
    frames=manifest['actorClips']['archer.fall']['frames']
    frames[0]['span']+=1;frames[1]['span']-=1
    assert sum(f['span'] for f in frames)==620
    paths[1].write_text(json.dumps(manifest))
    run('one-ms-interval-transfer','Each fall pose owns its exact half-open')
    paths[1].write_bytes(original[paths[1]])

    manifest=json.loads(original[paths[1]])
    manifest['actorClips']['archer.fall']['death']['fadeMs']=159
    paths[1].write_text(json.dumps(manifest))
    run('one-ms-short-fade','At 535 ms only the ordered death event starts')
    paths[1].write_bytes(original[paths[1]])

    manifest=json.loads(original[paths[1]])
    manifest['actorClips']['archer.fall']['frames'][4]['frame']='fall-contact'
    paths[1].write_text(json.dumps(manifest))
    run('duplicate-fall-pose','The C01 archer selects six authored falling poses')
finally:
    for p,data in original.items():p.write_bytes(data)
    summary['restoredInputsMatch']=all(p.read_bytes()==data for p,data in original.items())
    summary['productionInputsUnchanged']=production_before=={str(p.relative_to(repo)):sha(p) for p in production_paths}
    summary['productionSha256']=production_before
    (out/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
run('restored-positive')
summary['restoredPositivePassed']=True
(out/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
assert summary['restoredInputsMatch'] and summary['productionInputsUnchanged']
print(json.dumps(summary,indent=2))
