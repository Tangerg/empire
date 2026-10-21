#!/usr/bin/env python3
"""Narrow guards for a sole composition recipe and explicit resolved consumers."""
from pathlib import Path
import re, sys
root=Path(sys.argv[1] if len(sys.argv)>1 else Path(__file__).resolve().parents[3])
engine=(root/'godot/Empire.Core/Engine/GrayFlagEngine.cs').read_text()
persistence=(root/'godot/Empire.Core/Engine/GrayFlagEngine.Persistence.cs').read_text()
for forbidden in [r'new\s+(?:BattleResources|GridRegistry|DefaultTacticalSpace|DefaultBattleRandomSource|StatusBehaviorRegistry|ThresholdRankProgressionPolicy|BattleSaveReader)\s*\(',
                  r'Default(?:Abilities|AiIntents|AbilityAiEvaluators|AiObjectiveAdvisors|CombatModifiers|WeaponHitEffects)\.Create\s*\(',
                  r'new\s+AiPlanner\s*\(\s*this\b',r'\b(?:ApplyAction|Registry)\s*\(']:
    if re.search(forbidden,engine): raise SystemExit(f'Engine bypasses composition/owner boundary: {forbidden}')
if any(token in persistence for token in ['SupportedPolicies', 'SupportedConditions', 'SupportedEffects', 'SupportedObjectives', 'SupportedHitEffects', '.Register(']):
    raise SystemExit('Persistence uses frozen rule IDs instead of resolved capabilities')
if 'engine.tactical-rules' in persistence or 'engine.content' in persistence:
    raise SystemExit('Persistence carries a second fixed plugin recipe')
if 'BattleEngineFactory.Compose' not in engine:
    raise SystemExit('Convenience constructor bypasses sole composition path')
if 'new SplitMixRandom' in (root/'godot/Empire.Core/Lifecycle/ScenarioSystem.cs').read_text():
    raise SystemExit('Scenario chance bypasses composed full-state random policy')
lifecycle=(root/'godot/Empire.Core/Lifecycle/BattleLifecycle.cs').read_text()
if 'BattleLifecycleRules rules' not in lifecycle or re.search(r'new\s+(?:TurnOrderSystem|ScenarioSystem|VictorySystem|BattleResources|StatusLifecycle)\s*\(',lifecycle):
    raise SystemExit('Lifecycle recreates defaults instead of consuming its explicit rules')
for path in (root/'godot/Empire.Core/Ai').glob('*.cs'):
    if 'GrayFlagEngine' in path.read_text(): raise SystemExit(f'AI captures the engine: {path.name}')
print('PASS canonical recipe, actual manifest, action owner and AI port guards')
