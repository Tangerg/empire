#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json;
using Godot;
using Empire.Presentation.Tactical;
using Empire.Core.State;

namespace Empire.Presentation;

public enum MiniatureRenderPreset
{
    Reference,
    NoMsaa,
    NoShadowsDiagnostic
}

/// <summary>Read-only 3D presentation. Canonical session state and tactical queries remain the only game model.</summary>
public partial class MiniatureBoardView : SubViewportContainer
{
    private readonly SubViewport _viewport;
    private readonly DirectionalLight3D _sun;
    private int _activeEffects;
    public MiniatureRenderPreset RenderPreset
    {
        get; private set;
    }
    private readonly ReadOnlyContent _content;
    private readonly JsonObject _roleManifest;
    private readonly MiniaturePoseAtlas _poseAtlas;
    private readonly Dictionary<int, (string Role, bool Backward, Texture2D Idle)> _unitRoles = new();
    private readonly Dictionary<Texture2D, Image> _alphaImages = new();
    private readonly List<(BoardCell Cell, AuthoredMesh Mesh)> _sites = new();
    private readonly Node3D _world = new() { Name = "MiniatureWorld" };
    private readonly Node3D _terrain = new() { Name = "CanonicalSurface" };
    private readonly Node3D _actors = new() { Name = "Actors" };
    private readonly Node3D _ranges = new() { Name = "TacticalRanges" };
    private readonly Camera3D _camera = new() { Name = "TacticalCamera", Projection = Camera3D.ProjectionType.Orthogonal, Near = .05f, Far = 180 };
    private sealed record AuthoredMesh(MeshInstance3D Node, TriangleMesh Picking, Aabb Bounds);
    private readonly List<AuthoredMesh> _authoredMeshes = new();
    private readonly Dictionary<int, (Sprite3D Sprite, BoardCell Cell, Image Alpha)> _units = new();
    private readonly Dictionary<string, Texture2D> _textures = new();
    private readonly Dictionary<int, MeshInstance3D> _ownerRings = new();
    private MiniatureBoardGeometry? _geometry;
    private BoardCell? _selected;
    public MiniatureBoardGeometry Geometry => _geometry ?? throw new InvalidOperationException("Render a snapshot before using miniature geometry.");
    public Camera3D Camera => _camera;

    public MiniatureBoardView(ReadOnlyContent content)
    {
        _content = content;
        _roleManifest = PresentationJson.Load<JsonObject>("res://assets/c01-miniature-roles/manifest.json");
        _poseAtlas = new MiniaturePoseAtlas();
        Name = "MiniaturePresentation";
        MouseFilter = MouseFilterEnum.Ignore;
        Stretch = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _viewport = new SubViewport
        {
            Name = "MiniatureViewport",
            OwnWorld3D = true,
            HandleInputLocally = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(_viewport);
        _viewport.AddChild(_world);
        _world.AddChild(_terrain);
        _world.AddChild(_actors);
        _world.AddChild(_ranges);
        _world.AddChild(_camera);
        _sun = new DirectionalLight3D
        {
            Name = "Sun",
            RotationDegrees = new(-55, -32, 0),
            LightColor = new Color("#ffe8c4"),
            LightEnergy = .65f,
            ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            DirectionalShadowMaxDistance = 65
        };
        _world.AddChild(_sun);
        _world.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("#3c4948"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("#b9c8d6"),
                AmbientLightEnergy = .30f,
                TonemapMode = Godot.Environment.ToneMapper.Linear,
                TonemapExposure = 1
            }
        });
        Resized += RequestFrame;
        VisibilityChanged += RequestFrame;
    }

    public override void _Ready() => RequestFrame();

    public override void _Process(double delta)
    {
        if (_activeEffects > 0)
            RequestFrame();
    }

    public override void _ExitTree() => _activeEffects = 0;

    public void ApplyRenderPreset(MiniatureRenderPreset preset)
    {
        if (!Enum.IsDefined(preset))
            throw new ArgumentOutOfRangeException(nameof(preset));
        RenderPreset = preset;
        _viewport.Msaa3D = preset == MiniatureRenderPreset.Reference ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Disabled;
        _sun.ShadowEnabled = preset != MiniatureRenderPreset.NoShadowsDiagnostic;
        RequestFrame();
    }

    private void RequestFrame() => _viewport.RenderTargetUpdateMode = IsInsideTree() && IsVisibleInTree()
        ? SubViewport.UpdateMode.Once : SubViewport.UpdateMode.Disabled;

    public void LoadAuthoredScene(string path)
    {
        var scene = GD.Load<PackedScene>(path) ?? throw new InvalidOperationException($"Missing miniature scene: {path}");
        var root = scene.Instantiate<Node3D>();
        _world.AddChild(root);
        Collect(root);
        RequestFrame();
        void Collect(Node node)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh is not null)
            {
                var vertices = mesh.Mesh.GetFaces().Select(v => mesh.GlobalTransform * v).ToArray();
                var bounds = mesh.GlobalTransform * mesh.GetAabb();
                _authoredMeshes.Add(new(mesh, MiniatureBoardGeometry.CreateTree(vertices), bounds));
            }
            foreach (var child in node.GetChildren())
                Collect(child);
        }
    }

    public void Render(BoardProjection projection, JsonObject state, IReadOnlySet<int>? visibleUnits)
    {
        if (_geometry?.Projection != projection)
        {
            _geometry = new(projection);
            Clear(_terrain);
            var map = state["map"]!.AsObject();
            var width = map["width"]!.GetValue<int>();
            var market = MiniatureGroundMaterials.MarketContact(Geometry, state);
            foreach (var group in Geometry.Surface.GroupBy(t => map["tiles"]![t.Cell.Y * width + t.Cell.X]!.GetValue<string>()))
            {
                var terrain = _content.Definition("terrains", group.Key) ?? throw new InvalidOperationException($"Missing terrain {group.Key}.");
                var tags = terrain.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ToHashSet();
                var color = tags.Contains("blocking") ? new Color("#9a9684")
                    : tags.Contains("rough") ? new Color("#667b48") : new Color("#b0a47e");
                AddMesh(_terrain, group.Select(t => (t.A, t.B, t.C)), color, group.Key, MiniatureGroundMaterials.Surface(color, market));
                if (group.Key == "forest")
                    _terrain.AddChild(MiniatureOrchard.Create(Geometry, group.Select(t => t.Cell).Distinct().ToArray()));
            }
            if (Geometry.Cliffs.Count > 0)
                AddMesh(_terrain, Geometry.Cliffs, new Color("#84765f"), "CanonicalCliffs",
                    market.HasValue ? MiniatureGroundMaterials.RetainingWall() : null);
            var skirt = Geometry.OutsideSkirt();
            if (skirt.Count > 0)
                AddMesh(_terrain, skirt, new Color("#656155"), "CanonicalOutsideSkirt");
        }
        Clear(_actors);
        _units.Clear();
        _unitRoles.Clear();
        _ownerRings.Clear();
        _sites.Clear();
        var siteMap = state["map"]!.AsObject();
        var siteWidth = siteMap["width"]!.GetValue<int>();
        for (var index = 0; index < siteMap["tiles"]!.AsArray().Count; index++)
        {
            var definition = _content.Definition("terrains", siteMap["tiles"]![index]!.GetValue<string>())!.Value;
            if (!definition.GetProperty("capturable").GetBoolean())
                continue;
            var cell = new BoardCell(index % siteWidth, index / siteWidth);
            var owner = siteMap["owners"]![index]!.GetValue<int>();
            var color = owner == 0 ? new Color("#dfc687") : Color.FromHtml(state["players"]!.AsArray()
                .Select(p => p!.AsObject()).Single(p => p["id"]!.GetValue<int>() == owner)["color"]!.GetValue<string>());
            var packed = GD.Load<PackedScene>("res://assets/c01-miniature-environment/c01_market_beacon.glb")
                ?? throw new InvalidOperationException("Missing miniature capturable-site beacon.");
            var marker = packed.Instantiate<Node3D>();
            marker.Name = $"Site_{cell.X}_{cell.Y}";
            marker.Position = Geometry.Contact(cell) + new Vector3(.32f, 0, .32f);
            marker.SetMeta("tactical_cell_x", cell.X);
            marker.SetMeta("tactical_cell_y", cell.Y);
            _actors.AddChild(marker);
            AddSiteMeshes(marker);
            void AddSiteMeshes(Node root)
            {
                if (root is MeshInstance3D mesh && mesh.Mesh is not null)
                {
                    if (mesh.Name == "Banner")
                        mesh.MaterialOverride = CreateMaterial(color);
                    var vertices = mesh.Mesh.GetFaces().Select(v => mesh.GlobalTransform * v).ToArray();
                    _sites.Add((cell, new(mesh, MiniatureBoardGeometry.CreateTree(vertices), mesh.GlobalTransform * mesh.GetAabb())));
                }
                foreach (var child in root.GetChildren())
                    AddSiteMeshes(child);
            }
        }
        foreach (var node in state["units"]!.AsArray())
        {
            var unit = node!.AsObject();
            var id = unit["id"]!.GetValue<int>();
            if (visibleUnits is not null && !visibleUnits.Contains(id))
                continue;
            var cell = new BoardCell(unit["x"]!.GetValue<int>(), unit["y"]!.GetValue<int>());
            var type = unit["type"]!.GetValue<string>();
            var role = _roleManifest["unitRoles"]?[type]?.GetValue<string>()
                ?? throw new InvalidOperationException($"No miniature role for {type}.");
            var facing = unit["facing"]?.GetValue<string>();
            var backward = facing is "n" or "w" or "north" or "west";
            var key = role + (backward ? ":back" : ":front");
            if (!_textures.TryGetValue(key, out var texture))
            {
                var record = _roleManifest["roles"]![role]!.AsObject();
                var region = record[backward ? "backRegion" : "frontRegion"]!.AsArray();
                var source = GD.Load<Texture2D>(record["viewsTexture"]!.GetValue<string>())
                    ?? throw new InvalidOperationException($"Missing miniature role {role}.");
                texture = new AtlasTexture
                {
                    Atlas = source,
                    Region = new Rect2(region[0]!.GetValue<int>(), region[1]!.GetValue<int>(),
                    region[2]!.GetValue<int>(), region[3]!.GetValue<int>())
                };
                _textures.Add(key, texture);
            }
            var sprite = new Sprite3D
            {
                Name = $"Unit_{id}",
                Texture = texture,
                PixelSize = .022f,
                Offset = new(0, 24),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
                Position = Geometry.Contact(cell) + Vector3.Up * .025f,
                Shaded = true
            };
            _actors.AddChild(sprite);
            _units.Add(id, (sprite, cell, Alpha(texture)));
            _unitRoles.Add(id, (role, backward, texture));
            var player = state["players"]!.AsArray().Select(n => n!.AsObject())
                .Single(p => p["id"]!.GetValue<int>() == unit["owner"]!.GetValue<int>());
            var ring = new MeshInstance3D
            {
                Name = $"Owner_{id}",
                Mesh = new TorusMesh { InnerRadius = .27f, OuterRadius = .32f, Rings = 16, RingSegments = 8 },
                Position = sprite.Position,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = CreateMaterial(Color.FromHtml(player["color"]!.GetValue<string>()))
            };
            _actors.AddChild(ring);
            _ownerRings.Add(id, ring);
        }
        UpdateRoofs();
        RequestFrame();
    }

    public bool PresentPose(ActorPoseSample sample)
    {
        if (!_units.ContainsKey(sample.UnitId) || !_unitRoles.TryGetValue(sample.UnitId, out var role))
            return false;
        if (sample.Action == "idle")
        {
            SetPoseFrame(sample.UnitId, role.Idle);
            return true;
        }
        if (sample.Action is not ("attack" or "cast"))
            return false;
        var backward = sample.AimDirection is { } aim && aim.LengthSquared() > .0001f
            ? aim.X + aim.Y < 0 : role.Backward;
        var texture = _poseAtlas.Sample(role.Role, backward, sample);
        if (texture is null)
            return false;
        SetPoseFrame(sample.UnitId, texture);
        return true;
    }

    private Image Alpha(Texture2D texture)
    {
        if (!_alphaImages.TryGetValue(texture, out var image))
            _alphaImages.Add(texture, image = texture.GetImage());
        return image;
    }

    private void SetPoseFrame(int unitId, Texture2D texture)
    {
        var actor = _units[unitId];
        if (actor.Sprite.Texture == texture)
            return;
        actor.Sprite.Texture = texture;
        _units[unitId] = (actor.Sprite, actor.Cell, Alpha(texture));
        RequestFrame();
    }

    public void PresentEvent(JsonElement item)
    {
        var type = item.GetProperty("type").GetString();
        if (type == "death" && _units.TryGetValue(item.GetProperty("unit").GetInt32(), out var dead))
        {
            dead.Sprite.Visible = false;
            RequestFrame();
        }
        var targetKey = type is "attack" or "counter" or "supportAttack" or "partingShot" or "areaAttack" ? "defender"
            : type is "heal" ? "target" : type is "regen" or "statusDamage" or "collisionDamage" ? "unit" : null;
        if (targetKey is null || !item.TryGetProperty(targetKey, out var target) || !_units.TryGetValue(target.GetInt32(), out var actor))
            return;
        var amountKey = type is "attack" or "counter" or "supportAttack" or "partingShot" or "areaAttack" ? "damage" : "amount";
        if (!item.TryGetProperty(amountKey, out var amount))
            return;
        var label = new Label3D
        {
            Text = amount.GetDouble().ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            Position = actor.Sprite.Position + Vector3.Up * 1.4f,
            FontSize = 36,
            PixelSize = .012f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Modulate = type is "heal" or "regen" ? new Color("#a4f2aa") : new Color("#fff0d0")
        };
        _world.AddChild(label);
        _activeEffects++;
        var tween = CreateTween();
        tween.Finished += () => { _activeEffects--; RequestFrame(); };
        tween.TweenProperty(label, "position:y", label.Position.Y + .5f, .65);
        tween.TweenCallback(Callable.From(label.QueueFree));
        RequestFrame();
    }

    public void PresentContact(int unitId, Vector2 logical)
    {
        if (!_units.TryGetValue(unitId, out var actor))
            return;
        var position = Geometry.Point(logical, Geometry.Projection.Project(logical)) + Vector3.Up * .025f;
        actor.Sprite.Position = position;
        _ownerRings[unitId].Position = position;
        RequestFrame();
    }

    public void SyncCamera(BattlefieldCamera camera, Vector2 size)
    {
        if (_geometry is null || size.X <= 0 || size.Y <= 0)
            return;
        var logical = Geometry.Projection.LogicalGroundPoint(new((float)camera.Position.X, (float)camera.Position.Y)) / BoardProjection.TileSize;
        var target = new Vector3(logical.X, .6f, logical.Y);
        _camera.Position = target + new Vector3(17, 23, 17);
        _camera.LookAt(target);
        _camera.Size = (float)(size.Y * 1.2 / (32 * camera.Zoom));
        _camera.Current = true;
        UpdateRoofs();
        RequestFrame();
    }

    public void ShowOverlay(TacticalSelectionOverlay overlay, IReadOnlyList<BoardCell> objectives)
    {
        if (_geometry is null)
            return;
        _selected = overlay.Selected;
        Clear(_ranges);
        Draw(overlay.Move, new("#5ba9c0"), "Move");
        Draw(overlay.Attack, new("#d76d59"), "Attack");
        Draw(overlay.Helpful, new("#70c29a"), "Helpful");
        Draw(objectives, new("#e8ca78"), "Objectives");
        Draw(overlay.Path, new("#f2e1a4"), "Path");
        if (overlay.Selected is { } selected)
            Draw(new[] { selected }, new("#fff0af"), "Selected");
        if (overlay.Cursor is { } cursor)
            Draw(new[] { cursor }, new("#ffffff"), "Cursor");
        UpdateRoofs();
        RequestFrame();
        void Draw(IEnumerable<BoardCell> cells, Color color, string name)
        {
            var selected = cells.ToHashSet();
            var triangles = Geometry.Surface.Where(t => selected.Contains(t.Cell)).Select(t =>
            {
                var center = Geometry.Contact(t.Cell);
                Vector3 Inset(Vector3 v) => center.Lerp(v, .87f) + Vector3.Up * .045f;
                return (Inset(t.A), Inset(t.B), Inset(t.C));
            }).ToArray();
            if (triangles.Length > 0)
            {
                color.A = .46f;
                AddMesh(_ranges, triangles, color, name);
            }
        }
    }

    public TacticalHit? Pick(Vector2 screen)
    {
        if (_geometry is null)
            return null;
        var origin = _camera.ProjectRayOrigin(screen);
        var direction = _camera.ProjectRayNormal(screen);
        var nearest = float.PositiveInfinity;
        TacticalHit? result = null;
        var surfaceHit = Geometry.SurfaceTree.IntersectRay(origin, direction);
        if (surfaceHit.Count > 0)
        {
            nearest = origin.DistanceTo(surfaceHit["position"].AsVector3());
            result = TacticalHit.Ground(Geometry.Surface[surfaceHit["face_index"].AsInt32()].Cell);
        }
        if (Geometry.CliffTree is { } cliffs)
            OccludeTree(cliffs, null);
        foreach (var authored in _authoredMeshes.Where(m => m.Node.IsVisibleInTree() && m.Node.Transparency < .5f))
        {
            if (!authored.Bounds.IntersectsSegment(origin, origin + direction * 200))
                continue;
            var mesh = authored.Node;
            TacticalHit? subject = null;
            for (Node? node = mesh; node is not null && node != _world; node = node.GetParent())
                if (node.HasMeta("tactical_cell_x") && node.HasMeta("tactical_cell_y"))
                {
                    subject = TacticalHit.Object(new(node.GetMeta("tactical_cell_x").AsInt32(), node.GetMeta("tactical_cell_y").AsInt32()));
                    break;
                }
            OccludeTree(authored.Picking, subject);
        }
        foreach (var (cell, mesh) in _sites)
            if (mesh.Bounds.IntersectsSegment(origin, origin + direction * 200))
                OccludeTree(mesh.Picking, TacticalHit.Object(cell));
        foreach (var (id, unit) in _units)
        {
            if (!unit.Sprite.Visible)
                continue;
            var normal = _camera.GlobalBasis.Z;
            var denom = direction.Dot(normal);
            if (Math.Abs(denom) < .0001f)
                continue;
            var distance = (unit.Sprite.GlobalPosition - origin).Dot(normal) / denom;
            if (distance < 0 || distance >= nearest)
                continue;
            var delta = origin + direction * distance - unit.Sprite.GlobalPosition;
            var x = delta.Dot(_camera.GlobalBasis.X) / unit.Sprite.PixelSize + 32;
            var y = 56 - delta.Dot(_camera.GlobalBasis.Y) / unit.Sprite.PixelSize;
            if (x >= 0 && x < 64 && y >= 0 && y < 64 && unit.Alpha.GetPixel((int)x, (int)y).A > .5f)
            {
                nearest = distance;
                result = TacticalHit.Unit(unit.Cell, id);
            }
        }
        return result;
        void OccludeTree(TriangleMesh mesh, TacticalHit? subject)
        {
            var hit = mesh.IntersectRay(origin, direction);
            if (hit.Count > 0 && origin.DistanceTo(hit["position"].AsVector3()) is var distance && distance < nearest)
            {
                nearest = distance;
                result = subject;
            }
        }

    }

    private void UpdateRoofs()
    {
        foreach (var authored in _authoredMeshes.Where(m => m.Node.Name.ToString().Contains("Roof", StringComparison.OrdinalIgnoreCase)))
        {
            var mesh = authored.Node;
            mesh.Transparency = 0;
            if (_selected is not { } selected || _geometry is null || !_camera.IsInsideTree())
                continue;
            var contact = Geometry.Contact(selected) + Vector3.Up * .5f;
            var origin = _camera.ProjectRayOrigin(_camera.UnprojectPosition(contact));
            var ray = (contact - origin).Normalized();
            if (!authored.Bounds.IntersectsSegment(origin, origin + ray * 200))
                continue;
            var hit = authored.Picking.IntersectRay(origin, ray);
            if (hit.Count > 0 && origin.DistanceTo(hit["position"].AsVector3()) < origin.DistanceTo(contact))
                mesh.Transparency = .78f;
        }
    }

    private static StandardMaterial3D CreateMaterial(Color color) => new()
    {
        AlbedoColor = color,
        Roughness = .92f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        Transparency = color.A < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled
    };

    private static void AddMesh(Node3D parent, IEnumerable<(Vector3 A, Vector3 B, Vector3 C)> triangles, Color color, string name, Material? material = null) =>
        parent.AddChild(new MeshInstance3D
        {
            Name = name.Replace('.', '_'),
            Mesh = MiniatureBoardGeometry.Mesh(triangles),
            MaterialOverride = material ?? CreateMaterial(color),
            CastShadow = color.A < 1 ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On
        });

    private static void Clear(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
