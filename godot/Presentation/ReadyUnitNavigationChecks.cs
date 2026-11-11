#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>Native input and layout over real C01 sessions; no save files or synthetic readiness rules.</summary>
public partial class ReadyUnitNavigationChecks : Node
{
    private int assertions;

    private void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        assertions++;
    }

    private async Task Settle()
    {
        for (var frame = 0; frame < 6; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Activate(Button button, bool touch = false)
    {
        Require(button.IsVisibleInTree() && !button.Disabled, "Native input targets a visible enabled control.");
        var point = button.GetGlobalRect().GetCenter();
        if (!touch)
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        foreach (var down in new[] { true, false })
        {
            using InputEvent input = touch
                ? new InputEventScreenTouch { Index = 0, Position = point, Pressed = down }
                : new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = point, GlobalPosition = point, Pressed = down };
            Input.ParseInputEvent(input);
        }
        await Settle();
    }

    public override async void _Ready()
    {
        try
        {
            await Check();
            GD.Print($"READY_UNIT_NAVIGATION passed: {assertions} assertions; real mouse/touch/keyboard, counts, wait/undo, exhaustion, AI/deployment, stale input, rebinding and 960/1280/1440 layouts.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task Check()
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var level = BattleTextChecks.OpeningLevel();
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var screen = new BattleScreen();
        AddChild(screen);
        screen.Board.AnimationsEnabled = false;
        var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
        BattleScreenRules Rules(GrayFlagSession value) => new(text, presentation.Battle, PortraitCatalogResources.Load(), value,
            new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric));
        screen.BindSession(Rules(session), level, scene);
        Button Find(string name) => screen.FindChild(name, true, false) as Button
            ?? throw new InvalidOperationException("Missing native control: " + name);
        int[] Ready() => session.GetTacticalQueries(0).Actors().ToArray();
        void Count(int expected)
        {
            var next = Find("NextUnit");
            Require(next.Text == (expected > 0 ? text.Format("ui.battle.next_unit", expected) : text.Format("ui.battle.no_ready_units"))
                && next.Disabled == (expected == 0), "Displayed readiness and enablement match canonical actors exactly.");
        }
        var original = EcmaJson.Stringify(session.Snapshot());
        var actors = Ready();
        Require(actors.Length >= 3, "Opening campaign has multiple actors for order and wraparound checks.");
        Count(actors.Length);
        foreach (var size in new[] { new Vector2I(960, 640), new Vector2I(1280, 720), new Vector2I(1440, 900) })
        {
            GetWindow().Size = size;
            await Settle();
            var bounds = new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size);
            var global = screen.GetNode<Control>("GlobalOrders");
            Require(bounds.Encloses(global.GetGlobalRect()), "The readiness rail fits the native viewport at " + size + ".");
            foreach (var button in global.FindChildren("*", "Button", true, false).OfType<Button>())
                Require(global.GetGlobalRect().Encloses(button.GetGlobalRect())
                    && button.Size.X >= button.GetCombinedMinimumSize().X && button.Size.Y >= button.GetCombinedMinimumSize().Y,
                    "Native containers show full text and hit areas without clipping: " + button.Name + ".");
            var next = Find("NextUnit").GetGlobalRect();
            Require(next.End.Y <= Find("EndTurn").GetGlobalRect().Position.Y,
                "Read-only navigation is a separate row above consequential turn controls.");
        }
        GetWindow().Size = new Vector2I(960, 640);
        await Settle();
        await Activate(Find("NextUnit"));
        Require(screen.SelectedUnitId == actors[0], "Mouse finds the same first source-ordered actor as Space.");
        await Activate(Find("NextUnit"), touch: true);
        Require(screen.SelectedUnitId == actors[1], "One native quick touch advances once despite emulated mouse delivery.");
        var nextButton = Find("NextUnit");
        nextButton.GrabFocus();
        foreach (var down in new[] { true, false })
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = down }, true);
        await Settle();
        Require(screen.SelectedUnitId == actors[2] && nextButton.HasFocus(),
            "Native Enter activates the same stable navigation control without losing keyboard focus.");
        for (var index = 3; index <= actors.Length; index++)
            await Activate(Find("NextUnit"));
        Require(screen.SelectedUnitId == actors[0] && session.Revision == 0 && session.Events.Count == 0
            && EcmaJson.Stringify(session.Snapshot()) == original,
            "Cycling wraps in canonical order and changes neither state, revision nor event history.");

        GetViewport().GuiReleaseFocus();
        foreach (var down in new[] { true, false })
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Space, PhysicalKeycode = Key.Space, Pressed = down }, true);
        await Settle();
        Require(screen.SelectedUnitId == actors[1] && session.Revision == 0 && EcmaJson.Stringify(session.Snapshot()) == original,
            "Battlefield Space continues the exact same cohort after mouse, touch and Enter without committing an order.");
        var rail = screen.GetNode<Control>("GlobalOrders").GetGlobalRect();
        var hud = screen.GetNode<TacticalHud>("TacticalHud");
        foreach (var control in hud.GetChildren().OfType<Control>().Where(control => control.IsVisibleInTree()
            && control.Name.ToString() is "PortraitTray" or "OrderTray" or "InstructionTray" or "forecastPlate"))
            Require(!rail.Intersects(control.GetGlobalRect()), "The selected-unit tray and global navigation rail do not overlap at 960x640: " + control.Name + ".");

        var selected = screen.SelectedUnitId;
        Find("NextUnit").EmitSignal(BaseButton.SignalName.Pressed);
        screen.SetApplicationBusy(true);
        await Settle();
        Require(screen.SelectedUnitId == selected && Find("NextUnit").Disabled,
            "A queued navigation click cannot cross the application-busy boundary.");
        screen.SetApplicationBusy(false);
        await Settle();

        async Task Wait(int id)
        {
            GetViewport().GuiReleaseFocus();
            var unit = session.GetTacticalQueries(0).Unit(id)!;
            screen.SelectHit(TacticalHit.Unit(unit.At, id));
            await Settle();
            var wait = screen.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.GetMeta("hud_key", "").AsString() == "commands/wait");
            await Activate(wait);
            Require(!Ready().Contains(id), "An ordinary native Wait removes the acted unit from the canonical next-unit set.");
            Count(Ready().Length);
        }
        await Wait(actors[0]);
        Require(Ready().Length == actors.Length - 1, "A committed wait reduces readiness once.");
        await Activate(Find("NextUnit"));
        Require(screen.SelectedUnitId == actors[1], "Next unit skips the actor that already waited.");
        await Activate(Find("Undo"));
        Count(actors.Length);
        Require(EcmaJson.Stringify(session.Snapshot()) == original, "Undo restores exact battle state and the ready cohort.");
        foreach (var actor in actors)
            await Wait(actor);
        Count(0);
        var exhaustedSelection = screen.Selection;
        Find("NextUnit").EmitSignal(BaseButton.SignalName.Pressed);
        await Settle();
        Require(ReferenceEquals(screen.Selection, exhaustedSelection), "Even an emitted disabled signal cannot select a completed unit.");
        await Activate(Find("Undo"));
        Count(1);
        var stale = Find("NextUnit");
        var last = Ready().Single();
        var external = new TacticalSelectionController(session.GetTacticalQueries(0));
        external.Click(external.Stamp, TacticalHit.Unit(session.GetTacticalQueries(0).Unit(last)!.At, last));
        var externalWait = external.ChooseCommand(external.Stamp, "wait").Request!;
        Require(session.Dispatch(externalWait.Action!.Value, session.Revision).Accepted,
            "Fixture advances the real session without first refreshing the view.");
        var beforeStale = screen.Selection;
        stale.EmitSignal(BaseButton.SignalName.Pressed);
        await Settle();
        Require(ReferenceEquals(screen.Selection, beforeStale), "A stale revision callback cannot use old ready-unit facts.");
        screen.Refresh();
        await Settle();
        Count(0);
        await Activate(Find("EndTurn"));
        Require(session.IsComputerTurn && screen.FindChild("NextUnit", true, false) is null,
            "AI turns do not advertise enemy actors as units the player can command.");

        var nextSession = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        screen.BindSession(Rules(nextSession), level, scene);
        Find("NextUnit").EmitSignal(BaseButton.SignalName.Pressed);
        screen.BindSession(Rules(nextSession), level, scene);
        await Settle();
        Require(screen.SelectedUnitId is null && nextSession.Revision == 0,
            "A deferred click from a prior screen binding cannot select in the replacement binding.");
        var deploymentLevel = level.DeepClone().AsObject();
        var own = deploymentLevel["units"]!.AsArray().OfType<JsonObject>().Where(unit => unit["owner"]!.GetValue<int>() == 1);
        var cells = new JsonArray(own.Select(unit => (JsonNode)new JsonObject { ["x"] = unit["x"]!.GetValue<int>(), ["y"] = unit["y"]!.GetValue<int>() }).ToArray());
        deploymentLevel["scenario"]!.AsObject()["zones"] = new JsonArray(new JsonObject { ["id"] = "ready-check-zone", ["cells"] = cells });
        deploymentLevel["deployment"] = new JsonObject { ["order"] = new JsonArray(1), ["zones"] = new JsonArray(new JsonObject { ["player"] = 1, ["zone"] = "ready-check-zone" }) };
        var deployment = new GrayFlagSession(new(engine, presentation, new JsonArray()), deploymentLevel);
        screen.BindSession(Rules(deployment), deploymentLevel, scene);
        await Settle();
        Require(screen.FindChild("NextUnit", true, false) is null && !Find("FinishDeployment").Disabled,
            "Deployment exposes its own roster and completion control, never a false ready-unit count.");
        screen.BindSession(Rules(nextSession), level, scene);
        Find("NextUnit").EmitSignal(BaseButton.SignalName.Pressed);
        RemoveChild(screen);
        screen.Free();
        await Settle();
        Require(!GodotObject.IsInstanceValid(screen), "Pending navigation cannot resurrect an exited screen.");
    }
}
