import shutil, pathlib, subprocess, sys, tempfile

# Build each deliberately broken copy separately. Compiler failure is not a pass.
candidate = pathlib.Path(__file__).resolve().parents[3]
fixture = pathlib.Path(sys.argv[1]).resolve()
base = pathlib.Path(tempfile.mkdtemp(prefix='empire-reference-mutations-'))
print('Mutation logs:', base, flush=True)
mutations={
 'bypass-writer-reader':('Persistence/BattlePersistence.cs','try { return reader.Load(document, environment); }','try { return document; }','named reader override'),
 'erase-state-references':('Persistence/BattleSaveReader.References.cs','var issues = environment.ReferenceChecks.StateIssues(environment.Rules, State(save));','var issues = (IReadOnlyList<string>)Array.Empty<string>();','unknown current learned ability'),
 'reverse-check-order':('References/BattleReferenceCheckRegistry.cs','foreach (var check in All())','foreach (var check in All().Reverse())','reference phase order and first seen dedupe'),
 'guess-status-payload':('Persistence/BattleSaveReader.cs','    private void CheckBattlefield(JsonObject save)\n    {','    private void CheckBattlefield(JsonObject save)\n    {\n        foreach (var weapon in content.All("weapons"))\n            foreach (var effect in StateJson.Objects(weapon["hitEffects"], "hit effects"))\n                if (effect["status"] is { } status) RequireContent("statuses", StateJson.Text(status, "status"), "weapon");','custom hit effect owns opaque status-named payload'),
}
for name,(file,old,new,expected) in mutations.items():
 target=base/'mutation-controls'/name
 shutil.copytree(candidate/'godot/Empire.Core',target/'godot/Empire.Core',ignore=shutil.ignore_patterns('bin','obj'),dirs_exist_ok=True)
 shutil.copytree(candidate/'tools/godot/persistence-reference-checks',target/'tools/godot/persistence-reference-checks',ignore=shutil.ignore_patterns('bin','obj'),dirs_exist_ok=True)
 (target/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
 path=target/'godot/Empire.Core'/file;s=path.read_text();assert s.count(old)==1,(name,s.count(old));path.write_text(s.replace(old,new))
 cmd=['bash','-lc',f'source "{candidate}/tools/godot/env.sh"; dotnet run --project "{target}/tools/godot/persistence-reference-checks/ReferenceChecks.csproj" -- "{candidate}" "{fixture}"']
 run=subprocess.run(cmd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True);(base/f'mutation-{name}.log').write_text(run.stdout)
 assert run.returncode!=0,(name,'mutation survived')
 assert 'error CS' not in run.stdout,(name,'compile failure is not a detected semantic defect')
 assert any(line.startswith('FAIL ') and expected in line for line in run.stdout.splitlines()),(name,'required detector not fired')
 print('DETECTED',name,'by',expected,flush=True)
