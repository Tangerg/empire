#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

/// <summary>Opt-in visual verification scene, not a playable battle or campaign shortcut.</summary>
public partial class TerrainPreview : Node2D
{
    private string? _captureDirectory;
    private string _levelId = "c01-02";
    private string? _scenePath;
    private BoardView? _board;
    private Label? _heading;
    private int _sequence;
    private Rect2 _visualBounds;
    private float? _reviewScale;
    private Coord? _reviewFocus;
    private bool _hideObjects;
    private readonly List<Coord> _supportCells = [];

    public override async void _Ready()
    {
        try
        {
            foreach (var argument in OS.GetCmdlineUserArgs())
            {
                if (argument.StartsWith("--support-cells=", StringComparison.Ordinal))
                    foreach (var cell in argument[16..].Split(';'))
                    {
                        var pair = cell.Split(',');
                        if (pair.Length != 2)
                            throw new InvalidOperationException("Support samples must be semicolon-separated x,y cells.");
                        _supportCells.Add(new Coord(int.Parse(pair[0]), int.Parse(pair[1])));
                    }
                if (argument == "--hide-assembled-objects")
                    _hideObjects = true;
                if (argument.StartsWith("--scene=", StringComparison.Ordinal))
                    _scenePath = argument[8..];
                if (argument.StartsWith("--level=", StringComparison.Ordinal))
                    _levelId = argument[8..];
                if (argument.StartsWith("--scale=", StringComparison.Ordinal))
                    _reviewScale = float.Parse(argument[8..], System.Globalization.CultureInfo.InvariantCulture);
                if (argument.StartsWith("--focus=", StringComparison.Ordinal))
                {
                    var coordinates = argument[8..].Split(',');
                    if (coordinates.Length != 2)
                        throw new InvalidOperationException("Review focus must be x,y.");
                    _reviewFocus = new Coord(int.Parse(coordinates[0]), int.Parse(coordinates[1]));
                }
                if (argument.StartsWith("--capture-dir=", StringComparison.Ordinal))
                    _captureDirectory = argument[14..];
            }
            if (_captureDirectory is not null && !Path.IsPathFullyQualified(_captureDirectory))
                throw new InvalidOperationException("Capture directory must be absolute.");
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
                .Single(value => value["id"]!.GetValue<string>() == _levelId);
            var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
            var engine = new BattleEngine(packs);
            var state = engine.CreateState(level);
            if (_reviewFocus is { } focus && (focus.X < 0 || focus.Y < 0
                || focus.X >= level["width"]!.GetValue<int>() || focus.Y >= level["height"]!.GetValue<int>()))
                throw new InvalidOperationException("Review focus must lie inside the canonical map.");
            var unchanged = state.ToJsonString();
            var grid = engine.Grids.Get(level["rules"]?["grid"]?.GetValue<string>() ?? "square4");
            _board = new BoardView();
            AddChild(_board);
            if (_scenePath is not null && (!_scenePath.StartsWith("res://data/scenes/", StringComparison.Ordinal)
                || !_scenePath.EndsWith(".json", StringComparison.Ordinal) || _scenePath.Contains("..", StringComparison.Ordinal)))
                throw new InvalidOperationException("Preview scene override must be an explicit authored-scene resource.");
            var scene = AuthoredScene.Load(_scenePath ?? $"res://data/scenes/{_levelId}.json");
            var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
            _board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), grid, engine.Catalog, rendering.Plane), level, scene);
            _board.ConfigureGround(rendering.Ground);
            _board.ConfigureDynamicState();
            _board.RenderState(state);
            if (_supportCells.Count > 0)
            {
                if (_supportCells.Any(cell => cell.X < 0 || cell.Y < 0
                    || cell.X >= level["width"]!.GetValue<int>() || cell.Y >= level["height"]!.GetValue<int>()))
                    throw new InvalidOperationException("Support samples must lie inside the canonical map.");
                var guide = new Node2D { Name = "SupportContactGuide", ZIndex = 4094 };
                guide.Draw += () =>
                {
                    foreach (var cell in _supportCells)
                    {
                        var at = _board.Projection.CellContact(cell);
                        guide.DrawCircle(at, 4, Colors.Black, false, 2);
                        guide.DrawCircle(at, 3, Colors.White, false, 1);
                        guide.DrawString(ThemeDB.FallbackFont, at + new Vector2(5, -5), $"{cell.X},{cell.Y}", fontSize: 10);
                    }
                };
                _board.AddChild(guide);
            }
            if (_hideObjects)
            {
                var ground = _board.GetNode("AuthoredScene").GetChildren().OfType<AssembledGroundView>().Single();
                foreach (var body in ground.GetChildren().OfType<Sprite2D>().Where(body => body.Name != "CleanGround"))
                    body.Hide();
            }
            if (state.ToJsonString() != unchanged)
                throw new InvalidOperationException("Terrain preview mutated canonical state.");
            _heading = new Label
            {
                Text = $"{_levelId}  Native authored-scene preview | canonical initial state | no gameplay controls\nSurface ready: {_board.SurfaceReady}; rendering issues: {_board.RenderingIssueCount}; scale: {_reviewScale?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "fit"}",
                Position = new Vector2(18, 12),
                ZIndex = 4095
            };
            _heading.AddThemeFontSizeOverride("font_size", 15);
            AddChild(_heading);
            foreach (var diagnostic in _board.Diagnostics)
                GD.Print($"{_levelId}: {diagnostic}");
            _visualBounds = new Rect2(Vector2.Zero, _board.FieldSize);
            void Measure(Node2D node, Transform2D parent)
            {
                var transform = parent * node.Transform;
                if (node is Sprite2D sprite)
                {
                    var rect = sprite.GetRect();
                    foreach (var corner in new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), rect.End, new Vector2(rect.Position.X, rect.End.Y) })
                        _visualBounds = _visualBounds.Expand(transform * corner);
                }
                foreach (var child in node.GetChildren())
                    if (child is Node2D child2D)
                        Measure(child2D, transform);
            }
            foreach (var child in _board.GetChildren())
                if (child is Node2D child2D)
                    Measure(child2D, Transform2D.Identity);
            Layout();
            GetViewport().SizeChanged += Layout;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_captureDirectory is not null)
                Capture();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void Layout()
    {
        if (_board is null)
            return;
        var available = GetViewportRect().Size - new Vector2(64, 110);
        var scale = _reviewScale ?? Math.Min(available.X / _visualBounds.Size.X, available.Y / _visualBounds.Size.Y);
        if (!float.IsFinite(scale) || scale <= 0)
            throw new InvalidOperationException("Review scale must be finite and positive.");
        _board.Scale = Vector2.One * scale;
        _board.Position = _reviewFocus is { } focus
            ? GetViewportRect().Size / 2 - _board.Projection.CellContact(focus) * scale
            : new Vector2((GetViewportRect().Size.X - _visualBounds.Size.X * scale) / 2, 78) - _visualBounds.Position * scale;
    }
    public override void _Input(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F12 } && _captureDirectory is not null)
            Capture();
    }
    private async void Capture()
    {
        try
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Directory.CreateDirectory(_captureDirectory!);
            using var image = GetViewport().GetTexture().GetImage();
            var path = Path.Combine(_captureDirectory!, $"{_levelId}-{++_sequence:000}.png");
            if (image.SavePng(path) != Error.Ok)
                throw new IOException("Cannot save native terrain screenshot: " + path);
            GD.Print($"Terrain viewport captured: {path} ({image.GetWidth()}x{image.GetHeight()}).");
        }
        catch (Exception error) { GD.PushError(error.ToString()); }
    }
}
