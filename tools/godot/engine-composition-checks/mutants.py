#!/usr/bin/env python3
"""Compiled semantic bypasses, plus actual TypeScript root mutants in disposable copies."""
from pathlib import Path
import gzip,json,shutil,subprocess,sys,tempfile
root=Path(__file__).resolve().parents[3]
oracle=Path(sys.argv[1]).resolve(); evidence=Path(sys.argv[2]).resolve();evidence.mkdir(parents=True,exist_ok=True)
expected=json.loads(gzip.decompress(oracle.read_bytes()))
def mutate(file,old,new):
    text=file.read_text()
    if text.count(old)!=1: raise SystemExit(f'Expected exactly one anchor in {file}: {old}')
    file.write_text(text.replace(old,new));return text
native=[
 ('spatial-grid-install-bypass','Composition/DefaultBattlePlugins.cs','new DefaultTacticalSpace(context.Require(Keys.Content), context.Require(Keys.Grids))','new DefaultTacticalSpace(context.Require(Keys.Content), GridRegistry.Core())'),
 ('spatial-grid-undeclared','Composition/DefaultBattlePlugins.cs','provides: [Keys.Space], requiresCapabilities: [Keys.Content, Keys.Grids]','provides: [Keys.Space], requiresCapabilities: [Keys.Content]'),
 ('retained-context-lifetime','Composition/KernelCapabilities.cs','internal void Close() => closed = true;','internal void Close() => closed = false;'),
 ('state-grid-consumer-bypass','Engine/GrayFlagEngine.cs','stateRules = new BattleStateRules(Catalog, Grids, Reactions);','stateRules = new BattleStateRules(Catalog, GridRegistry.Core(), Reactions);'),
 ('state-grid-facing-bypass','State/BattleStateFactory.cs','StateJson.Text(authoredFacing, "facing") : grid.RestingFacing;','StateJson.Text(authoredFacing, "facing") : "south";'),
 ('ignore-custom-plugins','Composition/BattleEngineFactory.cs','kernel.UseAll(plugins);','kernel.UseAll(plugins.Where(_ => false));'),
 ('resource-default-bypass','Engine/GrayFlagEngine.cs','Resources = dependencies.Resources','Resources = new BattleResources()'),
 ('ability-default-bypass','Engine/GrayFlagEngine.cs','Abilities = dependencies.Abilities;','Abilities = DefaultAbilities.Create().Seal();'),
 ('space-default-bypass','Engine/GrayFlagEngine.cs','Space = dependencies.Space;','Space = new DefaultTacticalSpace(Catalog, grids);'),
 ('random-default-bypass','Engine/GrayFlagEngine.cs','Random = dependencies.Random;','Random = new DefaultBattleRandomSource();'),
 ('action-pipeline-bypass','Engine/GrayFlagEngine.cs','new(CreateState(level, seed), actionHandlers, actionPipeline)','new(CreateState(level, seed), actionHandlers)'),
 ('restored-pipeline-bypass','Engine/GrayFlagEngine.cs','new(state, actionHandlers, actionPipeline)','new(state, actionHandlers)'),
 ('omit-seal','Composition/BattleEngineFactory.cs','dependencies.Abilities.Seal();',''),
 ('manifest-default-bypass','Engine/GrayFlagEngine.Persistence.cs','Manifest.Create(Catalog, pluginManifest)','Manifest.Create(Catalog, pluginManifest.Where(p => p.Key.StartsWith("engine.")).ToDictionary(p => p.Key,p => p.Value))'),
 ('modifier-default-bypass','Engine/GrayFlagEngine.cs','dependencies.CombatModifiers, dependencies.HitEffects','DefaultCombatModifiers.Create(), dependencies.HitEffects'),
 ('hit-effect-default-bypass','Engine/GrayFlagEngine.cs','dependencies.CombatModifiers, dependencies.HitEffects','dependencies.CombatModifiers, DefaultWeaponHitEffects.Create()'),
 ('catalog-identity-bypass','Combat/CombatForecasts.cs','modifierRules = new(catalog, grids, Effectiveness);','modifierRules = new(new ReadOnlyContent(packs), grids, Effectiveness);'),
]
for name,old,new in [
 ('status-default-bypass','StatusBehaviors = dependencies.StatusBehaviors;','StatusBehaviors = new StatusBehaviorRegistry();'),
 ('progression-default-bypass','RankProgression = dependencies.Progression;','RankProgression = new ThresholdRankProgressionPolicy();'),
 ('turn-default-bypass','TurnOrders = dependencies.TurnOrders;','TurnOrders = TurnOrderRegistry.Core();'),
 ('reaction-default-bypass','Reactions = dependencies.Reactions;','Reactions = ReactionRegistry.Core();'),
 ('departure-default-bypass','UnitDepartures = dependencies.UnitDepartures;','UnitDepartures = DefaultUnitDepartureHandlers.Create();'),
 ('reference-default-bypass','ReferenceChecks = dependencies.ReferenceChecks;','ReferenceChecks = BattleReferenceCheckRegistry.CreateDefault();'),
 ('reader-default-bypass','Saves = dependencies.Saves;','Saves = new BattleSaveReader();'),
 ('condition-default-bypass','ScenarioConditions = dependencies.ScenarioConditions;','ScenarioConditions = DefaultScenarioConditions.Create(Random);'),
 ('effect-default-bypass','ScenarioEffects = dependencies.ScenarioEffects;','ScenarioEffects = DefaultScenarioEffects.Create();'),
 ('objective-default-bypass','Objectives = dependencies.Objectives;','Objectives = DefaultObjectives.Create();'),
]:
    native.append((name,'Engine/GrayFlagEngine.cs',old,new))
for name,path,old,new in [
 ('status-consumer-bypass','Engine/GrayFlagEngine.cs','new StatusLifecycle(Catalog, StatusBehaviors, combat)','new StatusLifecycle(Catalog, new StatusBehaviorRegistry(), combat)'),
 ('progression-consumer-bypass','Engine/GrayFlagEngine.cs','Reactions, RankProgression, UnitDepartures','Reactions, new ThresholdRankProgressionPolicy(), UnitDepartures'),
 ('turn-consumer-bypass','Engine/GrayFlagEngine.cs','new TurnOrderRules(Catalog, TurnOrders)','new TurnOrderRules(Catalog, TurnOrderRegistry.Core())'),
 ('reaction-consumer-bypass','Engine/GrayFlagEngine.cs','Reactions, RankProgression, UnitDepartures','ReactionRegistry.Core(), RankProgression, UnitDepartures'),
 ('departure-consumer-bypass','Engine/GrayFlagEngine.cs','Reactions, RankProgression, UnitDepartures','Reactions, RankProgression, DefaultUnitDepartureHandlers.Create()'),
 ('reference-consumer-bypass','Engine/GrayFlagEngine.Persistence.cs','new BattleDefinitionValidator(references, ReferenceChecks, Grids)','new BattleDefinitionValidator(references, BattleReferenceCheckRegistry.CreateDefault(), Grids)'),
 ('reader-consumer-bypass','Engine/GrayFlagEngine.Persistence.cs','new BattlePersistence(environment, Saves)','new BattlePersistence(environment, new BattleSaveReader())'),
 ('condition-consumer-bypass','Engine/GrayFlagEngine.cs','new ScenarioRules(Catalog, ScenarioConditions, ScenarioEffects, ScenarioCommands)','new ScenarioRules(Catalog, DefaultScenarioConditions.Create(Random), ScenarioEffects, ScenarioCommands)'),
 ('effect-consumer-bypass','Engine/GrayFlagEngine.cs','new ScenarioRules(Catalog, ScenarioConditions, ScenarioEffects, ScenarioCommands)','new ScenarioRules(Catalog, ScenarioConditions, DefaultScenarioEffects.Create(), ScenarioCommands)'),
 ('objective-consumer-bypass','Engine/GrayFlagEngine.cs','new VictorySystem(new ObjectiveRules(Catalog, ScenarioConditions), Objectives)','new VictorySystem(new ObjectiveRules(Catalog, ScenarioConditions), DefaultObjectives.Create())'),
]:
    native.append((name,path,old,new))
for name in ['StatusBehaviors','TurnOrders','Reactions','UnitDepartures','ReferenceChecks','ScenarioConditions','ScenarioEffects','Objectives']:
    native.append((f'omit-{name}-seal','Composition/BattleEngineFactory.cs',f'dependencies.{name}.Seal();',''))

if len(sys.argv)>4:
    native=[entry for entry in native if sys.argv[4] in entry[0]]
with tempfile.TemporaryDirectory(prefix='empire-root-native-') as temp:
    target=Path(temp)
    for folder in ['godot/Empire.Core','tools/godot/engine-composition-checks','tools/godot/check-support']:
        shutil.copytree(root/folder,target/folder,ignore=shutil.ignore_patterns('bin','obj'))
    project=target/'tools/godot/engine-composition-checks/EngineCompositionChecks.csproj'
    for name,path,old,new in native:
        file=target/'godot/Empire.Core'/path;original=mutate(file,old,new)
        build=subprocess.run(['dotnet','build',str(project),'-v','quiet'],capture_output=True,text=True)
        (evidence/f'native-{name}-build.log').write_text(build.stdout+build.stderr)
        if build.returncode: raise SystemExit(f'INVALID mutant {name}: did not compile\n{build.stdout}{build.stderr}')
        run=subprocess.run(['dotnet','run','--no-build','--project',str(project),'--',str(oracle),str(root)],capture_output=True,text=True)
        file.write_text(original);(evidence/f'native-{name}.log').write_text(run.stdout+run.stderr)
        if run.returncode==0:raise SystemExit(f'SURVIVED native {name}')
        print(f'KILLED native {name}',flush=True)
if len(sys.argv)>3 and sys.argv[3]=='true':
    print('Frozen mode: source mutation controls were not run',flush=True)
    sys.exit(0)
source_mutants=[
 ('ignore-custom-plugins','plugins/default.ts','createDefaultMicrokernel(content).useAll(plugins)','createDefaultMicrokernel(content).useAll(plugins.filter(() => false))'),
 ('lose-composed-manifest','plugins/default.ts','}, capabilities.pluginManifest);','}, new Map());'),
 ('omit-composition-seal','plugins/default.ts','  sealBattleCapabilities(capabilities);','  void capabilities;'),
 ('bypass-scenario-random','scenario.ts',"random.chance(state, condition.stream ?? 'scenario.chance', condition.percent)",'(void random, void state, void condition, false)'),
]
with tempfile.TemporaryDirectory(prefix='empire-root-source-') as temp:
    target=Path(temp);source=target/'packages/battle-engine/src';shutil.copytree(root/'packages/battle-engine/src',source)
    harness=target/'tools/godot/engine-composition-checks';harness.mkdir(parents=True)
    shutil.copy2(root/'tools/godot/engine-composition-checks/oracle.ts',harness/'oracle.ts')
    for name,path,old,new in source_mutants:
        file=source/path;original=mutate(file,old,new);output=evidence/f'source-{name}.json.gz'
        run=subprocess.run(['node',str(root/'node_modules/vite-node/vite-node.mjs'),str(harness/'oracle.ts'),str(output)],cwd=root,capture_output=True,text=True)
        file.write_text(original);(evidence/f'source-{name}.log').write_text(run.stdout+run.stderr)
        if run.returncode:
            # Removing registration must make the real root reject the named custom command.
            if name!='ignore-custom-plugins' or '没有处理行动类型' not in run.stderr:raise SystemExit(f'INVALID source mutant {name}: {run.stderr}')
        elif json.loads(gzip.decompress(output.read_bytes()))==expected:raise SystemExit(f'SURVIVED source {name}')
        print(f'KILLED source {name}',flush=True)
