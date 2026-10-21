#nullable enable
using System;
using System.Text.Json;
using Godot;

namespace Empire.Presentation;

/// <summary>Native world-transform checks; no canonical game state is advanced.</summary>
public partial class CameraChecks : Node
{
    private int assertions;

    public override void _Ready()
    {
        try
        {
            CheckWorldTransform();
            CheckGestureTransform();
            CheckFollow();
            GD.Print($"Native camera checks passed: {assertions} assertions.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void CheckWorldTransform()
    {
        var viewport = new Control { Position = new Vector2(100, 70), Size = new Vector2(800, 600), ClipContents = true };
        var world = new Node2D();
        AddChild(viewport);
        viewport.AddChild(world);
        var camera = new BattlefieldCamera(new(1152, 768), new(74, 62, 74, 74), new(500, 400));
        camera.Resize(new(800, 600));
        Apply(world, camera);
        Require(world.Scale == new Vector2(1.25f, 1.25f), "Oversized source map opens at 40 px per cell, not fitted scale");
        var focus = world.ToGlobal(new Vector2(500, 400));
        Near(focus, viewport.GlobalPosition + viewport.Size / 2, "First focus lies at map-area center");
        Near(world.ToLocal(focus), new Vector2(500, 400), "Native transform inverse agrees with logical field");
        var scroll = camera.Scroll;
        camera.Resize(new(900, 700));
        Apply(world, camera);
        Require(camera.Scroll == scroll && camera.Zoom == 1.25, "Resize preserves scroll and zoom");
        var center = camera.Position;
        for (var i = 0; i < 4; i++)
            camera.QueueZoom(.1);
        Require(world.Scale.X == 1.25f, "Wheel queue does not transform before frame application");
        Require(camera.ApplyPendingZoom(), "Accumulated wheel frame changes zoom once");
        Apply(world, camera);
        Near(ToVector(camera.Position), ToVector(center), "Zoom keeps tactical viewport center");
        Near(world.Scale, new Vector2(1.65f, 1.65f), "All queued deltas apply together");
        var field = new CameraPoint(333, 224);
        var localScreen = world.ToGlobal(ToVector(field)) - viewport.GlobalPosition;
        Near(ToVector(camera.ToField(new(localScreen.X, localScreen.Y))), ToVector(field), "Pointer inverse follows native world projection");

        camera.Pan(new(-100000, -100000));
        Apply(world, camera);
        Near(world.Position + new Vector2((1152 + 74) * world.Scale.X, (768 + 74) * world.Scale.Y),
            new Vector2(900, 700), "Right and bottom authored scene edges remain reachable");
        camera.Pan(new(100000, 100000));
        Apply(world, camera);
        Near(world.Position, new Vector2(74, 62) * world.Scale, "Left and top authored scene edges remain reachable");
        camera.Resize(new(5000, 4000));
        Apply(world, camera);
        Require(camera.Scroll == default, "Small world never acquires negative scroll range");
        Near(world.Position - new Vector2(74, 62) * world.Scale,
            (new Vector2(5000, 4000) - new Vector2(1300, 904) * world.Scale) / 2,
            "Small scene centers including its asymmetric vertical art insets");
        viewport.QueueFree();
    }

    private void CheckGestureTransform()
    {
        var world = new Node2D();
        AddChild(world);
        var camera = new BattlefieldCamera(new(1152, 768), new(74, 62, 74, 74), new(500, 400));
        camera.Resize(new(800, 600));
        Apply(world, camera);
        var clicks = 0;
        var leaves = 0;
        CameraPoint? clicked = null;
        using var gestures = new BattlefieldGestures(new(p => camera.ToField(p), (p, button) => { if (button == 0) { clicks++; clicked = p; } }, _ => { }, () => leaves++, delta => { camera.Pan(delta); Apply(world, camera); }, notches => camera.QueueZoom(notches * .1)));
        gestures.PointerDown(1, 0, new(400, 300));
        gestures.PointerMove(1, new(394, 300));
        gestures.PointerUp(1, 0, new(394, 300));
        Require(clicks == 0 && leaves == 1, "Exact six screen pixels pan without selection");
        Near(ToVector(camera.Position), new Vector2(504.8f, 400), "Six screen pixels convert to 4.8 logical pixels at source zoom");
        gestures.PointerDown(1, 0, new(400, 300));
        Require(clicks == 0, "Press does not select before release");
        gestures.PointerUp(1, 0, new(400, 300));
        Require(clicks == 1, "Stationary release selects once");
        Near(ToVector(clicked!.Value), ToVector(camera.Position), "Release projects through current panned geometry");
        gestures.PointerDown(1, 0, new(400, 300));
        gestures.Cancel();
        gestures.PointerUp(1, 0, new(400, 300));
        Require(clicks == 1, "Focus-loss/cancel prevents stranded click");
        gestures.Wheel(-123, true, false);
        gestures.Wheel(-1, false, true);
        camera.ApplyPendingZoom();
        Apply(world, camera);
        Near(world.Scale, new Vector2(1.45f, 1.45f), "Ctrl and Meta wheel share additive notches independent of magnitude");
        gestures.Dispose();
        gestures.PointerDown(1, 0, new(400, 300));
        gestures.PointerUp(1, 0, new(400, 300));
        Require(clicks == 1, "Disposed pointer owner cannot select");
        world.QueueFree();
    }

    private void CheckFollow()
    {
        using var state = JsonDocument.Parse("""{"players":[{"id":1,"controller":"human"}],"units":[{"id":7,"owner":1,"hp":10,"x":1,"y":1}]}""");
        using var action = JsonDocument.Parse("""{"kind":"command","unit":7,"path":[{"x":1,"y":1},{"x":20,"y":15}]}""");
        var before = state.RootElement.GetRawText();
        CameraPoint At(CameraPoint cell) => new((cell.X + .5) * 32, (cell.Y + .5) * 32);
        var camera = new BattlefieldCamera(new(1152, 768), new(74, 62, 74, 74), new(500, 400));
        camera.Resize(new(600, 400));
        var transition = new BattlePresentationTransition(7, state.RootElement, action.RootElement, [], state.RootElement, false);
        Require(BattlefieldFocus.CommandDestination(transition, At) is null, "Human commands do not follow");
        var destination = BattlefieldFocus.CommandDestination(transition with
        {
            IsComputerAction = true
        }, At);
        Require(destination == At(new(20, 15)), "AI follows only complete command path destination");
        camera.CenterOn(destination!.Value);
        Near(ToVector(camera.Position), ToVector(At(new(20, 15))), "AI destination uses the same camera geometry");
        var hidden = new BattlePresentationTransition(8, state.RootElement, action.RootElement, [], state.RootElement, true, new System.Collections.Generic.HashSet<int>());
        Require(BattlefieldFocus.CommandDestination(hidden, At) is null, "Canonical fog IDs suppress hidden AI camera locations");
        Require(before == state.RootElement.GetRawText(), "Camera focus reads detached facts without mutating rules");
    }

    private static void Apply(Node2D world, BattlefieldCamera camera)
    {
        world.Scale = Vector2.One * (float)camera.Zoom;
        world.Position = ToVector(camera.FieldOrigin);
    }
    private static Vector2 ToVector(CameraPoint point) => new((float)point.X, (float)point.Y);
    private void Near(Vector2 actual, Vector2 expected, string message) => Require(actual.DistanceTo(expected) <= .001f,
        $"{message}: expected {expected}, actual {actual}");
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new Exception(message);
    }
}
