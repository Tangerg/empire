using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Empire.Core.Ai;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Objectives;
using Empire.Core.Scenario;
using Empire.Core.Tactics;

internal static class EscortChecks
{
    public static void MissionAndIntent(string oracleRoot)
    {
        var data = Path.GetFullPath(Path.Combine(oracleRoot, "../../data"));
        var engine = new BattleEngine(Read(Path.Combine(data, "content-packs.json")).AsArray());
        var level = Read(Path.Combine(data, "grayflag-campaign-levels.json")).AsArray().OfType<JsonObject>()
            .Single(candidate => Text(candidate, "id") == "c01-02");
        CheckObjectiveTargets(engine, data, level);
        CheckScoreTargets(engine, data);
        CheckCompositeTargets(engine, data);
        var scene = Read(Path.Combine(data, "scenes/c01-02.json"));
        var registration = Text(scene!, "assembledGroundPath");
        Require(registration == "res://data/scenes/c01-02-layered-ground.json", "C02 formal scene selects its independently layered registration");
        var art = Read(Path.Combine(data, registration["res://data/".Length..]));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(EcmaJson.Stringify(level)))).ToLowerInvariant();
        Require(Text(art!, "levelSourceSha256") == hash, "Formal layered registration matches the unchanged canonical level");
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

    private static void CheckObjectiveTargets(BattleEngine engine, string data, JsonObject level)
    {
        var state = engine.CreateState(level);
        var before = EcmaJson.Stringify(state);
        var saved = EcmaJson.Stringify(engine.SaveBattle(state, "2026-11-11T00:00:00.000Z"));
        var primary = Player(state, 1)["objectives"]![0]!.AsObject();
        var outcome = engine.Victory.Outcome(state, 1, primary);
        IEnumerable<ObjectiveProgress> Flatten(IEnumerable<ObjectiveProgress> values) =>
            values.SelectMany(value => new[] { value }.Concat(Flatten(value.Children)));
        ObjectiveProgress Escort() => Flatten(engine.Victory.Objectives(state, 1)).Single(value => value.Kind == "escort");
        var escort = Escort();
        var expected = new[] { new Coord(18, 23), new Coord(19, 23), new Coord(20, 23), new Coord(18, 24), new Coord(19, 24), new Coord(20, 24) };
        Require(escort.TargetCells.SequenceEqual(expected), "C02 escort projects exactly the six canonical east-safety cells.");
        Require(Flatten(engine.Victory.Objectives(state, 1)).Where(value => value.Kind != "escort").All(value => value.TargetCells.Count == 0),
            "Non-spatial objectives and wrappers do not invent map targets.");
        Require(EcmaJson.Stringify(state) == before && EcmaJson.Stringify(engine.SaveBattle(state, "2026-11-11T00:00:00.000Z")) == saved
            && engine.Victory.Outcome(state, 1, primary) == outcome,
            "Target projection cannot change state, saves or victory assessment.");
        try
        {
            ((IList<Coord>)escort.TargetCells)[0] = new(-1, -1);
            throw new Exception("Target projection exposed writable coordinates.");
        }
        catch (NotSupportedException) { }
        state["scenario"]!["zones"]!["east-safety"]![0]!["x"] = 17;
        Require(Escort().TargetCells[0] == new Coord(17, 23) && escort.TargetCells.SequenceEqual(expected),
            "Current targets follow canonical zone changes without modifying previously detached projections.");
        var objectiveState = Player(state, 1)["objectiveStates"]![escort.Id]!.AsObject();
        foreach (var status in new[] { "completed", "failed", "inactive", "cancelled" })
        {
            objectiveState["status"] = status;
            Require(Escort().TargetCells.Count == 0, $"{status} objectives do not offer active map targets.");
        }
        objectiveState["status"] = "active";
        Require(Escort().TargetCells.Count == 6, "Reactivated objectives derive their current targets again.");

        var city = Read(Path.Combine(data, "grayflag-campaign-levels.json")).AsArray().OfType<JsonObject>()
            .Single(candidate => Text(candidate, "id") == "c01-07");
        var cityState = engine.CreateState(city);
        var controls = Flatten(engine.Victory.Objectives(cityState, 1)).Where(value => value.Kind == "control").ToDictionary(value => value.Id);
        Require(controls.Count == 2 && controls["granary"].TargetCells.SequenceEqual(new[] { new Coord(35, 14) })
            && controls["militia-barracks"].TargetCells.SequenceEqual(new[] { new Coord(65, 14) }),
            "C07 control goals project their distinct canonical capture cells, not a combined inferred center.");

        var supplied = new List<Coord> { new(2, 3), new(4, 5) };
        var handlers = engine.Victory.Handlers.Clone().Register(new ObjectiveHandler("test-target-location", (_, _) => new("pending", new()))
        {
            TargetCells = (_, _) => supplied
        });
        var custom = new JsonObject { ["id"] = "test-location", ["type"] = "test-target-location" };
        var projected = handlers.Project(engine.Victory.Rules, state, 1, custom);
        supplied.Clear();
        Require(projected.TargetCells.SequenceEqual(new[] { new Coord(2, 3), new Coord(4, 5) })
            && handlers.Project(engine.Victory.Rules, state, 1, custom).TargetCells.Count == 0,
            "Extension handlers supply target semantics through the same detached projection without kind switches.");
        Console.WriteLine("  Objective target cells: C02 six-cell escort, C07 distinct controls, active lifecycle, detached immutability, extension handler and save/outcome invariance passed.");
    }

    private static void CheckScoreTargets(BattleEngine engine, string data)
    {
        var level = Read(Path.Combine(data, "grayflag-campaign-levels.json")).AsArray().OfType<JsonObject>()
            .Single(candidate => Text(candidate, "id") == "c01-11").DeepClone().AsObject();
        JsonObject Score(JsonObject source) => source["players"]!.AsArray().OfType<JsonObject>()
            .Single(player => Integer(player, "id") == 1)["objectives"]![0]!["objective"]!["objectives"]![0]!.AsObject();
        Score(level).Remove("targetZones");
        var noTargets = engine.CreateState(level);
        ObjectiveProgress Project(JsonObject state) => engine.Victory.Handlers.Project(engine.Victory.Rules, state, 1, Score(state));
        Require(Project(noTargets).TargetCells.Count == 0, "Scores without authored locations must not infer targets from trigger effects.");
        Score(level)["targetZones"] = new JsonArray("name-a", "name-b", "name-a", "name-c");
        var state = engine.CreateState(level);
        var before = EcmaJson.Stringify(state);
        var saved = EcmaJson.Stringify(engine.SaveBattle(state, "2026-11-11T00:00:00.000Z"));
        var expected = new[] { new Coord(5, 3), new Coord(14, 7), new Coord(8, 12) };
        var projected = Project(state);
        Require(projected.TargetCells.SequenceEqual(expected), "Authored score locations use canonical zones in declaration order, with duplicate coordinates removed.");
        Require(projected.Facts.GetRawText() == Project(noTargets).Facts.GetRawText(),
            "Score location metadata cannot change the score assessment.");
        Require(EcmaJson.Stringify(state) == before && EcmaJson.Stringify(engine.SaveBattle(state, "2026-11-11T00:00:00.000Z")) == saved,
            "Score location projection must not mutate canonical state or saved state.");
        try
        {
            ((IList<Coord>)projected.TargetCells)[0] = new(-1, -1);
            throw new Exception("Score target projection exposed writable coordinates.");
        }
        catch (NotSupportedException) { }
        state["scenario"]!["zones"]!["name-a"]![0]!["x"] = 6;
        Require(Project(state).TargetCells[0] == new Coord(6, 3) && projected.TargetCells.SequenceEqual(expected),
            "Score targets follow canonical zone changes while prior projections remain detached.");
        state["scenario"]!["variables"]!["names"] = 3;
        engine.Victory.Evaluate(state, _ => { });
        Require(Project(state).Status == "completed" && Project(state).TargetCells.Count == 0,
            "A completed score no longer exposes active target cells.");
        CheckScoreVisitEquivalence(engine, level);
        Score(level)["targetZones"] = new JsonArray("missing-score-zone");
        try
        {
            engine.CreateState(level);
            throw new Exception("Unknown score target zone was admitted.");
        }
        catch (BattleLevelException error)
        {
            Require(error.Message.Contains("missing-score-zone", StringComparison.Ordinal),
                "Score zone references must be rejected by canonical level admission with the missing identity.");
        }
        Console.WriteLine("  Score target cells: optional authored zones, deduplication, canonical updates, immutable projection, admission and completion passed.");
    }

    private static void CheckScoreVisitEquivalence(BattleEngine engine, JsonObject authored)
    {
        // Isolate travel distance only; canonical grave zones, triggers and victory conditions are unchanged.
        var fixture = authored.DeepClone().AsObject();
        var initial = engine.CreateState(fixture);
        var movement = new MovementRules(new(engine.Catalog, engine.Grids));
        var destination = new Coord(5, 3);
        var start = movement.Board(initial).Neighbours(destination)
            .First(cell => movement.Battlefield(initial).Cell(cell).Admits("foot")
                && Units(initial).All(unit => TacticalQueries.Position(unit) != cell));
        var seed = fixture["units"]!.AsArray().OfType<JsonObject>().Single(unit => Text(unit, "key") == "campaign-laiya");
        seed["x"] = start.X;
        seed["y"] = start.Y;
        JsonObject Score(JsonObject source) => source["players"]!.AsArray().OfType<JsonObject>()
            .Single(player => Integer(player, "id") == 1)["objectives"]![0]!["objective"]!["objectives"]![0]!.AsObject();
        var baselineLevel = fixture.DeepClone().AsObject();
        Score(baselineLevel).Remove("targetZones");
        var live = engine.CreateBattle(fixture);
        var baseline = engine.CreateBattle(baselineLevel);
        void Dispatch(JsonObject action)
        {
            var actual = live.Dispatch(action);
            var expected = baseline.Dispatch(action);
            Require(actual.Events.Select(EcmaJson.Stringify).SequenceEqual(expected.Events.Select(EcmaJson.Stringify)),
                "Score location metadata must leave every canonical movement, trigger and turn event identical.");
            var state = live.Snapshot();
            Score(state).Remove("targetZones");
            Require(EcmaJson.Stringify(state) == EcmaJson.Stringify(baseline.Snapshot()),
                "Only the authored targetZones declaration may differ between battle states.");
        }
        void Move(Coord to)
        {
            var state = live.Snapshot();
            var unit = Units(state).Single(value => Text(value, "key") == "campaign-laiya");
            var field = movement.ComputeMoveField(state, unit);
            Require(field.Stops.Contains(movement.Board(state).IndexOf(to)), "Grave visit fixture must use a legal movement stop.");
            Dispatch(new JsonObject
            {
                ["kind"] = "command",
                ["unit"] = Integer(unit, "id"),
                ["path"] = TacticalQueries.PathJson(movement.PathTo(field, state, to)!),
                ["command"] = new JsonObject { ["ability"] = "wait" }
            });
        }
        Move(destination);
        Require(Number(live.Snapshot()["scenario"]!["variables"]!["names"], "names") == 1,
            "Completing a genuine move-and-wait at the first grave reads one name.");
        foreach (var to in new[] { start, destination })
        {
            Dispatch(new JsonObject { ["kind"] = "endTurn" });
            Dispatch(new JsonObject { ["kind"] = "endTurn" });
            Move(to);
        }
        var after = live.Snapshot();
        Require(Number(after["scenario"]!["variables"]!["names"], "names") == 1
            && after["scenario"]!["firedTriggerIds"]!.AsArray().Count(value => value!.GetValue<string>() == "name-a") == 1,
            "Leaving and revisiting a grave in later turns cannot read its name twice.");
    }

    private static void CheckCompositeTargets(BattleEngine engine, string data)
    {
        var level = Read(Path.Combine(data, "grayflag-campaign-levels.json")).AsArray().OfType<JsonObject>()
            .Single(candidate => Text(candidate, "id") == "c01-12");
        JsonObject Objective(JsonObject state) => Player(state, 1)["objectives"]![0]!["objective"]!.AsObject();
        ObjectiveProgress Project(JsonObject state) => engine.Victory.Handlers.Project(engine.Victory.Rules, state, 1, Objective(state));
        JsonObject Part(JsonObject state, string id) => state["structures"]!.AsArray().OfType<JsonObject>().Single(part => Text(part, "id") == id);
        var initial = engine.CreateState(level);
        var before = EcmaJson.Stringify(initial);
        var saved = EcmaJson.Stringify(engine.SaveBattle(initial, "2026-11-11T00:00:00.000Z"));
        var expected = new[] { new Coord(6, 4), new Coord(16, 4), new Coord(11, 10) };
        var projected = Project(initial);
        Require(projected.TargetCells.SequenceEqual(expected), "C12 composite targets identify all three canonical bell parts.");
        Require(EcmaJson.Stringify(initial) == before && EcmaJson.Stringify(engine.SaveBattle(initial, "2026-11-11T00:00:00.000Z")) == saved,
            "Composite target projection must not mutate state or save content.");
        try
        {
            ((IList<Coord>)projected.TargetCells)[0] = new(-1, -1);
            throw new Exception("Composite target coordinates were writable.");
        }
        catch (NotSupportedException) { }
        var moved = initial.DeepClone().AsObject();
        CompositeRules.Move(moved, "bell-controls", 0, 1, _ => { });
        Require(Project(moved).TargetCells.SequenceEqual(expected.Select(cell => new Coord(cell.X, cell.Y + 1)))
            && projected.TargetCells.SequenceEqual(expected), "Composite target locations follow canonical movement without changing old projections.");
        foreach (var mode in new[] { "disabled", "dead", "missing", "captured" })
        {
            var state = initial.DeepClone().AsObject();
            var part = Part(state, "bell-a");
            if (mode == "disabled")
                part["disabled"] = true;
            else if (mode == "dead")
                part["hp"] = 0;
            else if (mode == "missing")
                state["structures"]!.AsArray().Remove(part);
            else
                part["owner"] = 1;
            Require(Project(state).TargetCells.SequenceEqual(mode == "captured" ? expected : expected.Skip(1))
                && CompositeRules.Status(state, "bell-controls").Neutralized == (mode == "captured" ? 0 : 1),
                $"Composite {mode} target projection must share the exact neutralization semantics.");
        }
        var completed = initial.DeepClone().AsObject();
        Part(completed, "bell-a")["disabled"] = true;
        Part(completed, "bell-b")["hp"] = 0;
        engine.Victory.Evaluate(completed, _ => { });
        Require(Project(completed).Status == "completed" && Project(completed).TargetCells.Count == 0,
            "Reaching the objective threshold removes all active targets, including a surviving optional third part.");
        var overridden = initial.DeepClone().AsObject();
        Objective(overridden)["minimumNeutralized"] = 3;
        Part(overridden, "bell-a")["disabled"] = true;
        Part(overridden, "bell-b")["hp"] = 0;
        engine.Victory.Evaluate(overridden, _ => { });
        Require(CompositeRules.Status(overridden, "bell-controls").State == "neutralized"
            && Project(overridden).Status == "active" && Project(overridden).TargetCells.SequenceEqual(expected.Skip(2)),
            "An objective threshold override must retain its remaining target even after the composite default threshold is met.");
        var repeated = initial.DeepClone().AsObject();
        repeated["composites"]!.AsArray().OfType<JsonObject>().Single(value => Text(value, "id") == "bell-controls")["parts"]!.AsArray().Add("bell-a");
        Require(CompositeRules.Status(repeated, "bell-controls").Neutralized == 0
            && Project(repeated).TargetCells.SequenceEqual(expected), "Repeated part references preserve status multiplicity but never duplicate target coordinates.");
        Part(repeated, "bell-a")["disabled"] = true;
        Require(CompositeRules.Status(repeated, "bell-controls").Neutralized == 2
            && Project(repeated).TargetCells.SequenceEqual(expected.Skip(1)), "Repeated neutralized part references retain existing counting semantics.");
        Console.WriteLine("  Composite target cells: canonical positions, movement, disabled/dead/missing/captured states, completion, threshold override and duplicate references passed.");
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
