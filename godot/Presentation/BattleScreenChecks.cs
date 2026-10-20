#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Presentation.Tactical;
using System.Collections.Generic;
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
            CheckInputContracts();
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

    private sealed class ReplaceableSession(ITacticalBattleSession inner) : ITacticalBattleSession
    {
        public BattleSnapshot? Replacement { get; set; }
        public ITacticalSelectionQueries GetTacticalQueries(long binding) => new StampedQueries(inner.GetTacticalQueries(binding), new(binding, GetSnapshot().Revision));
        public TacticalHudProjection ProjectHud(TacticalStamp stamp, TacticalSelection selection, BoardCell? cursor, int? inspectId, bool busy)
        {
            var source = inner.ProjectHud(stamp with { Revision = inner.GetSnapshot().Revision }, selection, cursor, inspectId, busy);
            return source with { Stamp = stamp, Sections = source.Sections.Select(section => section with {
                Rows = section.Rows.Select(row => row with { Intent = row.Intent is { } intent ? intent with { Stamp = stamp } : null }).ToArray()
            }).ToArray() };
        }
        public bool CanUndo => inner.CanUndo;
        public BattleDispatchResult Undo(long expectedRevision) => inner.Undo(expectedRevision);

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
        var queries = session.GetTacticalQueries(0);
        var destination = queries.MoveStops(2).First(cell => cell != new BoardCell(5,8)
            && queries.CommandsAt(2,cell) is { Count: 1 } commands && commands[0].SelfTargeted);
        var order = session.GetLegalOrders(2).First(choice => choice.Target == destination && choice.PresentationRole == "move");
        var staleTurnButton = Buttons(screen).Single(button => button.Text == "结束回合");
        screen.SelectCell(destination);
        var after = session.GetSnapshot();
        Require(after.Revision == before.Revision + 1 && screen.DisplayedRevision == after.Revision,
            "A sole self-targeted destination command dispatches without a confirmation menu.");
        var actor = after.State.GetProperty("units").EnumerateArray()
            .Single(unit => unit.GetProperty("id").GetInt32() == 2);
        Require(actor.GetProperty("x").GetInt32() == destination.X
            && actor.GetProperty("y").GetInt32() == destination.Y
            && actor.GetProperty("done").GetBoolean(),
            "The real destination selection must commit the complete wait-at-destination command.");
        staleTurnButton.EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision == after.Revision, "A callback rendered before dispatch cannot end the newer turn.");
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
    private void CheckInputContracts()
    {
        var level = PresentationJson.Load<JsonObject>("res://data/grayflag-level.json");
        var engine = new GrayFlagEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var session = new GrayFlagSession(engine,level);
        var screen = new BattleScreen();AddChild(screen);screen.Board.AnimationsEnabled=false;
        screen.BindSession(session,AssetCatalog.Load(),level,AuthoredScene.Load(),EnvironmentCatalog.Load(),_=>false);
        void Keypress(Key key) => screen._UnhandledKeyInput(new InputEventKey { Pressed=true,Keycode=key });
        var snapshot = session.GetSnapshot();
        var staleSameRevision = Buttons(screen).Single(button=>button.Text=="结束回合");
        Keypress(Key.Space);
        Require(screen.SelectedUnitId==session.GetTacticalQueries(0).Actors()[0],"Space selects first source-ordered actor through native keyboard input.");
        staleSameRevision.EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision==snapshot.Revision,"Removed global button cannot dispatch after same-revision selection refresh.");
        Keypress(Key.Escape);
        Require(screen.Selection?.Kind==TacticalSelectionKind.Idle,"Native Escape follows the source back operation.");
        screen.SelectCell(new(5,8));
        var selected=screen.SelectedUnitId;
        var queries=session.GetTacticalQueries(0);
        var destination=queries.MoveStops(2).First(at=>at!=new BoardCell(5,8)&&queries.CommandsAt(2,at) is {Count:1} commands&&commands[0].SelfTargeted);
        var order=session.GetLegalOrders(2).First(choice=>choice.Target==destination&&choice.PresentationRole=="move");
        screen.SetApplicationBusy(true);
        screen.SelectCell(destination);screen.SubmitOrder(order);Keypress(Key.E);Keypress(Key.Escape);
        Require(session.GetSnapshot().Revision==snapshot.Revision&&screen.SelectedUnitId==selected,"Application busy blocks board, direct action, end-turn and back input.");
        screen.SetApplicationBusy(false);
        Require(screen.SelectedUnitId==selected,"Clearing application busy preserves the valid selection.");
        screen.SelectCell(destination);
        Require(session.GetSnapshot().Revision==snapshot.Revision+1,"Busy release re-enables source automatic destination dispatch.");
        var undo=Buttons(screen).Single(button=>button.Text=="撤销");
        Require(!undo.Disabled,"Real undo button becomes enabled after a reversible move.");
        undo.EmitSignal(Button.SignalName.Pressed);
        var undone=session.GetSnapshot();
        Require(undone.Revision==snapshot.Revision+2&&EcmaJson.DeepEquals(EcmaJson.Parse(undone.State.GetRawText()),EcmaJson.Parse(snapshot.State.GetRawText())),"Mounted undo restores exact canonical state and advances revision.");
        Require(screen.Selection?.Kind==TacticalSelectionKind.Idle&&screen.RecentMessages.Contains("已撤销上一步"),"Undo resets selection and appends the source cosmetic message.");
        var turn=Buttons(screen).Single(button=>button.Text=="结束回合");turn.EmitSignal(Button.SignalName.Pressed);
        Require(session.GetSnapshot().Revision==undone.Revision+1,"Mounted global end-turn commits once after undo.");

        // Fixture: original campaign map and units with an explicit deployment zone, not naturally reached campaign state.
        var deploymentLevel=EcmaJson.Parse(EcmaJson.Stringify(level))!.AsObject();
        var own=deploymentLevel["units"]!.AsArray().OfType<JsonObject>().Where(unit=>unit["owner"]!.GetValue<int>()==1).ToArray();
        var cells=new JsonArray(own.Select(unit=>(JsonNode)new JsonObject { ["x"]=unit["x"]!.GetValue<int>(),["y"]=unit["y"]!.GetValue<int>() }).ToArray());
        deploymentLevel["scenario"]!.AsObject()["zones"]=new JsonArray(new JsonObject { ["id"]="selection-check-zone",["cells"]=cells });
        deploymentLevel["deployment"]=new JsonObject { ["order"]=new JsonArray(1),["zones"]=new JsonArray(new JsonObject { ["player"]=1,["zone"]="selection-check-zone" }) };
        var deployment=new GrayFlagSession(engine,deploymentLevel);
        screen.BindSession(deployment,AssetCatalog.Load(),deploymentLevel,AuthoredScene.Load(),EnvironmentCatalog.Load(),_=>false);
        var roster=deployment.GetTacticalQueries(0).DeploymentUnits();
        var hud=(TacticalHud)screen.FindChild("TacticalHud",true,false);
        Button RosterButton(int id)
        {
            var row=hud.Projection!.Sections.SelectMany(section=>section.Rows.Select(row=>(section,row)))
                .Single(pair=>pair.row.Intent is {Kind:TacticalHudIntentKind.DeployPick,Unit:var unit}&&unit==id);
            return Buttons(screen).Single(button=>button.GetMeta("hud_key","").AsString()==row.section.Key+"/"+row.row.Key);
        }
        RosterButton(roster[0]).EmitSignal(Button.SignalName.Pressed);
        RosterButton(roster[1]).EmitSignal(Button.SignalName.Pressed);
        Require(screen.SelectedUnitId==roster[1]&&screen.Selection?.Kind==TacticalSelectionKind.DeploymentUnit&&deployment.GetSnapshot().Revision==0,
            "Two actual deployment roster clicks change the picked unit without dispatching a swap.");
        var confirm=Buttons(screen).Single(button=>button.Text=="确认部署");confirm.EmitSignal(Button.SignalName.Pressed);
        Require(deployment.GetSnapshot().Revision==1&&deployment.GetSnapshot().State.GetProperty("phase").GetString()=="playing","Mounted deployment confirmation opens battle through session dispatch.");
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
    // Only this synthetic replacement fixture rebases a revision. Production must never repair a mismatched stamp.
    private sealed class StampedQueries(ITacticalSelectionQueries inner, TacticalStamp stamp) : ITacticalSelectionQueries
    {
        public TacticalStamp Stamp => stamp;
        public string Phase => inner.Phase;
        public bool HumanInput => inner.HumanInput;
        public bool HumanTurn => inner.HumanTurn;
        public bool CanUndo => inner.CanUndo;
        public TacticalUnit? Unit(int id) => inner.Unit(id);
        public TacticalUnit? UnitAt(BoardCell at) => inner.UnitAt(at);
        public bool AreEnemies(int firstUnit,int secondUnit) => inner.AreEnemies(firstUnit,secondUnit);
        public bool CanOpenRecruitment(BoardCell at) => inner.CanOpenRecruitment(at);
        public bool IsEnemyOfViewer(int unit) => inner.IsEnemyOfViewer(unit);
        public IReadOnlyList<int> Actors() => inner.Actors();
        public IReadOnlyList<BoardCell> MoveStops(int unit) => inner.MoveStops(unit);
        public IReadOnlyList<BoardCell>? PathTo(int unit,BoardCell at) => inner.PathTo(unit,at);
        public IReadOnlyList<BoardCell> Threat(int unit) => inner.Threat(unit);
        public IReadOnlyList<BoardCell> ControlZone(int unit) => inner.ControlZone(unit);
        public IReadOnlyList<TacticalCommand> CommandsAt(int unit,BoardCell at) => inner.CommandsAt(unit,at);
        public TacticalAttack Attack(int unit,BoardCell target,BoardCell from,string weapon) => inner.Attack(unit,target,from,weapon);
        public IReadOnlyList<int> DeploymentUnits() => inner.DeploymentUnits();
        public IReadOnlyList<BoardCell> DeploymentZone() => inner.DeploymentZone();
        public IReadOnlyList<BoardCell> DeploymentSpots(int unit) => inner.DeploymentSpots(unit);
        public IReadOnlyList<TacticalTactic> Tactics() => inner.Tactics();
        public IReadOnlyList<TacticalPassenger> Passengers(int carrier) => inner.Passengers(carrier);
        public IReadOnlyList<BoardCell> DirectionSteps(BoardCell from) => inner.DirectionSteps(from);
    }

}
