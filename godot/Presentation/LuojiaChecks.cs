#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

/// <summary>C07 production contracts; detached scenario probes are not normal-AI acceptance.</summary>
public partial class LuojiaChecks : Node
{
    private const string LevelId = "c01-07";
    private int assertions;
    public override void _Ready()
    {
        try
        {
            var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
            var level = levels.OfType<JsonObject>().Single(value => Text(value, "id") == LevelId);
            var audit = PresentationJson.Load<JsonObject>("res://docs/art-provenance/luojia-geometry.json");
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            CheckMission(engine, level, audit);
            CheckMovement(engine, level, audit);
            CheckSites(engine, level, audit);
            CheckObjectives(engine, level);
            CheckSaveAdmission(engine, levels);
            CheckSourceBinding(engine, levels);
            CheckOpeningCaptureRoutes(engine, level);
            CheckPainting(engine, level, audit);
            ExportGuiFixtures(engine, levels);
            GD.Print($"Native Luojia passed {assertions} assertions: sixteen sites, authored rendering/picking, route geometry, sequential control objectives, deadline and strict save admission. Detached probes are not normal-AI balance acceptance.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void CheckMission(BattleEngine engine, JsonObject level, JsonObject audit)
    {
        var state = engine.CreateState(level);
        Require(Integer(level, "width") > 23 && Integer(level, "height") > 15, "C07 must use its authored larger city.");
        Require(Units(level).Count() == 23 && Units(level).Count(unit => Integer(unit, "owner") == 1) == 13,
            "Thirteen allied and ten hostile actors retain their identities.");
        Require(Integer(level["rules"]!, "turnLimit") == 15 && level["rules"]!["moraleEnabled"]!.GetValue<bool>(), "Fifteen-turn limit and morale remain canonical.");
        var sites = audit["sites"]!.AsArray().OfType<JsonObject>().ToArray();
        Require(sites.Length == 16 && sites.Count(site => Text(site, "terrain") == "v") == 8
            && sites.Count(site => Text(site, "terrain") == "B") == 4 && sites.Count(site => Text(site, "terrain") == "q") == 4, "Sixteen sites retain eight villages and eight nonproducing posts.");
        Require(sites.Count(site => Integer(site, "owner") == 1) == 1 && sites.Count(site => Integer(site, "owner") == 2) == 1,
            "The two original occupied outposts retain their distinct owners.");
        foreach (var zone in level["scenario"]!["zones"]!.AsArray().OfType<JsonObject>())
        {
            var position = TacticalQueries.Position(zone["cells"]![0]!.AsObject());
            Require(zone["cells"]!.AsArray().Count == 1 && sites.Any(site => Cell(site["cell"]!) == position && Text(site, "terrain") == "B"),
                "Each control target is a real field-post site, not an arbitrary plaza or production barracks.");
        }
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var session = new GrayFlagSession(new(engine, BattleTextChecks.Session(engine, text), new JsonArray()), level);
        var hud = session.ProjectHud(new(1, 0), new(), null, null, false);
        Require(hud.Sections.Single(section => section.Key == "crown").Rows.Single(row => row.Key == "turn").Label.Contains("1 / 15", StringComparison.Ordinal), "Production HUD projects the current fifteen-turn deadline.");
    }

    private void CheckPainting(BattleEngine engine, JsonObject level, JsonObject audit)
    {
        var state = engine.CreateState(level);
        var scene = AuthoredScene.Load("res://data/scenes/c01-07.json");
        Require(scene.RuntimeReady && scene.PaintedGroundPath == "res://data/scenes/c01-07-painted-ground.json", "C07 has one registered ready painting.");
        var art = PresentationJson.Load<PaintedGroundDocument>(scene.PaintedGroundPath!);
        art.ValidateLevel(level);
        Require(art.ResourcePath == "res://assets/native/scenes/luojia.png" && art.PixelSize.SequenceEqual([1536, 1024]), "The complete approved city image is used without cropping or resampling.");
        var board = new BoardView { AnimationsEnabled = false };
        AddChild(board);
        try
        {
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, art.CreatePlane), level, scene);
            board.ConfigureGround(art.Factory(state["map"]!.AsObject()));
            var before = EcmaJson.Stringify(state);
            board.RenderState(state);
            var ground = Descendants(board).OfType<PaintedGroundView>().Single();
            Require(board.RenderingIssueCount == 0 && board.RenderedUnitCount == 23, "Actual C07 opening renders all actors without missing art or invalid cues.");
            Require(ground.Sites.Count == 16 && ground.Sites.All(site => site.OwnsMapStructureBody) && board.RenderedStructureCount == 0,
                "Sixteen painted sites own their bodies without duplicate procedural buildings.");
            Require(ground.DepthEntries.Count == art.Occluders.Sum(item => item.Parts.Length), "Every reviewed material patch is depth composed exactly once.");
            foreach (var item in art.Occluders)
            {
                Require(item.Parts.Where(part => part.Id.Contains("roof", StringComparison.Ordinal)).All(part => part.Pointer.Kind == "pass-through"), "Roofs pass clicks through independently of ground collision.");
                if (item.SelectionCell is null)
                    continue;
                var cell = new BoardCell(item.SelectionCell[0], item.SelectionCell[1]);
                Require(item.Parts.Where(part => part.Pointer.Kind == "target").Any(part => InteriorSamples(part.Pointer.PolygonPixels!).Any(pixel => board.Pick(pixel * art.WorldScale) == TacticalHit.Object(cell))),
                    $"Real facade material must select {item.Id}.");
            }
            foreach (var site in audit["sites"]!.AsArray().OfType<JsonObject>())
            {
                var cell = Cell(site["cell"]!);
                Require(TacticalQueries.UnitAt(state, cell) is null && board.Pick(board.Projection.CellContact(cell)) == TacticalHit.Ground(new(cell.X, cell.Y)),
                    $"{Text(site, "id")}'s entrance remains independent ground, not a wall or invented floor.");
            }
            Require(EcmaJson.Stringify(state) == before, "Rendering cannot mutate the battle.");
            // Detached widest-silhouette probes; actual GUI pairs are separate visual evidence.
            foreach (var frontBack in new[] { false, true })
            {
                var adjacent = state.DeepClone().AsObject();
                var cavalry = Unit(adjacent, "raider-1").DeepClone().AsObject();
                var guard = Unit(adjacent, "militia").DeepClone().AsObject();
                Position(cavalry, new(48, 35));
                Position(guard, frontBack ? new(48, 36) : new(49, 35));
                adjacent["units"] = new JsonArray(cavalry, guard);
                board.RenderState(adjacent);
                foreach (var actor in new[] { cavalry, guard })
                {
                    var position = TacticalQueries.Position(actor);
                    var contact = board.Projection.CellContact(new(position.X, position.Y));
                    var hits = new List<Vector2>();
                    for (var y = -54; y <= 0; y += 2)
                        for (var x = -42; x <= 42; x += 2)
                            if (board.Pick(contact + new Vector2(x, y)) == TacticalHit.Unit(new(position.X, position.Y), Integer(actor, "id")))
                                hits.Add(new(x, y));
                    Require(hits.Count >= 40 && hits.Max(point => point.X) - hits.Min(point => point.X) >= 10
                        && hits.Max(point => point.Y) - hits.Min(point => point.Y) >= 10,
                        $"Adjacent cavalry/guard {(frontBack ? "front/back" : "side-by-side")} must retain substantial canonical picking regions, not a single exposed pixel.");
                }
            }
            board.RenderState(state);

        }
        finally { RemoveChild(board); board.Free(); }
    }

    private void CheckMovement(BattleEngine engine, JsonObject level, JsonObject audit)
    {
        var state = engine.CreateState(level);
        var width = Integer(level, "width");
        var movement = new MovementRules(new(engine.Catalog, engine.Grids));
        var battlefield = movement.Battlefield(state);
        foreach (var unit in Units(state))
            Require(battlefield.Cell(TacticalQueries.Position(unit)).Admits("foot"), "Every deployed actor stands on genuine walkable ground.");
        foreach (var polygon in audit["blockedPolygons"]!.AsArray().OfType<JsonObject>())
        {
            var cells = polygon["blockedCells"]!.AsArray().Select(cell => Cell(cell!)).ToArray();
            Require(cells.Length > 0 && cells.All(cell => !battlefield.Cell(cell).Admits("foot")), $"All of {Text(polygon, "id")}'s reviewed physical footprint blocks movement.");
        }
        Require(!battlefield.Cell(new(56, 23)).Admits("foot"), "The natural T3 closed-planter regression must remain blocked, not merely have its spawn relocated.");
        foreach (var cell in new Coord[] { new(28, 51), new(29, 51), new(30, 51), new(30, 52), new(31, 51), new(31, 52), new(32, 51),
            new(70, 51), new(71, 51), new(72, 51), new(73, 50), new(74, 49), new(74, 50), new(75, 49), new(76, 49) })
            Require(battlefield.Cell(cell).Admits("foot"), "Reviewed council and supply courtyard paving cannot be silently excluded as projection pockets.");
        var nook = new Coord(35, 47);
        Require(battlefield.Cell(nook).Admits("foot") && engine.Space.Board(state).Neighbours(nook).All(cell => !battlefield.Cell(cell).Admits("foot")),
            "The tiny council furniture nook stays truthful paving with no square4 whole-cell entrance, not invented outside scenery.");
        var empty = state.DeepClone().AsObject();
        empty["units"] = new JsonArray();
        var probe = Unit(state, "campaign-torren").DeepClone().AsObject();
        foreach (var route in audit["routes"]!.AsArray().OfType<JsonObject>())
        {
            var cells = route["cells"]!.AsArray().Select(cell => Cell(cell!)).ToArray();
            Require(cells.Length > 1, "Each visible ingress and mission route must have a genuine continuous path.");
            for (var index = 0; index < cells.Length - 1; index++)
            {
                Position(probe, cells[index]);
                Require(engine.Space.Board(empty).Distance(cells[index], cells[index + 1]) == 1
                    && engine.Space.PathTo(engine.Space.ComputeMoveField(empty, probe), empty, cells[index + 1]) is { Count: > 1 },
                    $"Production movement validates {Text(route, "id")} step {index}.");
            }
        }
        var occupied = Units(state).Select(TacticalQueries.Position).ToHashSet();
        var target = state["map"]!["tiles"]!.AsArray().Select((tile, index) => (tile, index))
            .Where(item => Text(item.tile!) == "wall").Select(item => new Coord(item.index % width, item.index / width))
            .First(cell => engine.Space.Board(state).Neighbours(cell).Any(next => battlefield.Cell(next).Admits("foot") && !occupied.Contains(next)));
        var origin = engine.Space.Board(state).Neighbours(target).First(cell => battlefield.Cell(cell).Admits("foot") && !occupied.Contains(cell));
        var fixture = level.DeepClone().AsObject();
        Position(Unit(fixture, "campaign-torren"), origin);
        var battle = engine.CreateBattle(fixture);
        var hash = battle.StateHash;
        var rejected = false;
        try
        {
            battle.Dispatch(WaitCommand(Unit(battle.Snapshot(), "campaign-torren"), [origin, target]));
        }
        catch (IllegalActionException) { rejected = true; }
        Require(rejected && battle.StateHash == hash, "A wall-entry command is rejected atomically by the actual movement owner.");
    }
    private void CheckSites(BattleEngine engine, JsonObject level, JsonObject audit)
    {
        foreach (var site in audit["sites"]!.AsArray().OfType<JsonObject>())
        {
            var at = Cell(site["cell"]!);
            var approach = Cell(site["approach"]!);
            var originalOwner = Integer(site, "owner");
            var capturer = originalOwner == 1 ? 2 : 1;
            var fixture = level.DeepClone().AsObject();
            var seed = Unit(fixture, "campaign-torren");
            seed["owner"] = capturer;
            Position(seed, approach);
            var maximum = engine.Catalog.Definition("units", Text(seed, "unit"))!.Value.GetProperty("maxHp").GetDouble();
            seed["hp"] = maximum - 30;
            var battle = engine.CreateBattle(fixture);
            if (capturer == 2)
                battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            var state = battle.Snapshot();
            var actor = Unit(state, "campaign-torren");
            var index = at.Y * Integer(level, "width") + at.X;
            Require(state["map"]!["owners"]![index]!.GetValue<int>() == originalOwner, "Detached interaction fixtures preserve the site's actual opening ownership.");
            var path = engine.Space.PathTo(engine.Space.ComputeMoveField(state, actor), state, at);
            Require(path is { Count: > 1 } && engine.Support.CanCapture(state, actor, at), $"{Text(site, "id")} offers a real capture from its entrance approach.");
            var command = WaitCommand(actor, path!);
            command["command"]!["ability"] = "capture";
            var first = battle.Dispatch(command);
            state = battle.Snapshot();
            Require(first.Events.Any(entry => Text(entry, "type") == "capture" && !entry["captured"]!.GetValue<bool>())
                && state["map"]!["owners"]![index]!.GetValue<int>() == originalOwner && state["map"]!["captureProgress"]![index]!.GetValue<double>() > 0,
                "An injured actor genuinely starts progressive capture without premature ownership.");
            var restored = engine.LoadSavedBattle(EcmaJson.Parse(engine.SaveBattleJson(state, "2026-10-25T00:00:00.000Z")));
            Require(restored.StateHash == battle.StateHash && EcmaJson.DeepEquals(restored.Snapshot(), state), "Partial capture round trips through production persistence.");
            battle = restored;
            battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            state = battle.Snapshot();
            actor = Unit(state, "campaign-torren");
            Require(actor["hp"]!.GetValue<double>() == maximum - 30, "A nonowned site cannot heal its occupier.");
            command = WaitCommand(actor, [at]);
            command["command"]!["ability"] = "capture";
            var second = battle.Dispatch(command);
            state = battle.Snapshot();
            Require(second.Events.Any(entry => Text(entry, "type") == "capture" && entry["captured"]!.GetValue<bool>())
                && state["map"]!["owners"]![index]!.GetValue<int>() == capturer, "Second capture commits the actual actor owner's control.");
            var terrain = engine.Catalog.Definition("terrains", Text(state["map"]!["tiles"]![index]!))!.Value;
            Require(!terrain.GetProperty("produces").EnumerateArray().Any(), "C07 civic buildings do not advertise invented recruitment.");
            var player = state["players"]!.AsArray().OfType<JsonObject>().Single(value => Integer(value, "id") == capturer);
            var funds = player["resources"]!["funds"]!["current"]!.GetValue<double>();
            var income = terrain.GetProperty("ownerTurnGrants").EnumerateArray().Where(grant => grant.GetProperty("resource").GetString() == "funds").Sum(grant => grant.GetProperty("amount").GetDouble());
            battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            Require(Unit(battle.Snapshot(), "campaign-torren")["hp"]!.GetValue<double>() == maximum - 30, "Ownership does not heal on the opposing turn.");
            battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            state = battle.Snapshot();
            player = state["players"]!.AsArray().OfType<JsonObject>().Single(value => Integer(value, "id") == capturer);
            Require(Unit(state, "campaign-torren")["hp"]!.GetValue<double>() == maximum - 30 + terrain.GetProperty("heal").GetDouble()
                && player["resources"]!["funds"]!["current"]!.GetValue<double>() == funds + income,
                "Owned sites grant exactly their canonical healing and income on their owner's next turn.");
        }
    }

    private void CheckObjectives(BattleEngine engine, JsonObject level)
    {
        var width = Integer(level, "width");
        var zones = level["scenario"]!["zones"]!.AsArray().OfType<JsonObject>().ToDictionary(zone => Text(zone, "id"), zone => TacticalQueries.Position(zone["cells"]![0]!.AsObject()));
        void Own(JsonObject state, string zone, int owner)
        {
            var cell = zones[zone];
            state["map"]!["owners"]![cell.Y * width + cell.X] = owner;
        }
        var state = engine.CreateState(level);
        Require(engine.Victory.Evaluate(state, _ => { }) is null, "The opening control mission remains pending.");
        Own(state, "granary", 1);
        Require(engine.Victory.Evaluate(state, _ => { }) is null
            && Text(state["players"]![0]!["objectiveStates"]!["granary"]!, "status") == "completed", "First control target completes without ending the whole mission.");
        Own(state, "granary", 2);
        Require(engine.Victory.Evaluate(state, _ => { }) is null
            && Text(state["players"]![0]!["objectiveStates"]!["granary"]!, "status") == "completed", "Existing completed control objectives latch even after recapture; this is not simultaneous control.");
        Own(state, "militia-barracks", 1);
        var won = engine.Victory.Evaluate(state, _ => { });
        Require(won?.Kind == "objectiveCompleted" && won.Team == 1 && won.Objective == "primary", "Separately completing both original control achievements wins.");
        foreach (var missingLeader in new[] { false, true })
        {
            var fixture = engine.CreateState(level);
            fixture["turn"] = 16;
            Own(fixture, "granary", 1);
            Own(fixture, "militia-barracks", 1);
            if (missingLeader)
                fixture["units"]!.AsArray().Remove(Unit(fixture, "campaign-laiya"));
            var outcome = engine.Victory.Evaluate(fixture, _ => { });
            Require(missingLeader ? outcome?.Team == 2 : outcome?.Kind == "objectiveCompleted" && outcome.Team == 1,
                "Laiya's critical loss outranks objective victory; otherwise objective completion precedes timeout.");
        }
        var timed = engine.CreateBattle(level);
        while (Integer(timed.Snapshot(), "turn") < 15)
            timed.Dispatch(new JsonObject { ["kind"] = "endTurn" });
        Require(Text(timed.Snapshot(), "phase") == "playing", "T15 remains fully playable.");
        while (Text(timed.Snapshot(), "phase") == "playing")
            timed.Dispatch(new JsonObject { ["kind"] = "endTurn" });
        Require(Integer(timed.Snapshot(), "turn") == 16 && Text(timed.Snapshot()["conclusion"]!, "kind") == "turnLimitReached", "The unchanged deadline evaluates upon entry to T16.");
    }

    private void CheckOpeningCaptureRoutes(BattleEngine engine, JsonObject level)
    {
        var battle = engine.CreateBattle(level);
        var width = Integer(level, "width");
        var assignments = new[] { (Key: "north-militia", Zone: "granary"), (Key: "militia-2", Zone: "militia-barracks") };
        var zones = level["scenario"]!["zones"]!.AsArray().OfType<JsonObject>().ToDictionary(zone => Text(zone, "id"), zone => TacticalQueries.Position(zone["cells"]![0]!.AsObject()));
        var orders = 0;
        while (Text(battle.Snapshot(), "phase") == "playing" && Integer(battle.Snapshot(), "turn") <= 15)
        {
            var state = battle.Snapshot();
            if (Integer(state, "currentPlayer") == 1)
            {
                foreach (var assignment in assignments)
                {
                    state = battle.Snapshot();
                    if (Text(state, "phase") != "playing")
                        break;
                    var target = zones[assignment.Zone];
                    if (state["map"]!["owners"]![target.Y * width + target.X]!.GetValue<int>() == 1)
                        continue;
                    var actor = Unit(state, assignment.Key);
                    var field = engine.Space.ComputeMoveField(state, actor);
                    var distances = new Dictionary<Coord, int> { [target] = 0 };
                    var queue = new Queue<Coord>();
                    queue.Enqueue(target);
                    var battlefield = new MovementRules(new(engine.Catalog, engine.Grids)).Battlefield(state);
                    while (queue.Count > 0)
                    {
                        var cell = queue.Dequeue();
                        foreach (var next in engine.Space.Board(state).Neighbours(cell))
                            if (battlefield.Cell(next).Admits("foot") && !distances.ContainsKey(next))
                            {
                                distances[next] = distances[cell] + 1;
                                queue.Enqueue(next);
                            }
                    }
                    var origin = TacticalQueries.Position(actor);
                    var best = field.Stops.Select(index => new Coord(index % width, index / width))
                        .Where(distances.ContainsKey).OrderBy(cell => distances[cell]).ThenBy(cell => cell.Y).ThenBy(cell => cell.X).First();
                    Require(best == target || distances[best] < distances[origin], $"The unchanged opening must advance {assignment.Key} toward {assignment.Zone}, not require teleportation.");
                    var path = engine.Space.PathTo(field, state, best);
                    Require(path is { Count: > 0 }, "Opening mission progress requires real movement paths.");
                    var command = WaitCommand(actor, path!);
                    if (best == target)
                        command["command"]!["ability"] = "capture";
                    battle.Dispatch(command);
                    orders++;
                }
            }
            if (Text(battle.Snapshot(), "phase") == "playing")
                battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
        }
        var final = battle.Snapshot();
        Require(Text(final, "phase") == "over" && Text(final["conclusion"]!, "kind") == "objectiveCompleted" && Integer(final["conclusion"]!, "team") == 1,
            "The unchanged opening must permit both civic targets to be captured through legal orders before the deadline; enemy handoffs alone are not combat balance acceptance.");
        GD.Print($"C07 unchanged-opening route feasibility: {orders} movement/capture orders, T{Integer(final, "turn")} victory; opposing sides only hand off, no teleports or actor/stat edits.");
    }

    private void CheckSaveAdmission(BattleEngine engine, JsonArray levels)
    {
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        var detached = definition.DeepClone().AsObject();
        detached["start"] = "brief-07";
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        GrayFlagCampaign Campaign(JsonArray source, JsonObject? rules = null) => new(new(engine, presentation), rules ?? detached, source);
        var live = Campaign(levels);
        live.Advance();
        var preBattle = live.Save();
        var restoredPreBattle = Campaign(levels, definition);
        restoredPreBattle.Load(preBattle);
        Require(!restoredPreBattle.HasPendingBattle && Text(restoredPreBattle.Node(), "id") == "battle-07"
            && EcmaJson.DeepEquals(restoredPreBattle.Snapshot(), live.Snapshot()), "Current detached C07 pre-battle save must load through unchanged production admission.");
        live.BeginBattle();
        var session = live.Battle!;
        var opening = session.Snapshot();
        var actor = Unit(opening, "campaign-torren");
        var path = OpeningPath(engine, opening, actor);
        Require(path is { Count: > 1 } && session.Dispatch(EcmaJson.ToElement(WaitCommand(actor, path)), session.Revision).Accepted && session.Events.Count > 0,
            "Atomic rejection must protect an actually played C07 session, not an untouched opening.");
        // Exact retired C07 from cee342ca; never registered in production.
        var retiredLevel = PresentationJson.Load<JsonObject>("res://tests/native/luojia-retired-level.json");
        Require(Text(retiredLevel, "id") == LevelId && Integer(retiredLevel, "width") == 23 && Integer(retiredLevel, "height") == 15,
            "Retired admission input must be the actual old 23 by 15 C07, not a fabricated current snapshot.");
        var retiredLevels = new JsonArray(levels.OfType<JsonObject>().Select(level => (JsonNode)(Text(level, "id") == LevelId ? retiredLevel : level).DeepClone()).ToArray());
        var retiredDefinition = definition.DeepClone().AsObject();
        retiredDefinition["start"] = "battle-07";
        var retired = Campaign(retiredLevels, retiredDefinition);
        retired.BeginBattle();
        var retiredSave = retired.Save();
        var admitted = engine.LoadBattle(retiredSave["battle"]);
        Require(Integer(admitted["map"]!, "width") == 23 && Integer(admitted["map"]!, "height") == 15
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
            Require(error.Message == "Saved battle does not match the campaign level.", "Retired C07 must fail for canonical level mismatch, not a malformed fixture.");
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
            "Current played C07 save must round-trip campaign, pending request, state and events.");
        live.ExitBattle();
        var campSave = live.Save();
        var camp = Campaign(levels, definition);
        camp.Load(campSave);
        Require(Text(campSave, "mode") == "camp" && campSave["battle"] is null && campSave["events"]!.AsArray().Count == 0
            && camp.AtCamp && EcmaJson.DeepEquals(camp.Battle!.Snapshot(), opening), "A current camp save must reproject the current opening without played state.");
    }

    private void CheckSourceBinding(BattleEngine engine, JsonArray levels)
    {
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        var detached = definition.DeepClone().AsObject();
        detached["start"] = "battle-07";
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        GrayFlagCampaign Campaign(JsonArray source) => new(new(engine, presentation), detached, source);
        var current = Campaign(levels);
        current.BeginBattle();
        var live = current.Battle!;
        var actor = Unit(live.Snapshot(), "campaign-torren");
        Require(live.Dispatch(EcmaJson.ToElement(WaitCommand(actor, OpeningPath(engine, live.Snapshot(), actor)!)), live.Revision).Accepted,
            "Source rejection protects a real played session.");
        var identity = Text(current.Save(), "battleSourceSha256");
        Require(identity == EcmaJson.Sha256(current.RequestSnapshot()!["level"]), "Pending identity binds the composed initialization request, not live HP or terrain.");
        void Reject(JsonObject input)
        {
            var campaignBefore = EcmaJson.Stringify(current.Snapshot());
            var requestBefore = EcmaJson.Stringify(current.RequestSnapshot());
            var stateBefore = EcmaJson.Stringify(live.Snapshot());
            var eventsBefore = Events(live);
            var revisionBefore = live.Revision;
            var bytesBefore = EcmaJson.Stringify(input);
            var rejected = false;
            try
            {
                current.Load(input);
            }
            catch (StoredDocumentException) { rejected = true; }
            Require(rejected && ReferenceEquals(current.Battle, live) && current.HasPendingBattle && !current.AtCamp
                && EcmaJson.Stringify(current.Snapshot()) == campaignBefore && EcmaJson.Stringify(current.RequestSnapshot()) == requestBefore
                && EcmaJson.Stringify(live.Snapshot()) == stateBefore && Events(live) == eventsBefore && live.Revision == revisionBefore
                && EcmaJson.Stringify(input) == bytesBefore, "Rejected source/schema must leave campaign, request, battle, events, revision and input unchanged.");
        }
        var oldLevel = PresentationJson.Load<JsonObject>("res://tests/native/luojia-pre-planter-level.json");
        var oldLevels = new JsonArray(levels.OfType<JsonObject>().Select(level => (JsonNode)(Text(level, "id") == LevelId ? oldLevel : level).DeepClone()).ToArray());
        var old = Campaign(oldLevels);
        old.BeginBattle();
        var oldSession = old.Battle!;
        var oldState = oldSession.Snapshot();
        var bran = Unit(oldState, "campaign-bran");
        var invalidCell = new Coord(56, 23);
        var path = engine.Space.PathTo(engine.Space.ComputeMoveField(oldState, bran), oldState, invalidCell);
        Require(path is { Count: > 1 } && oldSession.Dispatch(EcmaJson.ToElement(WaitCommand(bran, path)), oldSession.Revision).Accepted,
            "True published pre-planter source permits the observed four-cell Bran move through production rules.");
        var savedOld = old.Save();
        var independentlyValid = engine.LoadBattle(savedOld["battle"]);
        Require(TacticalQueries.Position(Unit(independentlyValid, "campaign-bran")) == invalidCell
            && !new MovementRules(new(engine.Catalog, engine.Grids)).Battlefield(live.Snapshot()).Cell(invalidCell).Admits("foot"),
            "Same-size independently valid old save stands on a now-closed real planter, not a fabricated hash-only fixture.");
        Reject(savedOld);
        foreach (var bad in new[] { "", "not-a-hash", new string('0', 64), new string('A', 64) })
        {
            var malformed = current.Save();
            malformed["battleSourceSha256"] = bad;
            Reject(malformed);
        }
        var missing = current.Save();
        missing.Remove("battleSourceSha256");
        Reject(missing);
        var obsolete = current.Save();
        obsolete["schema"] = 3;
        obsolete.Remove("battleSourceSha256");
        Reject(obsolete);
        var campaignOnly = Campaign(levels).Save();
        Require(campaignOnly["battleSourceSha256"] is null, "Campaign-only current envelopes do not invent a pending battle identity.");
        campaignOnly["schema"] = 3;
        campaignOnly.Remove("battleSourceSha256");
        Reject(campaignOnly);
        current.ExitBattle();
        Require(Text(current.Save(), "battleSourceSha256") == identity, "Camp reset retains its initialization identity.");
        current.RestartBattle();
        Require(Text(current.Save(), "battleSourceSha256") == identity, "Battle reset cannot relabel its source from live terrain.");

        // A legitimate scenario mutation proves source identity is not a pristine/live-map comparison.
        var changingLevels = levels.DeepClone().AsArray();
        var changingLevel = changingLevels.OfType<JsonObject>().Single(level => Text(level, "id") == LevelId);
        changingLevel["scenario"]!["zones"]!.AsArray().Add(new JsonObject { ["id"] = "source-test-ground", ["cells"] = new JsonArray(new JsonObject { ["x"] = 48, ["y"] = 35 }) });
        foreach (var player in changingLevel["players"]!.AsArray().OfType<JsonObject>())
            player["controller"] = "human";
        changingLevel["scenario"]!["triggers"] = new JsonArray();
        changingLevel["scenario"]!["triggers"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "source-test-terrain",
            ["timing"] = "turnStart",
            ["condition"] = new JsonObject { ["type"] = "turnAtLeast", ["turn"] = 2 },
            ["effects"] = new JsonArray(new JsonObject { ["type"] = "replaceTerrain", ["zone"] = "source-test-ground", ["terrain"] = "plain" })
        });
        var changing = Campaign(changingLevels);
        changing.BeginBattle();
        var changingIdentity = Text(changing.Save(), "battleSourceSha256");
        var pristineMap = EcmaJson.Stringify(changing.Battle!.Snapshot()["map"]);
        while (Integer(changing.Battle!.Snapshot(), "turn") < 2)
            Require(changing.Battle.Dispatch(EcmaJson.ToElement(new JsonObject { ["kind"] = "endTurn" }), changing.Battle.Revision).Accepted, "Scenario mutation is reached by real phase commands.");
        var changedSave = changing.Save();
        Require(Events(changing.Battle!).Contains("terrainChanged", StringComparison.Ordinal)
            && EcmaJson.Stringify(changing.Battle!.Snapshot()["map"]) != pristineMap
            && Text(changedSave, "battleSourceSha256") == changingIdentity, "Legal terrain mutation retains its initial source identity.");
        var restored = Campaign(changingLevels);
        restored.Load(changedSave);
        Require(EcmaJson.DeepEquals(restored.Battle!.Snapshot(), changing.Battle!.Snapshot()) && Events(restored.Battle!) == Events(changing.Battle!),
            "Current source accepts its legitimate dynamic terrain and event history exactly.");
        GD.Print("C07 source-binding checks passed: same-size published geometry rejection, immutable request identity, schema4 break and legal terrain-mutation round trip.");
    }

    private void ExportGuiFixtures(BattleEngine engine, JsonArray levels)
    {
        const string prefix = "--export-c07-gui-dir=";
        var supplied = OS.GetCmdlineUserArgs();
        Require(supplied.All(argument => argument.StartsWith(prefix, StringComparison.Ordinal)) && supplied.Length <= 1,
            "Only the explicit C07 evidence export option is supported.");
        var args = supplied.Where(argument => argument.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        if (args.Length == 0)
            return;
        var path = args[0][prefix.Length..];
        Require(Path.IsPathFullyQualified(path) && !Path.GetFullPath(path).StartsWith(Path.GetFullPath(OS.GetUserDataDir()), StringComparison.Ordinal),
            "GUI fixtures require an absolute caller-selected evidence directory outside user saves.");
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        var detached = definition.DeepClone().AsObject();
        detached["start"] = "brief-07";
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
        Export("c07-detached-briefing.json");
        campaign.Advance();
        Export("c07-detached-staging.json");
        campaign.BeginBattle();
        Export("c07-detached-opening.json");
        Require(campaign.Snapshot()["battleHistory"]!.AsArray().Count == 0, "Detached GUI fixtures cannot fabricate earlier campaign victories.");
        GD.Print("Exported detached C07 briefing, staging and opening to " + path + ". No earlier victories are claimed and no user save was changed.");
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
