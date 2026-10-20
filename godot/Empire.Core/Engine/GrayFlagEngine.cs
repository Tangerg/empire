using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Orders;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

namespace Empire.Core.Engine;

public sealed record CommandOption(string Key, string Ability, string? Weapon, string Name,
    string Hint, bool SelfTargeted, IReadOnlyList<Coord> Targets);

/// <summary>Native composition root. The battle owns state; nodes never own gameplay rules.</summary>
public sealed class GrayFlagEngine
{
    private readonly JsonArray packs;
    internal StateContent Content { get; }
    public GridRegistry Grids { get; }
    public MovementRules Movement { get; }
    public TurnOrderSystem Turns { get; }
    public TacticalCommands Commands { get; }
    public SupportOrders Support { get; }
    public BattleCombat Combat { get; }
    public BattleResources Resources { get; } = new();

    public GrayFlagEngine(JsonArray contentPacks)
    {
        packs = (JsonArray)contentPacks.DeepClone();
        Content = new StateContent(packs);
        Grids = new GridRegistry().Register(new Square4Grid()).Register(new Square8Grid()).Register(new OddRowHexGrid()).Seal();
        Movement = new MovementRules(packs, Grids);
        Turns = new TurnOrderSystem(packs);
        Commands = new TacticalCommands(Movement, Turns.MayAct, DirectiveArrivalRegistry.Core());
        Support = new SupportOrders(packs, Grids);
        Combat = new BattleCombat(packs, Grids, Resources, Movement);
    }

    public JsonObject CreateState(JsonObject level, uint? seed = null)
    {
        var state = BattleStateFactory.Create(level, packs, seed);
        if (StateJson.Text(state, "phase") == "playing") Lifecycle(state, _ => { }).Start();
        return state;
    }

    public Battle CreateBattle(JsonObject level, uint? seed = null) => new(CreateState(level, seed), Registry());
    public Battle RestoreBattle(JsonObject state) => new(state, Registry());

    private BattleActionRegistry Registry()
    {
        var registry = new BattleActionRegistry();
        foreach (var kind in new[] { "command", "endTurn", "tactic", "reaction", "face" })
            registry.Register(kind, ApplyAction);
        return registry;
    }

    private BattleLifecycle Lifecycle(JsonObject state, Action<JsonObject> emit) => new(state, packs, emit,
        Combat, Resources);

    private void ApplyAction(JsonObject state, JsonObject action, Action<JsonObject> output)
    {
        var phase = StateJson.Text(state, "phase");
        if (phase == "over") throw new IllegalActionException("对局已结束");
        if (phase == "deployment") throw new IllegalActionException("请先完成战前部署");
        void Emit(JsonObject value)
        {
            output(value);
            var counts = state["scenario"]!["eventCounts"]!.AsObject();
            var type = StateJson.Text(value, "type");
            counts[type] = StateJson.Number(counts, type, 0) + 1;
        }
        var lifecycle = Lifecycle(state, Emit);
        switch (StateJson.Text(action, "kind"))
        {
            case "command": ExecuteCommand(state, action, Emit); break;
            case "endTurn": lifecycle.AdvanceTurn(); break;
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
        if (StateJson.Text(state, "phase") == "playing") lifecycle.AfterAction();
    }

    private void ExecuteCommand(JsonObject state, JsonObject action, Action<JsonObject> emit)
    {
        var movement = Commands.ValidateMovement(state, action);
        var unit = TacticalQueries.RequireUnit(state, movement.UnitId);
        var command = StateJson.Object(action["command"], "command");
        var ability = StateJson.Text(command, "ability");
        var weapon = command["weapon"]?.GetValue<string>();
        if (ability == "attack" && weapon is null) weapon = StateJson.Text(Combat.Forecasts.PrimaryWeapon(unit), "id");
        var option = CommandsAt(state, unit, movement.Destination)
            .FirstOrDefault(candidate => candidate.Ability == ability &&
                (weapon is null || candidate.Weapon == weapon));
        if (option is null) throw new IllegalActionException($"此处无法使用「{ability}」");
        Coord? target = command["target"] is JsonObject targetObject ? TacticalQueries.Position(targetObject) : null;
        if (!option.SelfTargeted && (target is null || !option.Targets.Contains(target.Value)))
            throw new IllegalActionException("目标不合法");
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

    public IReadOnlyList<CommandOption> CommandsAt(JsonObject state, JsonObject unit, Coord at)
    {
        var definition = Content.Get("units", StateJson.Text(unit, "type"));
        var abilities = definition["abilities"]!.AsArray().Concat(unit["learnedAbilities"]!.AsArray())
            .Select(value => StateJson.Text(value, "ability")).Distinct(StringComparer.Ordinal);
        var options = new List<CommandOption>();
        foreach (var ability in abilities)
        {
            switch (ability)
            {
                case "wait": options.Add(new("wait", "wait", null, "待机", "结束该单位的行动。", true, [])); break;
                case "capture":
                    if (Support.CanCapture(state, unit, at)) options.Add(new("capture", "capture", null, "占领", "占领此处据点。", true, []));
                    break;
                case "attack":
                    foreach (var weapon in Combat.Forecasts.UnitWeapons(unit))
                    {
                        var id = StateJson.Text(weapon, "id");
                        if (!Combat.CanUseAttack(state, unit, at, at != TacticalQueries.Position(unit), id)) continue;
                        var targets = Combat.AttackTargets(state, unit, at, id);
                        if (targets.Count > 0) options.Add(new($"attack:{id}", "attack", id,
                            $"攻击 · {StateJson.Text(weapon, "name")}", "", false, targets));
                    }
                    break;
                case "heal":
                    if (Combat.Effects.BlockedAbilityStatus(unit, new[] { "healing" }) is not null) break;
                    var allies = HealTargets(state, unit, at);
                    if (allies.Count > 0) options.Add(new("heal", "heal", null, "治疗", "恢复相邻友军的生命值。", false, allies));
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
