using System.Text.Json.Nodes;
using Empire.Core.Combat;
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
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

namespace Empire.Core.Engine;

/// <summary>Native composition root. The battle owns state; nodes never own gameplay rules.</summary>
public sealed partial class GrayFlagEngine
{
    private readonly JsonArray packs;
    private readonly BattleActionRegistry actionHandlers;
    public bool HandsOffTurn(JsonObject action) => actionHandlers.HandsOffTurn(action);
    internal StateContent Content { get; }
    public ReadOnlyContent Catalog { get; }
    public VictorySystem Victory { get; }
    public GridRegistry Grids { get; }
    public MovementRules Movement { get; }
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
        Movement = new MovementRules(packs, Grids);
        Turns = new TurnOrderSystem(packs);
        Transport = new BattleTransport(packs, Movement);
        Directives = UnitDirectiveRegistry.Core().Seal();
        Commands = new TacticalCommands(Movement, Turns.MayAct, Directives);
        Deployment = new DeploymentSystem(packs, Commands.OwnUnit);
        AreaShapes = WeaponAreaRegistry.Core().Seal();
        Combat = new BattleCombat(packs, Grids, Resources, Movement, AreaShapes);
        Support = new SupportOrders(packs, Grids, Resources, Combat.Effects);
        Recruitment = new RecruitmentOrders(packs, Movement, Resources, (state, type, owner, at, done) =>
            BattleStateFactory.SpawnUnit(packs, Grids, state, type, owner, at, done: done));
        Progression = new BattleProgression(packs, Grids, Resources, Commands, Combat.Effects);
        Abilities = DefaultAbilities.Create().Seal();
        AbilityRules = new(Catalog, Movement, Combat, Support, Progression.Careers, Abilities);
        AbilityCommands = new(AbilityRules, Commands);
        ScenarioEffects = new ScenarioExtensions(packs, Movement, Combat, Resources, (state, source, done) =>
            BattleStateFactory.SpawnUnit(packs, Grids, state, StateJson.Text(source, "unit"),
                StateJson.Integer(source, "owner"), TacticalQueries.Position(source), done: done, source: source));
        Scenarios = new ScenarioSystem(packs, ScenarioEffects.ConditionMet, ScenarioEffects.Apply);
        Victory = new VictorySystem(Content, Scenarios, CompositeRules.ObjectiveOutcome,
            CompositeRules.DescribeObjective, CompositeRules.ObjectiveProgress);
        Ai = new AiPlanner(this, DefaultAiIntents.Create(), DefaultAbilityAiEvaluators.Create(), DefaultAiObjectiveAdvisors.Create());
        ComposePersistence();
        actionHandlers = Registry();
    }

    public JsonObject CreateState(JsonObject level, uint? seed = null)
    {
        level = Empire.Core.Determinism.EcmaJson.Normalize(level)!.AsObject();
        definitions.ValidateLevel(level);
        var state = BattleStateFactory.Create(level, packs, seed);
        if (StateJson.Text(state, "phase") == "playing") Lifecycle(state, _ => { }).Start();
        return state;
    }

    public Battle CreateBattle(JsonObject level, uint? seed = null) => new(CreateState(level, seed), actionHandlers);
    private Battle RestoreBattle(JsonObject state) => new(state, actionHandlers);

    private BattleActionRegistry Registry()
    {
        var registry = new BattleActionRegistry();
        foreach (var kind in new[] { "command", "tactic", "reaction", "face", "recruit", "deployUnit", "embark", "disembark", "changeCareer", "changeFormation" })
            registry.Register(kind, ApplyAction);
        registry.Register("endTurn", ApplyAction, handsOffTurn: true);
        registry.Register("finishDeployment", ApplyAction, handsOffTurn: true);
        return registry;
    }

    private BattleLifecycle Lifecycle(JsonObject state, Action<JsonObject> emit) => new(state, packs, emit,
        Combat, Resources, Scenarios, CompositeRules.ObjectiveOutcome, CompositeRules.DescribeObjective,
        CompositeRules.ObjectiveProgress, Victory);

    private void ApplyAction(JsonObject state, JsonObject action, Action<JsonObject> output)
    {
        var phase = StateJson.Text(state, "phase");
        if (phase == "over") throw new IllegalActionException("对局已结束");
        var kind = StateJson.Text(action,"kind");
        var deploymentAction = kind is "deployUnit" or "finishDeployment";
        if (phase == "deployment" && !deploymentAction) throw new IllegalActionException("请先完成战前部署");
        if (phase == "playing" && deploymentAction) throw new IllegalActionException("战斗开始后不能重新部署");
        void Emit(JsonObject value)
        {
            output(value);
            var counts = state["scenario"]!["eventCounts"]!.AsObject();
            var type = StateJson.Text(value, "type");
            counts[type] = StateJson.Number(counts, type, 0) + 1;
        }
        var lifecycle = Lifecycle(state, Emit);
        switch (kind)
        {
            case "deployUnit": Deployment.Deploy(state, StateJson.Integer(action,"unit"), TacticalQueries.Position(action["at"]!.AsObject()), Emit); break;
            case "finishDeployment": Deployment.Confirm(state, lifecycle.BeginPlaying, Emit); break;
            case "embark":
                var passenger = Commands.CommandableUnit(state, StateJson.Integer(action,"unit"), "登载");
                var carrier = Commands.OwnUnit(state, StateJson.Integer(action,"carrier"));
                Transport.Embark(state, StateJson.Integer(passenger,"id"), StateJson.Integer(carrier,"id"), Emit); break;
            case "disembark":
                var disembarking = Commands.CommandableUnit(state, StateJson.Integer(action,"carrier"), "卸载");
                Transport.Disembark(state, StateJson.Integer(disembarking,"id"), StateJson.Integer(action,"unit"), TacticalQueries.Position(action["at"]!.AsObject()), Emit);
                TacticalCommands.FinishCommand(state, StateJson.Integer(disembarking,"id")); break;
            case "changeCareer": Progression.ChangeCareer(state, StateJson.Integer(action,"unit"), StateJson.Text(action,"career"), Emit); break;
            case "changeFormation": Progression.ChangeFormation(state, StateJson.Integer(action,"unit"), action["formation"]?.GetValue<string>(), Emit); break;
            case "command": ExecuteCommand(state, action, Emit); break;
            case "endTurn": lifecycle.AdvanceTurn(); break;
            case "recruit": Recruitment.Recruit(state, StateJson.Text(action,"unit"), TacticalQueries.Position(action["at"]!.AsObject()), Emit); break;
            case "tactic":
                Support.ExecuteTactic(state, StateJson.Text(action, "commander"), StateJson.Text(action, "tactic"),
                    action["target"] is JsonObject target ? TacticalQueries.Position(target) : null, Emit);
                break;
            case "reaction":
                var reacting = Commands.CommandableUnit(state, StateJson.Integer(action, "unit"), "调整反应姿态");
                var stance = StateJson.Text(action, "stance");
                Combat.Forecasts.Reaction(stance);
                reacting["reaction"] = stance;
                Emit(new JsonObject { ["type"] = "reactionChanged", ["unit"] = reacting["id"]!.DeepClone(), ["stance"] = stance });
                break;
            case "face":
                var facing = Commands.CommandableUnit(state, StateJson.Integer(action, "unit"), "调整朝向");
                TacticalCommands.TurnToFace(facing, StateJson.Text(action, "facing"), Emit);
                break;
            default: throw new IllegalActionException("Unsupported action kind");
        }
        if (StateJson.Text(state, "phase") == "playing" && !deploymentAction) lifecycle.AfterAction();
    }

    private void ExecuteCommand(JsonObject state, JsonObject action, Action<JsonObject> emit) =>
        AbilityCommands.Execute(state, action, emit);

    public IReadOnlyList<CommandOption> CommandsAt(JsonObject state, JsonObject unit, Coord at) =>
        AbilityCommands.Options(state, unit, at);

    public double HealAmount(JsonObject unit) => DefaultAbilities.HealAmount(AbilityRules, unit);
}
