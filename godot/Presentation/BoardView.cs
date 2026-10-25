#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Empire.Core.State;
using Empire.Core.Geometry;
using Empire.Presentation.Tactical;

namespace Empire.Presentation;

public sealed record BoardViewRules(AssetCatalog Assets, EnvironmentCatalog Environment, ITacticalGrid Grid, ReadOnlyContent Content, BoardPlaneFactory Plane);

/// <summary>
/// Read-only projection of the canonical battle. This node never dispatches an order,
/// computes legality, advances a turn, or changes a supplied JSON object.
/// </summary>
public partial class BoardView : Node2D
{
    private AssetCatalog? _assets;
    private string _levelId = "";
    private Vector2I _mapSize;
    private Node2D? _scenery;
    private Node2D? _projection;
    private BoardGroundView? _terrain;
    private DynamicStateView? _dynamicState;
    private AuthoredScene? _scene;
    private EnvironmentCatalog? _environment;
    private ITacticalGrid? _grid;
    private ReadOnlyContent? _content;
    private BoardProjection? _surface;
    private BoardPlaneFactory _createPlane = null!;
    public BoardProjection Projection => _surface ?? throw new InvalidOperationException("Render a canonical snapshot before querying its surface.");
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
    public Vector2 FieldSize => Projection.Size;
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

    public void ConfigureGround(BoardGroundFactory createGround)
    {
        if (_scenery is null || _scene is null || _environment is null)
            throw new InvalidOperationException("Configure the board before its terrain materials.");
        ArgumentNullException.ThrowIfNull(createGround);
        var replacement = createGround(new(_environment, _grid!, _content!, _shadows), _scene);
        if (replacement.GetParent() is not null)
            throw new InvalidOperationException("A ground factory must return a detached owned node.");
        var authoredGround = _terrain is null
            ? _scenery.GetNode<Node2D>("AuthoredGround") : _terrain.DetachAuthoredGround();
        var previousParent = authoredGround.GetParent();
        previousParent?.RemoveChild(authoredGround);
        try
        {
            replacement.ConfigureAuthoredGround(authoredGround);
            _scenery.AddChild(replacement);
        }
        catch
        {
            authoredGround.GetParent()?.RemoveChild(authoredGround);
            if (_terrain is not null)
                _terrain.ConfigureAuthoredGround(authoredGround);
            else
                previousParent!.AddChild(authoredGround);
            replacement.Free();
            throw;
        }
        if (_terrain is not null)
        {
            _scenery.RemoveChild(_terrain);
            _terrain.QueueFree();
        }
        _terrain = replacement;
    }

    public void ConfigureDynamicState()
    {
        _dynamicState = new DynamicStateView(new(_assets ?? throw new InvalidOperationException("Configure the board before its dynamic state."), _content ?? throw new InvalidOperationException("Configure the canonical content before dynamic state."), _shadows));
    }

    public void Configure(BoardViewRules rules, JsonObject level, AuthoredScene scene)
    {
        _createPlane = rules.Plane;
        var catalog = rules.Assets;
        var environment = rules.Environment;
        ArgumentNullException.ThrowIfNull(rules.Grid);
        CancelPresentation();
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
        var distant = new Node2D { Name = "DistantFrame", ZIndex = -4095 };
        staging.AddChild(ground);
        staging.AddChild(standing);
        staging.AddChild(frame);
        staging.AddChild(distant);
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
                            entries.Add(new StandingEntry(sprite, sprite.Position, 2, index, new SpriteStandingVisual(sprite)));
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
                if (placement.Layer is not ("foreground" or "distant"))
                    throw new InvalidOperationException($"Unknown frame layer: {placement.Layer}");
                try
                {
                    var isDistant = placement.Layer == "distant";
                    var sprite = environment.CreateSprite(placement.Id, (float)placement.Scale, placement.Flip,
                        shadows: isDistant ? null : shadows, shadowKind: ShadowKind.Foreground);
                    if (isDistant)
                        sprite.Modulate = new Color(0.24f, 0.32f, 0.28f);
                    sprite.Name = $"Frame_{index}";
                    sprite.Position = contact * catalog.LogicalTileSize;
                    sprite.SetMeta("logical_contact", sprite.Position);
                    (isDistant ? distant : frame).AddChild(sprite);
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
        }
        catch
        {
            staging.Free();
            shadows.Dispose();
            throw;
        }

        ReplaceChild(ref _projection, null);
        _unitCues.Clear();
        _cueLayer = null;
        ReplaceChild(ref _scenery, staging);
        _shadows.Dispose();
        _shadows = shadows;
        _assets = catalog;
        _levelId = levelId;
        _mapSize = new Vector2I(width, height);
        _scene = scene;
        _environment = environment;
        _grid = rules.Grid;
        _content = rules.Content;
        _surface = null;
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
        var snapshot = ReadProjection(canonicalState);
        // This is the interaction surface, not a walkability test: walls remain
        // pickable; only canonical outside terrain is excluded.
        var playable = Enumerable.Range(0, snapshot.Tiles.Length).Where(index =>
        {
            var terrain = _content!.Definition("terrains", snapshot.Tiles[index])
                ?? throw new InvalidOperationException($"Missing terrain definition: {snapshot.Tiles[index]}");
            return !terrain.GetProperty("tags").EnumerateArray().Any(tag => tag.GetString() == "outside");
        }).Select(index => new Coord(index % _mapSize.X, index / _mapSize.X)).ToHashSet();
        var map = canonicalState["map"]!.AsObject();
        if (_surface is null || !_surface.MatchesSurface(map, playable))
            _surface = new BoardProjection(_grid!, _createPlane, map, playable);
        foreach (var entry in _authoredStanding)
            entry.Node.Position = Projection.Project(entry.Contact);
        foreach (var node in new[] { "AuthoredFrame", "DistantFrame" }
            .SelectMany(name => _scenery!.GetNode<Node2D>(name).GetChildren().OfType<Node2D>()))
        {
            if (!node.HasMeta("logical_contact"))
                node.SetMeta("logical_contact", node.Position);
            node.Position = Projection.Project(node.GetMeta("logical_contact").AsVector2());
        }
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
                _terrain.Render(Projection, snapshot.Tiles, snapshot.Elevation, _mapSize);
                entries.AddRange(_terrain.DepthEntries.Select((entry, index) =>
                    new StandingEntry(entry.Node, entry.LogicalContact, entry.Layer, index + _authoredStanding.Count, entry.Visual, entry.SelectionCell)));
                diagnostics.AddRange(_terrain.Diagnostics);
                foreach (var site in _terrain.Sites)
                {
                    var cell = site.Cell;
                    var owner = snapshot.Owners[cell.Y * _mapSize.X + cell.X];
                    if (owner == 0)
                        continue;
                    var banner = DynamicStateView.CreateOwnerBanner("#" + snapshot.Colors[owner].ToHtml(false), _shadows);
                    banner.Position = Projection.CellContact(cell);
                    staging.AddChild(banner);
                    entries.Add(new(banner, Projection.LogicalCellContact(cell), 3, entries.Count));
                }
            }
            for (var index = 0; index < snapshot.Tiles.Length; index++)
            {
                var terrain = snapshot.Tiles[index];
                var cell = new Coord(index % _mapSize.X, index / _mapSize.X);
                if (_terrain?.OwnsMapStructureBody(cell) == true || !assets.HasMapStructureBinding(terrain))
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
                    entries.Add(new StandingEntry(structure, LogicalContact(index % _mapSize.X, index / _mapSize.X), 1, index, new SpriteStandingVisual(sprite), new Coord(index % _mapSize.X, index / _mapSize.X)));
                }
                catch (MissingArtResourceException error)
                {
                    diagnostics.Add($"Map structure at ({index % _mapSize.X},{index / _mapSize.X}): {error.Message}");
                }
            }
            if (_dynamicState is not null)
            {
                var dynamicProjection = _dynamicState.Create(canonicalState, Projection);
                foreach (var node in dynamicProjection.Root.GetChildren().OfType<Node2D>())
                {
                    node.SetMeta("logical_contact", node.Position);
                    node.Position = Projection.Project(node.Position);
                }
                staging.AddChild(dynamicProjection.Root);
                foreach (var entry in dynamicProjection.Standing)
                {
                    PaintedSurface? body = null;
                    if (entry.Layer == 0)
                        foreach (var sprite in entry.Node.GetChildren().OfType<Sprite2D>().ToArray())
                        {
                            var surface = PaintedSurface.Create(Projection, sprite, entry.Node.Position);
                            entry.Node.AddChild(surface);
                            sprite.Visible = false;
                            if (sprite == entry.SurfaceBody)
                                body = surface;
                        }
                    entries.Add(new StandingEntry(entry.Node, entry.Node.GetMeta("logical_contact").AsVector2(),
                        entry.Layer, entry.Index, entry.Visual, entry.SelectionCell, Surface: body));
                }
                diagnostics.AddRange(dynamicProjection.Diagnostics);
                renderedStructures = dynamicProjection.Structures;
                renderedMarkers = dynamicProjection.Markers;
            }
            var cover = new Node2D { Name = "IllustratedCover", ZIndex = -3 };
            staging.AddChild(cover);
            renderedCover = BoardScenery.AddCover(cover, canonicalState["map"]!.AsObject(), assets, _shadows, diagnostics, Projection);
            foreach (var node in cover.GetChildren().OfType<Node2D>())
            {
                var contact = node.Position;
                node.Position = Projection.Project(contact);
                entries.Add(new(node, contact, 2, entries.Count, new SpriteStandingVisual((Sprite2D)node)));
            }
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
                    if (Projection.FacingVector(unit.Facing).X < 0)
                        sprite.Scale = new Vector2(-sprite.Scale.X, sprite.Scale.Y);
                    sprite.AddChild(SpriteShadows.CreateFigure(sprite, snapshot.Colors[unit.Owner], _shadows));
                    actor.AddChild(sprite);
                    if (_dynamicState is not null)
                        actor.AddChild(_dynamicState.CreateUnitBadges(unit.Type, unit.Hp, unit.Done,
                            snapshot.CaptureProgress[unit.Y * _mapSize.X + unit.X], snapshot.CaptureThreshold,
                            Projection.FacingVector(unit.Facing)));
                    actor.SetMeta("unit_id", unit.Id);
                    actor.SetMeta("logical_contact", LogicalContact(unit.X, unit.Y));
                    if (unit.Key is not null)
                        actor.SetMeta("actor_key", unit.Key);
                    actor.SetMeta("unit_type", unit.Type);
                    actor.SetMeta("owner", unit.Owner);
                    actor.SetMeta("hp", unit.Hp);
                    actor.SetMeta("done", unit.Done);
                    staging.AddChild(actor);
                    entries.Add(new StandingEntry(actor, LogicalContact(unit.X, unit.Y), 9, unit.Id, new SpriteStandingVisual(sprite), new Coord(unit.X, unit.Y), unit.Id));
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
            Projection.PrepareDepthPartitions(entries.Where(entry => entry.Layer != 0)
                .Select(entry => StandingDepth(entry)), Array.Empty<float>());
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
        diagnostics.AddRange(RebuildOcclusionCues(snapshot.Colors));
        ProjectionUnitCount = snapshot.Units.Count;
        RenderedUnitCount = renderedUnits;
        RenderedStructureCount = renderedStructures;
        RenderedMarkerCount = renderedMarkers;
        RenderedCoverPropCount = renderedCover;
        _diagnostics = diagnostics.Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
    }

    private BattleRenderSnapshot ReadProjection(JsonObject state)
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
        var elevation = ArrayValue(map, "elevation", count)
            .Select(node => Integer(node, "tile elevation")).ToArray();
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
        return new BattleRenderSnapshot(tiles, elevation, owners, colors, units, capture, captureThreshold);
    }

    private Vector2 LogicalContact(int x, int y) => Projection.LogicalCellContact(new Coord(x, y));
    private Vector2 CellContact(int x, int y) => Projection.CellContact(new Coord(x, y));

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

    public TacticalHit? Pick(Vector2 fieldPoint) => PickCue(fieldPoint) ?? PickScene(fieldPoint);

    private TacticalHit? PickScene(Vector2 fieldPoint)
    {
        var surface = Projection.Pick(fieldPoint);
        var global = ToGlobal(fieldPoint);
        var coverage = new List<(StandingEntry? Entry, Node2D Geometry, int Depth)>();
        // SpriteShadows suppresses its source handle's SelfModulate while its child still draws the complete body.
        foreach (var entry in _standing)
        {
            if (entry.Surface?.HitPolygon(global) is { } polygon)
                coverage.Add((entry, polygon, polygon.ZIndex));
            else if (entry.Visual is { } visual && visual.Node.IsVisibleInTree() && visual.Node.Modulate.A > 0 && visual.Covers(global))
                coverage.Add((entry, entry.Node, entry.Node.ZIndex));
        }
        // Frame artwork is deliberately in front of the battlefield. Its transparent
        // canvas must not block interaction, but its visible pixels must.
        foreach (var sprite in _scenery!.GetNode<Node2D>("AuthoredFrame").GetChildren().OfType<Sprite2D>())
            if (sprite.IsVisibleInTree() && sprite.Modulate.A > 0 && sprite.IsPixelOpaque(sprite.ToLocal(global)))
                coverage.Add((null, sprite, sprite.ZIndex + _scenery.GetNode<Node2D>("AuthoredFrame").ZIndex));
        // Use the same absolute Z and scene-tree tie order that draws these parts.
        coverage.Sort((left, right) => CompareDrawOrder(left.Geometry, left.Depth, right.Geometry, right.Depth));
        var entitiesVisible = true;
        foreach (var (entry, _, depth) in coverage)
        {
            if (surface is not null && depth < surface.DrawDepth)
                break;
            if (entry is null)
                return null; // Opaque frame artwork is an explicit blocker, at its actual draw depth.
            if (entry.Surface is null && entry.Visual is { } material && !material.ContainsPointer(global))
            {
                // This material hides actors/sites behind it, but exposes the ground
                // only if no further explicit blocker stands in front of that surface.
                entitiesVisible = false;
                continue;
            }
            if (entry.SelectionCell is not { } selected)
                return null;
            if (entitiesVisible)
            {
                var cell = new BoardCell(selected.X, selected.Y);
                return entry.UnitId is { } unitId ? TacticalHit.Unit(cell, unitId) : TacticalHit.Object(cell);
            }
        }
        return GroundHit();

        TacticalHit? GroundHit() => surface?.Cell is { } cell ? TacticalHit.Ground(new BoardCell(cell.X, cell.Y)) : null;
    }

    private static int CompareDrawOrder(Node2D left, int leftDepth, Node2D right, int rightDepth) =>
        leftDepth != rightDepth ? rightDepth.CompareTo(leftDepth)
        : left == right ? 0 : left.IsGreaterThan(right) ? 1 : -1;

    private float StandingDepth(StandingEntry entry)
    {
        var offset = entry.Visual?.FrontOffset(entry.Node) ?? Vector2.Zero;
        return Projection.Depth(entry.Contact) + offset.Y;
    }

    private void OrderStanding(List<StandingEntry> entries)
    {
        foreach (var entry in entries)
        {
            entry.Node.ZAsRelative = false;
            entry.Node.ZIndex = Projection.LayerAtDepth(StandingDepth(entry),
                entry.Layer == 0 ? BoardDepthLayer.Surface : BoardDepthLayer.Standing);
        }
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

    private sealed record StandingEntry(Node2D Node, Vector2 Contact, int Layer, int Index, StandingVisual? Visual = null, Coord? SelectionCell = null, int? UnitId = null, PaintedSurface? Surface = null);
    private sealed record UnitProjection(int Id, string? Key, string Type, int Owner,
        int X, int Y, int Hp, bool Done, string? Facing);
    private sealed record BattleRenderSnapshot(string[] Tiles, int[] Elevation, int[] Owners,
        Dictionary<int, Color> Colors, List<UnitProjection> Units, double[] CaptureProgress, double CaptureThreshold);
}
