#nullable enable
using System;
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
                GD.Print($"Native surface geometry: {board.Projection.Triangles.Count} source triangles, "
                    + $"{board.Projection.DepthPatches.Count} clipped patches, {board.Projection.DepthPartitions.Count} depth partitions.");
        }
        catch (Exception error) { GD.PushError(error.ToString()); }
        finally { _capturing = false; }
    }
}
