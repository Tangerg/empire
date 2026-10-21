using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Core.Transactions;
using Empire.Core;
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Core.Transport;

internal static class ReplayChecks
{
    public static void OpeningGameplay(string oracleRoot)
    {
        var engine = Engine(oracleRoot);
        var level = Levels(oracleRoot).Single(item => item["id"]!.GetValue<string>() == "c01-01");
        var fixture = Read(Path.Combine(oracleRoot, "grayflag-0.58.json"));
        var battle = engine.CreateBattle(level, fixture["replay"]!["seed"]!.GetValue<uint>());
        var recorder = engine.RecordBattle(battle.Snapshot());
        var index = 0;
        foreach (var step in fixture["steps"]!.AsArray().OfType<JsonObject>())
        {
            var action = engine.Ai.ChooseAction(battle.Snapshot(), 0.58);
            Equal(step["action"]!, action, $"opening legal AI action {index}");
            var receipt = battle.Dispatch(action);
            recorder.Record(action);
            var expectedEvents = step["events"]!.AsArray();
            if (receipt.Events.Count != expectedEvents.Count)
                throw new Exception($"Opening event count differs at {index}.");
            for (var eventIndex = 0; eventIndex < expectedEvents.Count; eventIndex++)
            {
                var expected = expectedEvents[eventIndex]!.AsObject();
                var actual = receipt.Events[eventIndex];
                if (expected["type"]!.GetValue<string>() != "gameOver")
                    Equal(expected, actual, $"opening unchanged event {index}/{eventIndex}");
                else
                {
                    if (actual["type"]!.GetValue<string>() != "gameOver")
                        throw new Exception("The terminal event moved or disappeared.");
                    var conclusion = BattleConclusion.Parse(actual["conclusion"]);
                    if (conclusion.Team != expected["team"]?.GetValue<int>())
                        throw new Exception("The semantic winner differs from the source outcome.");
                }
            }
            index++;
        }
        if (battle.Snapshot()["phase"]!.GetValue<string>() != "over")
            throw new Exception("The source terminal opening trace did not conclude natively.");
        VerifyPersistence(engine, level, battle, recorder.Replay(battle.Snapshot()));
        Console.WriteLine($"  Opening: {index} source-matched legal actions and unchanged events; terminal schema compared semantically, native replay/save hashes exact.");
    }

    public static void ShippedLevels(string oracleRoot)
    {
        var engine = Engine(oracleRoot);
        foreach (var level in Levels(oracleRoot))
        {
            var battle = engine.CreateBattle(level);
            var recorder = engine.RecordBattle(battle.Snapshot());
            var count = 0;
            while (count < 4096 && battle.Snapshot()["phase"]!.GetValue<string>() != "over")
            {
                var action = engine.Ai.ChooseAction(battle.Snapshot(), 0.58);
                battle.Dispatch(action);
                recorder.Record(action);
                count++;
            }
            if (battle.Snapshot()["phase"]!.GetValue<string>() != "over")
                throw new Exception($"{level["id"]}: battle exceeded the explicit 4096-action safety bound.");
            VerifyPersistence(engine, level, battle, recorder.Replay(battle.Snapshot()));
            Console.WriteLine($"  {level["id"]}: {count} legal native actions, exact save/replay round trip (independent level, not carried-state campaign victory).");
        }
    }

    public static void OwnershipAdmission(string oracleRoot)
    {
        var engine = Engine(oracleRoot);
        var state = engine.CreateState(Levels(oracleRoot).First());
        var commander = state["commanders"]!.AsArray().OfType<JsonObject>().First();
        var unit = TacticalQueries.RequireUnit(state, commander["unitId"]!.GetValue<int>());
        var owner = state["players"]!.AsArray().OfType<JsonObject>()
            .Select(player => player["id"]!.GetValue<int>()).First(id => id != unit["owner"]!.GetValue<int>());
        var invalidOwner = state["players"]!.AsArray().OfType<JsonObject>()
            .Max(player => player["id"]!.GetValue<int>()) + 1;
        var effect = new JsonObject
        {
            ["owner"] = invalidOwner,
            ["selector"] = new JsonObject { ["ids"] = new JsonArray(unit["id"]!.DeepClone()) },
        };
        var events = new List<JsonObject>();
        var before = state.DeepClone();
        Reject(() => new BattleUnitOwnership(state).Assign(new[] { unit.DeepClone().AsObject() }, owner));
        Equal(before, state, "detached live-unit ownership subject rejection");
        var duplicateCommanders = state.DeepClone().AsObject();
        duplicateCommanders["commanders"]!.AsArray().Add(commander.DeepClone());
        Reject(() => new BattleUnitOwnership(duplicateCommanders).Validate());
        Reject(() => BattleStateFactory.SpawnUnit(
            new BattleStateRules(engine.Catalog, engine.Grids, engine.Reactions), state,
            unit["type"]!.GetValue<string>(), invalidOwner, TacticalQueries.Position(unit)));
        Equal(before, state, "rejected spawned-unit ownership");
        if (events.Count != 0) throw new Exception("Rejected spawn emitted an event.");
        Reject(() => engine.ScenarioCommands.ChangeUnitOwner(state, effect, events.Add));
        Equal(before, state, "rejected direct ownership command");
        if (events.Count != 0) throw new Exception("Rejected owner emitted an event.");
        effect["owner"] = owner;
        engine.ScenarioCommands.ChangeUnitOwner(state, effect, events.Add);
        if (unit["owner"]!.GetValue<int>() != owner || commander["owner"]!.GetValue<int>() != owner)
            throw new Exception("Live unit and commander ownership diverged.");
        engine.SaveBattle(state, "2026-10-21T00:00:00Z");
        var invalidProjection = state.DeepClone().AsObject();
        invalidProjection["commanders"]![0]!["owner"] = before["commanders"]![0]!["owner"]!.DeepClone();
        Reject(() => engine.SaveBattle(invalidProjection, "2026-10-21T00:00:00Z"));

        var at = TacticalQueries.Position(unit);
        var marker = new JsonObject
        {
            ["id"] = 99999, ["kind"] = "corpse", ["owner"] = owner,
            ["at"] = TacticalQueries.CoordinateJson(at), ["fallenUnit"] = unit.DeepClone(),
        };
        state["units"]!.AsArray().Remove(unit);
        state["markers"]!.AsArray().Add(marker);
        before = state.DeepClone();
        events.Clear();
        Reject(() => BattleUnitReturn.Restore(engine.Catalog, state, marker,
            new UnitReturnRequest(at, Owner: invalidOwner), events.Add));
        Equal(before, state, "rejected returned-unit ownership");
        if (events.Count != 0) throw new Exception("Rejected return emitted an event.");
        var returned = BattleUnitReturn.Restore(engine.Catalog, state, marker,
            new UnitReturnRequest(at, Owner: 1), events.Add)
            ?? throw new Exception("Admitted unit did not return.");
        if (returned["owner"]!.GetValue<int>() != 1 || commander["owner"]!.GetValue<int>() != 1)
            throw new Exception("Returned unit and commander ownership diverged.");
        engine.SaveBattle(state, "2026-10-21T00:00:00Z");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (DomainInvariantException) { return; }
        throw new Exception("Invalid ownership was accepted.");
    }

    private static void VerifyPersistence(GrayFlagEngine engine, JsonObject level, Battle battle, JsonObject replay)
    {
        var state = battle.Snapshot();
        Equal(state, engine.LoadBattle(engine.SaveBattle(state, "2026-10-21T00:00:00Z")), "native save round trip");
        var verified = engine.ReplayBattle(level, replay);
        if (verified.DivergedAt is not null)
            throw new Exception($"Native replay diverged at {verified.DivergedAt}: {verified.Reason}");
        Equal(state, verified.State, "full native replay state");
        if (StateDigest.Hash(state) != StateDigest.Hash(verified.State))
            throw new Exception("Native replay changed the canonical digest.");
    }

    private static GrayFlagEngine Engine(string root) => new(Read(Path.Combine(root, "../../data/content-packs.json")).AsArray());
    private static IEnumerable<JsonObject> Levels(string root) =>
        Read(Path.Combine(root, "../../data/grayflag-campaign-levels.json")).AsArray().OfType<JsonObject>();
    private static JsonNode Read(string path) => EcmaJson.Parse(File.ReadAllText(path))!;
    private static void Equal(JsonNode expected, JsonNode actual, string subject)
    {
        if (!EcmaJson.DeepEquals(expected, actual)) throw new Exception(subject + " differs.");
    }
}
