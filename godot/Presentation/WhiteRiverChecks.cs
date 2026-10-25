#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Core.Transactions;
using Empire.Localization;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>C06 production contracts and explicitly detached no-AI timing probes, not normal play or GUI acceptance.</summary>
public partial class WhiteRiverChecks : Node
{
    private const string LevelId = "c01-06";
    private int assertions;

    public override void _Ready()
    {
        try
        {
            var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
            var level = levels.OfType<JsonObject>().Single(value => Text(value, "id") == LevelId);
            var audit = PresentationJson.Load<JsonObject>("res://docs/art-provenance/white-river-geometry.json");
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var state = engine.CreateState(level);
            CheckMissionSource(level, state);
            CheckPainting(engine, level, state, audit);
            CheckMovement(engine, level, state, audit);
            CheckSiteInteractions(engine, level, audit);
            CheckOpeningEscortMoves(engine, level);
            CheckNoAiEscortRoute(engine, level, audit);
            CheckDeadlineHud(engine, level);
            CheckNoAiTiming(engine, level);
            CheckSaveAdmission(engine, levels);
            ExportGuiFixtures(engine, levels);
            GD.Print($"Native WhiteRiver passed {assertions} assertions: ten captured/healing sites, door/roof picking, physical foundations, three crossings, escort commands, T4/T6/T14 no-AI timing and strict save admission. No-AI timing is not normal-play or GUI acceptance.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void CheckMissionSource(JsonObject level, JsonObject state)
    {
        Require(Integer(level, "width") == 50 && Integer(level, "height") == 32, "C06 must use its complete 50 by 32 river scene.");
        Require(Text(state, "phase") == "playing" && Integer(state, "turn") == 1, "The actual opening must begin playing at T1.");
        Require(Units(state).Count(unit => Integer(unit, "owner") == 1) == 13
            && Units(state).Count(unit => Integer(unit, "owner") == 2) == 8, "C06 retains thirteen allies and eight initial pursuers.");
        Require(Integer(level["rules"]!, "turnLimit") == 13 && Integer(level["extra"]!, "recommendedTurns") == 11,
            "The canonical hard deadline remains thirteen; eleven is only a recommendation.");
        var primary = level["players"]![0]!["objectives"]![0]!;
        var condition = primary["condition"]!;
        Require(Text(primary, "type") == "failOn" && Text(condition, "type") == "unitCount"
            && Text(condition, "op") == "lte" && Integer(condition, "value") == 0
            && condition["selector"]!["keys"]!.AsArray().Select(value => value!.GetValue<string>()).SequenceEqual(["campaign-laiya"]),
            "Laiya's loss must remain an immediate critical failure.");
        var escort = primary["objective"]!;
        Require(Text(escort, "type") == "escort" && Text(escort, "zone") == "north-bank" && Integer(escort, "count") == 4
            && escort["selector"]!["keys"]!.AsArray().Select(value => value!.GetValue<string>()).SequenceEqual(EscortKeys),
            "The original four specific noncombatants must all be present in the north-bank zone to complete the escort.");
        var triggers = level["scenario"]!["triggers"]!.AsArray().OfType<JsonObject>().ToArray();
        var wave = triggers.Single(trigger => Text(trigger, "id") == "pursuit-wave");
        Require(Text(wave, "timing") == "turnStart" && wave["repeat"] is null
            && Text(wave["condition"]!, "type") == "turnAtLeast" && Integer(wave["condition"]!, "turn") == 4
            && wave["effects"]![0]!["units"]!.AsArray().Count == 3
            && Text(wave["effects"]![1]!, "signal") == "white_river_pursuit_arrived", "Three one-shot pursuit reinforcements retain their T4 arrival and signal.");
        var damage = triggers.Single(trigger => Text(trigger, "id") == "bridge-damaged");
        Require(Text(damage, "timing") == "turnStart" && damage["repeat"] is null
            && Integer(damage["condition"]!, "turn") == 6 && damage["effects"]!.AsArray().Count == 1
            && Text(damage["effects"]![0]!, "type") == "emitSignal" && Text(damage["effects"]![0]!, "signal") == "white_river_bridge_damaged",
            "T6 remains a warning signal, not an invented bridge-collapse mechanic.");
    }

    private static readonly string[] EscortKeys = ["refugee-1", "refugee-2", "refugee-3", "refugee-4"];

    private void CheckDeadlineHud(BattleEngine engine, JsonObject level)
    {
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        foreach (var deadline in new[] { 13, 17 })
        {
            var fixture = level.DeepClone().AsObject();
            fixture["rules"]!["turnLimit"] = deadline;
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), fixture);
            var hud = session.ProjectHud(new(1, 0), new(), null, null, false);
            Require(hud.Sections.Single(section => section.Key == "crown").Rows.Single(row => row.Key == "turn").Label.Contains($"1 / {deadline}", StringComparison.Ordinal),
                "The production HUD must read the live rules.turnLimit, including an independently changed test deadline.");
        }
    }

    private void CheckPainting(BattleEngine engine, JsonObject level, JsonObject state, JsonObject audit)
    {
        var scene = AuthoredScene.Load("res://data/scenes/c01-06.json");
        Require(scene.RuntimeReady && scene.PaintedGroundPath == "res://data/scenes/c01-06-painted-ground.json", "The ready C06 scene must bind its reviewed mother painting.");
        var art = PresentationJson.Load<PaintedGroundDocument>(scene.PaintedGroundPath!);
        art.ValidateLevel(level);
        Require(art.LevelId == LevelId && art.ResourcePath == "res://assets/native/scenes/white-river.png"
            && art.PixelSize.SequenceEqual([1643, 957]) && art.WorldScale == 1.25f
            && art.OriginPixels.SequenceEqual([20f, 30f]) && art.CellXPixels.SequenceEqual([32f, 0f]) && art.CellYPixels.SequenceEqual([0f, 28f]),
            "C06 must preserve full-source registration, without cropping or a replacement floor.");
        var map = state["map"]!.AsObject();
        var sites = audit["sites"]!.AsArray().OfType<JsonObject>().Select(site => Cell(site["cell"]!)).ToHashSet();
        var tiles = map["tiles"]!.AsArray();
        var canonicalSites = tiles.Select((tile, index) => (tile, index))
            .Where(item => engine.Catalog.Definition("terrains", item.tile!.GetValue<string>())!.Value.GetProperty("capturable").GetBoolean())
            .Select(item => new Coord(item.index % 50, item.index / 50)).ToHashSet();
        Require(sites.Count == 10 && canonicalSites.SetEquals(sites)
            && tiles.Count(tile => tile!.GetValue<string>() == "village") == 5
            && tiles.Count(tile => tile!.GetValue<string>() == "c01.outpost") == 5, "Exactly five villages and five ordinary outposts must be canonical sites.");
        var board = new BoardView { AnimationsEnabled = false };
        AddChild(board);
        try
        {
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, art.CreatePlane), level, scene);
            board.ConfigureGround(art.Factory(map));
            var before = EcmaJson.Stringify(state);
            board.RenderState(state);
            var ground = Descendants(board).OfType<PaintedGroundView>().Single();
            Require(board.RenderingIssueCount == 0 && board.RenderedUnitCount == Units(state).Count(), "The production board must render every C06 actor without missing-art diagnostics.");
            Require(ground.Sites.Count == 10 && ground.Sites.Select(site => site.Cell).ToHashSet().SetEquals(sites)
                && ground.Sites.All(site => site.OwnsMapStructureBody) && board.RenderedStructureCount == 0, "Ten painted objects must own the ten sites without duplicate structure bodies.");
            Require(ground.DepthEntries.Count == art.Occluders.Sum(item => item.Parts.Length), "Every material part must join the production depth compositor exactly once.");
            foreach (var item in art.Occluders.Where(item => item.SelectionCell is not null))
            {
                var cell = new BoardCell(item.SelectionCell![0], item.SelectionCell[1]);
                var targets = item.Parts.Where(part => part.Pointer.Kind == "target").ToArray();
                Require(targets.Length > 0 && targets.Any(part => InteriorSamples(part.Pointer.PolygonPixels!)
                    .Any(pixel => board.Pick(pixel * art.WorldScale) == TacticalHit.Object(cell))),
                    $"The production picker must reach {item.Id}'s site through actual facade/rail material.");
                Require(item.Parts.Where(part => part.Id.Contains("roof", StringComparison.Ordinal)).All(part => part.Pointer.Kind == "pass-through"),
                    $"{item.Id}'s roof must not steal ground or unit selection.");
            }
            foreach (var site in audit["sites"]!.AsArray().OfType<JsonObject>())
            {
                var cell = Cell(site["cell"]!);
                Require(TacticalQueries.UnitAt(state, cell) is null
                    && board.Pick(board.Projection.CellContact(cell)) == TacticalHit.Ground(new(cell.X, cell.Y)),
                    $"{Text(site, "id")}'s open entrance must remain free ground rather than an invented facade floor.");
            }
            Require(board.Pick(new(-10, -10)) is null && board.Pick(board.FieldSize + new Vector2(10, 10)) is null,
                "Off-canvas scenery must not become tactical ground.");
            Require(EcmaJson.Stringify(state) == before, "Registration, rendering and picking cannot mutate canonical battle state.");
        }
        finally
        {
            RemoveChild(board);
            board.Free();
        }
    }

    private void CheckMovement(BattleEngine engine, JsonObject level, JsonObject state, JsonObject audit)
    {
        var movement = new MovementRules(new(engine.Catalog, engine.Grids));
        var battlefield = movement.Battlefield(state);
        var map = state["map"]!;
        var foot = Unit(state, "campaign-torren");
        var before = EcmaJson.Stringify(state);
        foreach (var house in audit["houses"]!.AsArray().OfType<JsonObject>())
        {
            var cells = house["blockedCells"]!.AsArray().Select(cell => Cell(cell!)).ToArray();
            Require(cells.Length > 0 && cells.All(cell => Text(map["tiles"]![cell.Y * 50 + cell.X]!) == "wall" && !battlefield.Cell(cell).Admits("foot")),
                $"{Text(house, "id")}'s complete conservative foundation must be physically impassable.");
            var probe = foot.DeepClone().AsObject();
            Position(probe, Cell(house["approach"]!));
            var field = engine.Space.ComputeMoveField(state, probe);
            Require(cells.All(cell => !field.Stops.Contains(cell.Y * 50 + cell.X) && engine.Space.PathTo(field, state, cell) is null),
                $"Production movement cannot enter any foundation cell of {Text(house, "id")}.");
        }
        var bridges = audit["bridges"]!.AsArray().OfType<JsonObject>().ToArray();
        Require(bridges.Length == 3, "The scene must register three real crossings.");
        foreach (var bridge in bridges)
        {
            var bridgeCells = bridge["cells"]!.AsArray().Select(cell => Cell(cell!)).ToHashSet();
            Require(bridgeCells.Count > 0 && bridgeCells.All(cell => Text(map["tiles"]![cell.Y * 50 + cell.X]!) == "bridge" && battlefield.Cell(cell).Admits("foot")),
                $"{Text(bridge, "id")} must use actual walkable bridge cells.");
            var route = bridge["route"]!.AsArray().Select(cell => Cell(cell!)).ToArray();
            Require(route.Length > 2, "Each crossing must have an explicit continuous bank-to-bank route.");
            for (var index = 0; index < route.Length - 1; index++)
            {
                var probe = foot.DeepClone().AsObject();
                Position(probe, route[index]);
                Require(engine.Space.Board(state).Distance(route[index], route[index + 1]) == 1
                    && engine.Space.PathTo(engine.Space.ComputeMoveField(state, probe), state, route[index + 1]) is { Count: > 1 },
                    $"The production movement owner must connect {Text(bridge, "id")} step {route[index]} to {route[index + 1]}.");
            }
        }
        var water = audit["waterCells"]!.AsArray().Select(cell => Cell(cell!)).ToArray();
        Require(water.Length > 0 && water.All(cell => !battlefield.Cell(cell).Admits("foot")), "The entire canonical river must reject foot movement.");
        foreach (var terrain in new[] { "wall", "water" })
        {
            var occupied = Units(level).Select(TacticalQueries.Position).ToHashSet();
            var target = map["tiles"]!.AsArray().Select((tile, index) => (tile, index))
                .Where(item => Text(item.tile!) == terrain).Select(item => new Coord(item.index % 50, item.index / 50))
                .First(cell => engine.Space.Board(state).Neighbours(cell).Any(next => battlefield.Cell(next).Admits("foot") && !occupied.Contains(next)));
            var origin = engine.Space.Board(state).Neighbours(target).First(cell => battlefield.Cell(cell).Admits("foot") && !occupied.Contains(cell));
            var fixture = level.DeepClone().AsObject();
            Position(Unit(fixture, "campaign-torren"), origin);
            var battle = engine.CreateBattle(fixture);
            var actor = Unit(battle.Snapshot(), "campaign-torren");
            var hash = battle.StateHash;
            var rejected = false;
            try
            {
                battle.Dispatch(WaitCommand(actor, [origin, target]));
            }
            catch (IllegalActionException) { rejected = true; }
            Require(rejected && battle.StateHash == hash, $"A dispatched move into {terrain} must be refused atomically, not merely hidden by rendering.");
        }
        Require(EcmaJson.Stringify(state) == before, "Detached movement queries must not mutate the canonical opening.");
    }

    private void CheckSiteInteractions(BattleEngine engine, JsonObject level, JsonObject audit)
    {
        foreach (var site in audit["sites"]!.AsArray().OfType<JsonObject>())
        {
            var at = Cell(site["cell"]!);
            var approach = Cell(site["approach"]!);
            var fixture = level.DeepClone().AsObject();
            var seed = Unit(fixture, "campaign-torren");
            Position(seed, approach);
            var maximum = engine.Catalog.Definition("units", Text(seed, "unit"))!.Value.GetProperty("maxHp").GetDouble();
            seed["hp"] = maximum - 30;
            var battle = engine.CreateBattle(fixture);
            var state = battle.Snapshot();
            var actor = Unit(state, "campaign-torren");
            var index = at.Y * 50 + at.X;
            var path = engine.Space.PathTo(engine.Space.ComputeMoveField(state, actor), state, at);
            Require(path is { Count: > 1 } && engine.Support.CanCapture(state, actor, at),
                $"{Text(site, "id")} must offer a real movement-and-capture order from its doorway approach.");
            var command = WaitCommand(actor, path!);
            command["command"]!["ability"] = "capture";
            var first = battle.Dispatch(command);
            state = battle.Snapshot();
            Require(first.Events.Any(entry => Text(entry, "type") == "capture" && !entry["captured"]!.GetValue<bool>())
                && state["map"]!["owners"]![index]!.GetValue<int>() == 0
                && state["map"]!["captureProgress"]![index]!.GetValue<double>() > 0,
                $"{Text(site, "id")} must retain genuine injured-unit progressive capture without premature ownership.");
            var saved = engine.SaveBattleJson(state, "2026-10-25T00:00:00.000Z");
            var restored = engine.LoadSavedBattle(EcmaJson.Parse(saved));
            Require(restored.StateHash == battle.StateHash && EcmaJson.DeepEquals(restored.Snapshot(), state),
                $"{Text(site, "id")} partial capture must round-trip exactly through production JSON persistence.");
            battle = restored;
            battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            state = battle.Snapshot();
            actor = Unit(state, "campaign-torren");
            Require(actor["hp"]!.GetValue<double>() == maximum - 30, "Neutral sites must not heal the occupying actor.");
            command = WaitCommand(actor, [at]);
            command["command"]!["ability"] = "capture";
            var captured = battle.Dispatch(command);
            state = battle.Snapshot();
            Require(captured.Events.Any(entry => Text(entry, "type") == "capture" && entry["captured"]!.GetValue<bool>())
                && state["map"]!["owners"]![index]!.GetValue<int>() == 1 && state["map"]!["captureProgress"]![index]!.GetValue<double>() == 0,
                $"{Text(site, "id")} must commit its second capture and clear partial progress.");
            var terrain = engine.Catalog.Definition("terrains", Text(state["map"]!["tiles"]![index]!))!.Value;
            var fundsBefore = state["players"]![0]!["resources"]!["funds"]!["current"]!.GetValue<double>();
            var income = terrain.GetProperty("ownerTurnGrants").EnumerateArray().Where(grant => grant.GetProperty("resource").GetString() == "funds")
                .Sum(grant => grant.GetProperty("amount").GetDouble());
            battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            Require(Unit(battle.Snapshot(), "campaign-torren")["hp"]!.GetValue<double>() == maximum - 30,
                "Ownership must not heal during the opposing turn.");
            var ownerTurn = battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            state = battle.Snapshot();
            var healing = terrain.GetProperty("heal").GetDouble();
            Require(Unit(state, "campaign-torren")["hp"]!.GetValue<double>() == maximum - 30 + healing
                && ownerTurn.Events.Any(entry => Text(entry, "type") == "regen" && Integer(entry, "unit") == Integer(actor, "id")
                    && entry["amount"]!.GetValue<double>() == healing),
                $"{Text(site, "id")} must heal by its canonical terrain rate on the next owner turn.");
            Require(state["players"]![0]!["resources"]!["funds"]!["current"]!.GetValue<double>() == fundsBefore + income,
                $"{Text(site, "id")} must grant only its canonical income, with no invented outpost revenue.");
        }
    }

    private void CheckOpeningEscortMoves(BattleEngine engine, JsonObject level)
    {
        foreach (var key in EscortKeys)
        {
            var battle = engine.CreateBattle(level);
            var state = battle.Snapshot();
            var actor = Unit(state, key);
            var path = OpeningPath(engine, state, actor);
            Require(path.Count > 1, $"{key} must have a legal opening movement order, not be trapped by the enlarged scenery.");
            battle.Dispatch(WaitCommand(actor, path));
            Require(TacticalQueries.Position(Unit(battle.Snapshot(), key)) == path[^1] && Text(battle.Snapshot(), "phase") == "playing",
                $"{key}'s real opening movement must commit and leave the escort pending.");
        }
    }

    private static IReadOnlyList<Coord> OpeningPath(BattleEngine engine, JsonObject state, JsonObject actor)
    {
        var field = engine.Space.ComputeMoveField(state, actor);
        var origin = TacticalQueries.Position(actor);
        var width = Integer(state["map"]!, "width");
        return field.Stops.Select(index => new Coord(index % width, index / width))
            .Where(cell => cell != origin).Select(cell => engine.Space.PathTo(field, state, cell))
            .First(path => path is { Count: > 1 })!;
    }

    private void CheckNoAiEscortRoute(BattleEngine engine, JsonObject level, JsonObject audit)
    {
        // Only the ordinary command owner advances this unchanged opening. Opposing sides hand off without AI orders.
        var battle = engine.CreateBattle(level);
        var sharedRoute = audit["escortRoute"]!.AsArray().Select(cell => Cell(cell!)).ToArray();
        var routes = new Dictionary<string, Coord[]>(StringComparer.Ordinal)
        {
            ["refugee-1"] = sharedRoute,
            ["refugee-2"] = sharedRoute[..^1].Concat(new Coord[] { new(27, 6), new(27, 5) }).ToArray(),
            ["refugee-3"] = sharedRoute[..^1].Concat(new Coord[] { new(27, 6), new(28, 6), new(28, 5) }).ToArray(),
            ["refugee-4"] = sharedRoute[..^1].Concat(new Coord[] { new(27, 6), new(28, 6) }).ToArray()
        };
        var orders = 0;
        var handoffs = 0;
        while (Text(battle.Snapshot(), "phase") == "playing" && Integer(battle.Snapshot(), "turn") <= 13 && handoffs < 26)
        {
            var state = battle.Snapshot();
            if (Integer(state, "currentPlayer") == 1)
            {
                var order = EscortKeys.OrderBy(key => TacticalQueries.Position(Unit(state, key)).Y)
                    .ThenByDescending(key => TacticalQueries.Position(Unit(state, key)).X).ToArray();
                foreach (var key in order)
                {
                    state = battle.Snapshot();
                    if (Text(state, "phase") != "playing")
                        break;
                    var actor = Unit(state, key);
                    var origin = TacticalQueries.Position(actor);
                    var route = routes[key];
                    if (origin == route[^1])
                        continue;
                    var field = engine.Space.ComputeMoveField(state, actor);
                    var waypoint = Array.FindLastIndex(route, cell => field.Stops.Contains(cell.Y * 50 + cell.X));
                    var originIndex = Array.IndexOf(route, origin);
                    Require(waypoint > originIndex, $"The unchanged opening route must advance {key} at T{Integer(state, "turn")}, not require a teleport or allied relocation.");
                    var path = engine.Space.PathTo(field, state, route[waypoint]);
                    Require(path is { Count: > 1 }, $"{key}'s authored route advance must have a real movement path.");
                    battle.Dispatch(WaitCommand(actor, path!));
                    orders++;
                }
            }
            if (Text(battle.Snapshot(), "phase") == "playing")
            {
                battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
                handoffs++;
            }
        }
        var final = battle.Snapshot();
        Require(Text(final, "phase") == "over" && Text(final["conclusion"]!, "kind") == "objectiveCompleted"
            && Integer(final["conclusion"]!, "team") == 1 && Text(final["conclusion"]!, "objective") == "primary"
            && Integer(final, "turn") <= 13, "All four actual opening escorts must complete the primary objective within the unchanged deadline through legal commands alone.");
        Require(Units(final).Count(unit => EscortKeys.Contains(Text(unit, "key"), StringComparer.Ordinal)) == 4,
            "Arrival must retain all four escort units rather than replacing or removing them.");
        GD.Print($"C06 unchanged-opening route feasibility: {orders} legal escort move/wait orders, completion T{Integer(final, "turn")}; no AI actions, teleportation, stat edits or map edits. This is route feasibility, not combat balance or GUI acceptance.");
    }

    private void CheckNoAiTiming(BattleEngine engine, JsonObject level)
    {
        // Detached timing probes dispatch handoffs but never AI orders. They are not defensive-play acceptance.
        foreach (var arrived in new[] { 0, 3, 4 })
        {
            var fixture = level.DeepClone().AsObject();
            var zone = fixture["scenario"]!["zones"]!.AsArray().OfType<JsonObject>().Single(zone => Text(zone, "id") == "north-bank")["cells"]!
                .AsArray().OfType<JsonObject>().Select(TacticalQueries.Position).ToArray();
            Require(zone.Length >= 4, "The extraction zone must physically fit all four escort units.");
            for (var index = 0; index < arrived; index++)
                Position(Unit(fixture, EscortKeys[index]), zone[index]);
            var battle = engine.CreateBattle(fixture);
            var signals = new Dictionary<string, int>(StringComparer.Ordinal);
            var initialMap = EcmaJson.Stringify(battle.Snapshot()["map"]!["tiles"]);
            var handoffs = 0;
            while (Text(battle.Snapshot(), "phase") == "playing" && handoffs < 28)
            {
                var receipt = battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
                handoffs++;
                foreach (var entry in receipt.Events.Where(entry => entry["signal"] is not null))
                {
                    var signal = Text(entry, "signal");
                    signals[signal] = signals.GetValueOrDefault(signal) + 1;
                }
                var after = battle.Snapshot();
                var turn = Integer(after, "turn");
                Require(Units(after).Count(unit => Text(unit, "key").StartsWith("pursuit-wave-", StringComparison.Ordinal)) == (turn >= 4 ? 3 : 0),
                    "The three pursuit units appear once at T4, not earlier or repeatedly.");
                Require(EcmaJson.Stringify(after["map"]!["tiles"]) == initialMap, "The T6 warning must not silently change physical bridge terrain.");
                if (arrived < 4 && turn <= 13)
                    Require(Text(after, "phase") == "playing", "Zero or three arrivals must remain pending through the full thirteenth turn.");
            }
            var final = battle.Snapshot();
            Require(Text(final, "phase") == "over", "The detached escort/timer probe must reach its canonical terminal boundary.");
            if (arrived == 4)
                Require(Text(final["conclusion"]!, "kind") == "objectiveCompleted" && Integer(final["conclusion"]!, "team") == 1
                    && Text(final["conclusion"]!, "objective") == "primary", "Four simultaneous arrivals complete the actual primary escort objective.");
            else
            {
                Require(Integer(final, "turn") == 14 && Text(final["conclusion"]!, "kind") == "turnLimitReached",
                    "Insufficient arrivals expire on entry to T14, not T8, T9 or the start of T13.");
                Require(signals.GetValueOrDefault("white_river_pursuit_arrived") == 1 && signals.GetValueOrDefault("white_river_bridge_damaged") == 1,
                    "T4 pursuit and T6 bridge warning each emit once.");
            }
        }
        var missingLeader = level.DeepClone().AsObject();
        missingLeader["scenario"]!["triggers"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "detached-leader-absence",
            ["timing"] = "turnStart",
            ["condition"] = new JsonObject { ["type"] = "turnAtLeast", ["turn"] = 2 },
            ["effects"] = new JsonArray(new JsonObject
            {
                ["type"] = "withdrawUnits",
                ["selector"] = new JsonObject { ["keys"] = new JsonArray("campaign-laiya") }
            })
        });
        var failed = engine.CreateBattle(missingLeader);
        for (var handoff = 0; Text(failed.Snapshot(), "phase") == "playing" && handoff < 3; handoff++)
            failed.Dispatch(new JsonObject { ["kind"] = "endTurn" });
        Require(Text(failed.Snapshot(), "phase") == "over" && Integer(failed.Snapshot()["conclusion"]!, "team") == 2
            && Integer(failed.Snapshot(), "turn") == 2,
            "A detached T2 Laiya-withdrawal fixture fails immediately through the canonical critical objective owner.");
        GD.Print("C06 detached no-AI probes: four arrivals complete; zero/three remain pending to T14; T4 pursuit and T6 signal-only warning; absent Laiya fails. These are not normal-play acceptance.");
    }

    private void CheckSaveAdmission(BattleEngine engine, JsonArray levels)
    {
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        var detached = definition.DeepClone().AsObject();
        detached["start"] = "brief-06";
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        GrayFlagCampaign Campaign(JsonArray source, JsonObject? rules = null) => new(new(engine, presentation), rules ?? detached, source);
        var live = Campaign(levels);
        live.Advance();
        Require(Text(live.Node(), "id") == "choice-white-river-priority", "The C06 briefing must retain its authored evacuation-priority choice.");
        live.Choose("people-first");
        var preBattle = live.Save();
        var restoredPreBattle = Campaign(levels, definition);
        restoredPreBattle.Load(preBattle);
        Require(!restoredPreBattle.HasPendingBattle && Text(restoredPreBattle.Node(), "id") == "battle-06"
            && EcmaJson.DeepEquals(restoredPreBattle.Snapshot(), live.Snapshot()), "Current detached C06 pre-battle save must load through unchanged production admission.");
        live.BeginBattle();
        var session = live.Battle!;
        var opening = session.Snapshot();
        var actor = Unit(opening, "refugee-1");
        var path = OpeningPath(engine, opening, actor);
        Require(path is { Count: > 1 } && session.Dispatch(EcmaJson.ToElement(WaitCommand(actor, path)), session.Revision).Accepted && session.Events.Count > 0,
            "Atomic rejection must protect an actually played C06 session, not an untouched opening.");
        // Exact C06 member from 0570488f:godot/data/grayflag-campaign-levels.json, never registered in production.
        var retiredLevel = PresentationJson.Load<JsonObject>("res://tests/native/white-river-retired-level.json");
        Require(Text(retiredLevel, "id") == LevelId && Integer(retiredLevel, "width") == 24 && Integer(retiredLevel, "height") == 14,
            "Retired admission input must be the actual old 24 by 14 C06, not a fabricated current snapshot.");
        var retiredLevels = new JsonArray(levels.OfType<JsonObject>().Select(level => (JsonNode)(Text(level, "id") == LevelId ? retiredLevel : level).DeepClone()).ToArray());
        var retiredDefinition = definition.DeepClone().AsObject();
        retiredDefinition["start"] = "battle-06";
        var retired = Campaign(retiredLevels, retiredDefinition);
        retired.BeginBattle();
        var retiredSave = retired.Save();
        var admitted = engine.LoadBattle(retiredSave["battle"]);
        Require(Integer(admitted["map"]!, "width") == 24 && Integer(admitted["map"]!, "height") == 14
            && EcmaJson.DeepEquals(admitted, retired.Battle!.Snapshot()), "Retired battle must be independently valid before strict current-campaign admission rejects it.");
        Require(!EcmaJson.DeepEquals(live.Snapshot(), retired.Snapshot()), "The candidate campaign must differ so premature runtime publication is observable.");
        var campaignBefore = live.Snapshot();
        var requestBefore = live.RequestSnapshot();
        var battleBefore = session.Snapshot();
        var eventsBefore = Events(session);
        var revisionBefore = session.Revision;
        var inputBefore = EcmaJson.Stringify(retiredSave);
        var rejected = false;
        try
        {
            live.Load(retiredSave);
        }
        catch (StoredDocumentException error)
        {
            Require(error.Message == "Saved battle does not match the campaign level.", "Retired C06 must fail for canonical level mismatch, not a malformed fixture.");
            rejected = true;
        }
        Require(rejected && EcmaJson.DeepEquals(live.Snapshot(), campaignBefore) && EcmaJson.DeepEquals(live.RequestSnapshot(), requestBefore)
            && live.HasPendingBattle && !live.AtCamp, "Rejected retired loading must preserve current campaign and pending request atomically.");
        Require(ReferenceEquals(live.Battle, session) && EcmaJson.DeepEquals(session.Snapshot(), battleBefore)
            && Events(session) == eventsBefore && session.Revision == revisionBefore && EcmaJson.Stringify(retiredSave) == inputBefore,
            "Rejected loading must preserve session identity, played state, events, revision and input bytes.");
        var restored = Campaign(levels, definition);
        restored.Load(live.Save());
        Require(EcmaJson.DeepEquals(restored.Snapshot(), campaignBefore) && EcmaJson.DeepEquals(restored.RequestSnapshot(), requestBefore)
            && EcmaJson.DeepEquals(restored.Battle!.Snapshot(), battleBefore) && Events(restored.Battle!) == eventsBefore,
            "Current played C06 save must round-trip campaign, pending request, state and events.");
        live.ExitBattle();
        var campSave = live.Save();
        var camp = Campaign(levels, definition);
        camp.Load(campSave);
        Require(Text(campSave, "mode") == "camp" && campSave["battle"] is null && campSave["events"]!.AsArray().Count == 0
            && camp.AtCamp && EcmaJson.DeepEquals(camp.Battle!.Snapshot(), opening), "A current camp save must reproject the current opening without played state.");
    }

    private void ExportGuiFixtures(BattleEngine engine, JsonArray levels)
    {
        const string prefix = "--export-c06-gui-dir=";
        var args = OS.GetCmdlineUserArgs();
        Require(args.All(argument => argument.StartsWith(prefix, StringComparison.Ordinal)) && args.Length <= 1, "Only the explicit C06 evidence export option is supported.");
        if (args.Length == 0)
            return;
        var path = args[0][prefix.Length..];
        Require(Path.IsPathFullyQualified(path) && !Path.GetFullPath(path).StartsWith(Path.GetFullPath(OS.GetUserDataDir()), StringComparison.Ordinal),
            "GUI fixtures require an absolute caller-selected evidence directory outside user saves.");
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        var detached = definition.DeepClone().AsObject();
        detached["start"] = "brief-06";
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        var campaign = new GrayFlagCampaign(new(engine, presentation), detached, levels);
        Directory.CreateDirectory(path);
        void Export(string filename)
        {
            var save = campaign.Save();
            var production = new GrayFlagCampaign(new(engine, presentation), definition, levels);
            production.Load(save);
            Require(EcmaJson.DeepEquals(production.Snapshot(), campaign.Snapshot()), "Exported evidence must pass the unchanged production campaign loader.");
            File.WriteAllText(Path.Combine(path, filename), EcmaJson.Stringify(save));
        }
        Export("c06-detached-briefing.json");
        campaign.Advance();
        Export("c06-detached-choice.json");
        campaign.Choose("people-first");
        Export("c06-detached-staging.json");
        campaign.BeginBattle();
        Export("c06-detached-opening.json");
        Require(campaign.Snapshot()["battleHistory"]!.AsArray().Count == 0, "Detached GUI fixtures cannot fabricate earlier campaign victories.");
        GD.Print("Exported detached C06 briefing, priority choice, staging and opening to " + path + ". No earlier victories are claimed and no user save was changed.");
    }

    private static JsonObject WaitCommand(JsonObject actor, IReadOnlyList<Coord> path) => new()
    {
        ["kind"] = "command",
        ["unit"] = Integer(actor, "id"),
        ["path"] = TacticalQueries.PathJson(path),
        ["command"] = new JsonObject { ["ability"] = "wait" }
    };
    private static string Events(GrayFlagSession session) => EcmaJson.Stringify(new JsonArray(session.Events.Select(value => (JsonNode)value).ToArray()));
    private static IEnumerable<Vector2> InteriorSamples(double[][] points)
    {
        var polygon = points.Select(point => new Vector2((float)point[0], (float)point[1])).ToArray();
        var triangles = Geometry2D.TriangulatePolygon(polygon);
        for (var index = 0; index < triangles.Length; index += 3)
            yield return (polygon[triangles[index]] + polygon[triangles[index + 1]] + polygon[triangles[index + 2]]) / 3;
        for (var y = polygon.Min(point => point.Y) + 1; y < polygon.Max(point => point.Y); y += 3)
            for (var x = polygon.Min(point => point.X) + 1; x < polygon.Max(point => point.X); x += 3)
                if (Geometry2D.IsPointInPolygon(new(x, y), polygon))
                    yield return new(x, y);
    }
    private static IEnumerable<Node> Descendants(Node root) => root.GetChildren().SelectMany(child => new[] { child }.Concat(Descendants(child)));
    private static IEnumerable<JsonObject> Units(JsonObject state) => state["units"]!.AsArray().OfType<JsonObject>();
    private static JsonObject Unit(JsonObject state, string key) => Units(state).Single(unit => Text(unit, "key") == key);
    private static string Text(JsonNode value, string key) => value[key]!.GetValue<string>();
    private static string Text(JsonNode value) => value.GetValue<string>();
    private static int Integer(JsonNode value, string key) => value[key]!.GetValue<int>();
    private static Coord Cell(JsonNode value) => new(value[0]!.GetValue<int>(), value[1]!.GetValue<int>());
    private static Vector2 Vector(JsonNode value) => new(value[0]!.GetValue<float>(), value[1]!.GetValue<float>());
    private static void Position(JsonObject unit, Coord at)
    {
        unit["x"] = at.X;
        unit["y"] = at.Y;
    }
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
