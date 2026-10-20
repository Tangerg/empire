#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

/// <summary>
/// Native material and authored-terrain composition. RGB textures remain the approved
/// resources; only topology alpha is generated. This reads no battle legality or RNG.
/// </summary>
public partial class TerrainView : Node2D
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
        void fragment() {
            vec4 fill = texture(fill_texture, UV * world_size / material_period);
            COLOR = vec4(fill.rgb, fill.a * texture(TEXTURE, UV).a);
        }
        """;
    private WorldMaterialsManifest _manifest = null!;
    private AuthoredScene _scene = null!;
    private EnvironmentCatalog _environment = null!;
    private string[]? _tiles;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly List<string> _diagnostics = new();
    private readonly List<Node2D> _standing = new();
    public IReadOnlyList<string> Diagnostics => _diagnostics;
    public IReadOnlyList<Node2D> Standing => _standing;
    public int RenderedSceneryCount => _standing.Count;

    public void Configure(WorldMaterialsManifest manifest, AuthoredScene scene, EnvironmentCatalog environment)
    {
        if (manifest.Schema != 1) throw new InvalidOperationException("Unsupported world materials schema.");
        _manifest = manifest;
        _scene = scene;
        _environment = environment;
        _tiles = null;
    }

    public void Render(string[] tiles, Vector2I size)
    {
        if (_tiles is not null && _tiles.SequenceEqual(tiles)) return;
        if (tiles.Length != size.X * size.Y) throw new InvalidOperationException("Terrain size differs from map.");
        foreach (var tile in tiles.Distinct())
            if (!_manifest.TerrainBindings.ContainsKey(tile))
                throw new InvalidOperationException($"No exported presentation material for terrain: {tile}");
        var staging = new Node2D { Name = "ComposedTerrain" };
        var diagnostics = new List<string>();
        var standing = new List<Node2D>();
        var ground = new Node2D { Name = "Ground", ZIndex = -300 };
        staging.AddChild(ground);
        string? TileAt(int x, int y) => x < 0 || y < 0 || x >= size.X || y >= size.Y ? null : tiles[y * size.X + x];
        TerrainMaterialBinding? At(int x, int y) => TileAt(x, y) is { } tile ? _manifest.TerrainBindings[tile] : null;
        bool Tagged(int x, int y, string family)
        {
            if (TileAt(x, y) is not { } tile) return false;
            if (!_manifest.TerrainTags.TryGetValue(tile, out var tags)
                || !_manifest.GroundTagFamilies.TryGetValue(family, out var familyTags))
                throw new InvalidOperationException($"Terrain semantic export missing: {tile}/{family}");
            return tags.Intersect(familyTags).Any();
        }
        try
        {
            TryLayer("Field material", () => ground.AddChild(CreateField()), diagnostics);
            TryLayer("Forest material", () => {
                var spec = _manifest.ForestMask;
                var source = Texture(spec.ResourcePath, spec.Columns * spec.CellWidth2x, spec.Rows * spec.CellHeight2x).GetImage();
                if (source.IsCompressed()) source.Decompress();
                source.Convert(Image.Format.Rgba8);
                using var mask = Image.CreateEmpty(size.X * 64, size.Y * 64, false, Image.Format.Rgba8);
                for (var y = 0; y < size.Y; y++)
                for (var x = 0; x < size.X; x++)
                {
                    if (At(x, y)?.Blend != spec.Id) continue;
                    var bits = 0;
                    for (var bit = 0; bit < Around.Length; bit++)
                        if (At(x + Around[bit].X, y + Around[bit].Y)?.Blend == spec.Id) bits |= 1 << bit;
                    var index = Array.IndexOf(spec.MaskOrder, CanonicalBlobMask(bits));
                    if (index < 0 || spec.CellWidth != 32 || spec.CellHeight != 32
                        || spec.CellWidth2x != 64 || spec.CellHeight2x != 64)
                        throw new InvalidOperationException("Forest mask does not declare the required Blob47 cell.");
                    mask.BlitRect(source, new Rect2I(index % spec.Columns * 64, index / spec.Columns * 64, 64, 64),
                        new Vector2I(x * 64, y * 64));
                }
                source.Dispose();
                ground.AddChild(Masked("ForestFloor", mask, MaterialRecord("forest-floor"), size));
            }, diagnostics);
            if (_scene.RoadCorridor is { } corridor)
                TryLayer("Road material", () => {
                    var material = MaterialRecord("road-material");
                    using var mask = RoadCorridorMask.Create(size.X, size.Y, corridor,
                        (x, y) => At(x, y)?.Connected is { } binding
                            && binding.Atlas == material.Replaces.Atlas && binding.Mask == material.Replaces.Mask,
                        (x, y) => Tagged(x, y, "way") || Tagged(x, y, "settlement"),
                        (x, y) => TileAt(x, y) == "plain");
                    ground.AddChild(Masked("RoadCorridor", mask, material, size));
                }, diagnostics);
            else diagnostics.Add("Authored scene has no reviewed native road corridor.");
            var detail = new Node2D { Name = "TerrainDecals", ZIndex = -200 };
            staging.AddChild(detail);
            var scenery = new Node2D { Name = "TerrainScenery" };
            staging.AddChild(scenery);
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
            {
                var material = At(x, y)!;
                var tile = TileAt(x, y)!;
                if (Tagged(x, y, "outside")) continue;
                if (material.Decals is { Chance: { } chance } decal && TileArtVariation.Hash(x, y, 1103) < chance)
                {
                    var id = Pick(decal, x, y, 1104);
                    TryLayer($"Terrain decal ({x},{y}) {id}", () => {
                        var sprite = _environment.CreateSprite(id, (float)decal.Scale,
                            TileArtVariation.Hash(x, y, 1105) > 0.5, 0.72f);
                        sprite.Position = new Vector2((x + 0.5f) * 32, (y + 1) * 32);
                        detail.AddChild(sprite);
                    }, diagnostics);
                }
                if (Authored(tile, x, y, surface: false)) continue;
                var enclosed = material.Blend is not null
                    && Cardinal.All(delta => At(x + delta.X, y + delta.Y)?.Blend == material.Blend);
                var spec = enclosed ? _manifest.Scenery.ForestFloor
                    : material.Scenery ?? (Cardinal.Any(delta => Tagged(x + delta.X, y + delta.Y, "settlement"))
                        ? _manifest.Scenery.SettlementLife : null);
                if (enclosed && spec is null) diagnostics.Add("Forest understory specification is not exported.");
                if (spec is null || spec.Chance is { } density && TileArtVariation.Hash(x, y, 1119) > density) continue;
                var pick = Pick(spec, x, y, 1120);
                TryLayer($"Terrain scenery ({x},{y}) {pick}", () => {
                    var sprite = _environment.CreateSprite(pick,
                        (float)(spec.Scale + TileArtVariation.Hash(x, y, 1121) * 0.12),
                        TileArtVariation.Hash(x, y, 1123) > 0.52, enclosed ? 0.9f : 1);
                    sprite.Position = new Vector2((float)((x + 0.5) * 32
                        + (TileArtVariation.Hash(x, y, 1122) - 0.5) * spec.Jitter), (y + 1.18f) * 32);
                    sprite.Name = $"Scenery_{x}_{y}";
                    scenery.AddChild(sprite);
                    standing.Add(sprite);
                }, diagnostics);
                if ((material.Surface is not null || material.Tone is not null) && !Authored(tile, x, y, surface: true))
                    diagnostics.Add($"Legacy surface/tone layer is not migrated: {tile} at ({x},{y}).");
            }
        }
        catch
        {
            staging.Free();
            throw;
        }
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        AddChild(staging);
        _standing.Clear();
        _standing.AddRange(standing);
        _diagnostics.Clear();
        _diagnostics.AddRange(diagnostics.Distinct(StringComparer.Ordinal));
        _tiles = tiles.ToArray();
    }

    private Polygon2D CreateField()
    {
        if (_scene.GroundOutline.Length < 3) throw new InvalidOperationException("Authored ground outline is absent.");
        var material = MaterialRecord("field-material");
        var texture = MaterialTexture(material);
        var polygon = _scene.GroundOutline.Select(pair => RasterResources.Pair(pair, "ground outline") * 32).ToArray();
        return new Polygon2D { Name = "WorldField", Polygon = polygon, Texture = texture,
            UV = polygon.Select(point => point * texture.GetSize()
                / new Vector2(material.LogicalSize[0], material.LogicalSize[1])).ToArray(),
            TextureRepeat = TextureRepeatEnum.Enabled, TextureFilter = TextureFilterEnum.Linear,
            Antialiased = true };
    }

    private Sprite2D Masked(string name, Image mask, WorldMaterialRecord material, Vector2I size)
    {
        var shader = new ShaderMaterial { Shader = new Shader { Code = MaskedMaterialShader } };
        shader.SetShaderParameter("fill_texture", MaterialTexture(material));
        shader.SetShaderParameter("world_size", new Vector2(size.X * 32, size.Y * 32));
        shader.SetShaderParameter("material_period", new Vector2(material.LogicalSize[0], material.LogicalSize[1]));
        return new Sprite2D { Name = name, Texture = ImageTexture.CreateFromImage(mask),
            Centered = false, Scale = Vector2.One / 2, Material = shader, TextureFilter = TextureFilterEnum.Linear };
    }
    private WorldMaterialRecord MaterialRecord(string key) => _manifest.Materials.TryGetValue(key, out var value)
        ? value : throw new InvalidOperationException($"World material metadata is absent: {key}");
    private Texture2D MaterialTexture(WorldMaterialRecord material)
    {
        if (material.SchemaVersion != 2 || material.LogicalSize.Length != 2 || material.RasterSize.Length != 2
            || material.LogicalSize.Any(value => value <= 0) || material.RasterSize.Any(value => value <= 0)
            || material.WorldOrigin.Length != 2 || material.WorldOrigin.Any(value => value != 0)
            || material.Fit.Inset != 0 || material.Fit.Bleed != 0)
            throw new InvalidOperationException($"Invalid world material geometry: {material.Id}");
        return Texture(material.ResourcePath, material.RasterSize[0] * 2, material.RasterSize[1] * 2);
    }
    private Texture2D Texture(string path, int width, int height)
    {
        if (_textures.TryGetValue(path, out var cached)) return cached;
        if (!path.StartsWith("res://assets/", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException($"World material resource escaped assets: {path}");
        if (!ResourceLoader.Exists(path))
            throw new MissingArtResourceException(path, $"Missing world material resource: {path}");
        var texture = ResourceLoader.Load<Texture2D>(path);
        if (texture is null || texture.GetWidth() != width || texture.GetHeight() != height)
            throw new InvalidOperationException($"World material physical dimensions differ: {path}, expected {width}×{height}.");
        _textures.Add(path, texture);
        return texture;
    }
    private bool Authored(string tile, int x, int y, bool surface) => _scene.Zones.Any(zone =>
        zone.Bounds.Length == 4 && x >= zone.Bounds[0] && y >= zone.Bounds[1]
        && x <= zone.Bounds[2] && y <= zone.Bounds[3]
        && (surface ? zone.AuthoredTerrainSurface : zone.AuthoredTerrainScenery).Contains(tile));
    private static string Pick(ScenerySpecification spec, int x, int y, int salt)
    {
        if (spec.Ids.Length == 0) throw new InvalidOperationException("Empty scenery selection.");
        return spec.Ids[(int)Math.Floor(TileArtVariation.Hash(x, y, salt) * spec.Ids.Length)];
    }
    internal static int CanonicalBlobMask(int mask)
    {
        if ((mask & 0x05) != 0x05) mask &= ~0x02;
        if ((mask & 0x14) != 0x14) mask &= ~0x08;
        if ((mask & 0x50) != 0x50) mask &= ~0x20;
        if ((mask & 0x41) != 0x41) mask &= ~0x80;
        return mask;
    }
    private static void TryLayer(string description, Action render, List<string> diagnostics)
    {
        try { render(); }
        catch (MissingArtResourceException error) { diagnostics.Add($"{description}: {error.Message}"); }
    }
}
