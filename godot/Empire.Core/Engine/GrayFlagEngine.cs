using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Abilities;
using Empire.Core.Actions;
using Empire.Core.Scenario;
using Empire.Core.Deployment;
using Empire.Core.Transport;
using Empire.Core.Progression;
using Empire.Core.Ai;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Orders;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

namespace Empire.Core.Engine;

/// <summary>Native composition root. The battle owns state; nodes never own gameplay rules.</summary>
public sealed partial class GrayFlagEngine
{
    private readonly JsonArray packs;
    private readonly BattleActionRegistry actionHandlers;
    private readonly ActionExecutionPipeline actionPipeline;
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
    public ScenarioExtensions ScenarioEffects { get; }
    public ScenarioSystem Scenarios { get; }
    public BattleResources Resources { get; } = new();

    public string UnitTypeName(string id) => StateJson.Text(Content.Get("units", id), "name");
    public string CareerName(string id) => StateJson.Text(Content.Get("careers", id), "name");

    public GrayFlagEngine(JsonArray contentPacks)
    {
        packs = Empire.Core.Determinism.EcmaJson.Normalize(contentPacks)!.AsArray();
        Content = new StateContent(packs);
        Catalog = new ReadOnlyContent(Content);
        Grids = new GridRegistry().Register(new Square4Grid()).Register(new Square8Grid()).Register(new OddRowHexGrid()).Seal();
        Space = new DefaultTacticalSpace(packs, Grids);
        Turns = new TurnOrderSystem(packs);
        Transport = new BattleTransport(packs, Grids);
        Directives = UnitDirectiveRegistry.Core().Seal();
        Commands = new TacticalCommands(new(Catalog, Grids, Space), Turns.MayAct, Directives);
        Deployment = new DeploymentSystem(packs, Commands.OwnUnit);
        AreaShapes = WeaponAreaRegistry.Core().Seal();
        Combat = new BattleCombat(packs, Grids, Resources, Space, AreaShapes);
        Support = new SupportOrders(packs, Grids, Resources, Combat.Effects);
        Recruitment = new RecruitmentOrders(packs, Grids, Resources, (state, type, owner, at, done) =>
            BattleStateFactory.SpawnUnit(packs, Grids, state, type, owner, at, done: done));
        Progression = new BattleProgression(packs, Grids, Resources, Commands, Combat.Effects);
        Abilities = DefaultAbilities.Create().Seal();
        AbilityRules = new(Catalog, Space, Combat, Support, Progression.Careers, Abilities);
        AbilityCommands = new(AbilityRules, Commands);
        ScenarioEffects = new ScenarioExtensions(packs, Combat, Resources, (state, source, done) =>
            BattleStateFactory.SpawnUnit(packs, Grids, state, StateJson.Text(source, "unit"),
                StateJson.Integer(source, "owner"), TacticalQueries.Position(source), done: done, source: source));
        Scenarios = new ScenarioSystem(packs, ScenarioEffects.ConditionMet, ScenarioEffects.Apply);
        Victory = new VictorySystem(Content, Scenarios, CompositeRules.ObjectiveOutcome,
            CompositeRules.DescribeObjective, CompositeRules.ObjectiveProgress);
        Ai = new AiPlanner(this, DefaultAiIntents.Create(), DefaultAbilityAiEvaluators.Create(), DefaultAiObjectiveAdvisors.Create());
        ComposePersistence();
        actionHandlers = DefaultActionHandlers.Create().Seal();
        actionPipeline = new(new(Commands, Deployment, Transport, Progression, AbilityCommands,
            Recruitment, Support, Combat, Lifecycle));
    }

    public JsonObject CreateState(JsonObject level, uint? seed = null)
    {
        level = Empire.Core.Determinism.EcmaJson.Normalize(level)!.AsObject();
        definitions.ValidateLevel(level);
        var state = BattleStateFactory.Create(level, packs, seed);
        if (StateJson.Text(state, "phase") == "playing") Lifecycle(state, _ => { }).Start();
        return state;
    }

    public Battle CreateBattle(JsonObject level, uint? seed = null) => new(CreateState(level, seed), actionHandlers, actionPipeline);
    private Battle RestoreBattle(JsonObject state) => new(state, actionHandlers, actionPipeline);

    private BattleLifecycle Lifecycle(JsonObject state, Action<JsonObject> emit) => new(state, packs, emit,
        Combat, Resources, Scenarios, CompositeRules.ObjectiveOutcome, CompositeRules.DescribeObjective,
        CompositeRules.ObjectiveProgress, Victory);

    public IReadOnlyList<CommandOption> CommandsAt(JsonObject state, JsonObject unit, Coord at) =>
        AbilityCommands.Options(state, unit, at);

    public double HealAmount(JsonObject unit) => DefaultAbilities.HealAmount(AbilityRules, unit);
}
