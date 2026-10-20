#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

/// <summary>Native end-to-end UI binding checks using the real application session, never a demo session.</summary>
public partial class BattleScreenChecks : Node
{
    public override void _Ready()
    {
        try
        {
            Check();
            GD.Print("Native BattleScreen real-session integration passed.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void Check()
    {
        var level = PresentationJson.Load<JsonObject>("res://data/grayflag-level.json");
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new GrayFlagEngine(packs);
        var session = new GrayFlagSession(engine, level);
        var screen = new BattleScreen();
        AddChild(screen);
        screen.BindSession(session, AssetCatalog.Load(), level, AuthoredScene.Load(), EnvironmentCatalog.Load(),
            facing => facing is not null && engine.Grids.Get("square4").Step(new Coord(0, 0), facing).X < 0);
        screen.ConfigureTerrain(WorldMaterialsManifest.Load());
        Require(screen.GetNode<ColorRect>("Backdrop").ZIndex < -300,
            "The screen backdrop must be below all negative-depth terrain layers.");
        var before = session.GetSnapshot();
        screen.SelectCell(new BoardCell(5, 8));
        Require(screen.SelectedUnitId == 2 && session.GetSnapshot().Revision == before.Revision,
            "Selecting Torren must not advance canonical state.");
        var order = session.GetLegalOrders(2).First(choice => choice.PresentationRole == "move"
            && choice.Target is { } cell && cell != new BoardCell(5, 8));
        screen.SelectCell(order.Target!.Value);
        Require(session.GetSnapshot().Revision == before.Revision,
            "Selecting a legal target must not commit before confirmation.");
        var confirmation = Buttons(screen).Single(button => button.Text == order.Label);
        confirmation.EmitSignal(Button.SignalName.Pressed);
        var after = session.GetSnapshot();
        Require(after.Revision == before.Revision + 1 && screen.DisplayedRevision == after.Revision,
            "A confirmed canonical command must refresh the displayed session revision.");
        var actor = after.State.GetProperty("units").EnumerateArray()
            .Single(unit => unit.GetProperty("id").GetInt32() == 2);
        Require(actor.GetProperty("x").GetInt32() == order.Target.Value.X
            && actor.GetProperty("y").GetInt32() == order.Target.Value.Y
            && actor.GetProperty("done").GetBoolean(),
            "The real move role must commit the complete wait-at-destination command.");
        var stale = session.Dispatch(order.Action, before.Revision);
        Require(!stale.Accepted && session.GetSnapshot().Revision == after.Revision,
            "An obsolete UI revision must not replay a command.");
        Require(before.State.GetProperty("units").EnumerateArray()
                .Single(unit => unit.GetProperty("id").GetInt32() == 2).GetProperty("x").GetInt32() == 5,
            "Earlier detached snapshots must remain immutable.");
        var endTurn = session.GetLegalOrders(null).Single(choice => choice.PresentationRole == "turn");
        var turnButton = Buttons(screen).Single(button => button.Text == endTurn.Label);
        turnButton.EmitSignal(Button.SignalName.Pressed);
        var nextTurn = session.GetSnapshot();
        Require(nextTurn.Revision == after.Revision + 1
            && nextTurn.State.GetProperty("currentPlayer").GetInt32() != after.State.GetProperty("currentPlayer").GetInt32()
            && screen.DisplayedRevision == nextTurn.Revision,
            "The sole turn button must commit through the real session and refresh current player.");
        screen.QueueFree();
    }
    private static System.Collections.Generic.IEnumerable<Button> Buttons(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button button) yield return button;
            foreach (var descendant in Buttons(child)) yield return descendant;
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
