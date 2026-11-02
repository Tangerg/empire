#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.State;
using Godot;

namespace Empire.Presentation;

/// <summary>Independent sprites and clean ground feed the existing board depth, projection and picking pipeline.</summary>
public partial class AssembledGroundView : BoardGroundView
{
    private AssembledGroundDocument _document = null!;
    private JsonObject _registeredMap = null!;
    private Texture2D _ground = null!;
    private Node2D? _authored;
    private PaintedSurface? _surface;
    private BoardProjection? _projection;
    private readonly List<(Sprite2D Sprite, Vector2 Contact, bool Pointer, Coord? Selection, Vector2[]? PointerPolygon)> _objects = new();
    private readonly List<(Node2D Node, Vector2 Contact, StandingVisual Visual, Coord? Selection)> _surfaces = new();
    private readonly List<TerrainDepthEntry> _depthEntries = new();
    private readonly List<CompiledSceneSurface.Instance> _surfaceInstances = new();
    private readonly List<(Node2D Node, Sprite2D Source, Vector2 Contact)> _contacts = new();
    private IReadOnlyList<GroundSite> _sites = Array.Empty<GroundSite>();
    public override IReadOnlyList<string> Diagnostics => Array.Empty<string>();
    public override IReadOnlyList<TerrainDepthEntry> DepthEntries => _depthEntries;
    public override IReadOnlyList<GroundSite> Sites => _sites;

    public static AssembledGroundView Create(AssembledGroundDocument document, Texture2D ground,
        SceneObjectCatalog catalog, IReadOnlyDictionary<string, Texture2D> textures, JsonObject map, ReadOnlyContent content, ShadowCache? shadows = null)
    {
        var width = map["width"]!.GetValue<int>();
        var height = map["height"]!.GetValue<int>();
        var tiles = map["tiles"]!.AsArray();
        var elevation = map["elevation"]!.AsArray().Select(value => value!.GetValue<int>()).ToArray();
        if (width <= 0 || height <= 0 || tiles.Count != checked(width * height) || elevation.Length != tiles.Count)
            throw new InvalidOperationException("Assembled ground requires canonical map geometry.");
        var plane = document.CreatePlane(new Vector2(width, height) * BoardProjection.TileSize, elevation.Min(), elevation.Max());
        if (ground.GetSize() != new Vector2(document.PixelSize[0], document.PixelSize[1]))
            throw new InvalidOperationException("Clean ground texture differs from its registered dimensions.");
        if (catalog.Schema != 1 || catalog.Objects is null || document.Objects is null)
            throw new InvalidOperationException("Independent scene objects require catalog schema 1 and explicit instances.");
        var geometry = new Dictionary<string, (Texture2D Texture, Vector2 Anchor, Vector2 Front, Vector2 ShadowRadii, Vector2[]? PointerPolygon, (Rect2 Region, Vector2 Front)[]? Strips, CompiledSceneSurface? Surfaces)>(StringComparer.Ordinal);
        using var materials = new CompiledSceneSurface.Materials();
        foreach (var (id, asset) in catalog.Objects)
        {
            if (string.IsNullOrWhiteSpace(id) || asset is null || string.IsNullOrWhiteSpace(asset.ResourcePath)
                || asset.PixelSize is not { Length: 2 } || asset.PixelSize[0] <= 0 || asset.PixelSize[1] <= 0
                || !textures.TryGetValue(id, out var texture) || texture is AtlasTexture
                || texture.GetSize() != new Vector2(asset.PixelSize[0], asset.PixelSize[1]) || ReferenceEquals(texture, ground))
                throw new InvalidOperationException($"Independent scene asset must be a complete, correctly sized texture: {id}.");
            var material = materials.Get(texture);
            if (asset.SurfaceGeometryPath is null && material.IsOpaque)
                throw new InvalidOperationException($"Independent scene object must have transparency: {id}.");
            var radii = asset.ContactShadowRadiiPixels is { } shadow
                ? RasterResources.Pair(shadow, id + " contact shadow radii") : Vector2.Zero;
            if (asset.SurfaceGeometryPath is null && asset.ContactShadowRadiiPixels is null)
                throw new InvalidOperationException($"Sprite asset requires contact shadow dimensions: {id}.");
            if (asset.ContactShadowRadiiPixels is not null && (radii.X <= 0 || radii.Y <= 0 || radii.X > asset.PixelSize[0] / 2f || radii.Y > asset.PixelSize[1] / 2f))
                throw new InvalidOperationException($"Contact shadow must fit the object's source dimensions: {id}.");
            var anchor = SpriteGroundContact.Read(asset.AnchorPixels, asset.PixelSize[0], asset.PixelSize[1], id + " anchor");
            Vector2[]? pointerPolygon = null;
            if (asset.PointerPolygonPixels is { } pixels)
            {
                if (pixels.Length < 3)
                    throw new InvalidOperationException($"Independent pointer contour needs at least three vertices: {id}.");
                var points = pixels.Select(point => SpriteGroundContact.Read(point, asset.PixelSize[0], asset.PixelSize[1], id + " pointer")).ToArray();
                StandingVisual.ValidatePolygon(points, id + " pointer");
                pointerPolygon = points.Select(point => point - anchor).ToArray();
            }
            if (asset.SurfaceGeometryPath is not null && (asset.DepthStrips is not null || asset.GroundFrontPixels is not null))
                throw new InvalidOperationException($"Authored surfaces own contact and cannot also declare sprite front or strips: {id}.");
            var surfaces = asset.SurfaceGeometryPath is { } surfacePath
                ? CompiledSceneSurface.Compile(PresentationJson.Load<SceneSurfaceDocument>(surfacePath), material, id)
                : null;
            (Rect2 Region, Vector2 Front)[]? strips = null;
            if (asset.DepthStrips is { } sections)
            {
                if (sections.Length < 2 || sections.Length > asset.PixelSize[0])
                    throw new InvalidOperationException($"Independent depth strips require a nonempty, bounded partition: {id}.");
                var left = 0;
                strips = new (Rect2 Region, Vector2 Front)[sections.Length];
                for (var index = 0; index < sections.Length; index++)
                {
                    var section = sections[index] ?? throw new InvalidOperationException($"Missing independent depth strip: {id}.");
                    if (section.RightPixels <= left || section.RightPixels > asset.PixelSize[0])
                        throw new InvalidOperationException($"Independent depth strips must cover the canvas exactly without gaps or overlaps: {id}.");
                    var front = SpriteGroundContact.Read(section.GroundFrontPixels, asset.PixelSize[0], asset.PixelSize[1], id + " strip front");
                    if (front.X < left || front.X > section.RightPixels)
                        throw new InvalidOperationException($"Independent strip contact must lie within its own physical span: {id}.");
                    strips[index] = (new Rect2(left, 0, section.RightPixels - left, asset.PixelSize[1]), front);
                    left = section.RightPixels;
                }
                if (left != asset.PixelSize[0])
                    throw new InvalidOperationException($"Independent depth strips cannot discard the rest of a whole object: {id}.");
            }
            geometry.Add(id, (texture, anchor,
                surfaces is null ? SpriteGroundContact.Read(asset.GroundFrontPixels, asset.PixelSize[0], asset.PixelSize[1], id + " front") : Vector2.Zero, radii, pointerPolygon, strips, surfaces));
        }
        var view = new AssembledGroundView
        {
            Name = "AssembledGround",
            _document = document,
            _registeredMap = map.DeepClone().AsObject(),
            _ground = ground
        };
        try
        {
            view.AddChild(new Sprite2D
            {
                Name = "CleanGround",
                Texture = ground,
                Centered = false,
                Scale = Vector2.One * document.WorldScale,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                ZIndex = -4096
            });
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var cells = new HashSet<Coord>();
            var sites = new List<GroundSite>();
            var samplers = new Dictionary<string, Sprite2D>(StringComparer.Ordinal);
            foreach (var instance in document.Objects)
            {
                if (instance is null || string.IsNullOrWhiteSpace(instance.Id) || !identities.Add(instance.Id)
                    || !geometry.TryGetValue(instance.AssetId, out var asset)
                    || !float.IsFinite(instance.Scale) || instance.Scale <= 0)
                    throw new InvalidOperationException("Scene object instances require unique identities, registered assets and positive scales.");
                var contact = RasterResources.Pair(instance.Contact, instance.Id + " contact");
                var registeredContact = plane.Ground(contact * BoardProjection.TileSize) + plane.Origin;
                if (!new Rect2(Vector2.Zero, plane.CanvasSize).HasPoint(registeredContact))
                    throw new InvalidOperationException($"Scene object contact lies outside the registered canvas: {instance.Id}.");
                Coord? selection = null;
                if (instance.SelectionCell is { } cell)
                {
                    if (cell.Length != 2 || cell[0] < 0 || cell[1] < 0 || cell[0] >= width || cell[1] >= height)
                        throw new InvalidOperationException($"Scene object selection cell is invalid: {instance.Id}.");
                    selection = new Coord(cell[0], cell[1]);
                    var terrain = content.Definition("terrains", tiles[cell[1] * width + cell[0]]!.GetValue<string>());
                    if (!cells.Add(selection.Value) || terrain is null || !terrain.Value.GetProperty("capturable").GetBoolean()
                        || terrain.Value.GetProperty("tags").EnumerateArray().Any(tag => tag.GetString() == "outside"))
                        throw new InvalidOperationException($"Scene site must reference a unique canonical capturable cell: {instance.Id}.");
                    sites.Add(new(instance.Id, selection.Value, instance.OwnsMapStructureBody));
                }
                if (instance.OwnsMapStructureBody && selection is null
                    || instance.Pointer == "target" && selection is null
                    || selection is not null && instance.Pointer != "target"
                    || instance.Pointer is not ("target" or "block" or "pass-through"))
                    throw new InvalidOperationException($"Scene object pointer and site ownership are inconsistent: {instance.Id}.");
                if (asset.ShadowRadii != Vector2.Zero)
                    view.AddGroundContact(instance.Id, asset.ShadowRadii, instance.Scale,
                        contact * BoardProjection.TileSize, shadows);
                if (asset.Surfaces is { } surfaces)
                {
                    if (!samplers.TryGetValue(instance.AssetId, out var sampler))
                    {
                        sampler = new Sprite2D { Texture = asset.Texture, Centered = false, Visible = false, TextureRepeat = CanvasItem.TextureRepeatEnum.Disabled };
                        view.AddChild(sampler);
                        samplers.Add(instance.AssetId, sampler);
                    }
                    var surfaceInstance = surfaces.Instantiate(view, instance.Id, instance.Scale, asset.Anchor,
                        instance.Pointer != "pass-through", asset.PointerPolygon, sampler);
                    view._surfaceInstances.Add(surfaceInstance);
                    foreach (var (node, visual) in surfaceInstance.Pieces)
                    {
                        view._surfaces.Add((node, contact * BoardProjection.TileSize, visual, selection));
                    }
                    continue;
                }
                var sections = asset.Strips ?? new[] { (Region: new Rect2(Vector2.Zero, asset.Texture.GetSize()), Front: asset.Front) };
                for (var index = 0; index < sections.Length; index++)
                {
                    var section = sections[index];
                    var sprite = new Sprite2D
                    {
                        Name = index == 0 ? instance.Id : $"{instance.Id}__depth_{index}",
                        Texture = asset.Texture,
                        Centered = false,
                        Offset = section.Region.Position - asset.Anchor,
                        RegionEnabled = asset.Strips is not null,
                        RegionRect = section.Region,
                        RegionFilterClipEnabled = true,
                        Scale = Vector2.One * instance.Scale,
                        TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps
                    };
                    SpriteGroundContact.Attach(sprite, section.Front - section.Region.Position);
                    view.AddChild(sprite);
                    view._objects.Add((sprite, contact * BoardProjection.TileSize, instance.Pointer != "pass-through", selection, asset.PointerPolygon));
                }
            }
            view._sites = sites.AsReadOnly();
            return view;
        }
        catch { view.Free(); throw; }
    }

    // Keep the source alive: it borrows a cached texture or owns the uncached procedural fallback.
    // A floor projection borrows that texture, never the first standing strip's depth or Pick owner.
    private void AddGroundContact(string identity, Vector2 radii, float scale, Vector2 contact, ShadowCache? shadows)
    {
        var source = BoardLighting.CreateGroundContact(radii, shadows);
        source.Position *= scale;
        source.Scale *= scale;
        source.Visible = false;
        var node = new Node2D { Name = identity + "__ground_contact" };
        AddChild(node);
        node.AddChild(source);
        _contacts.Add((node, source, contact));
    }

    public override void ConfigureAuthoredGround(Node2D authoredGround)
    {
        if (_authored is not null || authoredGround.GetParent() is not null || authoredGround.GetChildCount() != 0)
            throw new InvalidOperationException("Assembled ground cannot absorb competing authored decorations.");
        AddChild(authoredGround);
        _authored = authoredGround;
    }
    public override Node2D DetachAuthoredGround()
    {
        var authored = _authored ?? throw new InvalidOperationException("No authored ground is attached.");
        RemoveChild(authored);
        _authored = null;
        return authored;
    }
    public override void ApplyDrawOrder()
    {
        foreach (var instance in _surfaceInstances)
            instance.ApplyDrawOrder();
    }

    public override void Render(BoardProjection projection, string[] tiles, int[] elevation, Vector2I size)
    {
        if (_registeredMap["width"]!.GetValue<int>() != size.X || _registeredMap["height"]!.GetValue<int>() != size.Y
            || !_registeredMap["tiles"]!.AsArray().Select(EcmaJson.ReadString).SequenceEqual(tiles)
            || !_registeredMap["elevation"]!.AsArray().Select(value => value!.GetValue<int>()).SequenceEqual(elevation))
            throw new InvalidOperationException("Canonical terrain changed without a matching assembled surface.");
        var plane = _document.CreatePlane(new Vector2(size.X, size.Y) * BoardProjection.TileSize, elevation.Min(), elevation.Max());
        if (!projection.MatchesPlane(plane) || !projection.MatchesTerrainGeometry(_registeredMap))
            throw new InvalidOperationException("Assembled surface and tactical projection use different registration.");
        if (_projection != projection)
        {
            var replacement = PaintedSurface.Create(projection, _ground, _document.WorldScale);
            replacement.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
            if (_surface is not null)
            {
                RemoveChild(_surface);
                _surface.Free();
            }
            AddChild(replacement);
            _surface = replacement;
            foreach (var (node, source, contact) in _contacts)
            {
                // Preserve source size/opacity and use the existing registered ground-top depth partitions.
                // Rebuild only with a replacement projection; PaintedSurface observes partition changes itself.
                var position = projection.Project(contact);
                var floor = PaintedSurface.Create(projection, source, position);
                if (node.GetNodeOrNull<PaintedSurface>("RegisteredSurface") is { } old)
                {
                    node.RemoveChild(old);
                    old.Free();
                }
                node.Position = position;
                node.AddChild(floor);
                // Equal surface depth must draw after the base floor, but before standing materials.
                MoveChild(node, GetChildCount() - 1);
            }
            _projection = projection;
        }
        _depthEntries.Clear();
        foreach (var (node, contact, visual, selection) in _surfaces)
        {
            node.Position = projection.Project(contact);
            _depthEntries.Add(new(node, contact, 2, visual, selection));
        }
        foreach (var (sprite, contact, pointer, selection, pointerPolygon) in _objects)
        {
            sprite.Position = projection.Project(contact);
            _depthEntries.Add(new(sprite, contact, 2, new SpriteStandingVisual(sprite, pointer, pointerPolygon), selection));
        }
    }
}
