using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Abilities;
using Empire.Core.Ai;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Orders;
using Empire.Core.Progression;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;

using var file = File.OpenRead(args[0]);
using var reader = args[0].EndsWith(".gz", StringComparison.Ordinal) ? new StreamReader(new GZipStream(file, CompressionMode.Decompress)) : new StreamReader(file);
var input = EcmaJson.Parse(reader.ReadToEnd())!.AsObject();
var checks = 0;
void Check(string name, string part, JsonNode? actual, JsonNode? expected)
{
    checks++;
    if (EcmaJson.DeepEquals(actual, expected)) return;
    var path = Path.Combine(Path.GetDirectoryName(args[0])!, "failed-ai-planning.json");
    File.WriteAllText(path, new JsonObject { ["case"] = name, ["part"] = part, ["actual"] = actual?.DeepClone(), ["expected"] = expected?.DeepClone() }.ToJsonString(new() { WriteIndented = true }));
    throw new Exception($"Mismatch in {name}: {part}; inspect {path}");
}
foreach (var item in input["cases"]!.AsArray().OfType<JsonObject>())
{
    var name = Text(item, "name");
    if (args.Length > 1 && name != args[1]) continue;
    var config = item["config"]!.AsObject(); var state = item["state"]!.DeepClone().AsObject();
    var packs = input["packs"]!.DeepClone().AsArray();
    JsonObject Definition(string family, string id) => packs.OfType<JsonObject>().SelectMany(pack => pack[family]?.AsArray().OfType<JsonObject>() ?? []).First(value => Text(value, "id") == id);
    if (IsTrue(config["resourceWeapon"]))
    {
        var weapon = Definition("weapons", "soldier_sword");
        weapon["resourceRequirements"] = JsonNode.Parse("[{\"subject\":\"squad\",\"resource\":\"charge\",\"amount\":1}]");
        weapon["resourceCosts"] = weapon["resourceRequirements"]!.DeepClone();
    }
    if (IsTrue(config["tactic"]))
    {
        var tactic = Definition("tactics", "rally"); tactic["target"] = "self"; tactic["radius"] = 0;
        tactic["costs"] = JsonNode.Parse("[{\"resource\":\"command_points\",\"amount\":2}]");
    }
    var content = new ReadOnlyContent(packs);
    var grids = new GridRegistry().Register(new Square4Grid()).Register(new Square8Grid()).Register(new OddRowHexGrid());
    var space = new TestSpace(content, grids, config);
    var resources = new BattleResources();
    if (config["aiWeight"] is { } weight) resources.Adapters.Replace(resources.Adapters.Get("command_points") with { AiWeight = Number(weight, "aiWeight") });
    if (config["fundsAvailable"] is { } funds) resources.Adapters.Replace(resources.Adapters.Get("funds") with
    { Account = _ => new(() => new(Number(funds, "fundsAvailable"), null), _ => { }) });
    resources.Adapters.Register(new("charge", "Charge", "squad", true, 1, subject =>
    {
        var account = subject.Holder["resources"]!["charge"]!.AsObject();
        return new(() => new(Number(account["current"], "current"), Number(account["capacity"], "capacity")), current => account["current"] = current);
    }));
    resources.Subjects.Register(new("squad", (context, _) => new("squad", context.Player!), subject => new JsonObject { ["kind"] = "squad", ["id"] = subject.Holder["id"]!.DeepClone() }));
    var directives = UnitDirectiveRegistry.Core();
    var turns = new TurnOrderSystem(new(content, TurnOrderRegistry.Core()));
    var reactions = ReactionRegistry.Core();
    var commands = new TacticalCommands(new(grids, space, reactions), turns.MayAct, directives);
    var combat = new BattleCombat(content, grids, resources, space, WeaponAreaRegistry.Core(), DefaultCombatModifiers.Create(),
        DefaultWeaponHitEffects.Create(), reactions, new ThresholdRankProgressionPolicy(), DefaultUnitDepartureHandlers.Create());
    var support = new SupportOrders(content, grids, resources, combat.Effects);
    var careers = new CareerRules(content, resources, combat.Effects);
    var abilities = DefaultAbilities.Create()
        .Register(new("pulse", "Pulse") { Usable = (_, _) => !IsTrue(config["disableAbility"]) })
        .Register(new("charge", "Charge")
        {
            WeaponFor = (rules, query) => query.WeaponId is { } id ? rules.Combat.Forecasts.UnitWeapons(query.Unit).First(weapon => Text(weapon, "id") == id) : null,
            WeaponChoices = (rules, query) => rules.Combat.Forecasts.UnitWeapons(query.Unit),
        });
    var abilityCommands = new AbilityCommands(new(content, space, combat, support, careers, abilities), commands);
    var evaluators = DefaultAbilityAiEvaluators.Create().Clone()
        .Register(new("pulse", context => Number(config, "evaluatorScore", 1000) + context.At.X - context.At.Y * 0.01))
        .Register(new("charge", context => context.Option.Weapon == "soldier_javelin" ? 2000 : 1000));
    var rules = new AiRules(content, grids, space, abilityCommands, combat, support, resources, directives, turns, Empire.Core.Objectives.DefaultObjectives.Create());
    var planning = new AiPlanningDependencies(rules, evaluators, DefaultAiObjectiveAdvisors.Create());
    var planner = new AiPlanner(planning, DefaultAiIntents.Create());
    if (!ReferenceEquals(planner.Planning.Rules, rules) || !ReferenceEquals(planner.Abilities, evaluators) || !ReferenceEquals(rules.Space, space) || !ReferenceEquals(rules.Resources, resources))
        throw new Exception("AI dependencies lost resolved identity");
    var context = new AiTurnContext(planning, state, 1, 0.58);
    var actor = state["units"]![0]!.AsObject(); var expected = item["expected"]!.AsObject();
    Check(name, "action", planner.ChooseAction(state, 0.58), expected["action"]);
    Check(name, "threat", JsonSerializer.SerializeToNode(context.Threat.Select(pair => new object[] { pair.Key, pair.Value })), expected["threat"]);
    Check(name, "appraisals", JsonSerializer.SerializeToNode(new[] { new Coord(2, 3), new Coord(4, 3) }.Select(at => new TileAppraisal(context, actor, at).Value)), expected["appraisals"]);
    Check(name, "commands", JsonSerializer.SerializeToNode(abilityCommands.Options(state, actor, new(2, 3)).Select(option => option.Key)), expected["commands"]);
    Check(name, "actors", JsonSerializer.SerializeToNode(context.Actors().Select(unit => Integer(unit, "id"))), expected["actors"]);
    Check(name, "distance", JsonValue.Create(context.Distance(new(0, 0), new(2, 2))), expected["distance"]);
}
Console.WriteLine($"PASS {checks / 6} original source AI planning dependency cases, {checks} exact assertions");

sealed class TestSpace(ReadOnlyContent content, GridRegistry grids, JsonObject config) : DefaultTacticalSpace(content, grids)
{
    public override MoveField ComputeMoveField(JsonObject state, JsonObject unit)
    {
        var field = base.ComputeMoveField(state, unit);
        if (config["stop"] is { } stop) field.Stops = [Integer(stop, "stop")];
        if (IsTrue(config["brokenParents"])) foreach (var tile in field.Tiles.Values) if (tile.From != -1) tile.From = tile.Index;
        return field;
    }
    public override IReadOnlyList<Coord>? PathTo(MoveField field, JsonObject state, Coord destination) => IsTrue(config["rejectPath"]) ? null : base.PathTo(field, state, destination);
    public override IReadOnlySet<int> ThreatTiles(JsonObject state, JsonObject unit, MoveField? field = null) => config["threat"] is JsonArray threat
        ? threat.Select(value => Integer(value, "tile")).ToHashSet() : base.ThreatTiles(state, unit, field);
    public override IReadOnlyList<Coord> AttackTargetCoords(JsonObject state, JsonObject unit, Coord from, JsonObject weapon) => IsTrue(config["noTargets"]) ? [] : base.AttackTargetCoords(state, unit, from, weapon);
    public override TacticalBoard Board(JsonObject state) => IsTrue(config["poisonBoard"]) ? throw new Exception("AI must resolve grids, not space.board") : base.Board(state);
}
