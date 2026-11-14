using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Godot;

namespace Empire.Presentation;

/// <summary>C01 orchard terrain contracts; detached tactical fixtures are not campaign playthroughs.</summary>
public partial class C01OrchardChecks : Node
{
    public override void _Ready()
    {
        try
        {
            Check();
            GetTree().Quit(0);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void Check()
    {
        var count = 0;
        void Require(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException(message);
            count++;
        }
        var level = BattleTextChecks.OpeningLevel();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var state = engine.CreateState(level);
        var movement = new MovementRules(new(engine.Catalog, engine.Grids));
        var field = movement.Battlefield(state);
        Coord[] additions = [new(11, 18), new(12, 18), new(13, 18), new(12, 19), new(13, 19)];
        foreach (var cell in additions.Concat(new Coord[] { new(11, 19), new(11, 20) }))
        {
            var terrain = field.Cell(cell);
            Require(terrain.Terrain.GetProperty("id").GetString() == "forest" && terrain.Elevation == 0,
                $"Orchard must use existing zero-height forest at {cell}");
            Require(terrain.Cover == "half" && terrain.BlocksVision, "Orchard uses real forest cover and visibility");
        }
        var from = new Coord(10, 18);
        var target = new Coord(13, 18);
        Require(!movement.HasDirectLineOfSight(state, from, target), "Forest blocks a same-height direct ray through its interior");
        var plainFixture = level.DeepClone().AsObject();
        foreach (var cell in additions)
        {
            var row = plainFixture["terrain"]![cell.Y]!.GetValue<string>().ToCharArray();
            row[cell.X] = '.';
            plainFixture["terrain"]![cell.Y] = new string(row);
        }
        var plain = engine.CreateState(plainFixture);
        Require(movement.HasDirectLineOfSight(plain, from, target), "The same ray is open when only new orchard cells revert to plain");
        var archer = state["units"]!.AsArray().OfType<JsonObject>().Single(u => u["key"]!.GetValue<string>() == "ridge-scout");
        Require(movement.AttackTilesFrom(state, archer, from).Contains(target), "C01 ranger bow is arc fire and retains this target");
        var direct = JsonNode.Parse(engine.Catalog.Definition("weapons", "c01.longbow")!.Value.GetRawText())!.AsObject();
        Require(!movement.AttackTilesFrom(state, archer, from, direct).Contains(target), "Direct-weapon query rejects the blocked ray");
        Require(movement.AttackTilesFrom(plain, archer, from, direct).Contains(target), "Direct-weapon fixture detects the forest delta");
        foreach (var (profile, cost) in new[] { ("foot", 2), ("mounted", 3), ("heavy", 3) })
        {
            Require(field.TraversalCost(from, new(11, 18), profile) == cost, $"Forest traversal cost for {profile}");
            Require(field.TraversalCost(new(14, 18), new(15, 18), profile) == 1, $"East clear lane remains low-cost for {profile}");
        }
        foreach (var (key, profile) in new[] { ("camp-guard", "foot"), ("field-knight", "mounted") })
        {
            var fixture = MakeFixture(key);
            var battle = engine.CreateBattle(fixture);
            var actor = battle.Snapshot()["units"]!.AsArray().OfType<JsonObject>().Single(u => u["key"]!.GetValue<string>() == key);
            Require(engine.Catalog.Definition("units", actor["type"]!.GetValue<string>())!.Value.GetProperty("movementClass").GetString() == profile,
                "Actual armored C01 unit retains its own movement profile");
            battle.Dispatch(Command(actor, new JsonArray(Point(10, 18), Point(11, 18)), new JsonObject { ["ability"] = "wait" }));
            Require(battle.Snapshot()["units"]!.AsArray().OfType<JsonObject>().Single(u => u["key"]!.GetValue<string>() == key)["x"]!.GetValue<int>() == 11,
                "Actual armored unit can legally enter orchard, not an impassable decoration");
        }
        var ranged = engine.CreateBattle(MakeFixture("ridge-scout"));
        var shooter = ranged.Snapshot()["units"]!.AsArray().OfType<JsonObject>().Single(u => u["key"]!.GetValue<string>() == "ridge-scout");
        var receipt = ranged.Dispatch(Command(shooter, new JsonArray(Point(10, 18)), new JsonObject { ["ability"] = "attack", ["weapon"] = "c01.ranger-bow", ["target"] = Point(13, 18) }));
        Require(receipt.Events.Any(e => e["type"]?.GetValue<string>() == "attack"), "Actual C01 arc shot commits through forest");
        GD.Print($"C01_ORCHARD_CHECKS_OK {count} assertions");

        JsonObject MakeFixture(string actorKey)
        {
            var fixture = level.DeepClone().AsObject();
            var units = fixture["units"]!.AsArray().OfType<JsonObject>();
            var actor = units.Single(u => u["key"]!.GetValue<string>() == actorKey);
            var enemy = units.Single(u => u["key"]!.GetValue<string>() == "vanguard-1");
            actor["x"] = 10;
            actor["y"] = 18;
            enemy["x"] = 13;
            enemy["y"] = 18;
            return fixture;
        }
        static JsonObject Point(int x, int y) => new()
        {
            ["x"] = x,
            ["y"] = y
        };
        static JsonObject Command(JsonObject actor, JsonArray path, JsonObject command) => new()
        {
            ["kind"] = "command",
            ["unit"] = actor["id"]!.DeepClone(),
            ["path"] = path,
            ["command"] = command
        };
    }
}
