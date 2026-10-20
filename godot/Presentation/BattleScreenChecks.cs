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
            CheckPresentationTransitions();
            GD.Print("Native BattleScreen real-session integration passed.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void CheckPresentationTransitions()
    {
        var level = PresentationJson.Load<JsonObject>("res://data/grayflag-level.json");
        var engine = new GrayFlagEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var session = new GrayFlagSession(engine, level);
        var screen = new BattleScreen(); AddChild(screen);
        screen.Board.AdvanceAnimationsAutomatically = false;
        void Bind(GrayFlagSession value) => screen.BindSession(value, AssetCatalog.Load(), level,
            AuthoredScene.Load(), EnvironmentCatalog.Load(), _ => false);
        Bind(session);
        var order = session.GetLegalOrders(2).First(choice => choice.PresentationRole == "move"
            && choice.Target is { } at && at != new BoardCell(5, 8));
        Require(screen.RecentMessages.SequenceEqual(new[] { session.GetSnapshot().InitialLogMessage }),
            "New battle log must contain only the original opening line.");
        screen.SubmitOrder(order);
        Require(screen.RecentMessages.Count == 1, "Event text must wait until its animation completes.");
        Require(screen.IsPresenting && screen.Board.IsPresenting, "Accepted move must hold the presentation gate.");
        var revision = session.GetSnapshot().Revision;
        screen.SubmitOrder(order); screen.SelectCell(new BoardCell(5, 8)); screen.Refresh();
        Require(session.GetSnapshot().Revision == revision && screen.SelectedUnitId is null,
            "Repeated clicks and refresh must not dispatch or change selection during playback.");
        for (var step = 0; step < 100 && screen.IsPresenting; step++) screen.Board.AdvancePresentation(0.1);
        Require(!screen.IsPresenting && !screen.Board.IsPresenting && screen.DisplayedRevision == revision,
            "Timeline completion must settle the committed revision and release input.");
        var expectedLog = new Feedback.BattleLog();
        expectedLog.Add(session.GetSnapshot().InitialLogMessage!);
        foreach (var line in session.GetSnapshot().Transition!.EventDescriptions) expectedLog.Add(line);
        Require(screen.RecentMessages.SequenceEqual(expectedLog.Recent()), "Descriptions must append once in receipt order after presentation.");
        screen.Refresh();
        Require(!screen.IsPresenting && screen.RecentMessages.SequenceEqual(expectedLog.Recent()), "Refreshing a settled revision must not replay it.");
        var superseded = new GrayFlagSession(engine, level);
        var replaceable = new ReplaceableSession(superseded);
        screen.BindSession(replaceable, AssetCatalog.Load(), level, AuthoredScene.Load(), EnvironmentCatalog.Load(), _ => false);
        screen.SubmitOrder(order);
        var replacement = superseded.GetSnapshot() with { Revision = 9, Transition = null };
        replaceable.Replacement = replacement;
        screen.Refresh(); screen.Board.AdvancePresentation(100);
        Require(!screen.IsPresenting && screen.DisplayedRevision == 9,
            "A newer transition-free snapshot must invalidate older async completion.");
        screen.Refresh();
        Require(!screen.IsPresenting, "A transition-free revision must remain consumed.");
        var canceled = new GrayFlagSession(engine, level); Bind(canceled); screen.SubmitOrder(order);
        screen.Board.CancelPresentation();
        Require(!screen.IsPresenting && screen.DisplayedRevision == canceled.GetSnapshot().Revision,
            "Same-session cancellation must settle both board and controls to committed state.");
        var fresh = new GrayFlagSession(engine, level); Bind(fresh); screen.SubmitOrder(order);
        Require(screen.IsPresenting, "Second timeline did not start.");
        var loaded = GrayFlagSession.FromSave(engine, fresh.Save()); Bind(loaded);
        Require(!screen.IsPresenting && !screen.Board.IsPresenting && screen.DisplayedRevision == 0,
            "Rebinding a loaded session must cancel old playback without replaying its history.");
        screen.BindSession(loaded, AssetCatalog.Load(), level, AuthoredScene.Load(), EnvironmentCatalog.Load(), _ => false,
            initialLogMessage: "已读取第 1 回合的存档");
        Require(screen.RecentMessages.SequenceEqual(new[] { "已读取第 1 回合的存档" }), "Load must clear history and keep one load line.");
        screen.Board.AdvancePresentation(100);
        Require(screen.DisplayedRevision == 0, "Stale timeline completion overwrote the rebound view.");
        var last = new GrayFlagSession(engine, level); Bind(last); screen.SubmitOrder(order);
        var saves = 0;
        screen.BindApplicationActions(() => saves++);
        var staleSave = Buttons(screen).Single(button => button.Text == "保存");
        var lastRevision = last.GetSnapshot().Revision;
        RemoveChild(screen);
        staleSave.EmitSignal(Button.SignalName.Pressed);
        Require(saves == 0, "Unmounted application callbacks must not save or load.");
        screen.SubmitOrder(order);
        Require(last.GetSnapshot().Revision == lastRevision, "Unmounted callbacks must not dispatch.");
        Require(!screen.IsPresenting && !screen.Board.IsPresenting, "Unmount must release all presentation waiters.");
        screen.QueueFree();
    }

    private sealed class ReplaceableSession(IBattleSession inner) : IBattleSession
    {
        public BattleSnapshot? Replacement { get; set; }
        public BattleSnapshot GetSnapshot() => Replacement ?? inner.GetSnapshot();
        public System.Collections.Generic.IReadOnlyList<BattleOrderChoice> GetLegalOrders(int? selectedUnitId) => inner.GetLegalOrders(selectedUnitId);
        public BattleDispatchResult Dispatch(System.Text.Json.JsonElement action, long expectedRevision) => inner.Dispatch(action, expectedRevision);
    }

    private void Check()
    {
        var level = PresentationJson.Load<JsonObject>("res://data/grayflag-level.json");
        var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
        var engine = new GrayFlagEngine(packs);
        var session = new GrayFlagSession(engine, level);
        var screen = new BattleScreen();
        screen.Board.AnimationsEnabled = false; // This suite isolates synchronous binding, not timeline behavior.
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
