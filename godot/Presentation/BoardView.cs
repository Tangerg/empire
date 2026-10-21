#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Empire.Core.State;

namespace Empire.Presentation;

public sealed record BoardViewRules(AssetCatalog Assets, EnvironmentCatalog Environment,
    Func<string?, bool> FacesLeft, Func<string?, Vector2>? DirectionVector);

/// <summary>
/// Read-only projection of the canonical battle. This node never dispatches an order,
/// computes legality, advances a turn, or changes a supplied JSON object.
/// </summary>
public partial class BoardView : Node2D
{
    private AssetCatalog? _assets;
    private Func<string?, bool>? _facesLeft;
    private Func<string?, Vector2>? _directionVector;
    private string _levelId = "";
    private Vector2I _mapSize;
    private Node2D? _scenery;
    private Node2D? _projection;
    private TerrainView? _terrain;
    private DynamicStateView? _dynamicState;
    private AuthoredScene? _scene;
    private EnvironmentCatalog? _environment;
    private ShadowCache _shadows = new();
    private readonly List<StandingEntry> _authoredStanding = new();
    private readonly List<StandingEntry> _standing = new();
    private readonly List<string> _sceneDiagnostics = new();
    private ReadOnlyCollection<string> _diagnostics = Array.AsReadOnly(Array.Empty<string>());

    public ReadOnlyCollection<string> Diagnostics => _diagnostics;
    // Source art-review metadata remains diagnostic evidence, not player-facing acceptance prose.
    public int RenderingIssueCount => Diagnostics.Count(message => message is not
        "Authored scene is marked runtimeReady=false in the source document." and not
        "Environment catalog is marked runtimeReady=false in the source document.");
    public Vector2 FieldSize => new Vector2(_mapSize.X, _mapSize.Y) * (_assets?.LogicalTileSize ?? 0);
    public int RenderedUnitCount
    {
        get; private set;
    }
    public int ProjectionUnitCount
    {
        get; private set;
    }
    public int RenderedAuthoredPlacementCount
    {
        get; private set;
    }
    public int RenderedPerimeterPlacementCount
    {
        get; private set;
    }
    public int RenderedCoverPropCount
    {
        get; private set;
    }
    public int RenderedStructureCount
    {
        get; private set;
    }
    public int RenderedMarkerCount
    {
        get; private set;
    }
    public bool SurfaceReady => _terrain is not null;

    public void ConfigureTerrain(WorldMaterialsManifest materials)
    {
        if (_scenery is null || _scene is null || _environment is null)
            throw new InvalidOperationException("Configure the board before its terrain materials.");
        if (_terrain is not null)
        {
            _scenery.RemoveChild(_terrain);
            _terrain.QueueFree();
        }
        _terrain = new TerrainView { Name = "Terrain" };
        _terrain.Configure(materials, _scene, _environment, _shadows);
        _scenery.AddChild(_terrain);
    }

    public void ConfigureDynamicState(ReadOnlyContent content)
    {
        _dynamicState = new DynamicStateView(new(_assets ?? throw new InvalidOperationException("Configure the board before its dynamic state."), content, _shadows));
    }

    public void Configure(BoardViewRules rules, JsonObject level, AuthoredScene scene)
    {
        var catalog = rules.Assets;
        var environment = rules.Environment;
        var facesLeft = rules.FacesLeft;
        var directionVector = rules.DirectionVector;
        CancelPresentation();
        ArgumentNullException.ThrowIfNull(facesLeft);
        var width = Integer(level, "width");
        var height = Integer(level, "height");
        var levelId = Text(level, "id");
        if (width <= 0 || height <= 0 || scene.LevelId != levelId
            || scene.MapSize.Length != 2 || scene.MapSize[0] != width || scene.MapSize[1] != height)
            throw new InvalidOperationException("Authored scene and canonical level identity/dimensions differ.");
        if (catalog.LogicalTileSize != environment.LogicalTileSize || catalog.LogicalTileSize != 32)
            throw new InvalidOperationException("This exported GrayFlag presentation requires the declared 32-unit square layout.");

        var staging = new Node2D { Name = "AuthoredScene" };
        var ground = new Node2D { Name = "AuthoredGround", ZIndex = -100 };
        var standing = new Node2D { Name = "AuthoredStanding" };
        var frame = new Node2D { Name = "AuthoredFrame", ZIndex = 2048 };
        staging.AddChild(ground);
        staging.AddChild(standing);
        staging.AddChild(frame);
        var entries = new List<StandingEntry>();
        var shadows = new ShadowCache();
        var diagnostics = new List<string>();
        var rendered = 0;
        var perimeter = 0;
        try
        {
            for (var index = 0; index < scene.Placements.Count; index++)
            {
                var placement = scene.Placements[index];
                ValidatePlacement(placement);
                try
                {
                    var sprite = placement.TopicId is { } topic
                        ? CreateTopicPlacement(catalog, topic, placement)
                        : environment.CreateSprite(placement.Id!, (float)placement.Scale,
                            placement.Flip, (float)placement.Opacity, shadows,
                            placement.StandingShadow ? ShadowKind.Standing : ShadowKind.Environment);
                    if (placement.TopicId is { } topicId)
                        sprite.AddChild(SpriteShadows.Create(sprite, ShadowKind.Topic, catalog.ResolveTopic(topicId).PixelDensity, shadows));
                    sprite.Name = $"Placement_{index}";
                    sprite.Position = new Vector2((float)placement.X * catalog.LogicalTileSize,
                        ((float)placement.Y + 1) * catalog.LogicalTileSize);
                    switch (placement.Layer)
                    {
                        case "foundation":
                        case "ground-decal":
                            sprite.ZIndex = placement.Layer == "foundation" ? 0 : 1;
                            ground.AddChild(sprite);
                            break;
                        case "under-units":
                            standing.AddChild(sprite);
                            entries.Add(new StandingEntry(sprite, sprite.Position, 2, index));
                            break;
                        case "over-units":
                            frame.AddChild(sprite);
                            break;
                    }
                    rendered++;
                }
                catch (MissingArtResourceException error)
                {
                    diagnostics.Add($"Authored placement {index} ({placement.Id ?? placement.TopicId}): {error.Message}");
                }
            }
            for (var index = 0; index < scene.FramePlacements.Count; index++)
            {
                var placement = scene.FramePlacements[index];
                var contact = RasterResources.Pair(placement.Contact, $"frame contact {index}");
                RasterResources.Positive(placement.Scale, $"frame scale {index}");
                try
                {
                    // Authored frame placements emit is-scene-frame, not the generated
                    // is-frame-left/right/bottom classes. Region names do not select CSS.
                    var sprite = environment.CreateSprite(placement.Id, (float)placement.Scale, placement.Flip,
                        shadows: shadows, shadowKind: ShadowKind.Foreground);
                    sprite.Name = $"Frame_{index}";
                    sprite.Position = contact * catalog.LogicalTileSize;
                    frame.AddChild(sprite);
                    rendered++;
                }
                catch (MissingArtResourceException error)
                {
                    diagnostics.Add($"Frame placement {index} ({placement.Id}): {error.Message}");
                }
            }
            if (!scene.HasAuthoredFramePlacements)
                perimeter = BoardScenery.AddPerimeter(frame, environment, width, height, shadows, diagnostics);
            if (!scene.RuntimeReady)
                diagnostics.Add("Authored scene is marked runtimeReady=false in the source document.");
            if (!environment.RuntimeReady)
                diagnostics.Add("Environment catalog is marked runtimeReady=false in the source document.");
            OrderStanding(entries);
        }
        catch
        {
            staging.Free();
            shadows.Dispose();
            throw;
        }

        ReplaceChild(ref _projection, null);
        ReplaceChild(ref _scenery, staging);
        _shadows.Dispose();
        _shadows = shadows;
        _assets = catalog;
        _facesLeft = facesLeft;
        _directionVector = directionVector;
        _levelId = levelId;
        _mapSize = new Vector2I(width, height);
        _scene = scene;
        _environment = environment;
        _terrain = null;
        _dynamicState = null;
        _authoredStanding.Clear();
        _authoredStanding.AddRange(entries);
        _sceneDiagnostics.Clear();
        _sceneDiagnostics.AddRange(diagnostics);
        foreach (var message in diagnostics)
            GD.Print($"Scene diagnostic: {message}");
        _diagnostics = diagnostics.AsReadOnly();
        RenderedAuthoredPlacementCount = rendered;
        RenderedPerimeterPlacementCount = perimeter;
        RenderedCoverPropCount = 0;
        RenderedUnitCount = ProjectionUnitCount = 0;
        RenderedStructureCount = RenderedMarkerCount = 0;
    }

    /// <summary>Copies presentation values only. It does not retain the mutable source projection.</summary>
    public void RenderState(JsonObject canonicalState, IReadOnlySet<int>? visibleUnitIds = null)
    {
        var assets = _assets ?? throw new InvalidOperationException("Configure BoardView before rendering.");
        var facesLeft = _facesLeft!;
        var snapshot = ReadProjection(canonicalState);
        var staging = new Node2D { Name = "CanonicalProjection" };
        var entries = new List<StandingEntry>(_authoredStanding);
        var diagnostics = new List<string>(_sceneDiagnostics);
        var renderedUnits = 0;
        var renderedStructures = 0;
        var renderedMarkers = 0;
        var renderedCover = 0;
        try
        {
            if (_terrain is not null)
            {
                _terrain.Render(snapshot.Tiles, _mapSize);
                _terrain.ApplyGroundClip(_scenery!.GetNode<Node2D>("AuthoredGround"));
                entries.AddRange(_terrain.Standing.Select((node, index) =>
                    new StandingEntry(node, node.Position, 2, index + _authoredStanding.Count)));
                diagnostics.AddRange(_terrain.Diagnostics);
            }
            for (var index = 0; index < snapshot.Tiles.Length; index++)
            {
                var terrain = snapshot.Tiles[index];
                if (!assets.HasMapStructureBinding(terrain))
                    continue;
                try
                {
                    var asset = assets.ResolveMapStructure(terrain);
                    var sprite = assets.CreateSprite(asset, "normal");
                    sprite.Name = "AuthoredSprite";
                    var structure = new Node2D { Name = $"MapStructure_{index}", Position = CellContact(index % _mapSize.X, index / _mapSize.X) };
                    structure.AddChild(BoardLighting.CreateStructureContact(new(asset.FrameWidth, asset.FrameHeight), asset.PresentationScale, _shadows));
                    structure.AddChild(sprite);
                    if (snapshot.Owners[index] != 0)
                        structure.AddChild(DynamicStateView.CreateOwnerBanner("#" + snapshot.Colors[snapshot.Owners[index]].ToHtml(false), _shadows));
                    staging.AddChild(structure);
                    entries.Add(new StandingEntry(structure, structure.Position, 1, index));
                }
                catch (MissingArtResourceException error)
                {
                    diagnostics.Add($"Map structure at ({index % _mapSize.X},{index / _mapSize.X}): {error.Message}");
                }
            }
            if (_dynamicState is not null)
            {
                var dynamicProjection = _dynamicState.Create(canonicalState);
                staging.AddChild(dynamicProjection.Root);
                entries.AddRange(dynamicProjection.Standing.Select(entry =>
                    new StandingEntry(entry.Node, entry.Node.Position, entry.Layer, entry.Index)));
                diagnostics.AddRange(dynamicProjection.Diagnostics);
                renderedStructures = dynamicProjection.Structures;
                renderedMarkers = dynamicProjection.Markers;
            }
            var cover = new Node2D { Name = "IllustratedCover", ZIndex = -3 };
            staging.AddChild(cover);
            renderedCover = BoardScenery.AddCover(cover, canonicalState["map"]!.AsObject(), assets, _shadows, diagnostics);
            foreach (var unit in snapshot.Units)
            {
                if (visibleUnitIds is not null && !visibleUnitIds.Contains(unit.Id))
                    continue;
                try
                {
                    var asset = assets.ResolveUnit(unit.Type, unit.Key);
                    var sprite = assets.CreateSprite(asset);
                    var actor = new CanvasGroup { Name = $"Unit_{unit.Id}", Position = CellContact(unit.X, unit.Y) };
                    sprite.Name = "AuthoredSprite";
                    // Actor mirroring is around the canonical foot, not the sheet's center.
                    if (facesLeft(unit.Facing))
                        sprite.Scale = new Vector2(-sprite.Scale.X, sprite.Scale.Y);
                    sprite.AddChild(SpriteShadows.CreateFigure(sprite, snapshot.Colors[unit.Owner], _shadows));
                    actor.AddChild(sprite);
                    if (_dynamicState is not null)
                        actor.AddChild(_dynamicState.CreateUnitBadges(unit.Type, unit.Hp, unit.Done,
                            snapshot.CaptureProgress[unit.Y * _mapSize.X + unit.X], snapshot.CaptureThreshold,
                            _directionVector?.Invoke(unit.Facing)));
                    actor.SetMeta("unit_id", unit.Id);
                    if (unit.Key is not null)
                        actor.SetMeta("actor_key", unit.Key);
                    actor.SetMeta("unit_type", unit.Type);
                    actor.SetMeta("owner", unit.Owner);
                    actor.SetMeta("hp", unit.Hp);
                    actor.SetMeta("done", unit.Done);
                    staging.AddChild(actor);
                    entries.Add(new StandingEntry(actor, actor.Position, 9, unit.Id));
                    renderedUnits++;
                }
                catch (MissingArtResourceException error)
                {
                    diagnostics.Add($"Unit {unit.Id} ({unit.Key ?? unit.Type}): {error.Message}");
                }
            }
            foreach (var layer in _dynamicState is null ? new[] { "structures", "markers" } : Array.Empty<string>())
                if (canonicalState[layer] is JsonArray { Count: > 0 })
                    diagnostics.Add($"Canonical '{layer}' is present but dynamic presentation content is not configured.");
            OrderStanding(entries);
        }
        catch
        {
            staging.Free();
            throw;
        }

        ReplaceChild(ref _projection, staging);
        _standing.Clear();
        _standing.AddRange(entries);
        ProjectionUnitCount = snapshot.Units.Count;
        RenderedUnitCount = renderedUnits;
        RenderedStructureCount = renderedStructures;
        RenderedMarkerCount = renderedMarkers;
        RenderedCoverPropCount = renderedCover;
        _diagnostics = diagnostics.Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
    }

    private Projection ReadProjection(JsonObject state)
    {
        if (Text(state, "levelId") != _levelId)
            throw new InvalidOperationException("Projection does not belong to the configured level.");
        var map = state["map"] as JsonObject ?? throw new InvalidOperationException("Canonical map is absent.");
        if (Integer(map, "width") != _mapSize.X || Integer(map, "height") != _mapSize.Y)
            throw new InvalidOperationException("Canonical projection changed the configured map size.");
        var count = checked(_mapSize.X * _mapSize.Y);
        var tiles = ArrayValue(map, "tiles", count).Select(node => node?.GetValue<string>()
            ?? throw new InvalidOperationException("Null canonical terrain.")).ToArray();
        var owners = ArrayValue(map, "owners", count)
            .Select(node => Integer(node, "tile owner")).ToArray();
        ArrayValue(map, "elevation", count);
        var capture = ArrayValue(map, "captureProgress", count).Select(node => Number(node, "capture progress")).ToArray();
        var captureThreshold = Number(state["rules"]?["captureThreshold"], "capture threshold");

        var colors = new Dictionary<int, Color>();
        foreach (var node in ArrayValue(state, "players"))
        {
            var player = node as JsonObject ?? throw new InvalidOperationException("Invalid canonical player.");
            var color = Text(player, "color");
            if (!Color.HtmlIsValid(color) || !colors.TryAdd(Integer(player, "id"), Color.FromHtml(color)))
                throw new InvalidOperationException("Invalid or duplicate canonical player/color.");
        }
        foreach (var owner in owners)
            if (owner != 0 && !colors.ContainsKey(owner))
                throw new InvalidOperationException($"Map references absent owner: {owner}");
        var units = new List<UnitProjection>();
        var ids = new HashSet<int>();
        foreach (var node in ArrayValue(state, "units"))
        {
            var unit = node as JsonObject ?? throw new InvalidOperationException("Invalid canonical unit.");
            var id = Integer(unit, "id");
            var x = Integer(unit, "x");
            var y = Integer(unit, "y");
            var owner = Integer(unit, "owner");
            if (!ids.Add(id) || !colors.ContainsKey(owner) || x < 0 || y < 0 || x >= _mapSize.X || y >= _mapSize.Y)
                throw new InvalidOperationException($"Invalid canonical unit identity, owner, or coordinates: {id}");
            units.Add(new UnitProjection(id, unit["key"]?.GetValue<string>(), Text(unit, "type"),
                owner, x, y, Integer(unit, "hp"),
                unit["done"]?.GetValue<bool>() ?? throw new InvalidOperationException($"Unit {id} lacks done state."),
                unit["facing"]?.GetValue<string>()));
        }
        return new Projection(tiles, owners, colors, units, capture, captureThreshold);
    }

    private Vector2 CellContact(int x, int y) =>
        new(x * _assets!.LogicalTileSize + 16, y * _assets.LogicalTileSize + 31);

    private static Sprite2D CreateTopicPlacement(AssetCatalog catalog, string topic, AuthoredPlacement placement)
    {
        var asset = catalog.ResolveTopic(topic);
        var sprite = catalog.CreateSpriteAtFrame(asset, 0);
        // Topic placements own their authored scale; base presentationScale belongs to map structures.
        sprite.Scale = Vector2.One * ((float)placement.Scale / asset.PixelDensity);
        if (placement.Flip)
            sprite.Scale = new Vector2(-sprite.Scale.X, sprite.Scale.Y);
        sprite.Modulate = new Color(1, 1, 1, (float)placement.Opacity);
        return sprite;
    }

    private static void ValidatePlacement(AuthoredPlacement placement)
    {
        if ((placement.Id is null) == (placement.TopicId is null)
            || !double.IsFinite(placement.X) || !double.IsFinite(placement.Y)
            || !double.IsFinite(placement.Opacity) || placement.Opacity < 0 || placement.Opacity > 1
            || placement.Layer is not ("foundation" or "ground-decal" or "under-units" or "over-units"))
            throw new InvalidOperationException($"Invalid authored placement: {placement.Id ?? placement.TopicId}");
        RasterResources.Positive(placement.Scale, "placement scale");
    }

    private static void OrderStanding(List<StandingEntry> entries)
    {
        if (entries.Count >= 2048)
            throw new InvalidOperationException("Standing projection exceeds native depth range.");
        entries.Sort((a, b) =>
        {
            var y = a.Contact.Y.CompareTo(b.Contact.Y);
            if (y != 0)
                return y;
            var x = a.Contact.X.CompareTo(b.Contact.X);
            if (x != 0)
                return x;
            var layer = a.Layer.CompareTo(b.Layer);
            return layer != 0 ? layer : a.Index.CompareTo(b.Index);
        });
        for (var index = 0; index < entries.Count; index++)
            entries[index].Node.ZIndex = index;
    }

    private void ReplaceChild(ref Node2D? previous, Node2D? next)
    {
        if (previous is not null)
        {
            RemoveChild(previous);
            previous.QueueFree();
        }
        previous = next;
        if (next is not null)
            AddChild(next);
    }

    private static string Text(JsonObject value, string field) =>
        value[field]?.GetValue<string>() ?? throw new InvalidOperationException($"Canonical field is absent: {field}");

    private static int Integer(JsonObject value, string field) => Integer(value[field], field);

    private static int Integer(JsonNode? value, string field)
    {
        if (value is not JsonValue scalar || scalar.GetValueKind() != JsonValueKind.Number)
            throw new InvalidOperationException($"Canonical field must be an integer: {field}");
        if (scalar.TryGetValue<int>(out var integer))
            return integer;
        // Parsed JSON and in-memory factory values may have different CLR numeric types.
        var number = scalar.TryGetValue<double>(out var numeric)
            ? numeric : JsonSerializer.Deserialize<double>(scalar.ToJsonString());
        if (!double.IsFinite(number) || number != Math.Truncate(number)
            || number < int.MinValue || number > int.MaxValue)
            throw new InvalidOperationException($"Canonical field must be an integer: {field}");
        return (int)number;
    }

    private static double Number(JsonNode? value, string field)
    {
        if (value is not JsonValue scalar || scalar.GetValueKind() != JsonValueKind.Number)
            throw new InvalidOperationException($"Canonical field must be numeric: {field}");
        var number = scalar.TryGetValue<double>(out var numeric) ? numeric : JsonSerializer.Deserialize<double>(scalar.ToJsonString());
        if (!double.IsFinite(number))
            throw new InvalidOperationException($"Canonical field must be finite: {field}");
        return number;
    }

    private static JsonArray ArrayValue(JsonObject value, string field, int? length = null)
    {
        var array = value[field] as JsonArray
            ?? throw new InvalidOperationException($"Canonical array is absent: {field}");
        if (length is { } expected && array.Count != expected)
            throw new InvalidOperationException($"Canonical {field} length differs from map dimensions.");
        return array;
    }

    private sealed record StandingEntry(Node2D Node, Vector2 Contact, int Layer, int Index);
    private sealed record UnitProjection(int Id, string? Key, string Type, int Owner,
        int X, int Y, int Hp, bool Done, string? Facing);
    private sealed record Projection(string[] Tiles, int[] Owners,
        Dictionary<int, Color> Colors, List<UnitProjection> Units, double[] CaptureProgress, double CaptureThreshold);
}
