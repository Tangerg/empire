#!/usr/bin/env python3
from pathlib import Path
import shutil, subprocess, sys, tempfile
root = Path(__file__).resolve().parents[3]
fixture = Path(sys.argv[1]).resolve()
work = Path(tempfile.mkdtemp(prefix='empire-action-mutants.', dir=None))
core = work / 'godot/Empire.Core'
harness = work / 'tools/godot/action-extension-checks'
shutil.copytree(root / 'godot/Empire.Core', core, ignore=shutil.ignore_patterns('bin', 'obj'))
shutil.copytree(root / 'tools/godot/action-extension-checks', harness, ignore=shutil.ignore_patterns('bin', 'obj'))
command = ['dotnet', 'run', '--project', str(harness), '--', str(fixture), str(work)]
def run(name):
    result = subprocess.run(command, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (work / (name + '.log')).write_text(result.stdout)
    return result
if run('baseline').returncode: raise SystemExit('Isolated baseline failed: ' + str(work))
mutants = [
 ('deployment-metadata', 'Actions/ActionExecutionPipeline.cs', 'var deployment = registration?.DuringDeployment == true;', 'var deployment = false;'),
 ('event-count-owner', 'Actions/ActionExecutionContext.cs', 'counts[type] = StateJson.Number(counts, type, 0) + 1;', 'counts[type] = StateJson.Number(counts, type, 0);'),
 ('after-action', 'Actions/ActionExecutionPipeline.cs', 'context.Lifecycle.AfterAction();', '{}'),
 ('deployment-after-action', 'Actions/ActionExecutionPipeline.cs', '&& !deployment)', ')'),
 ('over-phase', 'Actions/ActionExecutionPipeline.cs', 'if (phase == "over")', 'if (phase == "never")'),
 ('unknown-precedence', 'Actions/ActionExecutionPipeline.cs', 'var phase = StateJson.Text(state, "phase");', 'if (registration is null) throw new IllegalActionException("early unknown"); var phase = StateJson.Text(state, "phase");'),
]
for name, relative, before, after in mutants:
    path = core / relative
    original = path.read_text()
    if before not in original: raise SystemExit('Missing mutant anchor: ' + name)
    path.write_text(original.replace(before, after))
    try:
        result = run(name)
        if result.returncode == 0 or 'error CS' in result.stdout or 'build failed' in result.stdout.lower():
            raise SystemExit('Mutant survived or did not compile: ' + name + '; ' + str(work))
        print('REJECTED ' + name, flush=True)
    finally: path.write_text(original)
print('PASS 6 compiled action pipeline mutants; ' + str(work))
