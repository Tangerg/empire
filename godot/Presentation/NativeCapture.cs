#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>
/// Opt-in verification harness around the real application scene. F12 saves the
/// actual native viewport, without MovieWriter changing its target aspect ratio.
/// </summary>
public partial class NativeCapture : Node
{
    private string _directory = "";
    private int _sequence;
    private bool _capturing;
    private bool _motionArmed;
    private bool _motionCapturing;
    private int _motionSequence;

    public override async void _Ready()
    {
        try
        {
            var argument = OS.GetCmdlineUserArgs().SingleOrDefault(value => value.StartsWith("--capture-dir=", StringComparison.Ordinal));
            if (argument is null || DisplayServer.GetName() == "headless")
                throw new InvalidOperationException("NativeCapture requires a real display and explicit --capture-dir=<absolute path>.");
            _directory = argument["--capture-dir=".Length..];
            if (!Path.IsPathFullyQualified(_directory))
                throw new InvalidOperationException("Capture directory must be absolute.");
            Directory.CreateDirectory(_directory);
            var sceneArgument = OS.GetCmdlineUserArgs().SingleOrDefault(value => value.StartsWith("--capture-scene=", StringComparison.Ordinal));
            var scenePath = sceneArgument is null ? "res://Scenes/GrayFlagGame.tscn" : sceneArgument["--capture-scene=".Length..];
            if (!scenePath.StartsWith("res://Scenes/", StringComparison.Ordinal) || !scenePath.EndsWith(".tscn", StringComparison.Ordinal))
                throw new InvalidOperationException("Capture target must be an explicit project scene.");
            AddChild(ResourceLoader.Load<PackedScene>(scenePath).Instantiate());
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var sizeArgument = OS.GetCmdlineUserArgs().SingleOrDefault(value => value.StartsWith("--capture-size=", StringComparison.Ordinal));
            if (sizeArgument is not null)
            {
                var dimensions = sizeArgument["--capture-size=".Length..].Split('x');
                if (dimensions.Length != 2 || !int.TryParse(dimensions[0], out var width) || !int.TryParse(dimensions[1], out var height)
                    || width < 640 || height < 480)
                    throw new InvalidOperationException("Capture size must be WIDTHxHEIGHT, at least 640x480.");
                GetWindow().Size = new Vector2I(width, height);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Capture();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    public override void _Input(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F12 })
            Capture();
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F8 } && !_motionCapturing)
        {
            _motionArmed = !_motionArmed;
            GD.Print($"Native motion capture armed: {_motionArmed}; next committed transition only.");
        }
    }

    public override void _Process(double delta)
    {
        if (_motionArmed && !_motionCapturing && FindBoard(this) is { IsPresenting: true } board)
            CaptureMotion(board);
    }

    // Verification only: observe actual rendered frames, never advance the battle or its clock.
    private async void CaptureMotion(BoardView board)
    {
        _motionArmed = false;
        _motionCapturing = true;
        var directory = Path.Combine(_directory, $"motion-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{++_motionSequence:000}");
        var identity = board.PresentationIdentity;
        var started = Time.GetTicksMsec();
        long bufferedBytes = 0;
        var frames = new List<(Image Image, string Trace)>();
        try
        {
            Directory.CreateDirectory(directory);
            while (true)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                if (board.PresentationIdentity is not null && !ReferenceEquals(identity, board.PresentationIdentity))
                    break;
                var size = GetViewport().GetVisibleRect().Size;
                var requiredBytes = (long)size.X * (long)size.Y * 4;
                if (bufferedBytes + requiredBytes > 64L * 1024 * 1024)
                    throw new IOException("Motion capture aborted: 64 MiB frame budget reached; no incomplete sequence saved.");
                var image = GetViewport().GetTexture().GetImage();
                frames.Add((image, "")); // Own immediately, including if tracing throws below.
                bufferedBytes += requiredBytes;
                var actors = board.GetNode<Node2D>("CanonicalProjection").GetChildren().OfType<Node2D>()
                    .Where(node => node.HasMeta("unit_id")).Select(node =>
                    {
                        var sprite = node.GetNode<Sprite2D>("AuthoredSprite");
                        return new
                        {
                            id = node.GetMeta("unit_id").AsInt32(),
                            position = new[] { node.Position.X, node.Position.Y },
                            supportPlane = node.HasMeta("logical_contact")
                                ? new[] { board.Projection.Project(node.GetMeta("logical_contact").AsVector2()).X,
                                    board.Projection.Project(node.GetMeta("logical_contact").AsVector2()).Y } : null,
                            frame = sprite.HasMeta("frame") ? sprite.GetMeta("frame").AsInt32() : 0,
                            offset = new[] { sprite.Offset.X, sprite.Offset.Y }
                        };
                    }).ToArray();
                frames[^1] = (image, System.Text.Json.JsonSerializer.Serialize(new
                {
                    elapsedMs = Time.GetTicksMsec() - started,
                    presenting = board.IsPresenting,
                    actors
                }));
                if (!board.IsPresenting)
                    break;
            }
            if (board.IsPresenting)
                throw new IOException("Motion capture aborted: another transition began before readback settled; no mixed sequence saved.");
            // Compress only after playback settles: PNG compression must not consume animation frames.
            for (var frame = 0; frame < frames.Count; frame++)
            {
                var path = Path.Combine(directory, $"frame-{frame:000}.png");
                if (frames[frame].Image.SavePng(path) != Error.Ok)
                    throw new IOException($"Motion capture failed: {path}");
                File.WriteAllText(Path.ChangeExtension(path, ".json"), frames[frame].Trace);
            }
            GD.Print($"Native motion capture complete: {directory}. Readback may affect frame pacing; timestamps are observations, not a performance benchmark.");
        }
        catch (Exception error) { GD.PushError(error.ToString()); }
        finally { foreach (var frame in frames) frame.Image.Dispose(); _motionCapturing = false; }
    }

    private static BoardView? FindBoard(Node root)
    {
        if (root is BoardView board)
            return board;
        foreach (var child in root.GetChildren())
            if (FindBoard(child) is { } found)
                return found;
        return null;
    }

    private static object TraceSprite(Sprite2D sprite)
    {
        using var image = SpriteShadows.ReadFrame(sprite.Texture);
        var left = image.GetWidth();
        var top = image.GetHeight();
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < image.GetHeight(); y++)
            for (var x = 0; x < image.GetWidth(); x++)
                if (image.GetPixel(x, y).A >= .05f)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
        var bounds = new Rect2(left, top, right - left + 1, bottom - top + 1);
        var offset = sprite.Offset - (sprite.Centered ? sprite.Texture.GetSize() / 2 : Vector2.Zero);
        var transform = sprite.GetGlobalTransformWithCanvas();
        var points = new[] { bounds.Position, bounds.Position + new Vector2(bounds.Size.X, 0),
            bounds.End, bounds.Position + new Vector2(0, bounds.Size.Y) }
            .Select(point => transform * (point + offset)).ToArray();
        return new
        {
            textureSize = new[] { image.GetWidth(), image.GetHeight() },
            atlasRegion = sprite.Texture is AtlasTexture atlas ? new[] { atlas.Region.Position.X, atlas.Region.Position.Y, atlas.Region.Size.X, atlas.Region.Size.Y } : null,
            scale = new[] { sprite.Scale.X, sprite.Scale.Y },
            canvasAxisLengths = new[] { transform.X.Length(), transform.Y.Length() },
            alphaBounds = new[] { bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y },
            // This is the unoccluded draw extent; neighboring figures and walls may cover it.
            alphaScreenBounds = new[] { points.Min(point => point.X), points.Min(point => point.Y),
                points.Max(point => point.X) - points.Min(point => point.X), points.Max(point => point.Y) - points.Min(point => point.Y) }
        };
    }

    private async void Capture()
    {
        if (_capturing || _directory.Length == 0)
            return;
        _capturing = true;
        try
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var fps = Performance.GetMonitor(Performance.Monitor.TimeFps);
            var processMilliseconds = Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000;
            var drawCalls = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
            var nodes = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            using var image = GetViewport().GetTexture().GetImage();
            var path = Path.Combine(_directory, $"native-{++_sequence:000}.png");
            var error = image.SavePng(path);
            if (error != Error.Ok)
                throw new IOException($"Native capture failed: {path}, {error}");
            GD.Print($"Native viewport captured: {path} ({image.GetWidth()}×{image.GetHeight()}).");
            GD.Print($"Native frame observation before readback: {fps:F1} FPS, process {processMilliseconds:F2} ms, "
                + $"draw calls {drawCalls}, scene nodes {nodes}.");
            if (FindBoard(this) is { ProjectionUnitCount: > 0 } board)
            {
                GD.Print($"Native surface geometry: {board.Projection.Triangles.Count} source triangles, "
                    + $"{board.Projection.DepthPatches.Count} clipped patches, {board.Projection.DepthPartitions.Count} depth partitions.");
                var figures = board.GetNode<Node2D>("CanonicalProjection").GetChildren().OfType<Node2D>()
                    .Where(node => node.HasMeta("unit_id"))
                    .Select(node => new
                    {
                        id = node.GetMeta("unit_id").AsInt32(),
                        type = node.GetMeta("unit_type").AsString(),
                        x = node.GetGlobalTransformWithCanvas().Origin.X,
                        y = node.GetGlobalTransformWithCanvas().Origin.Y,
                        art = node.GetNode<Sprite2D>("AuthoredSprite").GetMeta("art_identity").AsString(),
                        source = TraceSprite(node.GetNode<Sprite2D>("AuthoredSprite")),
                        figure = TraceSprite(node.GetNode<Sprite2D>("AuthoredSprite/SourceSpriteShadows"))
                    }).ToArray();
                File.WriteAllText(Path.ChangeExtension(path, ".actors.json"),
                    System.Text.Json.JsonSerializer.Serialize(figures, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (Exception error) { GD.PushError(error.ToString()); }
        finally { _capturing = false; }
    }
}
