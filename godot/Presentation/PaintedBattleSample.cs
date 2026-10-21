#nullable enable
using System;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation;

/// <summary>Explicit painted-scene composition using the production battle, session and screen.</summary>
public partial class PaintedBattleSample : Control
{
    [Export(PropertyHint.File, "*.json")]
    public string DefinitionPath { get; set; } = "";

    private PaintedBattleDefinition _definition = null!;
    private BattleEngine _engine = null!;
    private SessionPresentationText _presentation = null!;
    private JsonObject _level = null!;
    private JsonObject _registeredMap = null!;
    private PaintedGroundDocument _art = null!;
    private GrayFlagSession _session = null!;
    private BattleScreen? _screen;
    private readonly AiTurnPacer _pacer = new();
    private string SavePath => ProjectSettings.GlobalizePath(_definition.SavePath);

    public override void _Ready()
    {
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            if (string.IsNullOrWhiteSpace(DefinitionPath))
                throw new InvalidOperationException("A painted battle requires an explicit scene definition.");
            _definition = PresentationJson.Load<PaintedBattleDefinition>(DefinitionPath);
            _definition.Validate();
            var source = Godot.FileAccess.GetFileAsString(_definition.LevelPath);
            _level = EcmaJson.Parse(source)!.AsObject();
            if (_definition.OpeningFocus[0] >= _level["width"]!.GetValue<int>()
                || _definition.OpeningFocus[1] >= _level["height"]!.GetValue<int>())
                throw new InvalidOperationException("Painted battle opening focus lies outside its canonical level.");
            _art = PresentationJson.Load<PaintedGroundDocument>(_definition.GroundPath);
            _art.ValidateLevel(_level);
            var packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
            foreach (var path in _definition.AdditionalContentPaths)
                packs.Add(PresentationJson.Load<JsonObject>(path));
            _engine = new BattleEngine(packs);
            var contentText = EcmaJson.Parse(Godot.FileAccess.GetFileAsString("res://data/grayflag-content-text.json"))!.AsObject();
            var additions = PresentationJson.Load<JsonObject>(_definition.ContentTextAdditionsPath);
            foreach (var family in additions)
                foreach (var binding in family.Value!.AsObject())
                    contentText["content"]![family.Key]!.AsObject().Add(binding.Key, binding.Value!.DeepClone());
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            _presentation = SessionPresentationText.Compose(new(_engine, text), new JsonArray(_level.DeepClone()),
                Godot.FileAccess.GetFileAsString(_definition.LevelTextPath),
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
                OpeningFocus = _definition.OpeningFocus,
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
