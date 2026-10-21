#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

public sealed record TerrainViewRules(EnvironmentCatalog Environment, ITacticalGrid Grid, ShadowCache? Shadows);

/// <summary>
/// Native material and authored-terrain composition. RGB textures remain the approved
/// resources; only topology alpha is generated. This reads no battle legality or RNG.
/// </summary>
public partial class TerrainView : BoardGroundView
{
    private static readonly Vector2I[] Cardinal = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];
    private static readonly Vector2I[] Around = [
        new(0, -1), new(1, -1), new(1, 0), new(1, 1),
        new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1)];
    private const string MaskedMaterialShader = """
        shader_type canvas_item;
        uniform sampler2D fill_texture : source_color, repeat_enable, filter_linear;
        uniform vec2 world_size;
        uniform vec2 material_period;
        uniform bool has_playable_shape = false;
        uniform sampler2D playable_shape : filter_linear, repeat_disable;
        void fragment() {
            vec4 fill = texture(fill_texture, UV * world_size / material_period);
            float shape_alpha = has_playable_shape ? texture(playable_shape, UV).a : 1.0;
            COLOR = vec4(fill.rgb, fill.a * texture(TEXTURE, UV).a * shape_alpha);
        }
        """;
    private WorldMaterialsManifest _manifest = null!;
    private AuthoredScene _scene = null!;
    private EnvironmentCatalog _environment = null!;
    private ShadowCache? _shadows;
    private string[]? _tiles;
    private int[]? _elevation;
    private ITacticalGrid _grid = null!;
    private BoardProjection? _projection;
    private Node2D? _authoredGround;
    private Vector2I _renderedSize;
    private Texture2D? _groundShape;
    private Vector2 _groundSize;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly List<string> _diagnostics = new();
    private readonly List<Sprite2D> _standing = new();
    private readonly List<TerrainDepthEntry> _depthEntries = new();
    public override IReadOnlyList<string> Diagnostics => _diagnostics;
    public IReadOnlyList<Node2D> Standing => _standing;
    public override IReadOnlyList<TerrainDepthEntry> DepthEntries => _depthEntries;
    public int RenderedSceneryCount => _standing.Count;
    public int RenderedEdgeDecorationCount
    {
        get; private set;
    }

    public static BoardGroundFactory Factory(WorldMaterialsManifest materials) => (rules, scene) =>
    {
        var view = new TerrainView { Name = "Terrain" };
        try
        {
            view.Configure(new(rules.Environment, rules.Grid, rules.Shadows), materials, scene);
            return view;
        }
        catch
        {
            view.Free();
            throw;
        }
    };

    public void Configure(TerrainViewRules rules, WorldMaterialsManifest manifest, AuthoredScene scene)
    {
        if (manifest.Schema != 1)
            throw new InvalidOperationException("Unsupported world materials schema.");
        _manifest = manifest;
        _scene = scene;
        _environment = rules.Environment;
        _grid = rules.Grid;
        _shadows = rules.Shadows;
        _tiles = null;
        _elevation = null;
        _projection = null;
        _groundShape = null;
    }

    /// <summary>Transfers ownership of a detached authored ground subtree.</summary>
    public override void ConfigureAuthoredGround(Node2D authoredGround)
    {
        if (_authoredGround is not null || authoredGround.GetParent() is not null)
            throw new InvalidOperationException("Authored ground must be transferred once as a detached subtree.");
        AddChild(authoredGround);
        _authoredGround = authoredGround;
        _tiles = null;
    }

    public override Node2D DetachAuthoredGround()
    {
        var ground = _authoredGround
            ?? throw new InvalidOperationException("No authored ground is owned by this terrain view.");
        ground.GetParent()!.RemoveChild(ground);
        _authoredGround = null;
        _tiles = null;
        return ground;
    }

    public override void Render(BoardProjection projection, string[] tiles, int[] elevation, Vector2I size)
    {
        if (_tiles is not null && _renderedSize == size && _tiles.SequenceEqual(tiles)
            && _elevation is not null && _elevation.SequenceEqual(elevation)
            && _projection is not null && _projection.Triangles.SequenceEqual(projection.Triangles)
            && _projection.Faces.SequenceEqual(projection.Faces))
            return;
        if (tiles.Length != size.X * size.Y || elevation.Length != tiles.Length)
            throw new InvalidOperationException("Terrain size differs from map.");
        foreach (var tile in tiles.Distinct())
            if (!_manifest.TerrainBindings.ContainsKey(tile))
                throw new InvalidOperationException($"No exported presentation material for terrain: {tile}");
        var staging = new Node2D { Name = "ComposedTerrain" };
        var diagnostics = new List<string>();
        var standing = new List<Sprite2D>();
        var edgeCount = 0;
        var ground = new Node2D { Name = "Ground", ZIndex = -300 };
        staging.AddChild(ground);
        string? TileAt(int x, int y) => x < 0 || y < 0 || x >= size.X || y >= size.Y ? null : tiles[y * size.X + x];
        TerrainMaterialBinding? At(int x, int y) => TileAt(x, y) is { } tile ? _manifest.TerrainBindings[tile] : null;
        bool Tagged(int x, int y, string family)
        {
            if (TileAt(x, y) is not { } tile)
                return false;
            if (!_manifest.TerrainTags.TryGetValue(tile, out var tags)
                || !_manifest.GroundTagFamilies.TryGetValue(family, out var familyTags))
                throw new InvalidOperationException($"Terrain semantic export missing: {tile}/{family}");
            return tags.Intersect(familyTags).Any();
        }
        var cells = Enumerable.Range(0, size.X * size.Y).Select(index => new BoardCell(index % size.X, index / size.X))
            .Where(cell => !Tagged(cell.X, cell.Y, "outside")).ToArray();
        bool Water(int x, int y) => Matches(At(x, y), MaterialRecord("water-material").Replaces);
        bool Road(int x, int y) => Matches(At(x, y), MaterialRecord("road-material").Replaces);
        bool Route(int x, int y) => Tagged(x, y, "way") || Tagged(x, y, "settlement");
        bool Shore(int x, int y) => TileAt(x, y) is not null && !Water(x, y)
            && Cardinal.Any(delta => Water(x + delta.X, y + delta.Y));
        try
        {
            TryLayer("Field material", () => ground.AddChild(CreateField(size, (x, y) => Tagged(x, y, "outside"))), diagnostics);
            if (_scene.GroundOutline.Length == 0 && !tiles.Any(tile => _manifest.TerrainTags[tile]
                    .Intersect(_manifest.GroundTagFamilies["outside"]).Any()))
                diagnostics.Add("No authored ground outline: canonical rectangular field is rendered; generic perimeter backdrop remains unmigrated.");

            // Surface identity and tone come from the content export; world materials replace exact bindings only.
            var field = MaterialRecord("field-material");
            var surfaces = cells.Where(cell => !Authored(TileAt(cell.X, cell.Y)!, cell.X, cell.Y, surface: true))
                .GroupBy(cell => (Atlas: At(cell.X, cell.Y)!.Surface ?? field.Replaces.Atlas,
                    Tone: At(cell.X, cell.Y)!.Tone ?? field.Replaces.Tone ?? 0));
            foreach (var surface in surfaces)
            {
                if (surface.Key.Atlas == field.Replaces.Atlas && surface.Key.Tone == field.Replaces.Tone)
                    continue;
                var world = _manifest.Materials.Values.SingleOrDefault(material => material.Replaces.Atlas == surface.Key.Atlas
                    && material.Replaces.Tone == surface.Key.Tone);
                TryLayer($"Surface {surface.Key.Atlas}/{surface.Key.Tone}", () =>
                {
                    if (world is not null)
                    {
                        using var mask = SolidMask(size, surface);
                        ground.AddChild(Masked($"Surface_{world.Id}", mask, world, size));
                    }
                    else
                        foreach (var cell in surface)
                        {
                            var sprite = _environment.CreateGroundTile(surface.Key.Atlas, surface.Key.Tone,
                                _manifest.SurfaceFit.Inset, _manifest.SurfaceFit.Bleed);
                            sprite.Position += new Vector2(cell.X, cell.Y) * 32;
                            ground.AddChild(sprite);
                        }
                }, diagnostics);
            }

            var blends = cells.Select(cell => At(cell.X, cell.Y)!.Blend).Where(id => id is not null).Cast<string>().ToHashSet();
            if (_scene.River is null && cells.Any(cell => At(cell.X, cell.Y)!.Blend is null && Shore(cell.X, cell.Y)))
                blends.Add(_manifest.ShoreBlend ?? throw new InvalidOperationException("Shore blend identity is not exported."));
            foreach (var blend in blends)
            {
                bool Owns(int x, int y) => TileAt(x, y) is not null && !Tagged(x, y, "outside")
                    && (At(x, y)!.Blend == blend || _scene.River is null && blend == _manifest.ShoreBlend
                        && At(x, y)!.Blend is null && Shore(x, y));
                // Source shoreline connectivity counts every shore cell, even where another blend owns its RGB.
                bool Joins(int x, int y, bool shore) => shore ? Shore(x, y) : At(x, y)?.Blend == blend;
                int Index(int x, int y, ForestMaskRecord spec)
                {
                    var shore = At(x, y)!.Blend is null;
                    var index = Array.IndexOf(spec.MaskOrder, CanonicalBlobMask(NeighborMask(x, y, (nx, ny) => Joins(nx, ny, shore))));
                    return index >= 0 ? index : throw new InvalidOperationException($"Blob mask is absent from {blend}.");
                }
                var spec = blend == _manifest.ForestMask.Id ? _manifest.ForestMask
                    : blend == _manifest.EarthMask?.Id ? _manifest.EarthMask : null;
                TryLayer($"Blend {blend}", () =>
                {
                    if (spec is not null)
                    {
                        using var mask = AtlasMask(size, spec, Owns, (x, y) => Index(x, y, spec));
                        var forest = blend == _manifest.ForestMask.Id;
                        ground.AddChild(Masked(forest ? "ForestFloor" : "EarthFloor", mask,
                            MaterialRecord(forest ? "forest-floor" : "earth-floor"), size));
                    }
                    else
                    {
                        var atlas = _environment.ResolveAtlas(blend).Spec;
                        foreach (var cell in cells.Where(cell => Owns(cell.X, cell.Y)))
                        {
                            var bits = NeighborMask(cell.X, cell.Y, (x, y) => Joins(x, y, At(cell.X, cell.Y)!.Blend is null));
                            var index = Array.IndexOf(atlas.MaskOrder, CanonicalBlobMask(bits));
                            if (index < 0)
                                throw new InvalidOperationException($"Blob mask is absent from {blend}.");
                            var sprite = _environment.CreateGroundTile(blend, index);
                            sprite.Position += new Vector2(cell.X, cell.Y) * 32;
                            ground.AddChild(sprite);
                        }
                    }
                }, diagnostics);
            }

            if (cells.Any(cell => Water(cell.X, cell.Y)))
                TryLayer("Water material", () =>
                {
                    if (_scene.River is { } river)
                    {
                        using var bank = WaterMasks.River(size.X, size.Y, river, Water,
                            (x, y) => Water(x, y) || TileAt(x, y) == "plain" || Road(x, y) && !Tagged(x, y, "settlement"), true);
                        ground.AddChild(Masked("RiverBank", bank, MaterialRecord("road-material"), size));
                        using var channel = WaterMasks.River(size.X, size.Y, river, Water, Water, false);
                        ground.AddChild(Masked("RiverWater", channel, MaterialRecord("water-material"), size));
                    }
                    else
                    {
                        using var mask = WaterOccupancy(size, Water);
                        ground.AddChild(Masked("AreaWater", mask, MaterialRecord("water-material"), size));
                    }
                }, diagnostics);

            var connectors = new List<(string Atlas, BoardCell Cell, int Index)>();
            foreach (var cell in cells)
            {
                var binding = At(cell.X, cell.Y)!.Connected;
                if (binding is null || Water(cell.X, cell.Y) || _scene.RoadCorridor is not null && Road(cell.X, cell.Y))
                    continue;
                var mask = 0;
                for (var bit = 0; bit < Cardinal.Length; bit++)
                {
                    var neighbor = new BoardCell(cell.X + Cardinal[bit].X, cell.Y + Cardinal[bit].Y);
                    if (binding.Mask == "route" ? Route(neighbor.X, neighbor.Y)
                        : At(neighbor.X, neighbor.Y)?.Connected?.Atlas == binding.Atlas)
                        mask |= 1 << bit;
                }
                var index = _environment.ConnectedIndex(binding.Atlas, mask, (int)Math.Floor(TileArtVariation.Hash(cell.X, cell.Y, 1102) * 4));
                foreach (var id in new[] { binding.Atlas, binding.Edge }.Where(id => id is not null).Cast<string>())
                {
                    connectors.Add((id, cell, index));
                }
            }
            // Source emits route then edge for each row-major cell, filtering each
            // before compositing. Grouping by atlas changes overlapping seams.
            var routeImages = new Dictionary<string, Image>(StringComparer.Ordinal);
            Image RouteImage(string key, Texture2D texture)
            {
                if (routeImages.TryGetValue(key, out var cached))
                    return cached;
                var image = texture.GetImage();
                PrepareImage(image);
                routeImages.Add(key, image);
                return image;
            }
            try
            {
                foreach (var (atlasId, cell, index) in connectors)
                    TryLayer($"Connected terrain {atlasId}", () =>
                    {
                        Sprite2D sprite;
                        var position = new Vector2(cell.X, cell.Y) * 32;
                        if (_manifest.ConnectorMasks.TryGetValue(atlasId, out var spec))
                        {
                            var material = MaterialRecord("stone-paving");
                            var maskTexture = Texture(spec.ResourcePath, spec.Columns * 64, spec.Rows * 64);
                            var materialTexture = MaterialTexture(material);
                            var period = new Vector2(material.LogicalSize[0], material.LogicalSize[1]);
                            sprite = RouteComposition.Create(RouteImage(spec.ResourcePath, maskTexture),
                                new Rect2I(index % spec.Columns * 64, index / spec.Columns * 64, 64, 64),
                                RouteImage(material.ResourcePath, materialTexture), period, position,
                                ("route-cell", maskTexture, materialTexture, index, position.X % period.X, position.Y % period.Y), _shadows);
                        }
                        else
                        {
                            sprite = _environment.CreateGroundTile(atlasId, index);
                            sprite.Position += position;
                            var density = 1 / System.Math.Abs(sprite.Scale.X);
                            if (_shadows is not null)
                                sprite.AddChild(SpriteShadows.Create(sprite, ShadowKind.Route, density, _shadows));
                            else
                            {
                                using var input = SpriteShadows.ReadFrame(sprite.Texture);
                                var filtered = ShadowRaster.Filter(input, SpriteShadows.Drops(ShadowKind.Route), density);
                                using (filtered.Image)
                                {
                                    var effect = OwnedProceduralSprite.Create(filtered.Image);
                                    effect.Centered = sprite.Centered;
                                    effect.Offset = sprite.Offset - (sprite.Centered ? Vector2.Zero : Vector2.One * filtered.Padding);
                                    effect.TextureFilter = TextureFilterEnum.Linear;
                                    sprite.SelfModulate = new Color(1, 1, 1, 0);
                                    sprite.AddChild(effect);
                                }
                            }
                        }
                        sprite.SetMeta("route_atlas", atlasId);
                        sprite.SetMeta("route_cell_x", cell.X);
                        sprite.SetMeta("route_cell_y", cell.Y);
                        sprite.SetMeta("route_index", index);
                        ground.AddChild(sprite);
                    }, diagnostics);
            }
            finally { foreach (var image in routeImages.Values) image.Dispose(); }

            if (_scene.RoadCorridor is { } corridor)
                TryLayer("Road material", () =>
                {
                    using var mask = RoadCorridorMask.Create(size.X, size.Y, corridor, Road, Route, (x, y) => TileAt(x, y) == "plain");
                    ground.AddChild(Masked("RoadCorridor", mask, MaterialRecord("road-material"), size));
                }, diagnostics);

            var detail = new Node2D { Name = "TerrainDecals", ZIndex = -200 };
            staging.AddChild(detail);
            var decks = new Node2D { Name = "Crossings", ZIndex = -50 };
            staging.AddChild(decks);
            var crossingTopology = CrossingRenderer.Topology(size.X, size.Y, (x, y) => TileAt(x, y) == "bridge", Route);
            if (crossingTopology.Count != 0)
                TryLayer("Crossing resources", () => CrossingRenderer.Add(decks, _manifest.Crossings
                    ?? throw new MissingArtResourceException("crossings", "Registered crossing metadata is not exported."),
                    _environment, path => Texture(path), crossingTopology, diagnostics, _shadows), diagnostics);
            var edges = new Node2D { Name = "EdgeDecorations", ZIndex = -90 };
            staging.AddChild(edges);
            if (_manifest.EdgeDecorations is { } decoration)
                edgeCount = EdgeDecorationRenderer.Add(edges, decoration, _scene.LevelId, size, tiles,
                    path => Texture(path), diagnostics);
            else
                diagnostics.Add("Authored edge decoration dependency is not supplied.");
            var scenery = new Node2D { Name = "TerrainScenery" };
            staging.AddChild(scenery);
            foreach (var cell in cells)
            {
                var (x, y) = cell;
                var material = At(x, y)!;
                var tile = TileAt(x, y)!;
                if (_manifest.Foundations.TryGetValue(tile, out var foundation))
                    TryLayer($"Foundation ({x},{y}) {foundation}", () =>
                    {
                        var sprite = _environment.CreateSprite(foundation, 0.3f, shadows: _shadows);
                        sprite.Position = new Vector2((x + 0.5f) * 32, (y + 1) * 32);
                        detail.AddChild(sprite);
                    }, diagnostics);
                var decal = Water(x, y) ? _manifest.Scenery.RiverStones
                    : _scene.River is null && Shore(x, y) ? _manifest.Scenery.ShoreDecals : material.Decals;
                var decalChance = decal?.Chance;
                if (decal?.InteriorChance is { } interiorChance)
                {
                    if (!double.IsFinite(interiorChance) || interiorChance < 0 || interiorChance > 1
                        || decalChance is null)
                        throw new InvalidOperationException("Interior decal density requires a finite probability and an edge density.");
                    var interior = _grid.Adjacent(new Coord(x, y)).All(neighbor =>
                        TileAt(neighbor.X, neighbor.Y) == tile
                        && elevation[neighbor.Y * size.X + neighbor.X] == elevation[y * size.X + x]);
                    if (interior)
                        decalChance = interiorChance;
                }
                if (decal is not null && decalChance is { } chance && TileArtVariation.Hash(x, y, 1103) < chance)
                {
                    var id = Pick(decal, x, y, 1104);
                    TryLayer($"Terrain decal ({x},{y}) {id}", () =>
                    {
                        var sprite = _environment.CreateSprite(id, (float)decal.Scale,
                            TileArtVariation.Hash(x, y, 1105) > 0.5, 0.72f, _shadows);
                        sprite.Position = new Vector2((x + 0.5f) * 32, (y + 1) * 32);
                        detail.AddChild(sprite);
                    }, diagnostics);
                }
                if (tile == "bridge" || Authored(tile, x, y, surface: false))
                    continue;
                var enclosed = material.Blend is not null && Cardinal.All(delta => At(x + delta.X, y + delta.Y)?.Blend == material.Blend);
                var spec = enclosed ? _manifest.Scenery.ForestFloor
                    : material.Scenery ?? (Cardinal.Any(delta => Tagged(x + delta.X, y + delta.Y, "settlement"))
                        ? _manifest.Scenery.SettlementLife : null);
                if (enclosed && spec is null)
                    diagnostics.Add("Forest understory specification is not exported.");
                if (spec is null || spec.Chance is { } density && TileArtVariation.Hash(x, y, 1119) > density)
                    continue;
                var pick = Pick(spec, x, y, 1120);
                TryLayer($"Terrain scenery ({x},{y}) {pick}", () =>
                {
                    var sprite = _environment.CreateSprite(pick,
                        (float)(spec.Scale + TileArtVariation.Hash(x, y, 1121) * 0.12),
                        TileArtVariation.Hash(x, y, 1123) > 0.52, enclosed ? 0.9f : 1, _shadows,
                        enclosed ? ShadowKind.Environment : ShadowKind.Standing);
                    sprite.Position = new Vector2((float)((x + 0.5) * 32
                        + (TileArtVariation.Hash(x, y, 1122) - 0.5) * spec.Jitter), (y + 1.18f) * 32);
                    sprite.Name = $"Scenery_{x}_{y}";
                    scenery.AddChild(sprite);
                    standing.Add(sprite);
                }, diagnostics);
            }
        }
        catch { staging.Free(); throw; }
        var previousShape = _groundShape;
        var previousSize = _groundSize;
        var logical = new Node2D { Name = "ComposedTerrain" };
        var entries = new List<TerrainDepthEntry>();
        ProjectedGround? projected = null;
        var authoredParent = _authoredGround?.GetParent();
        try
        {
            _groundShape = ground.GetNodeOrNull<Sprite2D>("WorldField")?.Texture;
            _groundSize = new Vector2(size.X * 32, size.Y * 32);
            ApplyGroundClip(ground);
            ApplyGroundClip(staging.GetNode<Node2D>("TerrainDecals"));
            ApplyGroundClip(staging.GetNode<Node2D>("EdgeDecorations"));
            var lighting = BoardLighting.CreateField(size, tiles, _manifest, _groundShape);
            lighting.Hearths.ZIndex = -30;
            lighting.Sunlight.ZIndex = -20;
            staging.AddChild(lighting.Hearths);
            staging.AddChild(lighting.Sunlight);
            if (_shadows is not null)
            {
                var crossings = staging.GetNode<Node2D>("Crossings");
                foreach (var deck in crossings.GetChildren().OfType<Sprite2D>())
                {
                    var density = deck.HasMeta("environment_cell")
                        ? _environment.ResolveCell(deck.GetMeta("environment_cell").AsString()).PixelDensity
                        : 1 / Math.Abs(deck.Scale.X);
                    deck.AddChild(SpriteShadows.Create(deck, ShadowKind.Standing, density, _shadows));
                }
            }
            foreach (var child in staging.GetChildren().OfType<Node2D>().ToArray())
            {
                if (child.Name == "TerrainScenery")
                    continue;
                staging.RemoveChild(child);
                logical.AddChild(child);
            }
            CliffSurfaceMaterial? cliff = null;
            if (projection.Faces.Count != 0)
            {
                var material = MaterialRecord("cliff-front");
                var texture = MaterialTexture(material);
                var period = new Vector2(material.LogicalSize[0], material.LogicalSize[1]);
                if (!Mathf.IsEqualApprox(texture.GetSize().X / texture.GetSize().Y, period.X / period.Y))
                    throw new InvalidOperationException("Cliff material repeat must preserve source aspect ratio.");
                var fieldAtlas = MaterialRecord("field-material").Replaces.Atlas;
                var fieldTops = cells.Where(cell => Authored(TileAt(cell.X, cell.Y)!, cell.X, cell.Y, surface: true)
                    || (At(cell.X, cell.Y)!.Surface ?? fieldAtlas) == fieldAtlas)
                    .Select(cell => new Coord(cell.X, cell.Y)).ToHashSet();
                cliff = new CliffSurfaceMaterial(texture, period, fieldTops);
            }
            projected = ProjectedGround.Create(new(projection, cliff), logical, size);
            staging.AddChild(projected);
            if (_authoredGround is not null)
            {
                authoredParent!.RemoveChild(_authoredGround);
                logical.AddChild(_authoredGround);
                ApplyGroundClip(_authoredGround);
            }
            foreach (var node in standing)
            {
                var contact = node.Position;
                node.Position = projection.Project(contact);
                node.ZAsRelative = false;
                node.ZIndex = projection.LayerAtDepth(projection.Depth(contact) + SpriteGroundContact.Offset(node).Y, BoardDepthLayer.Standing);
                entries.Add(new(node, contact, 2, node));
            }
        }
        catch
        {
            if (_authoredGround is not null && _authoredGround.GetParent() != authoredParent)
            {
                _authoredGround.GetParent()?.RemoveChild(_authoredGround);
                authoredParent!.AddChild(_authoredGround);
            }
            _groundShape = previousShape;
            _groundSize = previousSize;
            if (_authoredGround is not null)
                ApplyGroundClip(_authoredGround);
            staging.Free();
            if (GodotObject.IsInstanceValid(projected) && projected!.GetParent() is null)
                projected.Free();
            if (GodotObject.IsInstanceValid(logical) && logical.GetParent() is null)
                logical.Free();
            throw;
        }
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        AddChild(staging);
        _depthEntries.Clear();
        _depthEntries.AddRange(entries);
        _projection = projection;
        RenderedEdgeDecorationCount = edgeCount;
        _standing.Clear();
        _standing.AddRange(standing);
        _diagnostics.Clear();
        _diagnostics.AddRange(diagnostics.Distinct(StringComparer.Ordinal));
        _tiles = tiles.ToArray();
        _elevation = elevation.ToArray();
        _renderedSize = size;
    }

    /// <summary>Apply the same terrain-owned silhouette to externally authored ground sprites.</summary>
    public void ApplyGroundClip(Node2D ground)
    {
        if (_groundShape is not null)
            GroundClipper.Apply(ground, _groundShape, _groundSize);
        else
            GroundClipper.Remove(ground);
    }

    private Node2D CreateField(Vector2I size, Func<int, int, bool> outside)
    {
        var material = MaterialRecord("field-material");
        if (Enumerable.Range(0, size.X * size.Y).Any(index => outside(index % size.X, index / size.X)))
        {
            using var mask = WaterMasks.PlayableShape(size.X, size.Y, outside);
            return Masked("WorldField", mask, material, size);
        }
        var texture = MaterialTexture(material);
        var polygon = _scene.GroundOutline.Length > 0
            ? _scene.GroundOutline.Select(pair => RasterResources.Pair(pair, "ground outline") * 32).ToArray()
            : new[] { Vector2.Zero, new Vector2(size.X * 32, 0), new Vector2(size.X * 32, size.Y * 32), new Vector2(0, size.Y * 32) };
        if (polygon.Length < 3)
            throw new InvalidOperationException("Authored ground outline has fewer than three points.");
        return new Polygon2D
        {
            Name = "WorldField",
            Polygon = polygon,
            Texture = texture,
            UV = polygon.Select(point => point * texture.GetSize() / new Vector2(material.LogicalSize[0], material.LogicalSize[1])).ToArray(),
            TextureRepeat = TextureRepeatEnum.Enabled,
            TextureFilter = TextureFilterEnum.Linear,
            Antialiased = true
        };
    }

    private Image WaterOccupancy(Vector2I size, Func<int, int, bool> occupied)
    {
        var spec = MaterialRecord("water-material").Occupancy ?? throw new InvalidOperationException("Water occupancy metadata is absent.");
        if (spec.TileSize != 32 || spec.Columns != 16 || spec.Rows != 16
            || spec.Geometry != "full-shared-interiors-exposed-shore-inset-concave-notch-v1"
            || string.Join(',', spec.BitOrder) != "N,NE,E,SE,S,SW,W,NW")
            throw new InvalidOperationException("Water requires its full original eight-neighbor occupancy atlas.");
        using var source = Texture(spec.ResourcePath, 1024, 1024).GetImage();
        PrepareImage(source);
        var mask = Image.CreateEmpty(size.X * 64, size.Y * 64, false, Image.Format.Rgba8);
        for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
            {
                if (!occupied(x, y))
                    continue;
                // Water uses all 256 raw masks, not canonical Blob47 diagonal suppression.
                var index = NeighborMask(x, y, occupied);
                mask.BlitRect(source, new Rect2I(index % 16 * 64, index / 16 * 64, 64, 64), new Vector2I(x * 64, y * 64));
            }
        return mask;
    }
    private Image AtlasMask(Vector2I size, ForestMaskRecord spec, Func<int, int, bool> occupied, Func<int, int, int> indexAt)
    {
        if (spec.CellWidth != 32 || spec.CellHeight != 32 || spec.CellWidth2x != 64 || spec.CellHeight2x != 64)
            throw new InvalidOperationException($"Native terrain requires declared 32-unit mask cells: {spec.Id}");
        using var source = Texture(spec.ResourcePath, spec.Columns * 64, spec.Rows * 64).GetImage();
        PrepareImage(source);
        var mask = Image.CreateEmpty(size.X * 64, size.Y * 64, false, Image.Format.Rgba8);
        for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
            {
                if (!occupied(x, y))
                    continue;
                var index = indexAt(x, y);
                if (index < 0 || index >= spec.Columns * spec.Rows)
                    throw new InvalidOperationException($"Mask index outside {spec.Id}: {index}");
                // Source runtimeTileMarkup expands mask cells by half a logical pixel.
                using var cell = source.GetRegion(new Rect2I(index % spec.Columns * 64, index / spec.Columns * 64, 64, 64));
                cell.Resize(66, 66, Image.Interpolation.Bilinear);
                mask.BlendRect(cell, new Rect2I(0, 0, 66, 66), new Vector2I(x * 64 - 1, y * 64 - 1));
            }
        return mask;
    }
    private static Image SolidMask(Vector2I size, IEnumerable<BoardCell> cells)
    {
        var mask = Image.CreateEmpty(size.X * 64, size.Y * 64, false, Image.Format.Rgba8);
        foreach (var cell in cells)
            mask.FillRect(new Rect2I(cell.X * 64 - 1, cell.Y * 64 - 1, 66, 66), Colors.White);
        return mask;
    }
    private static void PrepareImage(Image image)
    {
        if (image.IsCompressed() && image.Decompress() != Error.Ok)
            throw new InvalidOperationException("Cannot decompress terrain mask.");
        image.Convert(Image.Format.Rgba8);
    }
    private Sprite2D Masked(string name, Image mask, WorldMaterialRecord material, Vector2I size)
    {
        var materialTexture = MaterialTexture(material);
        return OwnedProceduralSprite.Create(mask, sprite =>
        {
            sprite.Name = name;
            sprite.Centered = false;
            sprite.Scale = Vector2.One / 2;
            sprite.TextureFilter = TextureFilterEnum.Linear;
            var shader = OwnedShaderMaterial.Attach(sprite, MaskedMaterialShader).Value;
            shader.SetShaderParameter("fill_texture", materialTexture);
            shader.SetShaderParameter("world_size", new Vector2(size.X * 32, size.Y * 32));
            shader.SetShaderParameter("material_period", new Vector2(material.LogicalSize[0], material.LogicalSize[1]));
            sprite.SetMeta("world_material", true);
        });
    }
    private static bool Matches(TerrainMaterialBinding? binding, MaterialReplacement replacement) =>
        binding?.Connected is { } connected && connected.Atlas == replacement.Atlas && connected.Mask == replacement.Mask;
    private WorldMaterialRecord MaterialRecord(string key) => _manifest.Materials.TryGetValue(key, out var value)
        ? value : throw new InvalidOperationException($"World material metadata is absent: {key}");
    private Texture2D MaterialTexture(WorldMaterialRecord material)
    {
        if (material.SchemaVersion != 2 || material.LogicalSize.Length != 2 || material.RasterSize.Length != 2
            || material.LogicalSize.Any(value => value <= 0) || material.RasterSize.Any(value => value <= 0)
            || (long)material.RasterSize[0] * material.LogicalSize[1] != (long)material.RasterSize[1] * material.LogicalSize[0]
            || material.WorldOrigin.Length != 2 || material.WorldOrigin.Any(value => value != 0)
            || material.Fit.Inset != 0 || material.Fit.Bleed != 0)
            throw new InvalidOperationException($"Invalid world material geometry: {material.Id}");
        return Texture(material.ResourcePath, material.RasterSize[0] * 2, material.RasterSize[1] * 2);
    }
    private Texture2D Texture(string path, int width = 0, int height = 0)
    {
        if (_textures.TryGetValue(path, out var cached))
            return cached;
        if (!path.StartsWith("res://assets/", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException($"World material resource escaped assets: {path}");
        if (!ResourceLoader.Exists(path))
            throw new MissingArtResourceException(path, $"Missing world material resource: {path}");
        var texture = ResourceLoader.Load<Texture2D>(path);
        if (texture is null || width > 0 && texture.GetWidth() != width || height > 0 && texture.GetHeight() != height)
            throw new InvalidOperationException($"World material physical dimensions differ: {path}, expected {width}×{height}.");
        _textures.Add(path, texture);
        return texture;
    }
    private bool Authored(string tile, int x, int y, bool surface) => _scene.Zones.Any(zone =>
        zone.Bounds.Length == 4 && x >= zone.Bounds[0] && y >= zone.Bounds[1] && x <= zone.Bounds[2] && y <= zone.Bounds[3]
        && (surface ? zone.AuthoredTerrainSurface : zone.AuthoredTerrainScenery).Contains(tile));
    private static string Pick(ScenerySpecification spec, int x, int y, int salt)
    {
        if (spec.Ids.Length == 0)
            throw new InvalidOperationException("Empty scenery selection.");
        return spec.Ids[(int)Math.Floor(TileArtVariation.Hash(x, y, salt) * spec.Ids.Length)];
    }
    private static int NeighborMask(int x, int y, Func<int, int, bool> joins) => Around.Select((delta, bit) =>
        joins(x + delta.X, y + delta.Y) ? 1 << bit : 0).Aggregate(0, (a, b) => a | b);
    internal static int CanonicalBlobMask(int mask)
    {
        mask &= 255;
        if ((mask & 0x05) != 0x05)
            mask &= ~0x02;
        if ((mask & 0x14) != 0x14)
            mask &= ~0x08;
        if ((mask & 0x50) != 0x50)
            mask &= ~0x20;
        if ((mask & 0x41) != 0x41)
            mask &= ~0x80;
        return mask;
    }
    private static void TryLayer(string description, Action render, List<string> diagnostics)
    {
        try
        {
            render();
        }
        catch (MissingArtResourceException error) { diagnostics.Add($"{description}: {error.Message}"); }
    }
}
