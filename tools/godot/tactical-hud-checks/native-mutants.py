"""Run native callback/lifetime mutations in a throwaway Godot project, never the source tree."""
from pathlib import Path
import os, shutil, subprocess, tempfile
root=Path.cwd(); godot=root/'godot'
mutants={
 'duplicate-submission':('TacticalHud.cs',' && !submitted',''),
 'disabled-signal':('TacticalHud.cs',' && !button.Disabled',''),
 'drop-card-release':('TacticalHudArt.cs','foreach (var texture in cards.Values) texture.Dispose(); cards.Clear();',''),
}
with tempfile.TemporaryDirectory(prefix='hud-native-mutants-') as folder:
 project=Path(folder)/'godot'
 shutil.copytree(godot,project,ignore=shutil.ignore_patterns('.godot','bin','obj','assets','data','tests'))
 for name in ['assets','data','tests']:(project/name).symlink_to(godot/name,target_is_directory=True)
 (project/'.godot').mkdir();(project/'.godot/imported').symlink_to(godot/'.godot/imported',target_is_directory=True)
 restore=subprocess.run(['dotnet','restore',str(project/'Empire.csproj'),'--ignore-failed-sources'],capture_output=True,text=True)
 if restore.returncode:raise SystemExit(restore.stdout+restore.stderr)
 for name,(file,old,new) in mutants.items():
  target=project/'Presentation/Tactical'/file;original=target.read_text()
  if old not in original:raise SystemExit('Mutation no longer applies '+name)
  mutated=original.replace(old,new)
  if name=="disabled-signal":mutated=mutated.replace(" && row.Enabled","")
  target.write_text(mutated)
  build=subprocess.run(['dotnet','build',str(project/'Empire.csproj'),'--no-restore','-v:q'],capture_output=True,text=True)
  if build.returncode:raise SystemExit('Compilation is not a killed mutant '+name+'\n'+build.stdout+build.stderr)
  run=subprocess.run([os.environ['GODOT_NET'],'--headless','--path',str(project),'res://Scenes/TacticalHudChecks.tscn'],capture_output=True,text=True,timeout=90)
  target.write_text(original)
  if run.returncode==0:raise SystemExit('SURVIVED '+name)
  output=run.stdout+run.stderr
  expected={'duplicate-submission':'One-shot duplicate signal suppression','disabled-signal':'Disabled emitted signal inert','drop-card-release':'Rebind releases owned crops'}[name]
  if expected not in output:raise SystemExit('Unrelated failure '+name+'\n'+output)
  print('KILLED native '+name,flush=True)
