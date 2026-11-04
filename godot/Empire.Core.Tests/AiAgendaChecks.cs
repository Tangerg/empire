using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Ai;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.State;

internal static class AiAgendaChecks
{
    public static void Run(string oracleRoot)
    {
        var data = Path.GetFullPath(Path.Combine(oracleRoot, "../../data"));
        var engine = new BattleEngine(Read(Path.Combine(data, "content-packs.json")).AsArray());
        BoundaryFixtures(engine.Ai.Planning);
        Console.WriteLine("  Synthetic agenda contracts: optional flags, exact failures/short circuits, ordered ownership/advisors and isolated catalogs.");
        var levels = Read(Path.Combine(data, "grayflag-campaign-levels.json")).AsArray().OfType<JsonObject>().ToArray();
        var fixtures = Read(Path.Combine(oracleRoot, "../native/ai-agenda-read.json"))["cases"]!.AsArray();
        foreach (var fixture in fixtures.OfType<JsonObject>())
        {
            var level = levels.Single(value => value["id"]!.GetValue<string>() == fixture["levelId"]!.GetValue<string>());
            var battle = engine.CreateBattle(level);
            foreach (var action in fixture["prefix"]!.AsArray().OfType<JsonObject>())
                battle.Dispatch(action);
            var state = battle.Snapshot();
            var hash = StateDigest.Hash(state);
            Require(hash == fixture["stateHash"]!.GetValue<string>(), "Formal AI witness must follow its admitted command prefix.");
            var save = EcmaJson.Stringify(engine.SaveBattle(state, "2026-11-04T00:00:00.000Z"));
            var player = state["currentPlayer"]!.GetValue<int>();
            var side = state["players"]!.AsArray().OfType<JsonObject>().Single(value => value["id"]!.GetValue<int>() == player);
            Require(side["controller"]!.GetValue<string>() == "ai", "Allocation witness must be an actual enemy AI turn.");
            var aggression = side["ai"]!["aggression"]!.GetValue<double>();
            AiAgenda Query() => new AiTurnContext(engine.Ai.Planning, state, player, aggression).Agenda;
            var expectedAgenda = JsonSerializer.Serialize(Query());
            for (var warmup = 0; warmup < 2; warmup++)
                Query();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var results = new AiAgenda[5];
            for (var attempt = 0; attempt < results.Length; attempt++)
                results[attempt] = Query();
            var allocated = (GC.GetAllocatedBytesForCurrentThread() - before) / results.Length;
            var tiles = state["map"]!["tiles"]!.AsArray().Count;
            // Measured read projection is ~300 bytes/tile; allow ample headroom but reject repeated full-definition clones (>3 KB/tile).
            Require(allocated < 768L * tiles + 65536, $"AI agenda allocation regressed: {allocated} bytes for {tiles} tiles.");
            Require(results.All(value => JsonSerializer.Serialize(value) == expectedAgenda), "Repeated agendas must preserve ordering and exact weights.");
            for (var attempt = 0; attempt < 3; attempt++)
                Require(EcmaJson.DeepEquals(engine.Ai.ChooseAction(state), fixture["action"]), "Formal AI action choice changed.");
            Require(StateDigest.Hash(state) == hash && EcmaJson.Stringify(engine.SaveBattle(state, "2026-11-04T00:00:00.000Z")) == save,
                "Planning must not change state, RNG or saved battle facts.");
            Console.WriteLine($"  {fixture["name"]}: {tiles} tiles, {allocated} agenda bytes/query; exact action, state and save preserved.");
        }
    }

    private static void BoundaryFixtures(AiPlanningDependencies planning)
    {
        JsonObject Terrain() => new()
        {
            ["id"] = "site",
            ["capturable"] = true,
            ["hq"] = false,
            ["produces"] = new JsonArray()
        };
        JsonArray Packs(params JsonObject[] terrains) => new(new JsonObject
        {
            ["id"] = "agenda-fixture",
            ["version"] = 1,
            ["terrains"] = new JsonArray(terrains.Select(value => (JsonNode)value.DeepClone()).ToArray())
        });
        JsonObject State(string[] tiles, int[] owners, params string[] objectives) => new()
        {
            ["rules"] = new JsonObject { ["grid"] = "square4" },
            ["map"] = new JsonObject
            {
                ["width"] = tiles.Length,
                ["height"] = 1,
                ["tiles"] = new JsonArray(tiles.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray()),
                ["owners"] = new JsonArray(owners.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray())
            },
            ["players"] = new JsonArray(Enumerable.Range(1, 3).Select(id => (JsonNode)new JsonObject
            {
                ["id"] = id,
                ["team"] = id == 2 ? 2 : 1,
                ["objectives"] = new JsonArray((id == 1 ? objectives : []).Select(type => (JsonNode)new JsonObject { ["type"] = type }).ToArray()),
                ["objectiveStates"] = new JsonObject()
            }).ToArray())
        };
        AiAgenda Agenda(ReadOnlyContent content, JsonObject state) =>
            new AiTurnContext(planning with
            {
                Rules = planning.Rules with
                {
                    Content = content
                }
            }, state, 1, .58).Agenda;
        AiAgenda Single(JsonObject terrain, int owner = 2) => Agenda(new ReadOnlyContent(Packs(terrain)), State(["site"], [owner]));
        foreach (var flag in new[] { "capturable", "hq" })
            foreach (var value in new string?[] { null, "null", "false", "true", "0", "\"true\"", "{}", "[]" })
            {
                var terrain = Terrain();
                if (value is null)
                    terrain.Remove(flag);
                else
                    terrain[flag] = JsonNode.Parse(value);
                var agenda = Single(terrain);
                var enabled = value == "true";
                Require(flag == "capturable" ? agenda.CaptureTargets.Count == (enabled ? 1 : 0)
                    : agenda.EnemyHqs.Count == (enabled ? 1 : 0) && agenda.CaptureTargets.Single().Weight == (enabled ? 6 : 2),
                    $"Optional {flag} must treat only JSON true as enabled: {value ?? "missing"}.");
            }
        var production = Terrain();
        production["produces"] = new JsonArray("fixture-unit");
        Require(Single(production).CaptureTargets.Single().Weight == 3, "A productive site keeps weight 3.");
        foreach (var bad in new string?[] { null, "null", "false", "5", "\"wrong\"", "{}" })
        {
            var terrain = Terrain();
            if (bad is null)
                terrain.Remove("produces");
            else
                terrain["produces"] = JsonNode.Parse(bad);
            Reject(() => Single(terrain), "produces must be an array");
            Require(Single(terrain, 1).CaptureTargets.Count == 0, "An owned site must not read produces.");
            terrain["hq"] = true;
            Require(Single(terrain).CaptureTargets.Single().Weight == 6, "An HQ must not read produces.");
            terrain["capturable"] = false;
            Require(Single(terrain).CaptureTargets.Count == 0, "An uncapturable tile must not read produces.");
        }
        var sites = Enumerable.Range(0, 6).Select(index =>
        {
            var terrain = Terrain();
            terrain["id"] = "s" + index;
            terrain["hq"] = index is 0 or 1 or 2 or 5;
            terrain["capturable"] = index != 5;
            if (index == 4)
                terrain["produces"] = new JsonArray("fixture-unit");
            return terrain;
        }).ToArray();
        var mixed = State(sites.Select(value => value["id"]!.GetValue<string>()).ToArray(), [1, 3, 2, 0, 2, 2], "captureHQ", "holdAllVillages");
        var content = new ReadOnlyContent(Packs(sites));
        var ordered = Agenda(content, mixed);
        Require(ordered.CaptureTargets.SequenceEqual(new[] { new CaptureTarget(new(1, 0), 6), new CaptureTarget(new(2, 0), 6),
            new CaptureTarget(new(3, 0), 2), new CaptureTarget(new(4, 0), 3) }), "Capture targets preserve tile order, allies and neutral sites.");
        Require(ordered.MyHqs.SequenceEqual(new[] { new Coord(0, 0) }) && ordered.EnemyHqs.SequenceEqual(new[] { new Coord(2, 0) }),
            "HQ lists preserve self/enemy semantics, excluding allies and uncapturable tiles.");
        Require(ordered.Mission.Destinations.Select(value => (value.At.X, value.Weight, value.CaptureOnly, value.Reason)).SequenceEqual(new[]
        {
            (2, 8d, true, "攻占敌方总部"), (5, 8d, true, "攻占敌方总部"),
            (1, 7d, true, "控制据点"), (2, 7d, true, "控制据点"), (3, 4d, true, "控制据点"), (4, 4d, true, "控制据点")
        }), "Objective advice retains its separate HQ-only semantics and append order.");
        var authored = Packs(Terrain());
        var detached = new ReadOnlyContent(authored);
        var retained = detached.Definition("terrains", "site")!.Value;
        authored[0]!["terrains"]![0]!["capturable"] = false;
        var independent = new ReadOnlyContent(authored);
        var one = State(["site"], [2]);
        var before = EcmaJson.Stringify(one);
        for (var iteration = 0; iteration < 3; iteration++)
        {
            Require(Agenda(detached, one).CaptureTargets.Count == 1 && Agenda(independent, one).CaptureTargets.Count == 0,
                "Same-ID independent catalogs and caller edits cannot change another engine's agenda.");
            Require(retained.GetProperty("capturable").GetBoolean() && EcmaJson.Stringify(one) == before,
                "Immutable content and input state must remain unchanged.");
        }
        var badOwner = State(["site"], [2]);
        badOwner["map"]!["owners"]![0] = "invalid";
        Require(Agenda(independent, badOwner).CaptureTargets.Count == 0, "Uncapturable tiles must skip owner parsing.");
        Reject(() => Agenda(detached, badOwner), "owner must be a finite number");
        Reject(() => Agenda(detached, State(["absent"], [2])), "unknown terrain \"absent\"");
    }

    private static JsonNode Read(string path) => EcmaJson.Parse(File.ReadAllText(path))!;
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    private static void Reject(Action action, string message)
    {
        try
        {
            action();
        }
        catch (DomainInvariantException error) when (error.Message == message) { return; }
        throw new InvalidOperationException("Expected exact invariant failure: " + message);
    }
}
