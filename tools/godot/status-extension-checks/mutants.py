#!/usr/bin/env python3
from pathlib import Path
import os, shutil, subprocess, sys, tempfile
root=Path(__file__).resolve().parents[3]
fixture=Path(sys.argv[1]).resolve()
work=Path(tempfile.mkdtemp(prefix='empire-status-mutants.',dir=os.environ.get('TMPDIR')))
core=work/'godot/Empire.Core'; harness=work/'tools/godot/status-extension-checks'
shutil.copytree(root/'godot/Empire.Core',core,ignore=shutil.ignore_patterns('bin','obj'))
shutil.copytree(root/'tools/godot/status-extension-checks',harness,ignore=shutil.ignore_patterns('bin','obj'))
command=['dotnet','run','--project',str(harness),'--',str(fixture)]
def run(name):
 result=subprocess.run(command,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
 (work/(name+'.log')).write_text(result.stdout)
 return result
if run('baseline').returncode: raise SystemExit('Isolated baseline failed: '+str(work))
mutations=[
 ('ignored-hook','Statuses/StatusLifecycle.cs','behaviors.OwnerTurnStart(context);','_ = behaviors;' ),
 ('ignored-periodic','Statuses/StatusLifecycle.cs','Text(periodic, "timing") == "ownerTurnStart"','Text(periodic, "timing") == "never"'),
 ('wrong-hook-duration','Statuses/StatusLifecycle.cs','behaviors.OwnerTurnStart(context);','instance["remaining"] = Number(instance["remaining"], "remaining") - 1; behaviors.OwnerTurnStart(context);'),
 ('dead-hook-continues','Statuses/StatusLifecycle.cs','if (!Present()) break;','// omitted departure guard'),
 ('wrong-expiry-identity','Statuses/StatusLifecycle.cs','StatusTransitions.Remove(unit, Text(instance, "id"), emit);','{ if (unit["statuses"]!.AsArray().Remove(instance)) emit(new JsonObject { ["type"]="statusRemoved", ["unit"]=Integer(unit,"id"), ["status"]=Text(instance,"id") }); }'),
 ('live-status-enumeration','Statuses/StatusLifecycle.cs','Objects(unit["statuses"], "statuses").ToArray()','Objects(unit["statuses"], "statuses")'),
 ('wrong-damage-return','Statuses/StatusLifecycleContext.cs','=> damage.DamageStatus(State, Unit, Status, requested, nonlethal, Emit);','{ damage.DamageStatus(State, Unit, Status, requested, nonlethal, Emit); return requested; }'),
 ('copied-content-identity','Statuses/StatusLifecycle.cs','new StatusLifecycleContext(content, damage','new StatusLifecycleContext(new ReadOnlyContent(content.CopyPacks()), damage'),
 ('unknown-hook-demand','Statuses/StatusBehaviorRegistry.cs','TryGet(context.Status["id"]!.GetValue<string>())?.OnOwnerTurnStart?.Invoke(context)','Get(context.Status["id"]!.GetValue<string>()).OnOwnerTurnStart?.Invoke(context)'),
 ('two-duration-ticks','Statuses/StatusLifecycle.cs','Number(instance["remaining"], "remaining") - 1','Number(instance["remaining"], "remaining") - 2'),
]
for name,relative,before,after in mutations:
 p=core/relative; original=p.read_text()
 if before not in original: raise SystemExit('Missing anchor: '+name)
 p.write_text(original.replace(before,after))
 try:
  result=run(name)
  if result.returncode==0 or 'error CS' in result.stdout or 'build failed' in result.stdout.lower(): raise SystemExit('Survived/noncompiling mutant: '+name+'; '+str(work))
  print('REJECTED '+name,flush=True)
 finally:p.write_text(original)
print('PASS 10 compiled status lifecycle mutants; '+str(work))
