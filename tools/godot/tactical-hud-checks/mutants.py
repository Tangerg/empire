"""Deliberately break presentation semantics in isolated source copies, never the worktree."""
from pathlib import Path
import subprocess, sys, tempfile
root=Path.cwd()
source=(root/'godot/Application/GrayFlagSession.Hud.cs').read_text()
mutations={
 'forecast-outside-candidates':(' && selection.Targets.Contains(target)',''),
 'forecast-without-weapon':('selection.Weapon is { } weapon','(selection.Weapon ?? "c01.border-blade") is { } weapon'),
 'forecast-ability-hardcoded':('if (selection.Kind == TacticalSelectionKind.Target && selected','if (selection.Ability == "attack" && selection.Kind == TacticalSelectionKind.Target && selected'),
 'inspected-controls-use-selected-id':(' && N(commandable,"id") == N(inspect,"id")',''),
 'busy-formation-options-lost':('var commandable = human','var commandable = !busy && human'),
 'no-human-objective-owner':('N(state["players"]!.AsArray().OfType<JsonObject>().FirstOrDefault(player => S(player,"controller") == "human") ?? state["players"]![0]!.AsObject(),"id")','Viewer(state)'),
 'hidden-selected-id':('selectedId && visible.Contains(selectedId)','selectedId'),
 'drop-disabled-careers':('engine.Progression.Careers.Options(state,commandable).Select(', 'engine.Progression.Careers.Options(state,commandable).Where(option => option.Eligible).Select('),
 'lose-current-formation-removal':('option.Eligible || option.Current,option.Current','option.Eligible,option.Current'),
 'recruit-full-legality-instead-of-affordability':('enabled:card.Affordable','enabled:engine.Recruitment.Refusal(state,card.Unit,new(recruitAt.X,recruitAt.Y)) is null'),
 'forecast-recipient-is-intended-defender':('TacticalQueries.UnitById(state,exchange.DamageRecipient) ?? defender','defender'),
 'erase-command-hints':('hint:command.Hint','hint:""'),
 'unbounded-null-account':('account.Current is null ? "∞"','account.Current is null ? "0"'),
 'drop-forecast':('Section("forecast","战斗预测",rows);',''),
}
for name,(old,new) in mutations.items():
 if old not in source: raise SystemExit('Mutation no longer applies: '+name)
 with tempfile.TemporaryDirectory(prefix='hud-mutant-') as folder:
  Path(folder,'GrayFlagSession.Hud.cs').write_text(source.replace(old,new))
  result=subprocess.run(['dotnet','run','--project','tools/godot/tactical-hud-checks','-p:HudRoot='+folder,'--',sys.argv[1],sys.argv[2]],capture_output=True,text=True)
  if result.returncode==0: raise SystemExit('SURVIVED '+name)
  if 'error CS' in result.stdout+result.stderr: raise SystemExit('Compile failure is not a killed mutant: '+name+'\n'+result.stdout+result.stderr)
  print('KILLED '+name)
