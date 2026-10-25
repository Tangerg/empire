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

/// <summary>C05 production contracts and explicitly detached no-AI timing probes, not normal play or GUI acceptance.</summary>
public partial class ReedVillageChecks : Node
{
    private const string LevelId = "c01-05";
    private int assertions;

    public override void _Ready()
    {
        try
        {
            var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
            var level = levels.OfType<JsonObject>().Single(value => Text(value, "id") == LevelId);
            var audit = PresentationJson.Load<JsonObject>("res://docs/art-provenance/reed-village-geometry.json");
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var state = engine.CreateState(level);
            CheckMissionSource(level, state);
            CheckPainting(engine, level, state, audit);
            CheckMovement(engine, level, state, audit);
            CheckOpeningEscape(engine, level);
            CheckNoAiTiming(engine, level);
            CheckSaveAdmission(engine, levels);
            ExportGuiFixtures(engine, levels);
            GD.Print($"Native ReedVillage passed {assertions} assertions: twelve sites, door/roof picking, physical foundations, bridge/water movement, opening civilian escape, T4/T8 no-AI timing and strict save admission. No-AI timing is not normal-play or GUI acceptance.");
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
        Require(Integer(level, "width") == 50 && Integer(level, "height") == 32, "C05 must use the canonical 50 by 32 map.");
        Require(Text(state, "phase") == "playing" && Integer(state, "turn") == 1, "The real opening must begin playing at T1.");
        Require(Units(state).Count(unit => Integer(unit, "owner") == 1) == 13
            && Units(state).Count(unit => Integer(unit, "owner") == 2) == 8
            && Units(state).Count(unit => Text(unit, "type") == "c01.refugee") == 3, "The opening must retain 13 allies, 8 enemies and all 3 civilians.");
        Require(Integer(level["rules"]!, "turnLimit") == 11 && Integer(level["extra"]!, "recommendedTurns") == 10,
            "The unchanged hard limit is eleven and recommendation is ten.");
        var primary = level["players"]![0]!["objectives"]![0]!;
        var condition = primary["condition"]!;
        Require(Text(primary, "type") == "failOn" && Text(condition, "type") == "unitCount"
            && Text(condition, "op") == "lte" && Integer(condition, "value") == 0
            && condition["selector"]!["keys"]!.AsArray().Select(value => value!.GetValue<string>()).SequenceEqual(["campaign-laiya"]),
            "The primary objective must fail if Laiya is no longer alive on the field.");
        var objective = primary["objective"]!;
        var children = objective["objectives"]!.AsArray();
        Require(Text(objective, "type") == "all" && children.Count == 2
            && Text(children[0]!, "type") == "protect" && Integer(children[0]!, "minimumAlive") == 2 && Integer(children[0]!, "untilTurn") == 7
            && children[0]!["selector"]!["anyTags"]!.AsArray().Select(value => value!.GetValue<string>()).SequenceEqual(["civilian"])
            && Text(children[1]!, "type") == "surviveTurns" && Integer(children[1]!, "turns") == 7,
            "Victory requires at least two civilians and survival through seven rounds, not merely arrival at T7.");
        var wave = level["scenario"]!["triggers"]!.AsArray().OfType<JsonObject>().Single();
        Require(Text(wave, "id") == "purifier-reinforcement" && Text(wave, "timing") == "turnStart" && wave["repeat"] is null
            && Text(wave["condition"]!, "type") == "turnAtLeast" && Integer(wave["condition"]!, "turn") == 4
            && wave["effects"]![0]!["units"]!.AsArray().OfType<JsonObject>().Select(TacticalQueries.Position).SequenceEqual([new Coord(13, 17), new(12, 18), new(13, 18)])
            && Text(wave["effects"]![1]!, "signal") == "gray_banner_raised", "The one-shot three-unit western wave must retain T4 timing and its campaign signal.");
    }

    private void CheckPainting(BattleEngine engine, JsonObject level, JsonObject state, JsonObject audit)
    {
        var scene = AuthoredScene.Load("res://data/scenes/c01-05.json");
        Require(scene.RuntimeReady && scene.PaintedGroundPath == "res://data/scenes/c01-05-painted-ground.json", "The ready C05 scene must bind its reviewed mother painting.");
        var art = PresentationJson.Load<PaintedGroundDocument>(scene.PaintedGroundPath!);
        art.ValidateLevel(level);
        Require(art.LevelId == LevelId && art.ResourcePath == "res://assets/native/scenes/reed-village.png"
            && art.PixelSize.SequenceEqual([1651, 953]) && art.WorldScale == 1.25f
            && art.OriginPixels.SequenceEqual([20f, 32f]) && art.CellXPixels.SequenceEqual([32f, 0f]) && art.CellYPixels.SequenceEqual([0f, 28f]),
            "C05 must preserve full-source registration, without cropping or a replacement floor.");
        var map = state["map"]!.AsObject();
        var sites = audit["sites"]!.AsArray().OfType<JsonObject>().Select(site => Cell(site["cell"]!)).ToHashSet();
        var tiles = map["tiles"]!.AsArray();
        var canonicalSites = tiles.Select((tile, index) => (tile, index))
            .Where(item => engine.Catalog.Definition("terrains", item.tile!.GetValue<string>())!.Value.GetProperty("capturable").GetBoolean())
            .Select(item => new Coord(item.index % 50, item.index / 50)).ToHashSet();
        Require(sites.Count == 12 && canonicalSites.SetEquals(sites)
            && tiles.Count(tile => tile!.GetValue<string>() == "village") == 7
            && tiles.Count(tile => tile!.GetValue<string>() == "c01.outpost") == 5, "Exactly seven villages and five ordinary outposts must be canonical sites.");
        var board = new BoardView { AnimationsEnabled = false };
        AddChild(board);
        try
        {
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, art.CreatePlane), level, scene);
            board.ConfigureGround(art.Factory(map));
            var before = EcmaJson.Stringify(state);
            board.RenderState(state);
            var ground = Descendants(board).OfType<PaintedGroundView>().Single();
            Require(board.RenderingIssueCount == 0 && board.RenderedUnitCount == Units(state).Count(), "The production board must render every C05 actor without missing-art diagnostics.");
            Require(ground.Sites.Count == 12 && ground.Sites.Select(site => site.Cell).ToHashSet().SetEquals(sites)
                && ground.Sites.All(site => site.OwnsMapStructureBody) && board.RenderedStructureCount == 0, "Twelve painted objects must own the twelve sites without duplicate structure bodies.");
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
            var probe = audit["roofRoadProbe"]!;
            var roof = art.Occluders.Single(item => item.Id == Text(probe, "objectId")).Parts.Single(part => part.Id == Text(probe, "partId"));
            var pixel = Vector(probe["pixel"]!);
            Require(Geometry2D.IsPointInPolygon(pixel, roof.PolygonPixels.Select(point => new Vector2((float)point[0], (float)point[1])).ToArray())
                && board.Pick(pixel * art.WorldScale) == TacticalHit.Ground(new(12, 11)), "A visible cottage roof over the road must select the independent road cell, not its distant doorway.");
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
        var bridge = audit["bridge"]!;
        var bridgeCells = bridge["cells"]!.AsArray().Select(cell => Cell(cell!)).ToHashSet();
        Require(bridgeCells.Count == 7 && bridgeCells.All(cell => Text(map["tiles"]![cell.Y * 50 + cell.X]!) == "bridge" && battlefield.Cell(cell).Admits("foot")),
            "All seven and only actual plank cells must use passable bridge terrain.");
        var route = bridge["route"]!.AsArray().Select(cell => Cell(cell!)).ToArray();
        for (var index = 0; index < route.Length - 1; index++)
        {
            var probe = foot.DeepClone().AsObject();
            Position(probe, route[index]);
            Require(engine.Space.Board(state).Distance(route[index], route[index + 1]) == 1
                && engine.Space.PathTo(engine.Space.ComputeMoveField(state, probe), state, route[index + 1]) is { Count: > 1 },
                $"The actual movement owner must connect bridge/bank step {route[index]} to {route[index + 1]}.");
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

    private void CheckOpeningEscape(BattleEngine engine, JsonObject level)
    {
        var battle = engine.CreateBattle(level);
        var state = battle.Snapshot();
        Require(TacticalQueries.Position(Unit(state, "purifier-5")) == new Coord(43, 24)
            && TacticalQueries.Position(Unit(state, "villager-3")) == new Coord(36, 23), "The final fair knight and civilian deployment must not regress to provisional coordinates.");
        var destination = new Coord(33, 23);
        var threat = Units(state).Where(unit => Integer(unit, "owner") == 2).SelectMany(unit => engine.Space.ThreatTiles(state, unit)).ToHashSet();
        Require(!threat.Contains(destination.Y * 50 + destination.X), "The civilian escape destination must lie outside every initial enemy move-and-attack threat.");
        var actor = Unit(state, "villager-3");
        var field = engine.Space.ComputeMoveField(state, actor);
        var path = engine.Space.PathTo(field, state, destination);
        Require(path is { Count: > 1 } && field.Stops.Contains(destination.Y * 50 + destination.X)
            && engine.CommandsAt(state, actor, destination).Any(command => command.Ability == "wait"), "The actual opening civilian must have a legal westward escape, with no fixture teleport or extra movement allowance.");
        battle.Dispatch(WaitCommand(actor, path!));
        Require(TacticalQueries.Position(Unit(battle.Snapshot(), "villager-3")) == destination && Text(battle.Snapshot(), "phase") == "playing",
            "A real first-turn civilian order must commit the safe escape without prematurely ending the mission.");
    }

    private void CheckNoAiTiming(BattleEngine engine, JsonObject level)
    {
        // These fixtures deliberately dispatch endTurn without asking the AI to act. They prove timing and failure boundaries, never ordinary defensive survival.
        foreach (var removed in new[] { Array.Empty<string>(), new[] { "villager-1" }, new[] { "villager-1", "villager-2" }, new[] { "campaign-laiya" } })
        {
            var fixture = level.DeepClone().AsObject();
            if (removed.Length > 0)
                fixture["scenario"]!["triggers"]!.AsArray().Add(new JsonObject
                {
                    ["id"] = "detached-absence-probe",
                    ["timing"] = "turnStart",
                    ["condition"] = new JsonObject { ["type"] = "turnAtLeast", ["turn"] = 2 },
                    ["effects"] = new JsonArray(new JsonObject
                    {
                        ["type"] = "withdrawUnits",
                        ["selector"] = new JsonObject { ["keys"] = new JsonArray(removed.Select(key => (JsonNode)JsonValue.Create(key)!).ToArray()) }
                    })
                });
            var battle = engine.CreateBattle(fixture);
            var signalCount = 0;
            var handoffs = 0;
            while (Text(battle.Snapshot(), "phase") == "playing" && handoffs < 16)
            {
                var state = battle.Snapshot();
                Require(Integer(state, "turn") <= 7, "No-AI timing must remain pending through T7 and complete on entry to T8.");
                var receipt = battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
                handoffs++;
                signalCount += receipt.Events.Count(entry => entry["signal"]?.GetValue<string>() == "gray_banner_raised");
                var after = battle.Snapshot();
                var wave = Units(after).Where(unit => Text(unit, "key").StartsWith("purifier-west-", StringComparison.Ordinal)
                    || Text(unit, "key") == "purifier-reinforcement").ToArray();
                Require(wave.Length == (Integer(after, "turn") >= 4 ? 3 : 0), "The real one-shot western wave must appear at T4, neither early nor repeatedly.");
            }
            var final = battle.Snapshot();
            var shouldWin = removed.Length < 2 && !removed.Contains("campaign-laiya", StringComparer.Ordinal);
            Require(Text(final, "phase") == "over" && Integer(final["conclusion"]!, "team") == (shouldWin ? 1 : 2),
                "The real objective owner must distinguish two civilians plus Laiya from one civilian or absent Laiya.");
            Require(Integer(final, "turn") == (shouldWin ? 8 : 2), "The no-AI objective must resolve at its exact T8 victory or T2 critical-failure boundary.");
            Require(signalCount == (shouldWin ? 1 : 0), "The no-AI wave emits exactly one signal before a surviving fixture finishes.");
            if (shouldWin)
                Require(Text(final["conclusion"]!, "kind") == "objectiveCompleted" && Text(final["conclusion"]!, "objective") == "primary",
                    "T8 completion must be the intended primary objective, not an unrelated elimination.");
        }
        GD.Print("C05 timing fixtures: no AI actions; T4 one-shot wave, three/two-civilian T8 completion, one-civilian/Laiya-absence T2 failure. This is not normal play.");
    }

    private void CheckSaveAdmission(BattleEngine engine, JsonArray levels)
    {
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        var detached = definition.DeepClone().AsObject();
        detached["start"] = "brief-05";
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        GrayFlagCampaign Campaign(JsonArray source, JsonObject? rules = null) => new(new(engine, presentation), rules ?? detached, source);
        var live = Campaign(levels);
        live.Advance();
        var preBattle = live.Save();
        var restoredPreBattle = Campaign(levels, definition);
        restoredPreBattle.Load(preBattle);
        Require(!restoredPreBattle.HasPendingBattle && Text(restoredPreBattle.Node(), "id") == "battle-05"
            && EcmaJson.DeepEquals(restoredPreBattle.Snapshot(), live.Snapshot()), "Current detached C05 pre-battle save must load through unchanged production admission.");
        live.BeginBattle();
        var session = live.Battle!;
        var opening = session.Snapshot();
        var actor = Unit(opening, "villager-3");
        var path = engine.Space.PathTo(engine.Space.ComputeMoveField(opening, actor), opening, new(33, 23));
        Require(path is { Count: > 1 } && session.Dispatch(EcmaJson.ToElement(WaitCommand(actor, path)), session.Revision).Accepted && session.Events.Count > 0,
            "Atomic rejection must protect an actually played C05 session, not an untouched opening.");
        // Exact C05 member from 4fa306c9:godot/data/grayflag-campaign-levels.json, never registered in production.
        var retiredLevel = PresentationJson.Load<JsonObject>("res://tests/native/reed-village-retired-level.json");
        Require(Text(retiredLevel, "id") == LevelId && Integer(retiredLevel, "width") == 21 && Integer(retiredLevel, "height") == 15,
            "Retired admission input must be the actual old 21 by 15 C05, not a fabricated current snapshot.");
        var retiredLevels = new JsonArray(levels.OfType<JsonObject>().Select(level => (JsonNode)(Text(level, "id") == LevelId ? retiredLevel : level).DeepClone()).ToArray());
        var retiredDefinition = definition.DeepClone().AsObject();
        retiredDefinition["start"] = "battle-05";
        var retired = Campaign(retiredLevels, retiredDefinition);
        retired.BeginBattle();
        var retiredSave = retired.Save();
        var admitted = engine.LoadBattle(retiredSave["battle"]);
        Require(Integer(admitted["map"]!, "width") == 21 && Integer(admitted["map"]!, "height") == 15
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
            Require(error.Message == "Saved battle does not match the campaign level.", "Retired C05 must fail for canonical level mismatch, not a malformed fixture.");
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
            "Current played C05 save must round-trip campaign, pending request, state and events.");
        live.ExitBattle();
        var campSave = live.Save();
        var camp = Campaign(levels, definition);
        camp.Load(campSave);
        Require(Text(campSave, "mode") == "camp" && campSave["battle"] is null && campSave["events"]!.AsArray().Count == 0
            && camp.AtCamp && EcmaJson.DeepEquals(camp.Battle!.Snapshot(), opening), "A current camp save must reproject the current opening without played state.");
    }

    private void ExportGuiFixtures(BattleEngine engine, JsonArray levels)
    {
        const string prefix = "--export-c05-gui-dir=";
        var args = OS.GetCmdlineUserArgs();
        Require(args.All(argument => argument.StartsWith(prefix, StringComparison.Ordinal)) && args.Length <= 1, "Only the explicit C05 evidence export option is supported.");
        if (args.Length == 0)
            return;
        var path = args[0][prefix.Length..];
        Require(Path.IsPathFullyQualified(path) && !Path.GetFullPath(path).StartsWith(Path.GetFullPath(OS.GetUserDataDir()), StringComparison.Ordinal),
            "GUI fixtures require an absolute caller-selected evidence directory outside user saves.");
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        var detached = definition.DeepClone().AsObject();
        detached["start"] = "brief-05";
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
        Export("c05-detached-briefing.json");
        campaign.Advance();
        Export("c05-detached-staging.json");
        campaign.BeginBattle();
        Export("c05-detached-opening.json");
        Require(campaign.Snapshot()["battleHistory"]!.AsArray().Count == 0, "Detached GUI fixtures cannot fabricate earlier campaign victories.");
        GD.Print("Exported detached C05 briefing, staging and opening to " + path + ". No earlier victories are claimed and no user save was changed.");
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
