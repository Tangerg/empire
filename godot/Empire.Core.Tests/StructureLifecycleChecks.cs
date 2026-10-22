using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

internal static class StructureLifecycleChecks
{
    public static void Run(string oracleRoot)
    {
        var data = Path.GetFullPath(Path.Combine(oracleRoot, "../../data"));
        var engine = new BattleEngine(JsonNode.Parse(File.ReadAllText(Path.Combine(data, "content-packs.json")))!.AsArray());
        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(data, "grayflag-campaign-levels.json")))!.AsArray()
            .OfType<JsonObject>().Single(level => Text(level, "id") == "c01-03");
        foreach (var route in new[] { "entry", "escort", "gate", "sappers", "attack" })
        {
            var level = original.DeepClone().AsObject();
            if (route is "entry" or "escort")
            {
                var visitor = Objects(level["units"], "units").Single(unit => Text(unit, "key") == (route == "escort" ? "campaign-laiya" : "campaign-torren"));
                visitor["x"] = 18;
                visitor["y"] = 7;
            }
            if (route == "gate")
                level["units"]!.AsArray().Remove(Objects(level["units"], "units").Single(unit => Text(unit, "key") == "redstone-shield"));
            if (route == "attack")
            {
                level["structures"]![0]!["hp"] = 1;
                var mage = Objects(level["units"], "units").Single(unit => Text(unit, "key") == "siege-mage");
                mage["x"] = 17;
                mage["y"] = 7;
            }
            var battle = engine.CreateBattle(level);
            var recorder = engine.RecordBattle(battle.Snapshot());
            var allEvents = new List<JsonObject>();
            BattleReceipt Dispatch(JsonObject action)
            {
                var receipt = battle.Dispatch(action);
                recorder.Record(action);
                allEvents.AddRange(receipt.Events);
                return receipt;
            }
            if (route == "sappers")
            {
                while (Integer(battle.Snapshot(), "turn") < 15)
                    Dispatch(new JsonObject { ["kind"] = "endTurn" });
            }
            else if (route == "attack")
            {
                var state = battle.Snapshot();
                var mage = Objects(state["units"], "units").Single(unit => Text(unit, "key") == "siege-mage");
                var target = new Coord(18, 7);
                var option = engine.CommandsAt(state, mage, TacticalQueries.Position(mage))
                    .First(command => command.Ability == "attack" && command.Targets.Contains(target));
                Dispatch(new JsonObject
                {
                    ["kind"] = "command",
                    ["unit"] = Integer(mage, "id"),
                    ["path"] = new JsonArray(TacticalQueries.CoordinateJson(TacticalQueries.Position(mage))),
                    ["command"] = new JsonObject { ["ability"] = "attack", ["weapon"] = option.Weapon, ["target"] = TacticalQueries.CoordinateJson(target) }
                });
            }
            else
                Dispatch(Wait(battle.Snapshot()));
            var final = battle.Snapshot();
            var node = Node(final);
            Require(Number(node["hp"], "hp") == (route == "attack" ? 0 : 100) && Integer(node, "owner") == 2 && IsTrue(node["disabled"]),
                $"{route}: closing preserves owner and HP, except real attack destruction");
            Require(Objects(final["units"], "units").Where(unit => new[] { "oath-guard-1", "oath-guard-2", "redstone-inquisitor", "keep-reserve" }.Contains(unit["key"]?.GetValue<string>()))
                .Count(unit => Integer(unit, "owner") == 1) == 4, $"{route}: all four oathbound released");
            Require(allEvents.Count(value => Text(value, "type") == "unitOwnerChanged") == 4, $"{route}: ownership events exactly once");
            var eventType = route == "attack" ? "structureDestroyed" : "structureDisabled";
            Require(allEvents.Count(value => Text(value, "type") == eventType) == 1, $"{route}: one lifecycle event");
            var transition = allEvents.Single(value => Text(value, "type") == eventType);
            Require(Text(transition, "structure") == "redstone-node" && Integer(transition["at"]!, "x") == 18
                && Integer(transition["at"]!, "y") == 7, $"{route}: event identifies canonical structure and position");
            if (route != "attack")
                Require(allEvents.All(value => Text(value, "type") is not ("structureDamaged" or "structureDestroyed")),
                    $"{route}: disable emits no invented damage or destruction");
            Require(Number(final["scenario"]!["eventCounts"]![eventType], eventType) == 1, $"{route}: event count committed");
            if (route is "entry" or "attack")
                Require(final["conclusion"] is null && Number(final["scenario"]!["variables"]!["gate_breached"], "gate_breached") == 0,
                    $"{route}: generic entry or destruction is not a fourth victory or named escort arrival");
            if (route is "escort" or "gate" or "sappers")
                Require(final["conclusion"]?["team"]?.GetValue<int>() == 1, $"{route}: original victory retained");
            var signal = route switch
            {
                "entry" or "escort" => "redstone_node_disabled",
                "gate" => "redstone_gate_chain_broken",
                "sappers" => "redstone_sappers_cut_chain",
                _ => null
            };
            if (signal is not null)
                Require(allEvents.Count(value => value["signal"]?.GetValue<string>() == signal) == 1, $"{route}: original route signal retained");
            Equal(final, engine.LoadBattle(engine.SaveBattle(final)), $"{route}: exact save/load");
            var replay = engine.ReplayBattle(level, recorder.Replay(final));
            Require(replay.DivergedAt is null, $"{route}: replay {replay.Reason}");
            Equal(final, replay.State, $"{route}: exact replay state including event counts and once ledger");
            var repeated = new List<JsonObject>();
            engine.Scenarios.Run(final, "afterAction", repeated.Add);
            Require(repeated.Count == 0, $"{route}: stabilized once rules do not emit again");
            engine.Combat.RepairStructure(final, "redstone-node", 10, repeated.Add);
            engine.Scenarios.Run(final, "afterAction", repeated.Add);
            Require(!IsTrue(Node(final)["disabled"]) && repeated.All(value => Text(value, "type") != "unitOwnerChanged"),
                $"{route}: repair enables without re-enslavement");
            Console.WriteLine($"  {route}: committed closure, four releases, original victory/signal, exact save/replay.");
        }
        PrimitiveAndAtomicity(engine, original);
    }

    private static void PrimitiveAndAtomicity(BattleEngine engine, JsonObject original)
    {
        var state = engine.CreateState(original);
        var events = new List<JsonObject>();
        engine.Combat.DisableStructure(state, "redstone-node", events.Add);
        engine.Combat.DisableStructure(state, "redstone-node", events.Add);
        Require(events.Count == 1 && Text(events[0], "type") == "structureDisabled", "Disable is idempotent");
        Require(engine.Combat.RepairStructure(state, "redstone-node", 10, events.Add) == 0, "Full-HP repair heals zero");
        Require(events.Count == 2 && Text(events[1], "type") == "structureEnabled", "Zero-heal repair emits availability transition");
        engine.Combat.RepairStructure(state, "redstone-node", 10, events.Add);
        Require(events.Count == 2, "Already-enabled repair emits no second transition");
        engine.Combat.DisableStructure(state, "redstone-node", events.Add);
        Require(engine.Combat.RepairStructure(state, "redstone-node", 0.1, events.Add) == 0 && !IsTrue(Node(state)["disabled"]),
            "Positive repair rounded to zero retains the existing intact-structure reactivation contract");
        Require(events.Count == 4 && Text(events[^1], "type") == "structureEnabled", "Fractional repair reports reactivation");
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, -1 })
        {
            var before = state.DeepClone();
            Reject(() => engine.Combat.RepairStructure(state, "redstone-node", invalid, events.Add));
            Equal(before, state, "Invalid repair must not mutate");
        }
        Reject(() => engine.Combat.DisableStructure(state, "absent", events.Add));
        engine.Combat.DamageStructure(state, "redstone-node", 112, events.Add);
        var count = events.Count;
        engine.Combat.DisableStructure(state, "redstone-node", events.Add);
        Require(events.Count == count, "Destroyed structure disable is a no-op");

        var repairLevel = original.DeepClone().AsObject();
        repairLevel["scenario"]!["triggers"] = new JsonArray(Trigger("repair-transition",
            new JsonObject { ["type"] = "disableStructure", ["id"] = "redstone-node" },
            new JsonObject { ["type"] = "disableStructure", ["id"] = "redstone-node" },
            new JsonObject { ["type"] = "repairStructure", ["id"] = "redstone-node", ["amount"] = 10 },
            new JsonObject { ["type"] = "repairStructure", ["id"] = "redstone-node", ["amount"] = 10 }));
        var repairBattle = engine.CreateBattle(repairLevel);
        var recorder = engine.RecordBattle(repairBattle.Snapshot());
        var repairAction = Wait(repairBattle.Snapshot());
        var receipt = repairBattle.Dispatch(repairAction);
        recorder.Record(repairAction);
        Require(receipt.Events.Count(value => Text(value, "type") == "structureDisabled") == 1
            && receipt.Events.Count(value => Text(value, "type") == "structureEnabled") == 1
            && receipt.Events.All(value => Text(value, "type") != "structureRepaired"), "Real zero-heal transaction has exactly two availability transitions");
        var repaired = repairBattle.Snapshot();
        Equal(repaired, engine.LoadBattle(engine.SaveBattle(repaired)), "Enabled event counter persists");
        var replay = engine.ReplayBattle(repairLevel, recorder.Replay(repaired));
        Require(replay.DivergedAt is null, "Enable transition replays");
        Equal(repaired, replay.State, "Enabled event counter replays exactly");

        var invalidLevel = original.DeepClone().AsObject();
        invalidLevel["scenario"]!["triggers"]!.AsArray().Add(Trigger("invalid-reference", new JsonObject { ["type"] = "disableStructure", ["id"] = "absent" }));
        try
        {
            engine.CreateState(invalidLevel);
            throw new Exception("Expected invalid structure reference rejection");
        }
        catch (BattleLevelException) { }

        // A runtime fault after earlier successful effects must discard the whole real action,
        // including its trigger ledger, events, and unit's completed command.
        var broken = original.DeepClone().AsObject();
        broken["scenario"]!["triggers"]!.AsArray().Insert(0, Trigger("atomic-fault",
            new JsonObject { ["type"] = "disableStructure", ["id"] = "redstone-node" },
            new JsonObject { ["type"] = "addVariable", ["key"] = "not-numeric", ["amount"] = 1 }));
        broken["scenario"]!["variables"]!["not-numeric"] = "reject";
        var battle = engine.CreateBattle(broken);
        var beforeAction = battle.Snapshot();
        Reject(() => battle.Dispatch(Wait(beforeAction)));
        Equal(beforeAction, battle.Snapshot(), "Real action failure rolls back all lifecycle state and counters");
        var saved = engine.SaveBattle(state);
        saved["ruleset"]!["plugins"]!["engine.mission-rules"] = 2;
        try
        {
            engine.LoadBattle(saved);
            throw new Exception("Expected old ruleset rejection");
        }
        catch (StoredDocumentException) { }
        Console.WriteLine("  Full-HP enable, idempotence, invalid IDs/numbers, destroyed no-op, real transaction rollback, and old ruleset rejection.");
    }

    private static JsonObject Trigger(string id, params JsonObject[] effects) => new()
    {
        ["id"] = id,
        ["timing"] = "afterAction",
        ["condition"] = new JsonObject { ["type"] = "turnAtLeast", ["turn"] = 1 },
        ["effects"] = new JsonArray(effects.Select(effect => (JsonNode)effect).ToArray())
    };
    private static JsonObject Wait(JsonObject state)
    {
        var unit = Objects(state["units"], "units").First(value => Integer(value, "owner") == Integer(state, "currentPlayer") && !IsTrue(value["done"]));
        return new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = Integer(unit, "id"),
            ["path"] = new JsonArray(TacticalQueries.CoordinateJson(TacticalQueries.Position(unit))),
            ["command"] = new JsonObject { ["ability"] = "wait" }
        };
    }
    private static IEnumerable<JsonObject> Objects(JsonNode? node, string field) => (node ?? throw new Exception($"Missing {field}")).AsArray().OfType<JsonObject>();
    private static string Text(JsonNode node, string field) => node[field]!.GetValue<string>();
    private static int Integer(JsonNode node, string field) => node[field]!.GetValue<int>();
    private static double Number(JsonNode? node, string field) => node?.GetValue<double>() ?? throw new Exception($"Missing {field}");
    private static bool IsTrue(JsonNode? node) => node?.GetValue<bool>() == true;
    private static JsonObject Node(JsonObject state) => state["structures"]![0]!.AsObject();
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
    private static void Equal(JsonNode expected, JsonNode actual, string message) => Require(EcmaJson.Stringify(expected) == EcmaJson.Stringify(actual), message);
    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (DomainInvariantException) { return; }
        throw new Exception("Expected domain invariant rejection");
    }
}
