"""Every negative control must compile and fail a source-parity assertion, never a build/import failure."""
from pathlib import Path
import shutil
import subprocess
import sys

root, scratch, oracle = map(Path, sys.argv[1:])
mutants = [
    ('bypass-resolved-modifiers', 'CombatForecasts.cs', 'var result = Modifiers.Evaluate(', 'var result = DefaultCombatModifiers.Create().Evaluate('),
    ('bypass-resolved-hit-effects', 'BattleCombat.Execution.cs', 'HitEffects.Apply(hitEffectRules, state, attacker, target, effects, emit);', 'DefaultWeaponHitEffects.Create().Apply(hitEffectRules, state, attacker, target, effects, emit);'),
    ('lose-final-affine-offset', 'CombatModifierPolicies.cs', 'finalOffset *= modifier.Value;', 'finalOffset += 0;'),
    ('hardcoded-cap', 'CombatModifierPolicies.cs', 'Math.Min(mitigationCap, mitigation)', 'Math.Min(0.6, mitigation)'),
    ('ignore-priority', 'CombatModifierPolicies.cs', 'providers.Ordered().SelectMany', 'providers.All().SelectMany'),
    ('omit-default-strength', 'CombatModifierPolicies.cs', '        .Register(new("core.attacker-strength", 300, Strength))\n', ''),
    ('reverse-hit-order', 'WeaponHitEffectPolicies.cs', 'foreach (var effect in effects)', 'foreach (var effect in effects.Reverse())'),
    ('lose-shared-effect-context', 'WeaponHitEffectPolicies.cs', '.Apply(context, effect);', '.Apply(new WeaponHitEffectContext(rules, state, attacker, target, emit), effect);'),
    ('run-effects-after-death', 'BattleCombat.Execution.cs', 'if (outcome.LeftField) return outcome;', '// mutant: ignore departure'),
    ('bypass-content-admission', 'Validation/BattleContentValidator.cs', '                    rules.HitEffects.Validate(rules.Content, effect, owner);', '                    _ = owner;',),
    ('bypass-handler-admission', 'WeaponHitEffectPolicies.cs', 'Get(StateJson.Text(effect, "type")).Validate(content, effect, owner);', '_ = content;',),
    ('bypass-execution-admission', 'WeaponHitEffectPolicies.cs', '            Validate(rules.Content, effect, "weapon hit effect");', '',),
    ('drop-status-source', 'WeaponHitEffectPolicies.cs', 'StateJson.Integer(context.Attacker, "id"), context.Emit)', 'null, context.Emit)'),
    ('drop-forced-collision-damage', 'WeaponHitEffectPolicies.cs', 'StateJson.Number(effect, "collisionDamage", 0), context.Emit)', '0, context.Emit)'),
]
for name, file, before, after in mutants:
    destination = scratch / name
    shutil.copytree(root / 'godot/Empire.Core', destination, ignore=shutil.ignore_patterns('bin', 'obj'))
    source = destination / (file if '/' in file else 'Combat/' + file)
    text = source.read_text()
    if text.count(before) != 1:
        raise RuntimeError(f'{name}: exact mutation anchor count is {text.count(before)}')
    source.write_text(text.replace(before, after))
    build = subprocess.run(['dotnet', 'build', str(scratch / 'CombatPolicyChecks.csproj'), '--configfile', str(scratch / 'NuGet.Config'), '--disable-build-servers', '--nologo', '-m:1', '-p:UseSharedCompilation=false', f'-p:EmpireRoot={root}', f'-p:EmpireCoreRoot={destination}'], capture_output=True, text=True)
    (destination / 'build.log').write_text(build.stdout + build.stderr)
    if build.returncode:
        raise RuntimeError(f'{name}: mutant did not compile; see {destination}/build.log')
    run = subprocess.run(['dotnet', str(scratch / 'bin/Debug/net8.0/CombatPolicyChecks.dll'), str(oracle)], capture_output=True, text=True)
    (destination / 'run.log').write_text(run.stdout + run.stderr)
    if run.returncode == 0 or 'Mismatch in ' not in run.stdout + run.stderr:
        raise RuntimeError(f'{name}: missing expected source-parity assertion failure; see {destination}/run.log')
    print(f'PASS mutant {name}: compiled and rejected by source parity', flush=True)
print(f'PASS all {len(mutants)} compiled combat policy mutants', flush=True)
