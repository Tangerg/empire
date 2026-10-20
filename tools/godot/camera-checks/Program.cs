using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Presentation;

if (args.Length != 1) throw new ArgumentException("Provide the live TS camera oracle JSON path.");
using var document = JsonDocument.Parse(File.ReadAllText(args[0]));
var assertions = 0;
foreach (var test in document.RootElement.GetProperty("cameraCases").EnumerateArray())
{
    var camera = new BattlefieldCamera(Point(test.GetProperty("fieldSize")), Insets(test.GetProperty("sceneInsets")), Point(test.GetProperty("initialFocus")));
    var index = 0;
    foreach (var operation in test.GetProperty("operations").EnumerateArray())
    {
        switch (operation.GetProperty("kind").GetString())
        {
            case "resize": camera.Resize(Point(operation.GetProperty("viewport")), Insets(operation.GetProperty("padding"))); break;
            case "center": camera.CenterOn(Point(operation.GetProperty("point"))); break;
            case "pan": camera.Pan(Point(operation.GetProperty("delta"))); break;
            case "reveal": camera.Reveal(Point(operation.GetProperty("point")), operation.GetProperty("margin").GetDouble()); break;
            case "queueZoom": camera.QueueZoom(operation.GetProperty("delta").GetDouble()); break;
            case "applyZoom": camera.ApplyPendingZoom(); break;
            default: throw new Exception("Unknown camera operation.");
        }
        var expected = test.GetProperty("expected")[index++];
        var context = $"{test.GetProperty("name").GetString()} operation {index}";
        Near(camera.Zoom, expected.GetProperty("zoom").GetDouble(), context + " zoom");
        EqualPoint(camera.Scroll, Point(expected.GetProperty("scroll")), context + " scroll");
        EqualPoint(camera.FieldOrigin, Point(expected.GetProperty("fieldOrigin")), context + " field origin");
        EqualPoint(camera.Position, Point(expected.GetProperty("position")), context + " position");
        Require(camera.HasViewport == expected.GetProperty("hasViewport").GetBoolean(), context + " first layout");
        var field = new CameraPoint(17.25, -3.5);
        EqualPoint(camera.ToField(new CameraPoint(camera.FieldOrigin.X + field.X * camera.Zoom,
            camera.FieldOrigin.Y + field.Y * camera.Zoom)), field, context + " inverse");
    }
}

foreach (var test in document.RootElement.GetProperty("pointerCases").EnumerateArray())
{
    var box = test.GetProperty("box");
    var scene = test.GetProperty("scene");
    var callbacks = new JsonArray();
    CameraPoint? Project(CameraPoint point)
    {
        var width = box.GetProperty("width").GetDouble();
        var height = box.GetProperty("height").GetDouble();
        if (width <= 0 || height <= 0) return null;
        var scale = Math.Min(width / scene.GetProperty("width").GetDouble(), height / scene.GetProperty("height").GetDouble());
        var left = (width - scene.GetProperty("width").GetDouble() * scale) / 2;
        var top = (height - scene.GetProperty("height").GetDouble() * scale) / 2;
        return new CameraPoint((point.X - box.GetProperty("left").GetDouble() - left) / scale,
            (point.Y - box.GetProperty("top").GetDouble() - top) / scale);
    }
    using var gestures = new BattlefieldGestures(Project,
        (point, button) => callbacks.Add(new JsonObject { ["kind"] = "press", ["point"] = JsonPoint(point), ["button"] = button }),
        point => callbacks.Add(new JsonObject { ["kind"] = "move", ["point"] = point is { } p ? JsonPoint(p) : null }),
        () => callbacks.Add(new JsonObject { ["kind"] = "leave" }),
        delta => callbacks.Add(new JsonObject { ["kind"] = "pan", ["delta"] = JsonPoint(delta) }),
        notches => callbacks.Add(new JsonObject { ["kind"] = "scale", ["notches"] = notches }));
    var index = 0;
    foreach (var operation in test.GetProperty("operations").EnumerateArray())
    {
        callbacks.Clear();
        var prevented = false;
        var id = operation.TryGetProperty("pointerId", out var pointerId) ? pointerId.GetInt64() : 1;
        var button = operation.TryGetProperty("button", out var sourceButton) ? sourceButton.GetInt32() : 0;
        var point = operation.TryGetProperty("point", out var at) ? Point(at) : default;
        switch (operation.GetProperty("kind").GetString())
        {
            case "down": prevented = gestures.PointerDown(id, button, point, !operation.TryGetProperty("isPrimary", out var primary) || primary.GetBoolean()); break;
            case "move": prevented = gestures.PointerMove(id, point); break;
            case "up": prevented = gestures.PointerUp(id, button, point); break;
            case "leave": gestures.PointerLeave(); break;
            case "cancel": case "lostCapture": gestures.Cancel(id); break;
            case "wheel": prevented = gestures.Wheel(operation.GetProperty("deltaY").GetDouble(), Flag(operation, "ctrlKey"), Flag(operation, "metaKey")); break;
            case "dispose": gestures.Dispose(); break;
            case "setBox": box = operation.GetProperty("box"); break;
            default: throw new Exception("Unknown pointer operation.");
        }
        var expected = test.GetProperty("expected")[index++];
        var context = $"{test.GetProperty("name").GetString()} operation {index}";
        EqualJson(JsonSerializer.SerializeToElement(callbacks), expected.GetProperty("callbacks"), context + " ordered callbacks");
        Require(gestures.IsDragging == expected.GetProperty("panning").GetBoolean(), context + " dragging");
        Require(prevented == expected.GetProperty("defaultPrevented").GetBoolean(), context + " default prevention");
    }
}

CheckFocus();
CheckBoundaries();
Console.WriteLine($"Camera/gesture differential checks passed: {document.RootElement.GetProperty("cameraCases").GetArrayLength()} camera cases, {document.RootElement.GetProperty("pointerCases").GetArrayLength()} pointer cases, {assertions} assertions.");

void CheckFocus()
{
    using var state = JsonDocument.Parse("""
        {"players":[{"id":2,"controller":"ai"},{"id":3,"controller":"human"},{"id":1,"controller":"human"}],
        "units":[{"id":1,"owner":3,"hp":10,"x":2,"y":4},{"id":2,"owner":3,"hp":1,"x":4,"y":8},
        {"id":3,"owner":3,"hp":0,"x":99,"y":99},{"id":4,"owner":1,"hp":10,"x":90,"y":90}]}
        """);
    CameraPoint At(CameraPoint p) => new((p.X + .5) * 32, (p.Y + .5) * 32);
    EqualPoint(BattlefieldFocus.Initial(state.RootElement, At, new(9, 7)), At(new(3, 6)), "first human living unit centroid");
    using var empty = JsonDocument.Parse("""{"players":[{"id":2,"controller":"ai"}],"units":[]}""");
    EqualPoint(BattlefieldFocus.Initial(empty.RootElement, At, new(9, 7)), new(9, 7), "no human uses explicit fallback");
    using var command = JsonDocument.Parse("""{"kind":"command","unit":1,"path":[{"x":2,"y":4},{"x":8,"y":9}]}""");
    var transition = new BattlePresentationTransition(1, state.RootElement, command.RootElement, [], state.RootElement, true);
    EqualPoint(BattlefieldFocus.CommandDestination(transition, At)!.Value, At(new(8, 9)), "AI command follows final point");
    Require(BattlefieldFocus.CommandDestination(transition with { IsComputerAction = false }, At) is null, "human movement never follows");
    var hidden = new BattlePresentationTransition(2, state.RootElement, command.RootElement, [], state.RootElement, true, new HashSet<int> { 2 });
    Require(BattlefieldFocus.CommandDestination(hidden, At) is null, "hidden AI path does not reveal movement");
    using var absent = JsonDocument.Parse("""{"kind":"command","unit":99,"path":[{"x":2,"y":4},{"x":8,"y":9}]}""");
    Require(BattlefieldFocus.CommandDestination(new(3, state.RootElement, absent.RootElement, [], state.RootElement, true), At) is null, "absent pre-action actor does not follow");
    using var stationary = JsonDocument.Parse("""{"kind":"command","unit":1,"path":[{"x":2,"y":4}]}""");
    Require(BattlefieldFocus.CommandDestination(new(4, state.RootElement, stationary.RootElement, [], state.RootElement, true), At) is null, "single-cell command does not follow");
    using var other = JsonDocument.Parse("""{"kind":"endTurn"}""");
    Require(BattlefieldFocus.CommandDestination(new(5, state.RootElement, other.RootElement, [], state.RootElement, true), At) is null, "non-command does not follow");
}

void CheckBoundaries()
{
    var camera = new BattlefieldCamera(new(1152, 768), new(74, 62, 74, 74), new(300, 300));
    camera.Resize(new(0, 0));
    Require(!camera.HasViewport && camera.Zoom == 1.25, "unusable first layout does not open or fit");
    camera.Resize(new(800, 600));
    camera.QueueZoom(99);
    Require(camera.ApplyPendingZoom() && camera.Zoom == 2.2, "maximum zoom applies once");
    camera.QueueZoom(.1);
    Require(!camera.ApplyPendingZoom(), "clamped zoom does not report a visual scale change");
    camera.QueueZoom(-99);
    Require(camera.ApplyPendingZoom() && camera.Zoom == .5, "minimum zoom");
    camera.QueueZoom(.1);
    camera.QueueZoom(-.1);
    Require(!camera.ApplyPendingZoom(), "canceling zoom deltas share one frame");
    Throws(() => camera.QueueZoom(double.NaN), "nonfinite zoom is rejected");
    Throws(() => camera.Resize(new(double.PositiveInfinity, 10)), "nonfinite viewport rejected");
    Throws(() => camera.Reveal(new(0, 0), -1), "negative reveal margin rejected");
    var clicks = 0;
    using var gestures = new BattlefieldGestures(p => p, (_, _) => clicks++, _ => { }, () => { }, _ => { }, _ => { });
    gestures.PointerDown(5, 0, new(0, 0)); gestures.Cancel(); gestures.PointerUp(5, 0, new(0, 0));
    Require(clicks == 0 && gestures.ActivePointerId is null, "screen cancellation suppresses pending click");
}

void Throws(Action action, string context)
{
    try { action(); } catch (ArgumentOutOfRangeException) { assertions++; return; }
    throw new Exception(context);
}
void EqualJson(JsonElement actual, JsonElement expected, string context)
{
    Require(actual.ValueKind == expected.ValueKind, context + " kind");
    switch (actual.ValueKind)
    {
        case JsonValueKind.Array:
            Require(actual.GetArrayLength() == expected.GetArrayLength(), context + " length");
            for (var i = 0; i < actual.GetArrayLength(); i++) EqualJson(actual[i], expected[i], context + $"[{i}]");
            break;
        case JsonValueKind.Object:
            Require(actual.EnumerateObject().Count() == expected.EnumerateObject().Count(), context + " properties");
            foreach (var property in expected.EnumerateObject()) EqualJson(actual.GetProperty(property.Name), property.Value, context + "." + property.Name);
            break;
        case JsonValueKind.Number: Near(actual.GetDouble(), expected.GetDouble(), context); break;
        default: Require(actual.GetRawText() == expected.GetRawText(), context); break;
    }
}
void EqualPoint(CameraPoint actual, CameraPoint expected, string context)
{
    Near(actual.X, expected.X, context + " x"); Near(actual.Y, expected.Y, context + " y");
}
void Near(double actual, double expected, string context) => Require(Math.Abs(actual - expected) <= 1e-8, $"{context}: expected {expected:R}, actual {actual:R}");
void Require(bool condition, string context)
{
    assertions++;
    if (!condition) throw new Exception(context);
}
CameraPoint Point(JsonElement p) => new(p.GetProperty("x").GetDouble(), p.GetProperty("y").GetDouble());
CameraInsets Insets(JsonElement p) => new(p.GetProperty("left").GetDouble(), p.GetProperty("top").GetDouble(), p.GetProperty("right").GetDouble(), p.GetProperty("bottom").GetDouble());
JsonObject JsonPoint(CameraPoint point) => new() { ["x"] = point.X, ["y"] = point.Y };
bool Flag(JsonElement value, string key) => value.TryGetProperty(key, out var flag) && flag.GetBoolean();
