using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Actions;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Abilities;
using Empire.Core.Scenario;
using Empire.Core.Deployment;
using Empire.Core.Transport;
using Empire.Core.Progression;
using Empire.Core.Ai;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Orders;
using Empire.Core.Resources;
using Empire.Core.References;
using Empire.Core.Persistence;
using Empire.Core.Objectives;
using Empire.Core.Statuses;
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

namespace Empire.Core.Engine;

/// <summary>Application-facing native rules. The battle remains the transactional state writer.</summary>
public sealed partial class GrayFlagEngine
{
    private readonly BattleStateRules stateRules;
    private readonly BattleActionRegistry actionHandlers;
    private readonly ActionExecutionPipeline actionPipeline;
    private readonly Func<JsonObject, Action<JsonObject>, BattleLifecycle> createLifecycle;
    public BattleActionRegistry ActionHandlers => actionHandlers;
    public IBattleRandomSource Random { get; }
    public bool HandsOffTurn(JsonObject action) => actionHandlers.HandsOffTurn(action);
    internal StateContent Content { get; }
    public ReadOnlyContent Catalog { get; }
    public VictorySystem Victory { get; }
    public GridRegistry Grids { get; }
    public ITacticalSpace Space { get; }
    public TurnOrderSystem Turns { get; }
    public TacticalCommands Commands { get; }
    public UnitDirectiveRegistry Directives { get; }
    public SupportOrders Support { get; }
    public RecruitmentOrders Recruitment { get; }
    public DeploymentSystem Deployment { get; }
    public BattleTransport Transport { get; }
    public BattleProgression Progression { get; }
    public BattleCombat Combat { get; }
    public AbilityRegistry Abilities { get; }
    public AbilityRules AbilityRules { get; }
    public AbilityCommands AbilityCommands { get; }
    public WeaponAreaRegistry AreaShapes { get; }
    public AiPlanner Ai { get; }
    public StatusBehaviorRegistry StatusBehaviors { get; }
    public IRankProgressionPolicy RankProgression { get; }
    public TurnOrderRegistry TurnOrders { get; }
    public ReactionRegistry Reactions { get; }
    public UnitDepartureHandlerRegistry UnitDepartures { get; }
    public BattleReferenceCheckRegistry ReferenceChecks { get; }
    public IBattleSaveReader Saves { get; }
    public ScenarioConditionHandlerRegistry ScenarioConditions { get; }
    public ScenarioEffectHandlerRegistry ScenarioEffects { get; }
    public ObjectiveHandlerRegistry Objectives { get; }
    public ScenarioCommands ScenarioCommands { get; }
    public ScenarioSystem Scenarios { get; }
    public BattleResources Resources { get; }

    public GrayFlagEngine(JsonArray contentPacks)
        : this(BattleEngineFactory.Compose(new ReadOnlyContent(contentPacks))) { }

    internal GrayFlagEngine(BattleEngineDependencies dependencies)
    {
        Catalog = dependencies.Content;
        Content = Catalog.Source;
        var grids = Grids = dependencies.Grids;
        Space = dependencies.Space;
        var resources = Resources = dependencies.Resources;
        Random = dependencies.Random;
        TurnOrders = dependencies.TurnOrders;
        Turns = new TurnOrderSystem(new TurnOrderRules(Catalog, TurnOrders));
        Reactions = dependencies.Reactions;
        stateRules = new BattleStateRules(Catalog, Grids, Reactions);
        StatusBehaviors = dependencies.StatusBehaviors;
        RankProgression = dependencies.Progression;
        UnitDepartures = dependencies.UnitDepartures;
        ReferenceChecks = dependencies.ReferenceChecks;
        Saves = dependencies.Saves;
        Transport = new BattleTransport(Catalog, grids);
        Directives = dependencies.Directives;
        Commands = new TacticalCommands(new(grids, Space, Reactions), Turns.MayAct, Directives);
        Deployment = new DeploymentSystem(Catalog, Commands.OwnUnit);
        AreaShapes = dependencies.AreaShapes;
        var combat = Combat = new BattleCombat(new BattleCombatRules(Catalog, grids, resources, Space, AreaShapes, dependencies.CombatModifiers, dependencies.HitEffects, Reactions, RankProgression, UnitDepartures));
        Support = new SupportOrders(new SupportOrderRules(Catalog, grids, resources, combat.Effects));
        Recruitment = new RecruitmentOrders(new RecruitmentRules(Catalog, grids, resources, (state, type, owner, at, done) =>
            BattleStateFactory.SpawnUnit(stateRules, state, type, owner, at, done: done)));
        Progression = new BattleProgression(new BattleProgressionRules(Catalog, grids, resources, Commands, combat.Effects));
        Abilities = dependencies.Abilities;
        AbilityRules = new(Catalog, Space, combat, Support, Progression.Careers, Abilities);
        AbilityCommands = new(AbilityRules, Commands);
        ScenarioConditions = dependencies.ScenarioConditions;
        ScenarioEffects = dependencies.ScenarioEffects;
        Objectives = dependencies.Objectives;
        ScenarioCommands = new ScenarioCommands(new ScenarioCommandRules(Catalog, combat, resources, (state, source, done) =>
            BattleStateFactory.SpawnUnit(stateRules, state, StateJson.Text(source, "unit"),
                StateJson.Integer(source, "owner"), TacticalQueries.Position(source), done: done, source: source)));
        var scenarios = Scenarios = new ScenarioSystem(new ScenarioRules(Catalog, ScenarioConditions, ScenarioEffects, ScenarioCommands));
        var victory = Victory = new VictorySystem(new ObjectiveRules(Catalog, ScenarioConditions), Objectives);
        Ai = new AiPlanner(new AiPlanningDependencies(new AiRules(Catalog, grids, Space, AbilityCommands, combat, Support, resources, Directives, Turns, Objectives), dependencies.AbilityAiEvaluators, dependencies.AiObjectiveAdvisors), dependencies.AiIntents);
        var lifecycleRules = new BattleLifecycleRules(Catalog, Turns, scenarios, victory, combat, resources,
            new StatusLifecycle(Catalog, StatusBehaviors, combat));
        createLifecycle = (state, emit) => new BattleLifecycle(lifecycleRules, state, emit);
        actionHandlers = dependencies.ActionHandlers;
        actionPipeline = new(new ActionRules(Commands, Deployment, Transport, Progression, AbilityCommands,
            Recruitment, Support, combat, createLifecycle));
        ComposePersistence(dependencies.PluginManifest);
    }

    public JsonObject CreateState(JsonObject level, uint? seed = null)
    {
        level = Empire.Core.Determinism.EcmaJson.Normalize(level)!.AsObject();
        definitions.ValidateLevel(level);
        var state = BattleStateFactory.Create(stateRules, level, seed);
        if (StateJson.Text(state, "phase") == "playing") createLifecycle(state, _ => { }).Start();
        return state;
    }

    public Battle CreateBattle(JsonObject level, uint? seed = null) => new(CreateState(level, seed), actionHandlers, actionPipeline);
    private Battle RestoreBattle(JsonObject state) => new(state, actionHandlers, actionPipeline);

    public IReadOnlyList<CommandOption> CommandsAt(JsonObject state, JsonObject unit, Coord at) =>
        AbilityCommands.Options(state, unit, at);

    public double HealAmount(JsonObject unit) => DefaultAbilities.HealAmount(Catalog, unit);
}
