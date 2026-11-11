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
            CheckBoundsFraming();
            CheckRegisteredOpening();
            CheckGestureTransform();
            CheckFollow();
            GD.Print($"Native camera checks passed: {assertions} assertions.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void CheckBoundsFraming()
    {
        var camera = new BattlefieldCamera(new(1800, 1400), new(74, 62, 74, 74), new(600, 500));
        var viewport = new CameraPoint(960, 640);
        var padding = new CameraInsets(16, 110, 16, 134);
        camera.Resize(viewport, padding);
        var minimum = new CameraPoint(180, 160);
        var maximum = new CameraPoint(1000, 700);
        const double margin = 12;
        // A skewed set's bounds, not the mean of its three points, determine framing.
        var skewed = new[] { minimum, new CameraPoint(1000, 160), maximum };
        camera.QueueZoom(.8);
        Require(camera.FrameBounds(minimum, maximum, margin), "A wide objective set must fit the HUD-safe viewport when legal zoom permits.");
        Require(camera.Zoom < BattlefieldCamera.InitialZoom && camera.Zoom >= BattlefieldCamera.MinimumZoom,
            "Framing shrinks only as far as needed within the legal zoom range.");
        foreach (var point in skewed)
        {
            var x = camera.FieldOrigin.X + point.X * camera.Zoom;
            var y = camera.FieldOrigin.Y + point.Y * camera.Zoom;
            Require(x >= padding.Left + margin - .000001 && x <= viewport.X - padding.Right - margin + .000001
                && y >= padding.Top + margin - .000001 && y <= viewport.Y - padding.Bottom - margin + .000001,
                "Every skewed objective extent must fit the actual post-clamp screen transform.");
        }
        var zoom = camera.Zoom;
        var scroll = camera.Scroll;
        Require(!camera.ApplyPendingZoom() && camera.Zoom == zoom && camera.Scroll == scroll,
            "A deliberate objective framing cancels stale queued wheel zoom.");
        Require(camera.FrameBounds(minimum, maximum, margin) && camera.Zoom == zoom && camera.Scroll == scroll,
            "Repeated objective framing must be stable.");
        camera.FrameBounds(new(500, 400), new(500, 400), margin);
        Require(camera.Zoom == zoom, "A single target must neither shrink nor zoom back in.");
        camera.Resize(new(1920, 1080), padding);
        Require(camera.FrameBounds(minimum, maximum, margin) && camera.Zoom == zoom,
            "A larger viewport must not enlarge an already readable objective set.");
        camera.Resize(viewport, padding);
        Require(!camera.FrameBounds(new(0, 0), new(1800, 1400), margin)
            && camera.Zoom == BattlefieldCamera.MinimumZoom,
            "An unfit set reports failure instead of violating the minimum zoom limit.");
        var edge = new BattlefieldCamera(new(1800, 1400), default, new(500, 400));
        edge.Resize(viewport, padding);
        Require(!edge.FrameBounds(new(0, 0), new(0, 0), margin)
            && edge.Zoom == BattlefieldCamera.InitialZoom,
            "A scroll-clamped edge cannot falsely claim to provide unavailable screen margin.");
        var unlaid = new BattlefieldCamera(new(800, 600), default, new(400, 300));
        Require(!unlaid.FrameBounds(new(10, 10), new(50, 50), margin), "Framing before usable layout must report no fitted viewport.");
        foreach (var invalid in new Action[]
        {
            () => camera.FrameBounds(new(double.NaN, 0), maximum, margin),
            () => camera.FrameBounds(maximum, minimum, margin),
            () => camera.FrameBounds(minimum, maximum, -1),
            () => camera.FrameBounds(minimum, maximum, double.PositiveInfinity)
        })
        {
            try
            {
                invalid();
                throw new Exception("Invalid bounds framing input was accepted.");
            }
            catch (ArgumentOutOfRangeException) { assertions++; }
        }
    }

    private void CheckRegisteredOpening()
    {
        var camera = new BattlefieldCamera(new(1672 * .65, 941 * .65), new(74, 62, 74, 74), new(300, 350), frameInitialGround: true);
        camera.Resize(new(1180, 760), new(16, 110, 16, 134));
        Require(camera.Zoom == BattlefieldCamera.InitialZoom, "Registered opening must not shrink actors to fit.");
        Require(camera.FieldOrigin.X <= 0 && camera.FieldOrigin.X + 1672 * .65 * camera.Zoom >= 1180,
            "Registered opening left avoidable horizontal blank margin while cropping the opposite side.");
        Require(camera.FieldOrigin.Y <= 0 && camera.FieldOrigin.Y + 941 * .65 * camera.Zoom >= 760,
            "Registered opening left avoidable vertical blank margin.");
        camera.Pan(new(10000, 10000));
        var edge = camera.FieldOrigin;
        Require(edge.X > 0 && edge.Y > 0, "Initial framing must not remove edge panning space below HUD.");
        camera.Resize(new(1180, 760), new(16, 110, 16, 134));
        Require(camera.FieldOrigin == edge, "Later layout unexpectedly re-centered the user's camera.");
        camera.Reveal(new(0, 0), 20);
        Require(camera.FieldOrigin.X >= 16 && camera.FieldOrigin.Y >= 110,
            "Boundary actors cannot be revealed inside the HUD-safe area.");
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
        gestures.PointerDown(1, 0, new(400, 300));
        var beforeMismatch = camera.Position;
        gestures.PointerUp(2, 0, new(380, 300), allowClick: false);
        gestures.PointerUp(1, 1, new(380, 300), allowClick: false);
        Require(gestures.ActivePointerId == 1 && !gestures.IsDragging && camera.Position == beforeMismatch,
            "A suppressed release with the wrong pointer or button cannot move or release the active gesture.");
        gestures.PointerUp(1, 0, new(404, 300), allowClick: false);
        Require(clicks == 1 && gestures.ActivePointerId is null,
            "A matching HUD-owned short release suppresses its click and releases capture.");
        gestures.PointerDown(1, 0, new(400, 300));
        gestures.PointerMove(1, new(390, 300));
        var beforeFinalDelta = camera.Position;
        gestures.PointerUp(1, 0, new(380, 300), allowClick: false);
        Require(clicks == 1 && gestures.ActivePointerId is null,
            "A HUD-owned drag release ends capture without emitting a click.");
        Near(ToVector(camera.Position), ToVector(beforeFinalDelta) + new Vector2(8, 0),
            "Suppressing a click preserves the final drag delta at the current camera zoom.");
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
        using var stationary = JsonDocument.Parse("""{"kind":"command","unit":7,"path":[{"x":1,"y":1}]}""");
        using var activation = JsonDocument.Parse("""{"type":"attackActivated","attacker":7,"from":{"x":1,"y":1},"at":{"x":4,"y":3}}""");
        var attack = new BattlePresentationTransition(9, state.RootElement, stationary.RootElement, [activation.RootElement], state.RootElement, true);
        Require(BattlefieldFocus.StationaryAttack(attack, At) == (At(new(1, 1)), At(new(4, 3))), "Visible stationary AI attacks frame committed activation endpoints.");
        Require(BattlefieldFocus.StationaryAttack(attack with { IsComputerAction = false }, At) is null, "Human stationary attacks never steal camera focus.");
        Require(BattlefieldFocus.StationaryAttack(new(9, state.RootElement, stationary.RootElement, [], state.RootElement, true), At) is null, "Stationary waits and commands without committed activation do not move camera.");
        Require(BattlefieldFocus.StationaryAttack(new(9, state.RootElement, stationary.RootElement, [activation.RootElement], state.RootElement, true, new System.Collections.Generic.HashSet<int>()), At) is null, "Hidden stationary attackers never disclose coordinates through camera focus.");
        Require(BattlefieldFocus.StationaryAttack(new(9, state.RootElement, action.RootElement, [activation.RootElement], state.RootElement, true), At) is null, "Moving commands retain their existing destination-follow policy.");
        Require(BattlefieldFocus.StationaryAttack(new(9, state.RootElement, stationary.RootElement, [activation.RootElement], state.RootElement, true, new System.Collections.Generic.HashSet<int> { 7 }), At) == (At(new(1, 1)), At(new(1, 1))), "A visible attacker cannot expose a hidden aim cell through camera framing.");
        using var bothVisible = JsonDocument.Parse("""{"units":[{"id":7,"x":1,"y":1},{"id":8,"x":4,"y":3}]}""");
        Require(BattlefieldFocus.StationaryAttack(new(9, bothVisible.RootElement, stationary.RootElement, [activation.RootElement], bothVisible.RootElement, true, new System.Collections.Generic.HashSet<int> { 7, 8 }), At) == (At(new(1, 1)), At(new(4, 3))), "Fog-visible defender permits framing both visible combat endpoints.");
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
