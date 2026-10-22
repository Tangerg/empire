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
        var art = JsonNode.Parse(File.ReadAllText(Path.Combine(data, "scenes/c01-03-painted-ground.json")))!;
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(EcmaJson.Stringify(original)))).ToLowerInvariant();
        Require(Text(art, "levelSourceSha256") == hash, "Formal Redstone painting is registered to its only gameplay definition");
        Require(Integer(original, "width") == 101 && Integer(original, "height") == 52
            && Integer(original["rules"]!, "turnLimit") == 18, "Authored large-map scale and deadline remain unchanged");
        Require(!original["scenario"]!["variables"]!.AsObject().ContainsKey("gate_breached"), "No duplicate gate victory fact survives");
        foreach (var key in Objects(original["units"], "units").Where(unit => Integer(unit, "owner") == 1).Select(unit => Text(unit, "key")))
        {
            var level = original.DeepClone().AsObject();
            Put(LevelUnit(level, key), 79, 24);
            var battle = engine.CreateBattle(level);
            var recorder = engine.RecordBattle(battle.Snapshot());
            var action = Move(engine, battle.Snapshot(), key, new(80, 24));
            var receipt = ConsistentAction(engine, battle, action, undo: true);
            recorder.Record(action);
            Closed(battle.Snapshot(), receipt.Events, 4, "structureDisabled", key);
            ExactReplay(engine, level, battle, recorder);
        }
        Console.WriteLine("  Every initial P1 actor can enter the node: identical closure/victory/release, save/load, undo and exact replay.");

        foreach (var gate in new[] { false, true })
        {
            var level = original.DeepClone().AsObject();
            Put(LevelUnit(level, "siege-mage"), gate ? 67 : 79, gate ? 23 : 24);
            if (gate)
                LevelUnit(level, "redstone-shield")["hp"] = 1;
            else
                level["structures"]![0]!["hp"] = 1;
            var battle = engine.CreateBattle(level);
            var recorder = engine.RecordBattle(battle.Snapshot());
            var action = Attack(engine, battle.Snapshot(), "siege-mage", gate ? new(68, 23) : new(80, 24));
            var receipt = ConsistentAction(engine, battle, action, undo: true);
            recorder.Record(action);
            Closed(battle.Snapshot(), receipt.Events, 4, gate ? "structureDisabled" : "structureDestroyed", gate ? "gate kill" : "node destruction");
            Require(!gate || receipt.Events.Any(value => Text(value, "type") == "death"), "Gate solution requires real shield death");
            ExactReplay(engine, level, battle, recorder);
        }
        var woundedGate = engine.CreateState(original);
        Damage(engine, woundedGate, "redstone-shield", 1);
        var gateEvents = new List<JsonObject>();
        engine.Scenarios.Run(woundedGate, "afterAction", gateEvents.Add);
        Require(!IsTrue(Node(woundedGate)["disabled"]) && engine.Victory.Evaluate(woundedGate, gateEvents.Add) is null,
            "Wounding the shield does not cut the chain");

        var hqLevel = original.DeepClone().AsObject();
        Put(LevelUnit(hqLevel, "redstone-inquisitor"), 86, 14);
        Put(LevelUnit(hqLevel, "campaign-torren"), 84, 14);
        var hq = engine.CreateBattle(hqLevel);
        var hqRecorder = engine.RecordBattle(hq.Snapshot());
        var capture = Move(engine, hq.Snapshot(), "campaign-torren", new(85, 14), "capture");
        var captured = ConsistentAction(engine, hq, capture, undo: true);
        hqRecorder.Record(capture);
        Require(hq.Snapshot()["conclusion"]?["team"]?.GetValue<int>() == 1 && !IsTrue(Node(hq.Snapshot())["disabled"])
            && captured.Events.Any(value => Text(value, "type") == "capture" && IsTrue(value["captured"]))
            && captured.Events.All(value => Text(value, "type") != "unitOwnerChanged"), "HQ capture wins independently without inventing a chain release");
        ExactReplay(engine, hqLevel, hq, hqRecorder);
        Casualties(engine, original);
        SapperSupport(engine, original);
        VillageRecovery(engine, original);
        PrimitiveAndAtomicity(engine, original);
    }

    private static void Casualties(BattleEngine engine, JsonObject level)
    {
        foreach (var laiyaDead in new[] { false, true })
        {
            var state = engine.CreateState(level);
            Damage(engine, state, "oath-guard-1", 10000);
            if (laiyaDead)
                Damage(engine, state, "campaign-laiya", 10000);
            engine.Combat.DisableStructure(state, "redstone-node", _ => { });
            var events = new List<JsonObject>();
            engine.Scenarios.Run(state, "afterAction", events.Add);
            var outcome = engine.Victory.Evaluate(state, events.Add);
            Require(!Objects(state["units"], "units").Any(unit => Text(unit, "key") == "oath-guard-1")
                && events.Count(value => Text(value, "type") == "unitOwnerChanged") == 3, "Only the three surviving oathbound change allegiance");
            Require(outcome?.Team == (laiyaDead ? 2 : 1), "Laiya death takes precedence over successful chain neutralization");
            Equal(state, engine.LoadBattle(engine.SaveBattle(state)), "Casualty markers, objective result and release ledger persist");
        }
        Console.WriteLine("  Node destruction, shield death and HQ capture are genuine commands; casualties never revive and Laiya failure wins priority.");
    }

    private static void SapperSupport(BattleEngine engine, JsonObject original)
    {
        foreach (var key in new[] { "siege-engineer", "north-sapper", "redstone-wave-engineer" })
        {
            var level = original.DeepClone().AsObject();
            if (key != "redstone-wave-engineer")
                Put(LevelUnit(level, key), 78, 30);
            else
            {
                var spawn = Objects(level["scenario"]!["triggers"], "triggers").Single(trigger => Text(trigger, "id") == "redstone-second-wave")["effects"]![0]!["units"]!;
                Put(Objects(spawn, "spawn").Single(unit => Text(unit, "key") == key), 78, 30);
            }
            var battle = engine.CreateBattle(level);
            var recorder = engine.RecordBattle(battle.Snapshot());
            ToTurn(battle, 14, 2, recorder);
            Require(!IsTrue(Node(battle.Snapshot())["disabled"]), "Sapper presence before T15 is not early assistance");
            var end = new JsonObject { ["kind"] = "endTurn" };
            var receipt = ConsistentAction(engine, battle, end);
            recorder.Record(end);
            Closed(battle.Snapshot(), receipt.Events, 4, "structureDisabled", key + " T15");
            Require(Integer(battle.Snapshot(), "turn") == 15 && battle.Snapshot()["map"]!["owners"]![30 * 101 + 78]!.GetValue<int>() == 0,
                "Neutral village presence suffices; ownership is not a hidden requirement");
            Require(receipt.Events.Count(value => value["signal"]?.GetValue<string>() == "redstone_sappers_cut_chain") == 1,
                "T15 support signal occurs exactly once");
            ExactReplay(engine, level, battle, recorder);
            var repeated = new List<JsonObject>();
            var after = battle.Snapshot();
            engine.Scenarios.Run(after, "afterAction", repeated.Add);
            engine.Scenarios.Run(after, "turnStart", repeated.Add);
            Require(repeated.Count == 0, "Support and release cannot fire again");
        }
        foreach (var variant in new[] { "absent", "left", "dead", "non-engineer", "enemy-engineer" })
        {
            var level = original.DeepClone().AsObject();
            if (variant is "left" or "dead")
                Put(LevelUnit(level, "north-sapper"), 78, 30);
            if (variant == "non-engineer")
                Put(LevelUnit(level, "campaign-torren"), 78, 30);
            if (variant == "enemy-engineer")
            {
                var sapper = LevelUnit(level, "north-sapper");
                Put(sapper, 78, 30);
                sapper["owner"] = 2;
            }
            var battle = engine.CreateBattle(level);
            ToTurn(battle, 14, 1);
            if (variant == "left")
                ConsistentAction(engine, battle, Move(engine, battle.Snapshot(), "north-sapper", new(77, 30)), undo: true);
            if (variant == "dead")
            {
                var casualty = battle.Snapshot();
                Damage(engine, casualty, "north-sapper", 10000);
                battle = engine.LoadSavedBattle(engine.SaveBattle(casualty));
            }
            ToTurn(battle, 14, 2);
            var boundary = ConsistentAction(engine, battle, new JsonObject { ["kind"] = "endTurn" });
            Require(Integer(battle.Snapshot(), "turn") == 15 && battle.Snapshot()["conclusion"] is null
                && !IsTrue(Node(battle.Snapshot())["disabled"]) && boundary.Events.All(value => Text(value, "type") != "unitOwnerChanged"),
                variant + ": T15 alone cannot award support or victory");
            ToTurn(battle, 18, 2);
            Require(battle.Snapshot()["conclusion"] is null, "Turn 18 remains playable without a completed mission");
            ConsistentAction(engine, battle, new JsonObject { ["kind"] = "endTurn" });
            Require(Integer(battle.Snapshot(), "turn") == 19 && Text(battle.Snapshot()["conclusion"]!, "kind") == "turnLimitReached",
                variant + ": unchanged turn-limit contract ends at entry to T19");
        }
        var lateLevel = original.DeepClone().AsObject();
        Put(LevelUnit(lateLevel, "north-sapper"), 77, 30);
        var late = engine.CreateBattle(lateLevel);
        var lateRecorder = engine.RecordBattle(late.Snapshot());
        ToTurn(late, 16, 1, lateRecorder);
        var lateMove = Move(engine, late.Snapshot(), "north-sapper", new(78, 30));
        var lateReceipt = ConsistentAction(engine, late, lateMove, undo: true);
        lateRecorder.Record(lateMove);
        Closed(late.Snapshot(), lateReceipt.Events, 4, "structureDisabled", "late arrival");
        ExactReplay(engine, lateLevel, late, lateRecorder);
        Console.WriteLine("  T15 support: both initial engineers and actual T4 reinforcement, neutral site, absent/left/dead/wrong tags/wrong owner, late arrival, once-only, T18/T19.");
    }

    private static void VillageRecovery(BattleEngine engine, JsonObject original)
    {
        var level = original.DeepClone().AsObject();
        Put(LevelUnit(level, "north-sapper"), 77, 30);
        var battle = engine.CreateBattle(level);
        var recorder = engine.RecordBattle(battle.Snapshot());
        var capture = Move(engine, battle.Snapshot(), "north-sapper", new(78, 30), "capture");
        var receipt = ConsistentAction(engine, battle, capture, undo: true);
        recorder.Record(capture);
        Require(receipt.Events.Any(value => Text(value, "type") == "capture" && IsTrue(value["captured"])), "South village capture is a real command");
        ExactReplay(engine, level, battle, recorder);
        var state = battle.Snapshot();
        Damage(engine, state, "north-sapper", 25);
        var beforeHp = Number(Unit(state, "north-sapper")["hp"], "hp");
        var beforeFunds = Number(state["players"]![0]!["resources"]!["funds"]!["current"], "funds");
        battle = engine.LoadSavedBattle(engine.SaveBattle(state));
        ConsistentAction(engine, battle, new JsonObject { ["kind"] = "endTurn" });
        var healed = ConsistentAction(engine, battle, new JsonObject { ["kind"] = "endTurn" });
        Require(Number(Unit(battle.Snapshot(), "north-sapper")["hp"], "hp") == beforeHp + 20
            && Number(battle.Snapshot()["players"]![0]!["resources"]!["funds"]!["current"], "funds") == beforeFunds + 100,
            "Owned south village retains 20 HP next-own-turn healing and 100 funds");
        Require(healed.Events.Any(value => Text(value, "type") == "regen"), "Recovery emits the canonical building-regeneration event");
        Require(engine.Recruitment.Cards(battle.Snapshot(), new(78, 30)).Count == 0, "South village remains non-recruiting");
        Console.WriteLine("  South village capture/undo/replay and next-own-turn recovery/income/save-resume preserved (bounded state fixtures, not an assault playthrough).");
    }

    private static BattleReceipt ConsistentAction(BattleEngine engine, Battle battle, JsonObject action, bool undo = false)
    {
        var before = battle.Snapshot();
        var restored = engine.LoadSavedBattle(engine.SaveBattle(before));
        var result = battle.Dispatch(action);
        var peer = restored.Dispatch(action);
        Equal(battle.Snapshot(), restored.Snapshot(), "Save/load continuation keeps all state facts");
        Equal(new JsonArray(result.Events.Select(value => (JsonNode)value).ToArray()),
            new JsonArray(peer.Events.Select(value => (JsonNode)value).ToArray()), "Save/load continuation keeps ordered events");
        if (undo)
        {
            battle.Restore(result);
            Equal(before, battle.Snapshot(), "Undo restores physical node, objectives, ledger, owners and counters together");
            var repeated = battle.Dispatch(action);
            Equal(restored.Snapshot(), battle.Snapshot(), "Redispatch after undo yields exactly the same state");
            Equal(new JsonArray(result.Events.Select(value => (JsonNode)value).ToArray()),
                new JsonArray(repeated.Events.Select(value => (JsonNode)value).ToArray()), "Redispatch after undo yields exactly the same events");
        }
        return result;
    }

    private static void Closed(JsonObject state, IReadOnlyList<JsonObject> events, int released, string transition, string label)
    {
        Require(IsTrue(Node(state)["disabled"]) && Integer(Node(state), "owner") == 2
            && Number(Node(state)["hp"], "hp") == (transition == "structureDestroyed" ? 0 : 100), label + ": node owns neutralization");
        Require(state["conclusion"]?["team"]?.GetValue<int>() == 1, label + ": all neutralization routes win");
        Require(events.Count(value => Text(value, "type") == transition) == 1
            && events.Count(value => Text(value, "type") == "unitOwnerChanged") == released, label + ": transition and living releases occur once");
        var changed = events.Single(value => Text(value, "type") == transition);
        Require(Integer(changed["at"]!, "x") == 80 && Integer(changed["at"]!, "y") == 24, "Lifecycle event points to the real node");
        Require(Text(state["players"]![0]!["objectiveStates"]!["neutralize-chain"]!, "status") == "completed", "Objective reads the same neutralized node");
    }

    private static void ExactReplay(BattleEngine engine, JsonObject level, Battle battle, Empire.Core.Persistence.BattleRecorder recorder)
    {
        var replay = engine.ReplayBattle(level, recorder.Replay(battle.Snapshot()));
        Require(replay.DivergedAt is null, "Bounded legal-input replay: " + replay.Reason);
        Equal(battle.Snapshot(), replay.State, "Exact replay includes triggers, objectives, casualties, owners and event counts");
    }

    private static void ToTurn(Battle battle, int turn, int owner, Empire.Core.Persistence.BattleRecorder? recorder = null)
    {
        while (Integer(battle.Snapshot(), "turn") != turn || Integer(battle.Snapshot(), "currentPlayer") != owner)
        {
            Require(battle.Snapshot()["conclusion"] is null && Integer(battle.Snapshot(), "turn") <= turn, "Fixture unexpectedly ended or passed its target turn");
            var action = new JsonObject { ["kind"] = "endTurn" };
            battle.Dispatch(action);
            recorder?.Record(action);
        }
    }

    private static JsonObject Move(BattleEngine engine, JsonObject state, string key, Coord at, string ability = "wait")
    {
        var actor = Unit(state, key);
        var path = engine.Space.PathTo(engine.Space.ComputeMoveField(state, actor), state, at)
            ?? throw new Exception($"No legal fixture path for {key} to {at}");
        return new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = Integer(actor, "id"),
            ["path"] = new JsonArray(path.Select(cell => (JsonNode)TacticalQueries.CoordinateJson(cell)).ToArray()),
            ["command"] = new JsonObject { ["ability"] = ability }
        };
    }
    private static JsonObject Attack(BattleEngine engine, JsonObject state, string key, Coord at)
    {
        var actor = Unit(state, key);
        var option = engine.CommandsAt(state, actor, TacticalQueries.Position(actor)).First(command => command.Ability == "attack" && command.Targets.Contains(at));
        return new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = Integer(actor, "id"),
            ["path"] = new JsonArray(TacticalQueries.CoordinateJson(TacticalQueries.Position(actor))),
            ["command"] = new JsonObject { ["ability"] = "attack", ["weapon"] = option.Weapon, ["target"] = TacticalQueries.CoordinateJson(at) }
        };
    }
    private static JsonObject Unit(JsonObject state, string key) => Objects(state["units"], "units").Single(unit => Text(unit, "key") == key);
    private static JsonObject LevelUnit(JsonObject level, string key) => Unit(level, key);
    private static void Put(JsonObject unit, int x, int y)
    {
        unit["x"] = x;
        unit["y"] = y;
    }
    private static void Damage(BattleEngine engine, JsonObject state, string key, double amount)
    {
        var unit = Unit(state, key);
        engine.Combat.Damage.Resolve(state, Integer(unit, "id"), amount, false, _ => new JsonObject { ["type"] = "testDamage" }, _ => { });
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
