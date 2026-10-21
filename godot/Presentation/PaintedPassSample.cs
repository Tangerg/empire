#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation;

/// <summary>Explicit composition entry for the authored pass, using the production battle/session/screen.</summary>
public partial class PaintedPassSample : Control
{
    private BattleEngine _engine = null!;
    private SessionPresentationText _presentation = null!;
    private JsonObject _level = null!;
    private JsonObject _registeredMap = null!;
    private PaintedGroundDocument _art = null!;
    private GrayFlagSession _session = null!;
    private BattleScreen? _screen;
    private readonly AiTurnPacer _pacer = new();
    private string SavePath => ProjectSettings.GlobalizePath("user://painted-pass-sample/battle.json");

    public override void _Ready()
    {
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var source = Godot.FileAccess.GetFileAsString("res://data/samples/painted-pass-level.json");
            _level = EcmaJson.Parse(source)!.AsObject();
            _art = PresentationJson.Load<PaintedGroundDocument>("res://data/samples/painted-pass-art.json");
            var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
            if (sourceHash != _art.LevelSourceSha256)
                throw new InvalidOperationException("Painted scene registration does not match its reviewed canonical level source.");
            var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
            packs.Add(PresentationJson.Load<JsonObject>("res://data/samples/painted-pass-content.json"));
            _engine = new BattleEngine(packs);
            var contentText = EcmaJson.Parse(Godot.FileAccess.GetFileAsString("res://data/grayflag-content-text.json"))!.AsObject();
            contentText["content"]!["terrains"]!.AsObject().Add("painted-pass.rocks",
                new JsonObject { ["name"] = "sample.painted_pass.rocks" });
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            _presentation = SessionPresentationText.Compose(new(_engine, text), new JsonArray(_level.DeepClone()),
                Godot.FileAccess.GetFileAsString("res://data/samples/painted-pass-text.json"),
                contentText.ToJsonString());
            var initial = new GrayFlagSession(new(_engine, _presentation), _level);
            _registeredMap = initial.Snapshot()["map"]!.AsObject();
            Mount(initial);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Mount(GrayFlagSession session)
    {
        _pacer.Cancel();
        var next = new BattleScreen();
        AddChild(next);
        try
        {
            var scene = new AuthoredScene
            {
                LevelId = _art.LevelId,
                MapSize = [_level["width"]!.GetValue<int>(), _level["height"]!.GetValue<int>()],
                RuntimeReady = true,
                OpeningFocus = [8, 4],
                FramePlacements = []
            };
            next.BindSession(new(_presentation.Messages, _presentation.Battle, session,
                new(AssetCatalog.Load(), EnvironmentCatalog.Load(), _engine.Grids.Get("square4"), _engine.Catalog, _art.CreatePlane)), _level, scene);
            next.ConfigureGround(_art.Factory(_registeredMap));
            next.ConfigureDynamicState();
            next.BindApplicationActions(Save, Load, () => Mount(new GrayFlagSession(new(_engine, _presentation), _level)));
        }
        catch { RemoveChild(next); next.Free(); throw; }
        if (_screen is not null)
        {
            RemoveChild(_screen);
            _screen.QueueFree();
        }
        _session = session;
        _screen = next;
    }

    private void Save()
    {
        if (_screen?.IsInputBlocked != false || _pacer.IsRunning)
            return;
        try
        {
            SaveFile.WriteDocument(SavePath, _session.Save());
            _screen.AppendMessage(_presentation.Messages.Format("feedback.save.round_saved", _session.Snapshot()["turn"]!.GetValue<int>()));
        }
        catch (SaveStorageException error)
        {
            GD.PushWarning(error.ToString());
            _screen.AppendMessage(_presentation.Messages.Format("error.save.failed"));
        }
    }

    private void Load()
    {
        if (_screen?.IsInputBlocked != false || _pacer.IsRunning)
            return;
        try
        {
            var document = SaveFile.ReadDocument(SavePath)
                ?? throw new StoredDocumentException("Empty sample save document.");
            Mount(GrayFlagSession.FromSave(new(_engine, _presentation), document));
            _screen!.AppendMessage(_presentation.Messages.Format("feedback.load.completed"));
        }
        catch (Exception error) when (error is SaveStorageException or StoredDocumentException)
        {
            GD.PushWarning(error.ToString());
            _screen!.AppendMessage(_presentation.Messages.Format("error.load.failed_unchanged"));
        }
    }

    public override void _Input(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key)
            return;
        if (key.Keycode == Key.F5)
            Save();
        else if (key.Keycode == Key.F9)
            Load();
        else
            return;
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (_screen is not { } screen)
            return;
        _pacer.Tick(Time.GetTicksMsec(), _session.IsComputerTurn, screen.IsPresenting,
            () => _session.AdvanceComputerStep(screen.AppendMessage)
                ?? throw new InvalidOperationException("Computer turn changed before its synchronous commit."),
            () => { screen.Refresh(); return screen.IsPresenting; }, screen.SetApplicationBusy);
    }

    public override void _ExitTree() => _pacer.Cancel();
}
