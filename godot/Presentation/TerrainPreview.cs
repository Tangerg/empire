#nullable enable
using System;
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
    private BoardView? _board;
    private Label? _heading;
    private int _sequence;
    private Rect2 _visualBounds;

    public override async void _Ready()
    {
        try
        {
            foreach (var argument in OS.GetCmdlineUserArgs())
            {
                if (argument.StartsWith("--level=", StringComparison.Ordinal)) _levelId = argument[8..];
                if (argument.StartsWith("--capture-dir=", StringComparison.Ordinal)) _captureDirectory = argument[14..];
            }
            if (_captureDirectory is not null && !Path.IsPathFullyQualified(_captureDirectory))
                throw new InvalidOperationException("Capture directory must be absolute.");
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
                .Single(value => value["id"]!.GetValue<string>() == _levelId);
            var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
            var engine = new GrayFlagEngine(packs);
            var state = engine.CreateState(level);
            var unchanged = state.ToJsonString();
            var grid = engine.Grids.Get(level["rules"]?["grid"]?.GetValue<string>() ?? "square4");
            _board = new BoardView();
            AddChild(_board);
            _board.Configure(AssetCatalog.Load(), level, TerrainChecks.Scene(level), EnvironmentCatalog.Load(),
                facing => facing is not null && grid.Step(new Coord(0, 0), facing).X < 0);
            _board.ConfigureTerrain(WorldMaterialsManifest.Load());
            _board.ConfigureDynamicState(packs);
            _board.RenderState(state);
            if (state.ToJsonString() != unchanged) throw new InvalidOperationException("Terrain preview mutated canonical state.");
            _heading = new Label { Text = $"{_levelId}  Native terrain verification | canonical initial state | no gameplay controls\n{_board.StatusText}",
                Position = new Vector2(18, 12), ZIndex = 4095 };
            _heading.AddThemeFontSizeOverride("font_size", 15);
            AddChild(_heading);
            foreach (var diagnostic in _board.Diagnostics) GD.Print($"{_levelId}: {diagnostic}");
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
                foreach (var child in node.GetChildren()) if (child is Node2D child2D) Measure(child2D, transform);
            }
            foreach (var child in _board.GetChildren()) if (child is Node2D child2D) Measure(child2D, Transform2D.Identity);
            Layout();
            GetViewport().SizeChanged += Layout;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_captureDirectory is not null) Capture();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void Layout()
    {
        if (_board is null) return;
        var available = GetViewportRect().Size - new Vector2(64, 110);
        var scale = Math.Min(available.X / _visualBounds.Size.X, available.Y / _visualBounds.Size.Y);
        _board.Scale = Vector2.One * scale;
        _board.Position = new Vector2((GetViewportRect().Size.X - _visualBounds.Size.X * scale) / 2, 78) - _visualBounds.Position * scale;
    }
    public override void _Input(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F12 } && _captureDirectory is not null) Capture();
    }
    private async void Capture()
    {
        try
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Directory.CreateDirectory(_captureDirectory!);
            using var image = GetViewport().GetTexture().GetImage();
            var path = Path.Combine(_captureDirectory!, $"{_levelId}-{++_sequence:000}.png");
            if (image.SavePng(path) != Error.Ok) throw new IOException("Cannot save native terrain screenshot: " + path);
            GD.Print($"Terrain viewport captured: {path} ({image.GetWidth()}x{image.GetHeight()}).");
        }
        catch (Exception error) { GD.PushError(error.ToString()); }
    }
}
