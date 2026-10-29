#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core;
using Empire.Core.Campaign;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Core.Transactions;
using Empire.Localization;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>C04 production rendering and focused contract probes, not natural-route or desktop acceptance.</summary>
public partial class BlackFlagCampChecks : Node
{
    private static readonly Coord[] Sites = [new(21, 12), new(24, 15), new(28, 18), new(27, 8), new(14, 18),
        new(43, 16), new(7, 21), new(42, 5), new(16, 15), new(37, 26)];
    private static readonly string[] Inspectors = ["campaign-torren", "campaign-elin", "scout-1"];
    private static readonly string[] Signals = ["found_prisoners", "found_oath_template", "saved_witnesses"];
    private int assertions;

    public override void _Ready()
    {
        try
        {
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json")
                .OfType<JsonObject>().Single(value => Text(value, "id") == "c01-04");
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var state = engine.CreateState(level);
            CheckMissionSource(engine, level, state);
            CheckStagingChoices(engine, level);
            CheckSaveAdmission(engine);
            CheckRegisteredPainting(engine, level, state);
            CheckFoundations(engine, level, state);
            CheckEvidenceCommands(engine, level);
            GD.Print($"Native BlackFlagCamp passed {assertions} assertions: registration, ten sites, material picking, foundations, staging, save admission and detached legal-command mission probes. Natural route play and GUI acceptance are separate.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void CheckMissionSource(BattleEngine engine, JsonObject level, JsonObject state)
    {
        Require(Integer(level, "width") == 50 && Integer(level, "height") == 30, "C04 must retain its canonical 50 by 30 board.");
        Require(Evidence(state) == 0 && Text(state, "phase") == "playing", "A fresh camp battle must begin playing with zero battle evidence.");
        Require(level["rules"]!["turnLimit"] is null && Integer(level["extra"]!, "recommendedTurns") == 12,
            "Twelve turns is a recommendation, not a hard loss deadline.");
        Require(Units(state).Count(unit => Integer(unit, "owner") == 1) == 10 && Units(state).Count(unit => Integer(unit, "owner") == 2) == 9,
            "The actual opening must contain ten allies and nine enemies.");
        var map = state["map"]!;
        var owned = map["owners"]!.AsArray().Select((owner, index) => (owner, index)).Where(pair => pair.owner!.GetValue<int>() == 2);
        Require(owned.All(pair => engine.Catalog.Definition("terrains", map["tiles"]![pair.index]!.GetValue<string>())!
                .Value.GetProperty("produces").GetArrayLength() == 0),
            "Enemy camp ownership must not keep a production faction alive after all nine enemies are defeated.");
        var primary = level["players"]!.AsArray().OfType<JsonObject>().Single(player => Integer(player, "id") == 1)["objectives"]![0]!;
        Require(Text(primary, "type") == "failOn" && Text(primary["condition"]!, "type") == "unitCount"
            && primary["condition"]!["selector"]!["keys"]!.AsArray().Select(value => value!.GetValue<string>()).SequenceEqual(["campaign-laiya"])
            && Text(primary["condition"]!, "op") == "lte" && Integer(primary["condition"]!, "value") == 0,
            "Laiya's loss must fail the primary objective even when Mirelle is an eligible extractor.");
        var sequence = primary["objective"]!;
        var objectives = sequence["objectives"]!.AsArray();
        Require(Text(sequence, "type") == "sequence" && objectives.Count == 2
            && Text(objectives[0]!, "type") == "score" && Text(objectives[0]!, "variable") == "evidence" && Integer(objectives[0]!, "atLeast") == 3
            && Text(objectives[1]!, "type") == "escort" && Text(objectives[1]!, "zone") == "west-exit" && Integer(objectives[1]!, "count") == 1
            && objectives[1]!["selector"]!["keys"]!.AsArray().Select(value => value!.GetValue<string>()).ToHashSet()
                .SetEquals(["campaign-laiya", "campaign-mirelle"]),
            "Victory must require evidence first, followed by either living eligible hero at the western exit.");
        Require(state["scenario"]!["zones"]!["west-exit"]!.AsArray().OfType<JsonObject>().Select(TacticalQueries.Position).ToHashSet()
            .SetEquals([new Coord(7, 21), new Coord(8, 21), new Coord(9, 21)]), "West extraction must use precisely its three canonical cells.");
        var triggers = level["scenario"]!["triggers"]!.AsArray().OfType<JsonObject>().ToArray();
        Require(triggers.Length == 4 && triggers.All(trigger => Text(trigger, "timing") == "afterAction" && trigger["repeat"] is null),
            "C04 must have three one-shot inspections and one one-shot withdrawal, all afterAction.");
        var withdrawal = triggers.Single(trigger => Text(trigger, "id") == "withdraw-with-evidence");
        Require(Text(withdrawal["condition"]!, "type") == "variable" && Text(withdrawal["condition"]!, "key") == "evidence"
            && Text(withdrawal["condition"]!, "op") == "gte" && Integer(withdrawal["condition"]!, "value") == 3,
            "Withdrawal must begin at evidence >= 3, not earlier or only at exact equality.");
        for (var index = 0; index < 3; index++)
        {
            var id = $"inspect-{(char)('a' + index)}";
            var zone = $"tent-{(char)('a' + index)}";
            var trigger = triggers.Single(value => Text(value, "id") == id);
            Require(Text(trigger["condition"]!, "type") == "unitInZone" && Text(trigger["condition"]!, "zone") == zone
                && Integer(trigger["condition"]!, "owner") == 1
                && state["scenario"]!["zones"]![zone]!.AsArray().OfType<JsonObject>().Select(TacticalQueries.Position).SequenceEqual([Sites[index]]),
                $"{id} must be an allied visit to its actual camp site, not a capture or click condition.");
        }
    }

    private void CheckStagingChoices(BattleEngine engine, JsonObject level)
    {
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        // Starting this detached campaign at its real choice avoids fabricating earlier battle outcomes.
        definition["start"] = "choice-black-camp-plan";
        foreach (var choice in new[] { "rescue-and-evidence", "split-the-force" })
        {
            var campaign = new CampaignRuntime(CampaignRules.Create(), definition);
            Require(campaign.Choices().OfType<JsonObject>().Select(value => Text(value, "id")).ToHashSet()
                .SetEquals(["rescue-and-evidence", "split-the-force"]), "Both authored C04 staging choices must remain available.");
            campaign.Choose(choice);
            var context = campaign.Snapshot();
            var split = choice == "split-the-force";
            Require(Text(campaign.Node(), "id") == "battle-04" && context["flags"]!.AsArray().Any(value => value!.GetValue<string>() == (split ? "black_camp_split" : "black_camp_rescue"))
                && context["variables"]![split ? "evidence" : "witnesses"]!.GetValue<double>() == 2,
                "The selected staging choice must apply its real campaign flag and campaign reward.");
            var request = GrayFlagCampaignPolicy.ApplyBattleContext(new JsonObject { ["levelId"] = "c01-04", ["level"] = level.DeepClone() }, context);
            var battle = engine.CreateState(request["level"]!.AsObject());
            Require(Evidence(battle) == 0 && Text(battle, "phase") == "playing", $"{choice} must not pre-credit campaign evidence to the battle's three inspections.");
        }
    }

    private void CheckSaveAdmission(BattleEngine engine)
    {
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        // Detached admission probes start at the real choice, without inventing an earlier campaign victory.
        definition["start"] = "choice-black-camp-plan";
        var presentation = BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese());
        GrayFlagCampaign Campaign(JsonArray source) => new(new(engine, presentation), definition, source);
        static string Events(GrayFlagSession session) => EcmaJson.Stringify(
            new JsonArray(session.Events.Select(value => (JsonNode)value).ToArray()));

        var current = Campaign(levels);
        current.Choose("rescue-and-evidence");
        var preBattle = current.Save();
        var live = Campaign(levels);
        live.Load(preBattle);
        Require(!live.HasPendingBattle && live.Battle is null && Text(live.Node(), "id") == "battle-04"
            && EcmaJson.DeepEquals(live.Snapshot(), current.Snapshot()),
            "A current C04 pre-battle save must retain its exact campaign state without inventing a pending battle.");
        live.BeginBattle();
        var session = live.Battle!;
        var opening = session.Snapshot();
        Require(Integer(opening["map"]!, "width") == 50 && Integer(opening["map"]!, "height") == 30,
            "An admitted pre-battle save must enter the current 50 by 30 C04.");
        var actor = Unit(opening, "scout-4");
        var destination = new Coord(15, 21);
        var field = engine.Space.ComputeMoveField(opening, actor);
        var path = engine.Space.PathTo(field, opening, destination);
        Require(path is { Count: > 1 } && field.Stops.Contains(destination.Y * 50 + destination.X)
            && engine.CommandsAt(opening, actor, destination).Any(option => option.Ability == "wait")
            && session.Dispatch(EcmaJson.ToElement(WaitCommand(actor, path)), session.Revision).Accepted
            && session.Events.Count > 0 && !EcmaJson.DeepEquals(opening, session.Snapshot()),
            "The load-rejection probe must protect an actually played session with nonempty events.");

        // Exact C04 object from f2b74e5eb20265dcfc23e2b1fcc02d586dd6cbcf:godot/data/grayflag-campaign-levels.json.
        // This retired test input is never registered by the application or used as a compatibility path.
        var retiredLevel = PresentationJson.Load<JsonObject>("res://tests/native/blackflag-retired-level.json");
        Require(Text(retiredLevel, "id") == "c01-04" && Integer(retiredLevel, "width") == 20 && Integer(retiredLevel, "height") == 14,
            "The historical admission fixture must be the actual retired 20 by 14 C04 definition.");
        var retiredLevels = new JsonArray(levels.OfType<JsonObject>().Select(level =>
            (JsonNode)(Text(level, "id") == "c01-04" ? retiredLevel : level).DeepClone()).ToArray());
        var retired = Campaign(retiredLevels);
        // Distinct real choices make an accidental runtime publication observable, not just a session swap.
        retired.Choose("split-the-force");
        retired.BeginBattle();
        var retiredSave = retired.Save();
        var admittedBattle = engine.LoadBattle(retiredSave["battle"]);
        Require(Integer(admittedBattle["map"]!, "width") == 20 && Integer(admittedBattle["map"]!, "height") == 14
            && EcmaJson.DeepEquals(admittedBattle, retired.Battle!.Snapshot()),
            "The retired snapshot must come from the real campaign and pass native battle validation before campaign admission.");
        Require(!EcmaJson.DeepEquals(live.Snapshot(), retired.Snapshot()),
            "The rejected campaign must differ from the live campaign so partial runtime publication cannot pass unnoticed.");
        var campaignBefore = live.Snapshot();
        var requestBefore = live.RequestSnapshot();
        var battleBefore = session.Snapshot();
        var eventsBefore = Events(session);
        var revisionBefore = session.Revision;
        var retiredBytes = EcmaJson.Stringify(retiredSave);
        var rejected = false;
        try
        {
            live.Load(retiredSave);
        }
        catch (StoredDocumentException error)
        {
            Require(error.Message == "Saved battle does not match the campaign level.",
                "A valid retired C04 snapshot must fail at the campaign level boundary, not malformed data or presentation.");
            rejected = true;
        }
        Require(rejected, "The current campaign must reject the retired 20 by 14 C04 battle snapshot.");
        Require(EcmaJson.DeepEquals(live.Snapshot(), campaignBefore) && EcmaJson.DeepEquals(live.RequestSnapshot(), requestBefore)
            && live.HasPendingBattle && !live.AtCamp,
            "Rejected C04 loading must not publish the candidate campaign runtime or pending request.");
        Require(ReferenceEquals(live.Battle, session) && EcmaJson.DeepEquals(session.Snapshot(), battleBefore)
            && Events(session) == eventsBefore && session.Revision == revisionBefore,
            "Rejected C04 loading must preserve the existing session object, played battle state, events and revision.");
        Require(EcmaJson.Stringify(retiredSave) == retiredBytes,
            "Rejected C04 admission must not rewrite its input save.");

        var restoredBattle = Campaign(levels);
        restoredBattle.Load(live.Save());
        Require(EcmaJson.DeepEquals(restoredBattle.Snapshot(), campaignBefore)
            && EcmaJson.DeepEquals(restoredBattle.RequestSnapshot(), requestBefore)
            && EcmaJson.DeepEquals(restoredBattle.Battle!.Snapshot(), battleBefore)
            && Events(restoredBattle.Battle!) == eventsBefore,
            "A current played C04 save must round-trip its campaign, pending request, battle and events.");
        live.ExitBattle();
        var campSave = live.Save();
        Require(Text(campSave, "mode") == "camp" && campSave["battle"] is null && campSave["events"]!.AsArray().Count == 0,
            "A current camp save must contain campaign progress, not a played battle snapshot.");
        var restoredCamp = Campaign(levels);
        restoredCamp.Load(campSave);
        Require(restoredCamp.AtCamp && EcmaJson.DeepEquals(restoredCamp.Snapshot(), live.Snapshot())
            && EcmaJson.DeepEquals(restoredCamp.Battle!.Snapshot(), opening),
            "A current camp save must reproject the current C04 opening and remain at camp.");

        retired.ExitBattle();
        var retiredCampSave = retired.Save();
        Require(Text(retiredCampSave, "mode") == "camp" && retiredCampSave["battle"] is null
            && retiredCampSave["events"]!.AsArray().Count == 0,
            "The historical camp probe must have no retired battle snapshot to restore.");
        var projected = Campaign(levels);
        projected.Choose("split-the-force");
        projected.BeginBattle();
        var projectedOpening = projected.Battle!.Snapshot();
        var projectedBefore = EcmaJson.Stringify(projected.Snapshot());
        var projectedRequest = EcmaJson.Stringify(projected.RequestSnapshot());
        var projectedSession = projected.Battle;
        var retiredCampBytes = EcmaJson.Stringify(retiredCampSave);
        rejected = false;
        try
        {
            projected.Load(retiredCampSave);
        }
        catch (StoredDocumentException error)
        {
            Require(error.Message == "Saved battle does not match the campaign level.",
                "A historical camp carries its immutable initialization source even without played battle state.");
            rejected = true;
        }
        Require(rejected && !projected.AtCamp && ReferenceEquals(projected.Battle, projectedSession)
            && EcmaJson.Stringify(projected.Snapshot()) == projectedBefore
            && EcmaJson.Stringify(projected.RequestSnapshot()) == projectedRequest
            && EcmaJson.DeepEquals(projected.Battle!.Snapshot(), projectedOpening)
            && EcmaJson.Stringify(retiredCampSave) == retiredCampBytes,
            "Schema4 refuses historical camp source changes atomically instead of silently reprojecting them.");
        restoredCamp.BeginBattle();
        Require(!restoredCamp.AtCamp && EcmaJson.DeepEquals(restoredCamp.Battle!.Snapshot(), opening),
            "Re-entering a restored current-source camp starts its unchanged opening.");
    }

    private void CheckRegisteredPainting(BattleEngine engine, JsonObject level, JsonObject state)
    {
        var scene = AuthoredScene.Load("res://data/scenes/c01-04.json");
        Require(scene.RuntimeReady && scene.PaintedGroundPath == "res://data/scenes/c01-04-painted-ground.json",
            "C04 must bind its reviewed ready painted scene, not a fallback terrain board.");
        var art = PresentationJson.Load<PaintedGroundDocument>(scene.PaintedGroundPath!);
        art.ValidateLevel(level);
        Require(art.LevelId == "c01-04" && art.ResourcePath == "res://assets/native/scenes/blackflag-camp.png"
            && art.PixelSize.SequenceEqual([1660, 948]) && art.WorldScale == 1.25f && art.OriginPixels.SequenceEqual([30f, 24f])
            && art.CellXPixels.SequenceEqual([32f, 0f]) && art.CellYPixels.SequenceEqual([0f, 30f]), "The complete C04 painting must retain its audited source registration.");
        var map = state["map"]!.AsObject();
        var expected = Sites.ToHashSet();
        var canonicalSites = map["tiles"]!.AsArray().Select((tile, index) => (tile, index))
            .Where(pair => engine.Catalog.Definition("terrains", pair.tile!.GetValue<string>())!.Value.GetProperty("capturable").GetBoolean())
            .Select(pair => new Coord(pair.index % 50, pair.index / 50)).ToHashSet();
        Require(canonicalSites.SetEquals(expected), "All ten and only the ten authored C04 sites must be canonical capturable terrain.");
        var board = new BoardView { AnimationsEnabled = false };
        AddChild(board);
        try
        {
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, art.CreatePlane), level, scene);
            board.ConfigureGround(art.Factory(map));
            var before = EcmaJson.Stringify(state);
            board.RenderState(state);
            var ground = Descendants(board).OfType<PaintedGroundView>().Single();
            Require(board.RenderingIssueCount == 0 && board.RenderedUnitCount == Units(state).Count(), "The production board must render every C04 actor without missing-art diagnostics.");
            Require(ground.Sites.Count == 10 && ground.Sites.Select(site => site.Cell).ToHashSet().SetEquals(expected)
                && ground.Sites.All(site => site.OwnsMapStructureBody) && board.RenderedStructureCount == 0,
                "Ten painted objects must own ten sites, with no duplicate map-structure bodies.");
            Require(ground.DepthEntries.Count == art.Occluders.Sum(item => item.Parts.Length), "Every authored material part must join the production depth compositor exactly once.");
            foreach (var item in art.Occluders.Where(item => item.SelectionCell is not null))
            {
                var cell = new BoardCell(item.SelectionCell![0], item.SelectionCell[1]);
                var parts = item.Parts.Where(part => part.Pointer.Kind == "target").ToArray();
                Require(parts.Length > 0, $"{item.Id} needs an explicitly selectable material, not its whole floor.");
                foreach (var part in parts)
                    Require(InteriorSamples(part.Pointer.PolygonPixels!).Any(pixel => board.Pick(pixel * art.WorldScale) == TacticalHit.Object(cell)),
                        $"The production picker must reach {item.Id}/{part.Id}'s actual canonical site through a visible material target.");
            }
            var uncoveredOutside = 0;
            for (var index = 0; index < map["tiles"]!.AsArray().Count; index++)
            {
                var terrain = engine.Catalog.Definition("terrains", map["tiles"]![index]!.GetValue<string>())!.Value;
                if (!terrain.GetProperty("tags").EnumerateArray().Any(tag => tag.GetString() == "outside"))
                    continue;
                var point = board.Projection.CellCenter(new(index % 50, index / 50));
                if (ground.DepthEntries.Any(entry => entry.Visual!.Covers(board.ToGlobal(point))))
                    continue;
                uncoveredOutside++;
                Require(board.Pick(point) is null, "Bare canonical outside scenery must never become a tactical ground hit.");
            }
            Require(uncoveredOutside > 100 && board.Pick(new(-10, -10)) is null
                && board.Pick(board.FieldSize + new Vector2(10, 10)) is null, "Outside sampling must cover real perimeter cells and both off-canvas corners.");
            // The open floor is transparent, not a rule that suppresses a visible neighboring actor.
            var guard = state["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["key"]!.GetValue<string>() == "camp-guard-1");
            var guardId = guard["id"]!.GetValue<int>();
            Require(guard["x"]!.GetValue<int>() == 21 && guard["y"]!.GetValue<int>() == 13,
                "The adjacent-actor fixture must retain the real camp guard below the first shelter.");
            var overlap = board.Projection.CellContact(new(21, 12)) + Vector2.Down * 4 * art.WorldScale;
            var globalOverlap = board.ToGlobal(overlap);
            var sprite = board.GetNode<Sprite2D>($"CanonicalProjection/Unit_{guardId}/AuthoredSprite");
            Require(sprite.IsPixelOpaque(sprite.ToLocal(globalOverlap))
                && ground.DepthEntries.All(entry => !entry.Visual!.Covers(globalOverlap)),
                "The open-mouth overlap witness must contain real guard pixels and no foreground house material.");
            Require(board.Pick(overlap) == TacticalHit.Unit(new(21, 13), guardId),
                "Visible neighboring guard pixels must select that guard, not the shelter floor beneath them.");
            var empty = state.DeepClone().AsObject();
            empty["units"] = new JsonArray();
            board.RenderState(empty);
            Require(board.RenderedUnitCount == 0, "Empty-mouth geometry checks must contain no actor pixels.");
            CheckShelterMouths(board, ground, art);
            board.RenderState(state);
            Require(board.Pick(overlap) == TacticalHit.Unit(new(21, 13), guardId),
                "Restoring the unchanged opening must restore the same visible neighboring guard target.");
            Require(EcmaJson.Stringify(state) == before, "C04 registration, rendering and pointer queries cannot mutate canonical battle state.");
        }
        finally
        {
            RemoveChild(board);
            board.Free();
        }
    }

    private void CheckShelterMouths(BoardView board, PaintedGroundView ground, PaintedGroundDocument art)
    {
        foreach (var cell in Sites.Take(6))
        {
            var point = board.Projection.CellContact(cell);
            Require(point.IsEqualApprox(new Vector2(30 + (cell.X + .5f) * 32, 24 + (cell.Y + .5f) * 30) * art.WorldScale),
                $"Shelter {cell} must retain canonical feet at its registered source-pixel contact.");
            foreach (var delta in new[] { Vector2.Zero, Vector2.Left * 4, Vector2.Right * 4, Vector2.Up * 4, Vector2.Down * 4 })
            {
                var sample = point + delta * art.WorldScale;
                Require(ground.DepthEntries.All(entry => !entry.Visual!.Covers(board.ToGlobal(sample))),
                    $"Shelter {cell}'s open floor must not be copied into a foreground whole-floor polygon.");
                Require(board.Pick(sample) == TacticalHit.Ground(new(cell.X, cell.Y)),
                    $"Shelter {cell}'s unoccupied open mouth must remain Ground, not a facade or hidden occupant.");
            }
        }
    }

    private void CheckFoundations(BattleEngine engine, JsonObject level, JsonObject state)
    {
        var map = state["map"]!;
        var walls = map["tiles"]!.AsArray().Select((tile, index) => (tile, index))
            .Where(pair => pair.tile!.GetValue<string>() == "wall").Select(pair => new Coord(pair.index % 50, pair.index / 50)).ToHashSet();
        Require(walls.Count > 0 && engine.Catalog.Definition("terrains", "wall")!.Value.GetProperty("cost").EnumerateObject()
            .All(cost => cost.Value.ValueKind == JsonValueKind.Null), "Physical foundations and substantial boulders must use genuinely impassable canonical wall terrain.");
        foreach (var site in Sites.Skip(6))
        {
            var grid = engine.Space.Board(state);
            var wall = walls.OrderBy(cell => grid.Distance(site, cell)).ThenBy(cell => cell.Y).ThenBy(cell => cell.X).First();
            var occupied = Units(level).Select(TacticalQueries.Position).ToHashSet();
            var origin = grid.Neighbours(wall).First(cell => !occupied.Contains(cell)
                && engine.Catalog.Definition("terrains", map["tiles"]![cell.Y * 50 + cell.X]!.GetValue<string>())!.Value.GetProperty("cost").GetProperty("foot").ValueKind == JsonValueKind.Number);
            var fixture = level.DeepClone().AsObject();
            Position(Unit(fixture, "campaign-torren"), origin);
            var battle = engine.CreateBattle(fixture);
            var snapshot = battle.Snapshot();
            var actor = Unit(snapshot, "campaign-torren");
            var field = engine.Space.ComputeMoveField(snapshot, actor);
            Require(walls.All(cell => !field.Stops.Contains(cell.Y * 50 + cell.X)) && engine.Space.PathTo(field, snapshot, wall) is null,
                $"The canonical movement owner must reject the adjacent physical house foundation near {site}.");
            var hash = battle.StateHash;
            var refused = false;
            try
            {
                battle.Dispatch(WaitCommand(actor, [origin, wall]));
            }
            catch (IllegalActionException) { refused = true; }
            Require(refused && battle.StateHash == hash, "A real command into a physical foundation must be refused atomically, not only hidden by presentation.");
        }
        GD.Print($"C04 physical obstacles: {walls.Count} canonical wall cells; counts derive from the authored level, including boulders.");
    }

    private void CheckEvidenceCommands(BattleEngine engine, JsonObject level)
    {
        foreach (var extractor in new[] { "campaign-laiya", "campaign-mirelle" })
        {
            // Explicit detached setup: only starting positions differ. The battle writer receives normal legal orders thereafter.
            var fixture = level.DeepClone().AsObject();
            Coord[] inspectionApproaches = [new(21, 11), new(24, 16), new(28, 19)];
            for (var index = 0; index < 3; index++)
                Position(Unit(fixture, Inspectors[index]), inspectionApproaches[index]);
            Position(Unit(fixture, extractor), new(10, 21));
            var early = engine.CreateBattle(fixture);
            MoveWait(engine, early, extractor, new(9, 21));
            Require(Evidence(early.Snapshot()) == 0 && Text(early.Snapshot(), "phase") == "playing", "An eligible hero at the exit cannot win before collecting all three pieces of evidence.");
            var battle = engine.CreateBattle(fixture);
            for (var index = 0; index < 3; index++)
            {
                var receipt = MoveWait(engine, battle, Inspectors[index], Sites[index]);
                var snapshot = battle.Snapshot();
                Require(Evidence(snapshot) == index + 1 && receipt.Events.Any(value => value["signal"]?.GetValue<string>() == Signals[index]),
                    $"Legal entry to evidence site {(char)('A' + index)} must add exactly one evidence and emit its authored signal.");
                Require(Text(snapshot, "phase") == "playing", "Three inspections alone must not replace the required western extraction.");
                if (index < 2)
                    Require(new[] { "campaign-laiya", "campaign-mirelle" }.All(hero => Text(Unit(snapshot, hero)["directive"]!, "mode") != "retreat"),
                        "Neither eligible hero may acquire the withdrawal directive before the third inspection.");
            }
            var complete = battle.Snapshot();
            foreach (var hero in new[] { "campaign-laiya", "campaign-mirelle" })
            {
                var directive = Unit(complete, hero)["directive"]!;
                Require(Text(directive, "mode") == "retreat" && directive["waypoints"]!.AsArray().OfType<JsonObject>()
                    .Select(TacticalQueries.Position).SequenceEqual([new Coord(8, 21)]), "Evidence >= 3 must assign both eligible heroes the canonical western retreat directive.");
            }
            battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            MoveWait(engine, battle, Inspectors[0], Sites[0]);
            Require(Evidence(battle.Snapshot()) == 3, "Waiting again on an inspected site must not duplicate one-shot evidence.");
            MoveWait(engine, battle, extractor, new(9, 21));
            var final = battle.Snapshot();
            Require(Text(final, "phase") == "over" && Text(final["conclusion"]!, "kind") == "objectiveCompleted"
                && Integer(final["conclusion"]!, "team") == 1 && Text(final["conclusion"]!, "objective") == "primary"
                && Units(final).Any(unit => Text(unit, "key") == "campaign-laiya"),
                $"{extractor} must legally complete extraction after evidence while Laiya survives.");
        }
    }

    private BattleReceipt MoveWait(BattleEngine engine, Battle battle, string key, Coord destination)
    {
        var state = battle.Snapshot();
        var actor = Unit(state, key);
        var field = engine.Space.ComputeMoveField(state, actor);
        var path = engine.Space.PathTo(field, state, destination);
        Require(path is not null && field.Stops.Contains(destination.Y * 50 + destination.X)
            && engine.CommandsAt(state, actor, destination).Any(option => option.Ability == "wait"),
            $"Detached probe requires a real legal wait command and canonical path: {key} to {destination}.");
        return battle.Dispatch(WaitCommand(actor, path!));
    }

    private static JsonObject WaitCommand(JsonObject actor, IReadOnlyList<Coord> path) => new()
    {
        ["kind"] = "command",
        ["unit"] = Integer(actor, "id"),
        ["path"] = TacticalQueries.PathJson(path),
        ["command"] = new JsonObject { ["ability"] = "wait" }
    };
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
    private static int Integer(JsonNode value, string key) => value[key]!.GetValue<int>();
    private static double Evidence(JsonObject state) => state["scenario"]!["variables"]!["evidence"]!.GetValue<double>();
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
