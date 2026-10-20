using System.Text.Json.Nodes;
using Empire.Core.Combat;
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

public sealed record CommandOption(string Key, string Ability, string? Weapon, string Name,
    string Hint, bool SelfTargeted, IReadOnlyList<Coord> Targets, string? Engagement = null);

/// <summary>Native composition root. The battle owns state; nodes never own gameplay rules.</summary>
public sealed partial class GrayFlagEngine
{
    public static IReadOnlyList<string> SupportedAbilities { get; } = System.Array.AsReadOnly(new[] { "wait", "capture", "attack", "heal" });
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
        Support = new SupportOrders(packs, Grids);
        Recruitment = new RecruitmentOrders(packs, Movement, Resources, (state, type, owner, at, done) =>
            BattleStateFactory.SpawnUnit(packs, Grids, state, type, owner, at, done: done));
        AreaShapes = WeaponAreaRegistry.Core().Seal();
        Combat = new BattleCombat(packs, Grids, Resources, Movement, AreaShapes);
        Progression = new BattleProgression(packs, Grids, Resources, Commands, Combat.Effects);
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

    private void ExecuteCommand(JsonObject state, JsonObject action, Action<JsonObject> emit)
    {
        var movement = Commands.ValidateMovement(state, action);
        var unit = TacticalQueries.RequireUnit(state, movement.UnitId);
        var command = StateJson.Object(action["command"], "command");
        var ability = StateJson.Text(command, "ability");
        var name = AbilityName(ability);
        if (!Progression.Careers.UnitAbilityIds(unit).Contains(ability))
            throw new IllegalActionException($"{StateJson.Text(Content.Get("units", StateJson.Text(unit, "type")), "name")} 没有「{name}」");
        var weapon = command["weapon"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(weapon) && (ability != "attack" ||
            !Combat.Forecasts.UnitWeapons(unit).Any(candidate => StateJson.Text(candidate, "id") == weapon)))
            throw new IllegalActionException($"「{name}」无法使用「{weapon}」");
        if (!CanUseAbility(state, unit, movement.Destination, ability, weapon))
            throw new IllegalActionException($"此处无法使用「{name}」");
        Coord? target = command["target"] is JsonObject targetObject ? TacticalQueries.Position(targetObject) : null;
        if (ability is "attack" or "heal")
        {
            if (target is null) throw new IllegalActionException($"「{name}」需要指定目标");
            var targets = ability == "attack" ? Combat.AttackTargets(state, unit, movement.Destination, weapon)
                : HealTargets(state, unit, movement.Destination);
            if (!targets.Contains(target.Value)) throw new IllegalActionException("目标不合法");
        }
        var surviving = Commands.ApplyMovement(state, movement, emit, Combat.ResolvePartingShots);
        if (surviving is null) return;
        if (target is { } at) TacticalCommands.TurnToFace(surviving, Movement.Grid(state).Toward(movement.Destination, at), emit);
        switch (ability)
        {
            case "wait": break;
            case "capture": Support.Capture(state, surviving, movement.Destination, emit); break;
            case "attack": Combat.ExecuteAttack(state, surviving, movement.Destination, target!.Value, weapon, emit); break;
            case "heal": Heal(state, surviving, target!.Value, emit); break;
            default: throw new DomainInvariantException($"Unsupported ability \"{ability}\"");
        }
        TacticalCommands.FinishCommand(state, movement.UnitId);
    }

    private static string AbilityName(string id) => id switch
    {
        "wait" => "待机", "capture" => "占领", "attack" => "攻击", "heal" => "治疗",
        _ => throw new DomainInvariantException($"unknown ability \"{id}\""),
    };

    private bool CanUseAbility(JsonObject state, JsonObject unit, Coord at, string ability, string? weapon = null) => ability switch
    {
        "attack" => Combat.CanUseAttack(state, unit, at, at != TacticalQueries.Position(unit), weapon),
        "capture" => Support.CanCapture(state, unit, at),
        "heal" => Combat.Effects.BlockedAbilityStatus(unit, new[] { "healing" }) is null,
        "wait" => true,
        _ => throw new DomainInvariantException($"unknown ability \"{ability}\""),
    };

    public IReadOnlyList<CommandOption> CommandsAt(JsonObject state, JsonObject unit, Coord at)
    {
        var abilities = Progression.Careers.UnitAbilityIds(unit);
        var options = new List<CommandOption>();
        foreach (var ability in abilities)
        {
            switch (ability)
            {
                case "wait": options.Add(new("wait", "wait", null, AbilityName(ability), "结束该单位的行动。", true, [])); break;
                case "capture":
                    if (CanUseAbility(state, unit, at, ability)) options.Add(new("capture", "capture", null, AbilityName(ability), "占领此处据点。", true, []));
                    break;
                case "attack":
                    foreach (var weapon in Combat.Forecasts.UnitWeapons(unit))
                    {
                        var id = StateJson.Text(weapon, "id");
                        if (!CanUseAbility(state, unit, at, ability, id)) continue;
                        var targets = Combat.AttackTargets(state, unit, at, id);
                        if (targets.Count > 0) options.Add(new($"attack:{id}", "attack", id,
                            $"{AbilityName(ability)} · {StateJson.Text(weapon, "name")}", "", false, targets, "attack"));
                    }
                    break;
                case "heal":
                    if (!CanUseAbility(state, unit, at, ability)) break;
                    var allies = HealTargets(state, unit, at);
                    if (allies.Count > 0) options.Add(new("heal", "heal", null, AbilityName(ability), "恢复相邻友军的生命值。", false, allies));
                    break;
                default: throw new DomainInvariantException($"Unsupported ability \"{ability}\"");
            }
        }
        int Priority(string id) => id switch { "capture" => 5, "attack" => 10, "heal" => 20, "wait" => 90, _ => 50 };
        return options.OrderBy(option => Priority(option.Ability)).ThenBy(option => option.Key, StringComparer.Ordinal).ToArray();
    }

    private IReadOnlyList<Coord> HealTargets(JsonObject state, JsonObject unit, Coord from) =>
        Movement.HealTargetsFrom(state, unit, from).Select(TacticalQueries.Position).ToArray();

    public double HealAmount(JsonObject unit) => Math.Min(30,
        StateJson.Number(Content.Get("units", StateJson.Text(unit, "type"))["maxHp"], "maxHp") - StateJson.Number(unit["hp"], "hp"));

    private void Heal(JsonObject state, JsonObject source, Coord target, Action<JsonObject> emit)
    {
        var unit = TacticalQueries.UnitAt(state, target) ?? throw new DomainInvariantException("no unit to heal");
        var maximum = StateJson.Number(Content.Get("units", StateJson.Text(unit, "type"))["maxHp"], "maxHp");
        var hp = StateJson.Number(unit["hp"], "hp");
        var amount = Math.Min(Math.Max(0, StateJson.Round(HealAmount(unit))), Math.Max(0, maximum - hp));
        unit["hp"] = hp + amount;
        emit(new JsonObject { ["type"] = "heal", ["source"] = source["id"]!.DeepClone(), ["target"] = unit["id"]!.DeepClone(), ["amount"] = amount });
        Combat.Effects.AwardRankProgress(source, Math.Max(5, amount), emit);
    }

}
