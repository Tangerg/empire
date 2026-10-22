using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Empire.Core.Ai;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;

internal static class EscortChecks
{
    public static void MissionAndIntent(string oracleRoot)
    {
        var data = Path.GetFullPath(Path.Combine(oracleRoot, "../../data"));
        var engine = new BattleEngine(Read(Path.Combine(data, "content-packs.json")).AsArray());
        var level = Read(Path.Combine(data, "grayflag-campaign-levels.json")).AsArray().OfType<JsonObject>()
            .Single(candidate => Text(candidate, "id") == "c01-02");
        var art = Read(Path.Combine(data, "scenes/c01-02-painted-ground.json"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(EcmaJson.Stringify(level)))).ToLowerInvariant();
        Require(Text(art!, "levelSourceSha256") == hash, "Painted registration matches reviewed rule-only level change");
        Require(engine.Victory.Evaluate(engine.CreateState(level), _ => { }) is null, "All required units alive keeps mission active");
        foreach (var key in new[] { "campaign-laiya", "refugee-1", "refugee-2", "refugee-3" })
        {
            var state = engine.CreateState(level);
            var unit = Units(state).Single(candidate => candidate["key"]?.GetValue<string>() == key);
            var events = new List<JsonObject>();
            engine.Combat.Damage.Resolve(state, Integer(unit, "id"), Number(unit["hp"], "hp"), false,
                _ => new JsonObject { ["type"] = "testDamage" }, events.Add);
            var outcome = engine.Victory.Evaluate(state, events.Add);
            Require(outcome?.Team == 2 && !IsTrue(Player(state, 1)["alive"]), $"{key} casualty defeats the mission owner");
            Require(Text(Player(state, 1)["objectiveStates"]!["primary"]!, "status") == "failed", "Primary objective records failure");
            Require(events.Any(e => Text(e, "type") == "death"), "Real casualty transition emitted death");
        }

        var fixture = EscortLevel();
        var battle = engine.CreateBattle(fixture);
        var initial = battle.Snapshot();
        var arrived = Units(initial).Single(unit => Text(unit, "key") == "first");
        var selected = initial.DeepClone().AsObject();
        foreach (var unit in Units(selected))
            if (Integer(unit, "owner") == 1)
                unit["done"] = Integer(unit, "id") != Integer(arrived, "id");
        var holding = engine.Ai.ChooseAction(selected, 0.58);
        Require(Text(holding, "kind") == "command" && Integer(holding, "unit") == Integer(arrived, "id"), "Arrived escort receives a real AI command");
        var stop = holding["path"]!.AsArray().Last()!.AsObject();
        Require(InZone(stop), "Arrived escort remains in safety while others are pending");
        Console.WriteLine($"  Arrived escort choice: {EcmaJson.Stringify(holding)}");
        battle.Dispatch(holding);
        for (var actions = 0; actions < 4 && Status(battle.Snapshot()) == "active"; actions++)
            battle.Dispatch(engine.Ai.ChooseAction(battle.Snapshot(), 0.58));
        var completed = battle.Snapshot();
        Require(Status(completed) == "completed", "Holding arrivals leaves a place for the last escort to complete");
        var mission = new AiTurnContext(engine.Ai.Planning, completed, 1, 0.58).Agenda.Mission;
        Require(mission.ProtectedUnits.Count == 0 && mission.Destinations.Count == 1, "Completed escort releases its intent while elimination remains");

        foreach (var target in new[] { new Coord(1, 1), new Coord(3, 0) })
        {
            var movable = engine.CreateBattle(fixture);
            var state = movable.Snapshot();
            var actor = Units(state).Single(unit => Text(unit, "key") == "first");
            var field = engine.Space.ComputeMoveField(state, actor);
            var path = engine.Space.PathTo(field, state, target);
            Require(path is not null, "Arrived escort can legally reposition inside or leave the safety zone");
            movable.Dispatch(new JsonObject
            {
                ["kind"] = "command",
                ["unit"] = Integer(actor, "id"),
                ["path"] = TacticalQueries.PathJson(path!),
                ["command"] = new JsonObject { ["ability"] = "wait" }
            });
            Require(TacticalQueries.Position(Units(movable.Snapshot()).Single(unit => Text(unit, "key") == "first")) == target,
                "Soft objective intent does not lock legal movement");
        }

        var generic = engine.CreateState(fixture);
        var absent = Units(generic).Single(unit => Text(unit, "key") == "last");
        engine.Combat.Damage.Resolve(generic, Integer(absent, "id"), Number(absent["hp"], "hp"), false,
            _ => new JsonObject { ["type"] = "testDamage" }, _ => { });
        Require(engine.Victory.Evaluate(generic, _ => { }) is null && Status(generic) == "active", "Generic escort remains pending for a future return");
        engine.ScenarioCommands.ReviveMarkers(generic, new JsonObject { ["selector"] = new JsonObject { ["kind"] = "corpse" } }, _ => { });
        Require(Units(generic).Any(unit => Text(unit, "key") == "last") && Status(generic) == "active", "Existing revival remains available to generic escort");

        AbilityPositionContract(engine);

        var armed = EscortLevel();
        armed["units"]![0]!["unit"] = "c01.swordsman";
        armed["units"]![3]!["x"] = 3;
        armed["units"]![3]!["y"] = 2;
        var armedState = engine.CreateState(armed);
        foreach (var unit in Units(armedState).Where(unit => Integer(unit, "owner") == 1 && Text(unit, "key") != "first"))
            unit["done"] = true;
        var armedBattle = engine.LoadSavedBattle(engine.SaveBattle(armedState));
        var attack = engine.Ai.ChooseAction(armedBattle.Snapshot(), 0.58);
        for (var preparations = 0; preparations < 3 && Text(attack, "kind") == "reaction"; preparations++)
        {
            armedBattle.Dispatch(attack);
            attack = engine.Ai.ChooseAction(armedBattle.Snapshot(), 0.58);
        }
        Require(attack["command"]?["ability"]?.GetValue<string>() == "attack",
            $"Armed escort chooses a real existing-content attack on the mission target; received {attack}");
        Require(InZone(attack["path"]!.AsArray().Last()!.AsObject()), "Armed escort attacks while retaining its safety-zone position");
        Console.WriteLine($"  Armed escort choice: {EcmaJson.Stringify(attack)}");
        armedBattle.Dispatch(attack);
        Console.WriteLine("  Escort intent: arrived hold, last arrival completion/release, legal internal/external movement, generic revival, existing-content armed attack.");
    }

    private static void AbilityPositionContract(BattleEngine engine)
    {
        var level = EscortLevel();
        level["units"]![0]!["unit"] = "c01.gravekeeper";
        level["units"]![3]!["x"] = 3;
        level["units"]![3]!["y"] = 2;
        level["terrain"]![1] = "...v........";
        var state = engine.CreateState(level);
        var actor = Units(state).Single(unit => Text(unit, "key") == "first");
        actor["reaction"] = "guard";
        foreach (var unit in Units(state).Where(unit => Integer(unit, "owner") == 1 && Text(unit, "key") != "first"))
            unit["done"] = true;
        Units(state).Single(unit => Text(unit, "key") == "second")["hp"] = 20;
        var at = TacticalQueries.Position(actor);
        var context = new AiTurnContext(engine.Ai.Planning, state, 1, 0.58);
        var position = new TileAppraisal(context, actor, at);
        Require(position.MissionPositionValue > 0 && position.Value == position.MissionPositionValue + position.TacticalPositionValue,
            "Position total derives from its mission and tactical components");
        Console.WriteLine($"  Same-cell position: mission={position.MissionPositionValue}, tactical={position.TacticalPositionValue}; four abilities observed.");
        var observed = new HashSet<string>();
        var evaluators = engine.Ai.Abilities.Clone();
        foreach (var ability in new[] { "wait", "attack", "heal", "capture" })
        {
            var original = evaluators.TryGet(ability)!;
            evaluators.Replace(original with
            {
                Score = evaluation =>
                {
                    if (Integer(evaluation.Unit, "id") == Integer(actor, "id") && evaluation.At == at)
                    {
                        Require(evaluation.TacticalValue == position.TacticalPositionValue,
                            $"{ability} receives only the same-cell tactical component; mission value belongs to the common scorer");
                        observed.Add(ability);
                    }
                    return original.Score(evaluation);
                }
            });
        }
        engine.Ai.Intents.Choose(new AiTurnContext(engine.Ai.Planning with
        {
            AbilityEvaluators = evaluators
        }, state, 1, 0.58));
        Require(observed.SetEquals(new[] { "wait", "attack", "heal", "capture" }), "All four real abilities share the same-cell position contract");
        foreach (var invalidWeight in new[] { double.NaN, double.MaxValue })
        {
            var advisors = engine.Ai.Planning.ObjectiveAdvisors.Clone();
            advisors.Replace(new("escort", (advice, _) => advice.Destination(at, invalidWeight, "invalid position test", new[] { Integer(actor, "id") })));
            var invalidContext = new AiTurnContext(engine.Ai.Planning with
            {
                ObjectiveAdvisors = advisors
            }, state, 1, 0.58);
            Require(!double.IsFinite(new TileAppraisal(invalidContext, actor, at).MissionPositionValue), "Test exercises a non-finite composed mission value");
            Require(engine.Ai.Intents.TryGet("command")!.Propose(invalidContext) is null, "Common scorer rejects NaN and overflowing final totals without clamping or selecting an invalid candidate");
        }
    }

    private static string Status(JsonObject state) => Text(Player(state, 1)["objectiveStates"]!["escort"]!, "status");
    private static bool InZone(JsonObject at) => Integer(at, "y") == 1 && Integer(at, "x") is >= 1 and <= 3;
    private static JsonObject EscortLevel() => EcmaJson.Parse("""
        {
          "schema":3,"id":"test.escort-intent","width":12,"height":4,
          "terrain":["............","............","............","............"],
          "players":[
            {"id":1,"team":1,"color":"#336699","controller":"human","resources":{},
             "objectives":[{"id":"mission","type":"all","objectives":[
               {"id":"escort","type":"escort","selector":{"keys":["first","second","last"]},"zone":"safe","count":3},
               {"id":"target","type":"eliminate","selector":{"keys":["foe"]}}
             ]}]},
            {"id":2,"team":2,"color":"#993333","controller":"ai","resources":{},"ai":{"aggression":0.58},"objectives":[{"id":"route","type":"routEnemies"}]}
          ],
          "units":[
            {"key":"first","unit":"c01.refugee","owner":1,"x":3,"y":1},
            {"key":"second","unit":"c01.refugee","owner":1,"x":2,"y":1},
            {"key":"last","unit":"c01.refugee","owner":1,"x":4,"y":1},
            {"key":"foe","unit":"c01.refugee","owner":2,"x":10,"y":1}
          ],
          "scenario":{"zones":[{"id":"safe","cells":[{"x":1,"y":1},{"x":2,"y":1},{"x":3,"y":1}]}]},
          "rules":{"moraleEnabled":false,"turnLimit":null}
        }
        """)!.AsObject();
    private static IEnumerable<JsonObject> Units(JsonObject state) => state["units"]!.AsArray().OfType<JsonObject>();
    private static IEnumerable<JsonObject> Players(JsonObject state) => state["players"]!.AsArray().OfType<JsonObject>();
    private static string Text(JsonNode node, string field) => node[field]!.GetValue<string>();
    private static int Integer(JsonNode node, string field) => node[field]!.GetValue<int>();
    private static double Number(JsonNode? node, string field) => node?.GetValue<double>() ?? throw new Exception($"Missing {field}");
    private static bool IsTrue(JsonNode? node) => node?.GetValue<bool>() == true;
    private static JsonObject Player(JsonObject state, int owner) => Players(state).Single(player => Integer(player, "id") == owner);
    private static JsonNode Read(string path) => EcmaJson.Parse(File.ReadAllText(path))!;
    private static void Require(bool condition, string label)
    {
        if (!condition)
            throw new Exception(label);
    }
}
