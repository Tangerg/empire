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
    private readonly List<(Sprite2D Sprite, Vector2 Contact, bool Pointer, Coord? Selection)> _objects = new();
    private readonly List<TerrainDepthEntry> _depthEntries = new();
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
        var geometry = new Dictionary<string, (Texture2D Texture, Vector2 Anchor, Vector2 Front, Vector2 ShadowRadii)>(StringComparer.Ordinal);
        foreach (var (id, asset) in catalog.Objects)
        {
            if (string.IsNullOrWhiteSpace(id) || asset is null || string.IsNullOrWhiteSpace(asset.ResourcePath)
                || asset.PixelSize is not { Length: 2 } || asset.PixelSize[0] <= 0 || asset.PixelSize[1] <= 0
                || !textures.TryGetValue(id, out var texture) || texture is AtlasTexture
                || texture.GetSize() != new Vector2(asset.PixelSize[0], asset.PixelSize[1]) || ReferenceEquals(texture, ground))
                throw new InvalidOperationException($"Independent scene asset must be a complete, correctly sized texture: {id}.");
            using var image = texture.GetImage();
            if (image is null || image.DetectAlpha() == Image.AlphaMode.None)
                throw new InvalidOperationException($"Independent scene object must have transparency: {id}.");
            var radii = RasterResources.Pair(asset.ContactShadowRadiiPixels, id + " contact shadow radii");
            if (radii.X <= 0 || radii.Y <= 0 || radii.X > asset.PixelSize[0] / 2f || radii.Y > asset.PixelSize[1] / 2f)
                throw new InvalidOperationException($"Contact shadow must fit the object's source dimensions: {id}.");
            geometry.Add(id, (texture,
                SpriteGroundContact.Read(asset.AnchorPixels, asset.PixelSize[0], asset.PixelSize[1], id + " anchor"),
                SpriteGroundContact.Read(asset.GroundFrontPixels, asset.PixelSize[0], asset.PixelSize[1], id + " front"), radii));
        }
        var view = new AssembledGroundView { Name = "AssembledGround", _document = document,
            _registeredMap = map.DeepClone().AsObject(), _ground = ground };
        try
        {
            view.AddChild(new Sprite2D { Name = "CleanGround", Texture = ground, Centered = false,
                Scale = Vector2.One * document.WorldScale, TextureFilter = CanvasItem.TextureFilterEnum.Linear, ZIndex = -4096 });
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var cells = new HashSet<Coord>();
            var sites = new List<GroundSite>();
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
                var sprite = new Sprite2D { Name = instance.Id, Texture = asset.Texture, Centered = false,
                    Offset = -asset.Anchor, Scale = Vector2.One * instance.Scale,
                    TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
                SpriteGroundContact.Attach(sprite, asset.Front);
                sprite.AddChild(BoardLighting.CreateGroundContact(asset.ShadowRadii, shadows));
                view.AddChild(sprite);
                view._objects.Add((sprite, contact * BoardProjection.TileSize, instance.Pointer != "pass-through", selection));
            }
            view._sites = sites.AsReadOnly();
            return view;
        }
        catch { view.Free(); throw; }
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
            if (_surface is not null) { RemoveChild(_surface); _surface.Free(); }
            AddChild(replacement);
            _surface = replacement;
            _projection = projection;
        }
        _depthEntries.Clear();
        foreach (var (sprite, contact, pointer, selection) in _objects)
        {
            sprite.Position = projection.Project(contact);
            _depthEntries.Add(new(sprite, contact, 2, new SpriteStandingVisual(sprite, pointer), selection));
        }
    }
}
