#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using Empire.Presentation.Tactical;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Godot;

namespace Empire.Presentation;

/// <summary>Native resources and canonical campaign states, plus explicitly isolated failure fixtures.</summary>
public partial class DynamicStateChecks : Node
{
    private int assertions;
    public override void _Ready()
    {
        try
        {
            Run();
            GD.Print($"Dynamic state presentation checks passed: {assertions} assertions.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void CheckCatalogBinding(AssetCatalog assets, JsonArray authoringPacks)
    {
        var customPacks = (JsonArray)authoringPacks.DeepClone();
        var definition = customPacks.OfType<JsonObject>()
            .SelectMany(pack => (pack["units"] as JsonArray)?.OfType<JsonObject>() ?? [])
            .First();
        definition["maxHp"] = 200;
        var type = definition["id"]!.GetValue<string>();
        var customEngine = new BattleEngine(customPacks);
        definition["maxHp"] = 1000;
        var view = new DynamicStateView(new(assets, customEngine.Catalog, null));
        var badges = view.CreateUnitBadges(type, 50, false, 0, 100);
        Require(badges.GetNode<Node2D>("HealthGauge").GetMeta("ratio").AsDouble() == .25,
            "Badge health must use the composed immutable catalog, not retained authoring packs.");
        badges.Free();
    }

    private void Run()
    {
        var assets = AssetCatalog.Load();
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new BattleEngine(packs);
        if (OS.GetCmdlineUserArgs().Contains("--failure-ownership-only", StringComparer.Ordinal))
        {
            CheckFailureOwnership(assets, engine, PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json"));
            return;
        }
        CheckCatalogBinding(assets, packs);
        CheckPaintedOccluders(packs);
        PaintedPickingChecks.CheckAdmission(Require);
        PaintedPickingChecks.CheckOpenMouth(this, Require);
        PaintedInteractionChecks.Run(Require);
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
        var environment = EnvironmentCatalog.Load();
        var total = 0;
        foreach (var level in levels.OfType<JsonObject>())
        {
            var id = level["id"]!.GetValue<string>();
            var path = $"res://data/scenes/{id}.json";
            var scene = Godot.FileAccess.FileExists(path) ? AuthoredScene.Load(path) : new AuthoredScene
            {
                LevelId = id,
                MapSize = [level["width"]!.GetValue<int>(), level["height"]!.GetValue<int>()],
                RuntimeReady = false,
            };
            var board = new BoardView();
            AddChild(board);
            var state = engine.CreateState(level);
            var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
            board.Configure(new(assets, environment, engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            // Keep this detached dynamic-sprite fixture groundless: its tile-body ownership checks
            // intentionally exercise bodies that source-image ground would replace.
            board.ConfigureDynamicState();
            var before = state.ToJsonString();
            board.RenderState(state);
            var count = state["structures"]!.AsArray().Count;
            Require(board.RenderedStructureCount == count, $"{id}: every canonical structure must be visible");
            total += count;
            Require(board.RenderedAuthoredPlacementCount == scene.Placements.Count + scene.FramePlacements.Count,
                $"{id}: dynamic state must not replace authored dressing");
            Require(!board.Diagnostics.Any(message => message.Contains("Structure ", StringComparison.Ordinal)),
                $"{id}: shipped structures must have a usable image or intentional source fallback");
            foreach (var node in DynamicNodes(board).Where(node => node.HasMeta("structure_id")))
            {
                var texture = node.GetChildren().OfType<Sprite2D>().First().Texture;
                using var image = texture.GetImage();
                Require(image.GetUsedRect().HasArea(), $"{id}: structure texture cannot be transparent");
            }
            if (id == "c01-03")
                CheckStructureTransitions(board, state, scene);
            if (id == "c01-09")
            {
                Require(count == 4 && DynamicNodes(board).Count(node => node.HasMeta("structure_id")) == 4,
                    "Composite must display its four canonical parts, not an invented fifth body");
                state["structures"]!.AsArray().RemoveAt(0);
                board.RenderState(state);
                Require(board.RenderedStructureCount == 3, "Removing a composite part must remove its picture");
            }
            if (id == "c01-15")
            {
                var mother = DynamicNodes(board).Single(node => node.HasMeta("structure_id"));
                Require(mother.GetMeta("rule_derived").AsBool() && mother.GetMeta("max_hp").AsDouble() == 500,
                    "Mother-root must use its source rule-derived drawing and canonical 500 HP");
            }
            if (id == "c01-01")
            {
                CheckMarkers(board, state);
                CheckBadges(board, state);
                CheckTacticalEmphasis(board, state, engine);
                CheckMapOwnership(board, state, assets);
                Require(board.RenderedAuthoredPlacementCount == scene.Placements.Count + scene.FramePlacements.Count,
                    "Marker updates must not duplicate or delete authored shelter and civilian decoration");
                var beforeVisibility = state.ToJsonString();
                var unitId = state["units"]![0]!["id"]!.GetValue<int>();
                board.RenderState(state, new System.Collections.Generic.HashSet<int> { unitId });
                Require(board.ProjectionUnitCount == state["units"]!.AsArray().Count && board.RenderedUnitCount == 1,
                    "Explicit visibility filters actors without changing canonical projection count");
                Require(state.ToJsonString() == beforeVisibility && !board.Diagnostics.Any(message => message.StartsWith("Unit ", StringComparison.Ordinal)),
                    "Hidden actors are not missing resources and visibility never mutates state");
            }
            if (id is not ("c01-01" or "c01-03" or "c01-09"))
                Require(state.ToJsonString() == before, $"{id}: projection must not mutate its supplied state");
            board.Free();
        }
        Require(total == 9, "Coverage must include all nine shipped GrayFlag live structures");
        var gauge = DynamicStateView.CreateGauge(0, 0);
        Require(gauge.GetMeta("ratio").AsDouble() == 1, "Zero maximum must match the source's whole gauge");
        gauge.Free();
        CheckFailureOwnership(assets, engine, levels);
    }

    private void CheckFailureOwnership(AssetCatalog assets, BattleEngine engine, JsonArray levels)
    {
        // These are failure-injection fixtures, not campaign completion or real death-flow coverage.
        // The orphan API is debug-only: prove it observes a detached node before trusting empty deltas.
        var probe = new Node();
        var probeId = probe.GetInstanceId();
        try
        {
            Require(NativeNodeChecks.OrphanIds().Contains(probeId), "Failure ownership checks require a working debug orphan-node API.");
        }
        finally { probe.Free(); }
        Require(!NativeNodeChecks.OrphanIds().Contains(probeId), "Free must synchronously remove the orphan probe.");

        var level = levels.OfType<JsonObject>().Single(item => item["id"]!.GetValue<string>() == "c01-03");
        var state = engine.CreateState(level);
        var map = state["map"]!.AsObject();
        var width = map["width"]!.GetValue<int>();
        var cells = Enumerable.Range(0, map["tiles"]!.AsArray().Count)
            .Select(index => new Empire.Core.Geometry.Coord(index % width, index / width)).ToHashSet();
        var scene = AuthoredScene.Load("res://data/scenes/c01-03.json");
        var rendering = BoardSceneRendering.Load(scene, level, map);
        var projection = new BoardProjection(engine.Grids.Get("square4"), rendering.Plane, map, cells);
        var structureType = state["structures"]![0]!["type"]!.GetValue<string>();
        var structureArt = assets.ResolveStructure(structureType, StructureVisualState.Captured)
            ?? throw new InvalidOperationException("Failure fixture requires the campaign's authored structure.");
        var topics = new HashSet<string>(StringComparer.Ordinal)
        {
            structureArt.Asset.TopicId, "C01-BPROP-AFTERMATH-2", "C01-BPROP-AFTERMATH-3", "C01-BPROP-EVIDENCE-1"
        };
        const string missingPath = "res://assets/failure-fixture-deliberately-missing.png";
        Require(!ResourceLoader.Exists(missingPath), "Failure fixture texture must not exist.");
        var manifest = PresentationJson.Load<JsonObject>("res://data/selected-assets.json");
        var originalManifest = manifest.ToJsonString();
        var missingManifest = (JsonObject)manifest.DeepClone();
        foreach (var record in missingManifest["assets"]!.AsArray().OfType<JsonObject>())
            if (topics.Contains(record["topicId"]!.GetValue<string>()))
                record["resourcePath"] = missingPath;
        var missingAssets = new AssetCatalog(missingManifest.Deserialize<AssetManifest>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!,
            PresentationJson.Load<ArtBindings>("res://data/art-bindings.json"));
        Require(manifest.ToJsonString() == originalManifest, "Failure fixtures must leave the source manifest unchanged.");
        var missingView = new DynamicStateView(new(missingAssets, engine.Catalog, null));
        var realView = new DynamicStateView(new(assets, engine.Catalog, null));
        var failures = new List<string>();
        var markerState = (JsonObject)state.DeepClone();
        markerState["structures"]!.AsArray().Clear();
        markerState["markers"] = new JsonArray(new[] { "fallen_corpse", "transport_wreck", "routed" }
            .Select((kind, index) => (JsonNode)new JsonObject
            {
                ["id"] = index + 1,
                ["kind"] = kind,
                ["owner"] = 1,
                ["at"] = new JsonObject { ["x"] = 4, ["y"] = 4 }
            }).ToArray());
        var markerBefore = markerState.ToJsonString();
        var structureBefore = state.ToJsonString();
        for (var cycle = 0; cycle < 3; cycle++)
        {
            CheckFailureCase($"missing marker fallback cycle {cycle}", () =>
            {
                var result = missingView.Create(markerState, projection);
                try
                {
                    Require(result.Markers == 3 && result.Structures == 0 && result.Diagnostics.Count == 3,
                        "Every missing marker image must retain its documented fallback and diagnostic.");
                    Require(result.Root.GetChildren().OfType<Node2D>().All(node =>
                        node.HasNode("RuleDerivedMarker") && !node.HasNode("AuthoredMarker")),
                        "Missing marker textures must produce only source rule-derived drawings.");
                    Require(result.Diagnostics.All(message => message.Contains($"Missing imported texture: {missingPath}", StringComparison.Ordinal)),
                        "Marker fallback diagnostics must preserve the missing resource error.");
                    Require(markerState.ToJsonString() == markerBefore, "Marker fallback must not mutate its supplied fixture.");
                }
                finally { result.Root.Free(); }
            }, failures);
            CheckFailureCase($"missing structure texture cycle {cycle}", () =>
            {
                RequireFailure<MissingArtResourceException>(() => missingView.Create(state, projection).Root.Free(),
                    "Missing structure textures must propagate the original resource error.",
                    error => error.Resource == missingPath && error.Message == $"Missing imported texture: {missingPath}");
                Require(state.ToJsonString() == structureBefore, "Structure failure must not mutate canonical state.");
            }, failures);
        }

        var badMarker = (JsonObject)markerState.DeepClone();
        badMarker["markers"]![0]!["id"] = "invalid-marker-id";
        CheckFailureCase("marker metadata failure after sprite creation", () =>
            RequireFailure<FormatException>(() => realView.Create(badMarker, projection).Root.Free(),
                "Invalid marker IDs must retain their numeric conversion failure."), failures);
        var unitType = state["units"]![0]!["type"]!.GetValue<string>();
        foreach (var direction in new[] { new Vector2(float.NaN, 0), new Vector2(0, float.PositiveInfinity), new Vector2(float.NegativeInfinity, 0) })
            CheckFailureCase($"non-finite facing {direction}", () =>
                RequireFailure<InvalidOperationException>(() => realView.CreateUnitBadges(unitType, 50, false, 0, 100, direction).Free(),
                    "Invalid directions must retain the finite-facing error.", error => error.Message == "Facing vector must be finite."), failures);
        var duplicatePlayers = (JsonObject)state.DeepClone();
        duplicatePlayers["players"]!.AsArray().Add(duplicatePlayers["players"]![0]!.DeepClone());
        CheckFailureCase("duplicate player IDs", () =>
            RequireFailure<ArgumentException>(() => realView.Create(duplicatePlayers, projection).Root.Free(),
                "Duplicate player IDs must retain their dictionary admission error."), failures);
        var malformedPlayers = (JsonObject)state.DeepClone();
        malformedPlayers["players"] = new JsonObject();
        CheckFailureCase("non-array players", () =>
            RequireFailure<InvalidOperationException>(() => realView.Create(malformedPlayers, projection).Root.Free(),
                "Malformed players must retain their JSON shape error."), failures);
        Require(failures.Count == 0, "Failure ownership regressions:\n" + string.Join("\n", failures));
    }

    private void CheckFailureCase(string name, Action action, List<string> failures)
    {
        var before = NativeNodeChecks.OrphanIds();
        try
        {
            action();
        }
        catch (Exception error) { failures.Add($"{name}: {error}"); }
        finally
        {
            var leaked = NativeNodeChecks.OrphanIds().Except(before).ToArray();
            // Record the failure before cleanup: this protects later cases, not the production owner.
            if (leaked.Length != 0)
                failures.Add($"{name}: leaked {leaked.Length} orphan node(s): {string.Join(", ", leaked)}.");
            else
                assertions++;
            foreach (var id in leaked)
                if (GodotObject.IsInstanceIdValid(id) && GodotObject.InstanceFromId(id) is Node node)
                    node.Free();
        }
    }

    private void RequireFailure<T>(Action action, string message, Func<T, bool>? matches = null) where T : Exception
    {
        try
        {
            action();
        }
        catch (T error)
        {
            Require(error.GetType() == typeof(T) && (matches is null || matches(error)), message);
            return;
        }
        throw new InvalidOperationException(message);
    }

    private void CheckPaintedOccluders(JsonArray basePacks)
    {
        var packs = (JsonArray)basePacks.DeepClone();
        packs.Add(PresentationJson.Load<JsonObject>("res://data/samples/three-bridges-content.json"));
        var engine = new BattleEngine(packs);
        var level = PresentationJson.Load<JsonObject>("res://data/samples/three-bridges-level.json");
        var state = engine.CreateState(level);
        var map = state["map"]!.AsObject();
        var art = PresentationJson.Load<PaintedGroundDocument>("res://data/samples/three-bridges-art.json");
        var texture = ResourceLoader.Load<Texture2D>(art.ResourcePath);
        var width = map["width"]!.GetValue<int>();
        var playable = map["tiles"]!.AsArray().Select((tile, index) => (tile, index))
            .Where(item => !engine.Catalog.Definition("terrains", item.tile!.GetValue<string>())!.Value
                .GetProperty("tags").EnumerateArray().Any(tag => tag.GetString() == "outside"))
            .Select(item => new Empire.Core.Geometry.Coord(item.index % width, item.index / width)).ToHashSet();
        var projection = new BoardProjection(engine.Grids.Get("square4"), art.CreatePlane, map, playable);
        var ground = PaintedGroundView.Create(art, texture, map, engine.Catalog);
        AddChild(ground);
        ground.Render(projection, map["tiles"]!.AsArray().Select(tile => tile!.GetValue<string>()).ToArray(),
            map["elevation"]!.AsArray().Select(height => height!.GetValue<int>()).ToArray(),
            new Vector2I(width, map["height"]!.GetValue<int>()));
        ground.ApplyDrawOrder();
        Require(ground.DepthEntries.Count == art.Occluders.Sum(item => item.Parts.Length), "Every painted foreground contour must share the standing depth pipeline.");
        foreach (var entry in ground.DepthEntries)
        {
            var polygon = (Polygon2D)entry.Node;
            Require(ReferenceEquals(polygon.Texture, texture), "Painted foreground must borrow the exact mother texture.");
            for (var i = 0; i < polygon.Polygon.Length; i++)
                Require(polygon.ToGlobal(polygon.Polygon[i]).IsEqualApprox(polygon.UV[i] * art.WorldScale),
                    "Foreground sampling must redraw the exact source pixel position without drifting from the base.");
        }
        ground.Free();
        Require(GodotObject.IsInstanceValid(texture), "Freeing foreground nodes must not dispose their borrowed mother texture.");
        var first = art.Occluders[0].Parts[0];
        var invalid = new PaintedGroundDocument
        {
            LevelId = art.LevelId,
            LevelSourceSha256 = art.LevelSourceSha256,
            ResourcePath = art.ResourcePath,
            PixelSize = art.PixelSize,
            WorldScale = art.WorldScale,
            OriginPixels = art.OriginPixels,
            CellXPixels = art.CellXPixels,
            CellYPixels = art.CellYPixels,
            Occluders = [new PaintedOccluder { Id = "Crossed", Parts = [new PaintedOccluderPart
            { Id = "Body", ContactCell = first.ContactCell, GroundFrontPixels = first.GroundFrontPixels,
                PolygonPixels = [[10, 10], [50, 50], [10, 50], [50, 10]], Pointer = new PaintedPointer { Kind = "pass-through" } }] }]
        };
        var rejected = false;
        try
        {
            PaintedGroundView.Create(invalid, texture, map, engine.Catalog).Free();
        }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && GodotObject.IsInstanceValid(texture), "Crossed foreground polygons must fail admission without destroying borrowed resources.");
    }

    private void CheckStructureTransitions(BoardView board, JsonObject state, AuthoredScene scene)
    {
        var structure = state["structures"]![0]!.AsObject();
        Node2D Current() => DynamicNodes(board).Single(node => node.HasMeta("structure_id"));
        Require(Current().GetMeta("visual_state").AsString() == "captured", "Owned healthy structure uses captured frame");
        var body = Current().GetChildren().OfType<Sprite2D>().First();
        var target = new Empire.Core.Geometry.Coord(structure["x"]!.GetValue<int>(), structure["y"]!.GetValue<int>());
        var selectable = false;
        for (var y = -60; y < 0 && !selectable; y += 3)
            for (var x = -30; x <= 30 && !selectable; x += 3)
            {
                var point = Current().Position + new Vector2(x, y);
                if (body.IsPixelOpaque(body.ToLocal(board.ToGlobal(point))) && board.Pick(point) is { Kind: TacticalHitKind.Object, UnitId: null } hit && hit.Cell == new BoardCell(target.X, target.Y))
                    selectable = true;
            }
        Require(selectable, "Visible opaque structure pixels must select their canonical target cell.");
        structure["hp"] = 50;
        board.RenderState(state);
        Require(Current().GetMeta("visual_state").AsString() == "damaged", "Exactly half HP selects damaged frame");
        Require(Math.Abs(Current().GetNode<Node2D>("HealthGauge").GetMeta("ratio").AsDouble() - 0.5) < 0.0001,
            "HP gauge must refresh to canonical damage");
        structure["hp"] = 100;
        structure["disabled"] = true;
        board.RenderState(state);
        Require(Current().GetMeta("visual_state").AsString() == "disabled", "Disabled healthy structure has its own logical state");
        Require(Current().GetNodeOrNull("OwnerMark") is null && Current().GetNodeOrNull("HealthGauge") is null,
            "Disabled structure hides active ownership and health decoration without changing owner or HP");
        structure["disabled"] = false;
        board.RenderState(state);
        Require(Current().GetNodeOrNull("OwnerMark") is not null && Current().GetNodeOrNull("HealthGauge") is not null,
            "Enabled structure restores active decoration from the same state projection");
        structure["owner"] = 0;
        structure["x"] = structure["x"]!.GetValue<int>() + 1;
        board.RenderState(state);
        Require(Current().GetMeta("visual_state").AsString() == "normal" && Current().Position.IsEqualApprox(board.Projection.CellContact(
                new(structure["x"]!.GetValue<int>(), structure["y"]!.GetValue<int>()))),
            "Ownership and location must refresh without stale objects");
        var before = state.ToJsonString();
        board.RenderState(state);
        board.RenderState(state);
        Require(state.ToJsonString() == before && DynamicNodes(board).Count(node => node.HasMeta("structure_id")) == 1,
            "Repeated snapshots must neither mutate state nor duplicate a structure");
        Require(board.GetNode<Node2D>("AuthoredScene/AuthoredStanding").GetChildCount() == scene.Placements.Count,
            "Existing authored decoration must remain one independent layer");
        structure["hp"] = 0;
        structure["disabled"] = true;
        board.RenderState(state);
        Require(board.RenderedStructureCount == 1 && Current().GetMeta("visual_state").AsString() == "destroyed" &&
            Current().GetNodeOrNull("HealthGauge") is null && Current().GetNodeOrNull("OwnerMark") is null,
            "A destroyed-but-retained structure must expose the explicit destroyed visual role");
        state["structures"]!.AsArray().Clear();
        board.RenderState(state);
        Require(board.RenderedStructureCount == 0 && !DynamicNodes(board).Any(node => node.HasMeta("structure_id")),
            "Removed structures must disappear, not remain as authored decoration");
    }

    private void CheckMarkers(BoardView board, JsonObject state)
    {
        foreach (var (kind, topic, id) in new[] { ("fallen_corpse", "C01-BPROP-AFTERMATH-2", 1),
            ("transport_wreck", "C01-BPROP-AFTERMATH-3", 2), ("routed", "C01-BPROP-EVIDENCE-1", 3) })
        {
            state["markers"]!.AsArray().Add(new JsonObject
            {
                ["id"] = id,
                ["kind"] = kind,
                ["owner"] = 1,
                ["at"] = new JsonObject { ["x"] = id + 3, ["y"] = 8 }
            });
            board.RenderState(state);
            var marker = DynamicNodes(board).Single(node => node.HasMeta("marker_id") && node.GetMeta("marker_id").AsInt32() == id);
            Require(marker.GetNode<Sprite2D>("AuthoredMarker").GetMeta("topic_id").AsString() == topic,
                $"Marker {kind} must use its actual source topic");
        }
        Require(board.RenderedMarkerCount == 3, "All canonical markers must render");
        var before = state.ToJsonString();
        board.RenderState(state);
        Require(state.ToJsonString() == before && board.RenderedMarkerCount == 3, "Repeated markers must be detached and nonduplicated");
        state["markers"]!.AsArray().Clear();
        board.RenderState(state);
        Require(board.RenderedMarkerCount == 0 && !DynamicNodes(board).Any(node => node.HasMeta("marker_id")),
            "Marker removal must remove its node");
    }

    private void CheckBadges(BoardView board, JsonObject state)
    {
        var unit = state["units"]![0]!.AsObject();
        var id = unit["id"]!.GetValue<int>();
        var x = unit["x"]!.GetValue<int>();
        var y = unit["y"]!.GetValue<int>();
        unit["hp"] = 20;
        unit["done"] = true;
        unit["facing"] = "south";
        unit["capture"] = 0;
        var threshold = double.Parse(state["rules"]!["captureThreshold"]!.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture);
        state["map"]!["captureProgress"]![y * state["map"]!["width"]!.GetValue<int>() + x] = threshold / 2;
        var before = state.ToJsonString();
        board.RenderState(state);
        var badges = board.GetNode<Node2D>($"CanonicalProjection/Unit_{id}/Badges");
        Require(badges.HasNode("HealthGauge") && badges.HasNode("DoneIndicator"), "Damaged/done units must have visible status badges without shading a square of painted scenery");
        Require(Math.Abs(badges.GetNode<Node2D>("CaptureGauge").GetMeta("ratio").AsDouble() - 0.5) < 0.0001,
            "Capture gauge must read canonical map progress, not unit capture");
        Require(state.ToJsonString() == before, "Badge projection must not advance unit or capture state");
        Require(badges.GetNode<Node2D>("FacingPip").GetMeta("direction").AsVector2() == board.Projection.FacingVector("south"),
            "Facing pip must project the injected canonical grid vector through the shared board plane");
    }

    private void CheckTacticalEmphasis(BoardView board, JsonObject source, BattleEngine engine)
    {
        var state = (JsonObject)source.DeepClone();
        var unit = state["units"]![0]!.AsObject();
        var id = unit["id"]!.GetValue<int>();
        var cell = new BoardCell(unit["x"]!.GetValue<int>(), unit["y"]!.GetValue<int>());
        var otherUnit = state["units"]!.AsArray().OfType<JsonObject>().First(other => other["id"]!.GetValue<int>() != id);
        var otherCell = new BoardCell(otherUnit["x"]!.GetValue<int>(), otherUnit["y"]!.GetValue<int>());
        var maximum = engine.Catalog.Definition("units", unit["type"]!.GetValue<string>())!.Value.GetProperty("maxHp").GetDouble();
        TacticalSelectionOverlay Overlay(BoardCell? selected = null, BoardCell? cursor = null) =>
            new(selected, cursor, [], [], [], [], [], null, []);
        var sourceBefore = source.ToJsonString();
        foreach (var hp in new[] { (int)maximum, (int)maximum - 1, (int)(maximum * .3) + 1, (int)(maximum * .3), 1 })
        {
            unit["hp"] = hp;
            var ratio = hp / maximum;
            var before = state.ToJsonString();
            board.RenderState(state);
            var badges = board.GetNode<Node2D>($"CanonicalProjection/Unit_{id}/Badges");
            var facing = badges.GetNode<CanvasItem>("FacingPip");
            var health = badges.GetNodeOrNull<CanvasItem>("HealthGauge");
            Require((health is not null) == (ratio < 1), "Full-health units must not acquire an invented health gauge when focused.");
            foreach (var focus in new[] { Overlay(), Overlay(selected: cell), Overlay(cursor: cell), Overlay(selected: otherCell, cursor: otherCell), Overlay(), Overlay(cursor: cell), Overlay() })
            {
                board.ShowTacticalEmphasis(focus);
                var focused = focus.Selected == cell || focus.Cursor == cell;
                Require(facing.Visible == focused, $"Facing follows selected/cursor emphasis and clears on dismissal (HP ratio {ratio}).");
                if (health is not null)
                    Require(health.Visible == (focused || ratio <= .3), $"Only critical HP stays visible without focus, across the nearest legal integer HP values around 30% ({ratio}).");
                Require(badges.GetNode<CanvasItem>("DoneIndicator").Visible,
                    "Selection changes must not conceal the already-acted indicator.");
                Require(badges.GetNode<CanvasItem>("CaptureGauge").Visible,
                    "Selection changes must not conceal canonical capture progress.");
                Require(state.ToJsonString() == before, "Repeated emphasis changes must not mutate supplied battle JSON.");
            }
        }
        Require(source.ToJsonString() == sourceBefore, "Emphasis fixture must not mutate the caller's campaign state.");
        board.RenderState(source);
    }

    private void CheckMapOwnership(BoardView board, JsonObject state, AssetCatalog assets)
    {
        var tiles = state["map"]!["tiles"]!.AsArray();
        var index = Enumerable.Range(0, tiles.Count).First(at => assets.HasMapStructureBinding(tiles[at]!.GetValue<string>()));
        Node2D Structure() => board.GetNode<Node2D>($"CanonicalProjection/MapStructure_{index}");
        state["map"]!["owners"]![index] = 1;
        board.RenderState(state);
        var first = Structure().GetNode<Sprite2D>("OwnerBanner");
        using var firstImage = first.Texture.GetImage();
        var firstPixels = firstImage.GetData();
        state["map"]!["owners"]![index] = 2;
        board.RenderState(state);
        var second = Structure().GetNode<Sprite2D>("OwnerBanner");
        using var secondImage = second.Texture.GetImage();
        Require(!firstPixels.SequenceEqual(secondImage.GetData()), "Changing site owner 1 to 2 must change actual rendered banner pixels");
        Require(second.GetMeta("owner_color").AsString() == "#" + Color.FromHtml(state["players"]![1]!["color"]!.GetValue<string>()).ToHtml(false),
            "Site banner must use the new canonical player's color");
        state["map"]!["owners"]![index] = 0;
        board.RenderState(state);
        Require(!Structure().HasNode("OwnerBanner"), "Neutral sites must remove the prior owner's banner");
    }

    private static Node2D[] DynamicNodes(BoardView board) =>
        board.GetNode<Node2D>("CanonicalProjection/DynamicState").GetChildren().OfType<Node2D>().ToArray();
    private void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        assertions++;
    }
}
