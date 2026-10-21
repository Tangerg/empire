using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Presentation;
using Empire.Presentation.Feedback;
using Godot;
using File = System.IO.File;

namespace Empire.Application;

/// <summary>Packaged runtime-content and real application lifecycle smoke, without source-test fixtures.</summary>
public partial class ReleaseDiagnostics : Node
{
    private readonly List<ShadowCache> lighting = new();
    private int assertions;
    private int documents;
    private int textures;

    public override async void _Ready()
    {
        var failed = false;
        try
        {
            PackageChecks.RequireFreshTemporaryData();
            Require(!DirAccess.DirExistsAbsolute("res://tests"), "Source-test fixtures must not be in the production package.");
            CheckResources();
            await CheckApplication();
        }
        catch (Exception error) { GD.PushError(error.ToString()); failed = true; }
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        if (lighting.Any(cache => !cache.IsDisposed || cache.TextureCount != 0))
        {
            GD.PushError("A battle lighting cache survived queued scene teardown.");
            failed = true;
        }
        if (!failed)
            GD.Print($"RELEASE_DIAGNOSTICS_OK {assertions} assertions; {documents} runtime JSON documents; {textures} textures; real story/choice/battle/move/save/load/restart/camp lifecycle and clean teardown. Not visual or campaign-outcome acceptance.");
        GetTree().Quit(failed ? 1 : 0);
    }

    private void CheckResources()
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        void Collect(JsonNode? node)
        {
            if (node is JsonObject obj)
                foreach (var entry in obj)
                    Collect(entry.Value);
            else if (node is JsonArray array)
                foreach (var value in array)
                    Collect(value);
            else if (node is JsonValue scalar && scalar.TryGetValue<string>(out var text)
                && text.StartsWith("res://assets/", StringComparison.Ordinal) && text.EndsWith(".png", StringComparison.Ordinal))
                paths.Add(text);
        }
        void ReadDirectory(string path)
        {
            foreach (var name in DirAccess.GetFilesAt(path).Where(name => name.EndsWith(".json", StringComparison.Ordinal)))
            {
                Collect(PresentationJson.Load<JsonNode>(path + "/" + name));
                documents++;
            }
            foreach (var name in DirAccess.GetDirectoriesAt(path))
                ReadDirectory(path + "/" + name);
        }
        ReadDirectory("res://data");
        // The authored catalog also records unshipped candidate/holdout atlas specs.
        // Validate its schema, but load the explicit res:// runtime bindings collected above.
        _ = EnvironmentCatalog.Load();
        Require(documents > 0 && paths.Count > 0, "No runtime documents or art were packaged.");
        foreach (var path in paths)
        {
            var texture = GD.Load<Texture2D>(path);
            Require(texture is not null && texture.GetWidth() > 0 && texture.GetHeight() > 0, "Cannot load runtime art: " + path);
            textures++;
        }
        Require(StoryDocument.Load(Empire.Localization.GodotTextCatalog.LoadSimplifiedChinese()).Stories.Count > 0, "Authored story data is absent.");
        _ = AssetCatalog.Load();
        _ = WorldMaterialsManifest.Load();
    }

    private async Task CheckApplication()
    {
        var game = new GrayFlagGame { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(game);
        var menu = View<CampaignMenuScreen>(game);
        Require(menu.Presented is { CanContinue: false }, "Fresh package unexpectedly offered Continue.");
        Require(menu.RenderingDiagnostics.Count == 0, "Title art did not resolve: " + string.Join("; ", menu.RenderingDiagnostics));
        Press(Button(menu, "NewCampaign"));
        var beats = 0;
        var choices = 0;
        while (View<Control>(game) is StoryScreen story && beats + choices < 128)
        {
            Require(story.RenderingDiagnostics.Count == 0, "Story art did not resolve: " + string.Join("; ", story.RenderingDiagnostics));
            if (story.PresentedChoice is not null)
            {
                Press(Descendants(story).OfType<Button>().First(button => button.Name.ToString().StartsWith("Choice_", StringComparison.Ordinal)));
                choices++;
            }
            else
            {
                Press(Button(story, "StoryPrimary"));
                beats++;
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var staging = View<CampaignStagingScreen>(game);
        Require(beats > 0 && choices > 0 && staging.RenderingDiagnostics.Count == 0, "Authored story/choice/staging flow did not complete.");
        var automatic = ProjectSettings.GlobalizePath("user://grayflag-campaign-autosave.json");
        var manual = ProjectSettings.GlobalizePath("user://grayflag-campaign-save.json");
        Require(File.Exists(automatic) && !File.Exists(manual), "Campaign and manual save slots did not stay separate.");
        var cleanCampaignState = EcmaJson.Parse(File.ReadAllText(automatic))!["state"];
        Press(Button(staging, "StartBattle"));
        // Entering battle intentionally rewrites the clean slot with a new savedAt.
        // Its canonical state must match; later manual operations must not touch its bytes.
        var cleanCampaignSave = File.ReadAllText(automatic);
        Require(EcmaJson.DeepEquals(EcmaJson.Parse(cleanCampaignSave)!["state"], cleanCampaignState),
            "Prebattle autosave changed canonical campaign state.");
        var battle = TrackBattle(game);
        Press(Button(battle, text: "保存"));
        JsonObject ReadManual() => EcmaJson.Parse(File.ReadAllText(manual))!.AsObject();
        var opening = ReadManual();
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = Empire.Localization.GodotTextCatalog.LoadSimplifiedChinese();
        var presentationText = SessionPresentationText.Compose(new SessionTextRules(engine, text),
            PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json"),
            Godot.FileAccess.GetFileAsString("res://data/grayflag-battle-text.json"),
            Godot.FileAccess.GetFileAsString("res://data/grayflag-content-text.json"));
        var savedSession = GrayFlagSession.FromSave(new(engine, presentationText), opening["battle"]!, opening["events"]!.AsArray());
        var openingHash = StateDigest.Hash(savedSession.Snapshot());
        var queries = savedSession.GetTacticalQueries(0);
        var move = queries.Actors().SelectMany(id => queries.MoveStops(id)
            .Where(at => at != queries.Unit(id)!.At && queries.CommandsAt(id, at) is { Count: 1 } commands && commands[0].SelfTargeted)
            .Select(at => (Origin: queries.Unit(id)!.At, Destination: at))).First();
        battle.Board.AdvanceAnimationsAutomatically = false;
        battle.SelectCell(move.Origin);
        battle.SelectCell(move.Destination);
        battle.Board.AdvancePresentation(100);
        Require(battle.DisplayedRevision > 0 && !battle.IsInputBlocked, "Real battle move did not commit and settle.");
        Press(Button(battle, text: "保存"));
        var moved = ReadManual();
        var movedHash = StateDigest.Hash(moved["battle"]!["state"]!.AsObject());
        Require(movedHash != openingHash && moved["events"]!.AsArray().Count > 0, "Manual save lost the committed move or event history.");
        Press(Button(battle, text: "重新开始"));
        Descendants(battle).OfType<ConfirmationDialog>().Single().EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        var restarted = TrackBattle(game);
        Require(!ReferenceEquals(battle, restarted) && restarted.DisplayedRevision == 0, "Restart did not replace the battle session.");
        Press(Button(restarted, text: "读取"));
        var loaded = TrackBattle(game);
        Require(!ReferenceEquals(restarted, loaded), "Load did not rebind the application battle screen.");
        Press(Button(loaded, text: "保存"));
        var restored = ReadManual();
        Require(StateDigest.Hash(restored["battle"]!["state"]!.AsObject()) == movedHash
            && EcmaJson.Stringify(restored["events"]) == EcmaJson.Stringify(moved["events"]), "Real save/load did not preserve state and events.");
        Press(Button(loaded, text: "战役营地"));
        var camp = View<CampaignStagingScreen>(game);
        Press(Button(camp, "CampaignExit"));
        var returnedMenu = View<CampaignMenuScreen>(game);
        Require(returnedMenu.Presented is { CanContinue: true }, "Returning from battle lost clean campaign continuation.");
        Press(Button(returnedMenu, "ContinueCampaign"));
        _ = View<CampaignStagingScreen>(game);
        Require(File.ReadAllText(automatic) == cleanCampaignSave, "Manual battle lifecycle changed the separate clean campaign slot.");
    }

    private BattleScreen TrackBattle(Node game)
    {
        var screen = View<BattleScreen>(game);
        Require(screen.Board.RenderedUnitCount > 0, "Packaged battle rendered no units.");
        lighting.Add(screen.Board.LightingResources);
        return screen;
    }
    private static T View<T>(Node game) where T : Control => game.GetChildren().OfType<T>().Single();
    private static Button Button(Node root, string? name = null, string? text = null) => Descendants(root).OfType<Button>()
        .Single(button => name is not null ? button.Name == name : button.Text == text);
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var next in Descendants(child))
                yield return next;
        }
    }
    private static void Press(Button button) => button.EmitSignal(Godot.Button.SignalName.Pressed);
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
