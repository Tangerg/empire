#nullable enable
using System.Reflection;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Presentation.Feedback;
using Godot;
using File = System.IO.File;
using Directory = System.IO.Directory;
using Path = System.IO.Path;

namespace Empire.Presentation;

/// <summary>Legally played campaign outcomes, normal native playback, and carried-state continuation.</summary>
public partial class CampaignPlayedFlowChecks : Node
{
    private int assertions;
    private string automaticPath = "";
    private string manualPath = "";

    public override async void _Ready()
    {
        var failed = false;
        try
        {
            RequireFreshData();
            var game = await CheckPlayedDefeatAndRetry();
            await CheckCarriedOpening(game);
        }
        catch (Exception error)
        {
            failed = true;
            GD.PushError(error.ToString());
        }
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        await Layout();
        GD.Print($"Campaign played-flow checks {(failed ? "failed" : "passed")}: {assertions} assertions. Native headless behavior, not visual or complete-campaign acceptance.");
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task<GrayFlagGame> CheckPlayedDefeatAndRetry()
    {
        var game = new GrayFlagGame();
        AddChild(game);
        await Activate(Button(View<CampaignMenuScreen>(game), "NewCampaign"));
        var beats = 0;
        while (Campaign(game).Node()["type"]!.GetValue<string>() == "story")
        {
            Require(++beats <= 40, "The authored opening did not reach its choice.");
            await Activate(Button(View<StoryScreen>(game), "StoryPrimary"));
        }
        await Activate(Button(View<StoryScreen>(game), "Choice_steady-advance"));
        await Activate(Button(View<CampaignStagingScreen>(game), "StartBattle"));
        var engine = Field<BattleEngine>(game, "engine");
        var definition = Field<JsonObject>(game, "definition");
        var levels = Field<JsonArray>(game, "levels");
        var rules = new CampaignApplicationRules(engine, Field<SessionPresentationText>(game, "presentationText"));
        var opening = Campaign(game).Battle!.Snapshot();
        var campaignBefore = Campaign(game).Snapshot();
        var originalRequest = Campaign(game).RequestSnapshot();
        var driver = new GrayFlagCampaign(rules, definition, levels);
        driver.Load(Campaign(game).Save());
        JsonObject? checkpoint = null;
        var terminalComputer = false;
        var orders = 0;
        while (!driver.Battle!.IsConcluded)
        {
            Require(++orders <= 512, "Legal human end-turn orders did not reach an actual opponent-caused defeat.");
            checkpoint = driver.Save();
            var battle = driver.Battle;
            terminalComputer = battle.IsComputerTurn;
            if (terminalComputer)
                Require(battle.AdvanceComputerStep() is { Committed: true }, "The normal opponent did not commit its legal decision.");
            else
                Require(battle.Dispatch(EcmaJson.ToElement(new JsonObject { ["kind"] = "endTurn" }), battle.Revision).Accepted,
                    "The passive player could not legally end its turn.");
        }
        Require(terminalComputer && driver.BattleSummary().Outcome == "defeat" && checkpoint is not null,
            "The checkpoint must precede a real AI-caused defeat, not a synthetic outcome or a turn-limit draw.");
        var expected = driver.Battle.Snapshot();
        var expectedAction = driver.Battle.ActionLog.Last();
        SaveFile.WriteDocument(manualPath, checkpoint!);
        var slots = SaveSlots();
        SendKey(Key.F9);
        var campaign = Campaign(game);
        var session = campaign.Battle!;
        var screen = View<BattleScreen>(game);
        Require(session.IsComputerTurn && session.Revision == 0 && session.ActionLog.Count == 0
            && EcmaJson.DeepEquals(session.Snapshot(), checkpoint!["battle"]!["state"]),
            "Native F9 must restore the exact legal pre-defeat AI state before the automatic pacer runs.");
        Require(screen.Board.AnimationsEnabled && screen.Board.AdvanceAnimationsAutomatically,
            "AI terminal acceptance requires normal automatic animation, not forced completion.");
        var presented = new List<int>();
        var presentedOnBattle = true;
        var completionCount = 0;
        void Entered(Node child)
        {
            if (child is BattleResultScreen)
                completionCount++;
        }
        game.ChildEnteredTree += Entered;
        screen.Board.EventPresented += (transition, index) =>
        {
            presented.Add(index);
            presentedOnBattle &= ReferenceEquals(View<Control>(game), screen) && screen.IsPresenting;
        };
        var deadline = Time.GetTicksMsec() + 30_000;
        while (ReferenceEquals(View<Control>(game), screen))
        {
            Require(Time.GetTicksMsec() < deadline, "The real AI terminal playback did not offer its result within 30 seconds.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var completion = View<BattleResultScreen>(game);
        var transition = session.GetSnapshot().Transition!;
        Require(session.Revision == 1 && session.ActionLog.Count == 1 && EcmaJson.DeepEquals(session.ActionLog.Single(), expectedAction)
            && EcmaJson.DeepEquals(session.Snapshot(), expected) && campaign.BattleSummary().Outcome == "defeat",
            "The actual AI pacer must commit precisely the independently derived losing order and terminal state.");
        Require(presentedOnBattle && presented.SequenceEqual(Enumerable.Range(0, transition.Events.Count))
            && transition.Events.Last().GetProperty("type").GetString() == "gameOver",
            "The result must wait for every terminal event, including gameOver, on the battle screen.");
        Require(!Field<AiTurnPacer>(game, "computerPacer").IsRunning && completionCount == 1,
            "AI post-playback pacing must finish and create exactly one completion screen.");
        Require(!Descendants(completion).OfType<Button>().Any(button => button.Name.ToString() is "Settle" or "Proceed")
            && EcmaJson.DeepEquals(campaign.Snapshot(), campaignBefore) && SaveSlots() == slots,
            "A real defeat may not settle progress, offer victory continuation, or touch either save slot.");
        await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
        Require(completionCount == 1 && ReferenceEquals(View<Control>(game), completion) && SaveSlots() == slots,
            "Continued normal application frames must not repeat the result or save the defeat.");
        var retry = Button(completion, "Retry");
        retry.GrabFocus();
        SendKey(Key.Enter);
        retry.EmitSignal(Godot.Button.SignalName.Pressed);
        await Layout();
        var restarted = Campaign(game).Battle!;
        Require(View<Control>(game) is BattleScreen && !ReferenceEquals(restarted, session)
            && restarted.Revision == 0 && restarted.ActionLog.Count == 0 && restarted.Events.Count == 0
            && EcmaJson.DeepEquals(restarted.Snapshot(), opening) && EcmaJson.DeepEquals(campaign.Snapshot(), campaignBefore)
            && EcmaJson.DeepEquals(campaign.RequestSnapshot(), originalRequest),
            "Native Retry and its duplicate callback must restore the exact original battle once without advancing the campaign.");
        await Layout();
        Require(ReferenceEquals(Campaign(game).Battle, restarted) && completionCount == 1 && SaveSlots() == slots,
            "Retry must preserve both existing save slots and ignore stale terminal navigation.");
        GD.Print($"PLAYED_AI_DEFEAT_OK orders={orders}; terminal={StateDigest.Hash(expected)}; events={transition.Events.Count}; completions={completionCount}; retry={StateDigest.Hash(restarted.Snapshot())}; both slots unchanged");
        game.ChildEnteredTree -= Entered;
        return game;
    }

    private void RequireFreshData()
    {
        var configured = System.Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        Require(!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured), "Explicit fresh temporary XDG_DATA_HOME is required.");
        var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(configured!).TrimEnd(Path.DirectorySeparatorChar);
        var relative = Path.GetRelativePath(temporary, root);
        Require(!Path.IsPathFullyQualified(relative) && relative.Split(Path.DirectorySeparatorChar)[0].StartsWith("empire-", StringComparison.Ordinal),
            "Played-flow checks only accept generated /tmp/empire-* user data.");
        var userData = Path.GetFullPath(ProjectSettings.GlobalizePath("user://")).TrimEnd(Path.DirectorySeparatorChar);
        Require(userData.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Godot ignored the isolated user-data namespace.");
        for (var directory = new DirectoryInfo(userData); directory is not null && directory.FullName != temporary; directory = directory.Parent)
            Require(!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) == 0, "Refusing symlinked data ancestors.");
        Require(!Directory.Exists(userData) || Directory.EnumerateFileSystemEntries(userData).All(entry =>
            (Path.GetFileName(entry) is "logs" or "shader_cache") && Directory.Exists(entry)
            && (File.GetAttributes(entry) & FileAttributes.ReparsePoint) == 0), "Refusing pre-existing user data.");
        string SavePath(string name)
        {
            var resource = (string)typeof(GrayFlagGame).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
            var path = Path.GetFullPath(ProjectSettings.GlobalizePath(resource));
            Require(Path.GetDirectoryName(path) == userData && !File.Exists(path) && !Directory.Exists(path), "Save target is not fresh and isolated.");
            return path;
        }
        automaticPath = SavePath("CampaignSavePath");
        manualPath = SavePath("SavePath");
        Directory.CreateDirectory(userData);
    }

    private string SaveSlots() => string.Join("|", new[] { automaticPath, manualPath }.Select(path =>
        $"{File.GetLastWriteTimeUtc(path).Ticks}:{Convert.ToHexString(File.ReadAllBytes(path))}"));

    private async Task Activate(Button button)
    {
        Require(button.IsInsideTree() && button.IsVisibleInTree() && !button.Disabled, "Native activation target must be reachable and enabled.");
        button.GrabFocus();
        SendKey(Key.Enter);
        await Layout();
    }

    private void SendKey(Key key)
    {
        foreach (var pressed in new[] { true, false })
        {
            using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed };
            GetViewport().PushInput(input, true);
        }
    }

    private async Task Layout()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static GrayFlagCampaign Campaign(GrayFlagGame game) => Field<GrayFlagCampaign>(game, "campaign");
    private static T View<T>(GrayFlagGame game) where T : Control => Field<Control>(game, "view") as T
        ?? throw new InvalidOperationException($"Expected {typeof(T).Name}, got {Field<Control>(game, "view").GetType().Name}.");
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static Button Button(Node root, string name) => Descendants(root).OfType<Button>().Single(button => button.Name == name);
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
