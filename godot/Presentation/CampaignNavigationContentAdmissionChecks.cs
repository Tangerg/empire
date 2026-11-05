#nullable enable
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Godot;
using File = System.IO.File;

namespace Empire.Presentation;

public partial class CampaignNavigationChecks
{
    private static readonly string[] SavedRosterContentFields =
        ["unitType", "career", "unlockedCareers", "learnedAbilities", "careerMastery"];

    private async Task CheckCampaignContentAdmission()
    {
        var originalManual = File.ReadAllBytes(manualPath);
        var originalManualStamp = File.GetLastWriteTimeUtc(manualPath);
        var originalAutomatic = File.Exists(automaticPath) ? File.ReadAllBytes(automaticPath) : null;
        var originalAutomaticStamp = File.GetLastWriteTimeUtc(automaticPath);
        GrayFlagGame? game = null;
        try
        {
            ResetAutosave();
            game = MountGame();
            var engine = Field<BattleEngine>(game, "engine");
            var presentation = Field<SessionPresentationText>(game, "presentationText");
            var levels = Field<JsonArray>(game, "levels");
            var text = presentation.Messages;
            GrayFlagCampaign New(JsonObject? state = null) => new(new(engine, presentation), definition, levels, state);
            var prologue = New().Save();
            await Activate(Button(View<CampaignMenuScreen>(game), "NewCampaign"));
            while (Campaign(game).Node()["type"]!.GetValue<string>() == "story")
                await CompleteStory(game);
            await Activate(Buttons(View<StoryScreen>(game)).First(button => button.Name.ToString().StartsWith("Choice_", StringComparison.Ordinal)));
            var staging = Campaign(game).Save();
            await Activate(Button(View<CampaignStagingScreen>(game), "StartBattle"));
            var owner = Campaign(game);
            var session = owner.Battle!;
            var screen = View<BattleScreen>(game);
            screen.Board.AnimationsEnabled = false;
            game.ProcessMode = ProcessModeEnum.Inherit;
            await Layout();
            var opening = Digest(session.Snapshot());
            TacticalSceneInputChecks.Move(screen, session);
            var played = owner.Save();
            var revision = session.Revision;
            Require(revision == 1 && session.CanUndo && Digest(session.Snapshot()) != opening && !screen.IsInputBlocked,
                "Content admission must protect a real played battle reached through New, story, choice and staging.");
            var camp = New();
            camp.Load(played);
            camp.ExitBattle();
            var documents = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
            {
                ["prologue"] = prologue,
                ["staging"] = staging,
                ["camp"] = camp.Save(),
                ["battle"] = played,
            };
            var liveFacts = ContentAdmissionFacts(owner);
            var request = EcmaJson.Stringify(owner.RequestSnapshot());
            var battle = EcmaJson.Stringify(session.Snapshot());
            var messages = screen.RecentMessages.ToArray();
            foreach (var (location, field) in new[]
            {
                ("prologue", "unitType"), ("staging", "career"), ("camp", "unlockedCareers"),
                ("battle", "learnedAbilities"), ("prologue", "careerMastery"),
            })
            {
                var invalid = InvalidRosterContent(documents[location], "laiya", field);
                SaveFile.WriteDocument(manualPath, invalid);
                var manualBytes = File.ReadAllBytes(manualPath);
                var manualTime = Stamp(manualPath);
                var automaticBytes = File.ReadAllBytes(automaticPath);
                var automaticTime = Stamp(automaticPath);
                screen.ShowFeedback("");
                ContentAdmissionKey(Key.F9);
                await Layout();
                var context = $"Saved {location}/{field}";
                Require(ReferenceEquals(Campaign(game), owner) && ReferenceEquals(View<Control>(game), screen)
                    && ReferenceEquals(owner.Battle, session) && session.Revision == revision && session.CanUndo
                    && ContentAdmissionFacts(owner) == liveFacts && EcmaJson.Stringify(owner.RequestSnapshot()) == request
                    && EcmaJson.Stringify(session.Snapshot()) == battle && screen.RecentMessages.SequenceEqual(messages),
                    context + " replaced or changed the active campaign, view, played session, request or history.");
                Require(File.ReadAllBytes(manualPath).SequenceEqual(manualBytes) && Stamp(manualPath) == manualTime
                    && File.ReadAllBytes(automaticPath).SequenceEqual(automaticBytes) && Stamp(automaticPath) == automaticTime,
                    context + " rejection changed either save slot's original bytes or timestamp.");
                Require(Labels(screen).Contains(text.Format("error.load.failed_unchanged"))
                    && !Descendants(game).OfType<AcceptDialog>().Any(dialog => dialog.DialogText == text.Format("feedback.load.completed")),
                    context + " did not report a recoverable load rejection or falsely reported success.");
            }
            SaveFile.WriteDocument(manualPath, played);
            var healthyManual = File.ReadAllBytes(manualPath);
            var healthyManualStamp = Stamp(manualPath);
            var healthyAutomatic = File.ReadAllBytes(automaticPath);
            var healthyAutomaticStamp = Stamp(automaticPath);
            ContentAdmissionKey(Key.F9);
            await Layout();
            Require(!ReferenceEquals(Campaign(game), owner) && View<Control>(game) is BattleScreen
                && Campaign(game).Battle is { Revision: 0 } restored && !restored.CanUndo
                && ContentAdmissionFacts(Campaign(game)) == liveFacts,
                "Content rejection must leave ordinary F9 able to restore the exact healthy played checkpoint.");
            Require(File.ReadAllBytes(manualPath).SequenceEqual(healthyManual) && Stamp(manualPath) == healthyManualStamp
                && File.ReadAllBytes(automaticPath).SequenceEqual(healthyAutomatic) && Stamp(automaticPath) == healthyAutomaticStamp,
                "Successful manual admission must also leave both source slots unchanged.");

            CheckDetachedRosterContent(New, documents);
            Retire(game);
            game = null;
            await Layout();
            foreach (var (location, field) in new[]
            {
                ("prologue", "unitType"), ("staging", "career"), ("prologue", "unlockedCareers"),
                ("staging", "learnedAbilities"), ("prologue", "careerMastery"),
            })
            {
                var invalid = InvalidRosterContent(documents[location], "mirelle", field);
                SaveFile.WriteDocument(automaticPath, invalid["campaign"]!);
                var bytes = File.ReadAllBytes(automaticPath);
                var stamp = Stamp(automaticPath);
                game = MountGame();
                var menu = View<CampaignMenuScreen>(game);
                Require(menu.Presented is { CanContinue: false, RejectedNotice: not null }
                    && Buttons(menu).Where(button => button.Name == "ContinueCampaign").All(button => button.Disabled),
                    $"Startup admitted {location}/{field} on a future unbound roster member instead of rejecting Continue.");
                Require(File.ReadAllBytes(automaticPath).SequenceEqual(bytes) && Stamp(automaticPath) == stamp
                    && File.ReadAllBytes(manualPath).SequenceEqual(healthyManual) && Stamp(manualPath) == healthyManualStamp,
                    "Rejecting autosave content must preserve both slots, including the rejected source document.");
                Retire(game);
                game = null;
                await Layout();
            }
            SaveFile.WriteDocument(automaticPath, staging["campaign"]!);
            var validAutomatic = File.ReadAllBytes(automaticPath);
            var validAutomaticStamp = Stamp(automaticPath);
            game = MountGame();
            Require(View<CampaignMenuScreen>(game).Presented is { CanContinue: true, RejectedNotice: null },
                "A healthy clean campaign must still offer Continue after content admission.");
            await Activate(Button(View<CampaignMenuScreen>(game), "ContinueCampaign"));
            Require(View<Control>(game) is CampaignStagingScreen && ContentAdmissionFacts(Campaign(game)) == StableContentDocument(staging),
                "Healthy Continue must activate the exact clean campaign without advancing or opening its battle.");
            Require(File.ReadAllBytes(automaticPath).SequenceEqual(validAutomatic) && Stamp(automaticPath) == validAutomaticStamp
                && File.ReadAllBytes(manualPath).SequenceEqual(healthyManual) && Stamp(manualPath) == healthyManualStamp,
                "Healthy Continue must not overwrite the autosave or the independent played manual checkpoint.");
        }
        finally
        {
            if (game is not null)
                Retire(game);
            File.WriteAllBytes(manualPath, originalManual);
            File.SetLastWriteTimeUtc(manualPath, originalManualStamp);
            if (originalAutomatic is null)
                ResetAutosave();
            else
            {
                File.WriteAllBytes(automaticPath, originalAutomatic);
                File.SetLastWriteTimeUtc(automaticPath, originalAutomaticStamp);
            }
        }
        await Layout();
    }

    private void CheckDetachedRosterContent(Func<JsonObject?, GrayFlagCampaign> create, IReadOnlyDictionary<string, JsonObject> documents)
    {
        var fallenState = documents["staging"]["campaign"]!["state"]!.DeepClone().AsObject();
        fallenState["roster"]!["torren"]!["disposition"] = "fallen";
        var fallen = create(fallenState);
        var fallenDocuments = new Dictionary<string, JsonObject>(StringComparer.Ordinal) { ["staging"] = fallen.Save() };
        fallen.BeginBattle();
        fallenDocuments["battle"] = fallen.Save();
        fallen.ExitBattle();
        fallenDocuments["camp"] = fallen.Save();
        var survivor = create(null);
        survivor.Load(documents["battle"]);
        var session = survivor.Battle!;
        var before = ContentAdmissionFacts(survivor);
        var request = EcmaJson.Stringify(survivor.RequestSnapshot());
        var revision = session.Revision;
        foreach (var (member, fixtures) in new[]
        {
            ("laiya", documents), ("mirelle", documents),
            ("torren", (IReadOnlyDictionary<string, JsonObject>)fallenDocuments),
        })
            foreach (var (location, source) in fixtures)
            {
                var healthy = create(null);
                healthy.Load(source);
                Require(ContentAdmissionFacts(healthy) == StableContentDocument(source),
                    $"The {location}/{member} content fixture must be a valid round trip before corrupting its references.");
                foreach (var field in SavedRosterContentFields)
                {
                    var invalid = InvalidRosterContent(source, member, field);
                    var raw = EcmaJson.Stringify(invalid);
                    var rejected = false;
                    try
                    {
                        survivor.Load(invalid);
                    }
                    catch (StoredDocumentException) { rejected = true; }
                    Require(rejected && ReferenceEquals(survivor.Battle, session) && session.Revision == revision
                        && ContentAdmissionFacts(survivor) == before && EcmaJson.Stringify(survivor.RequestSnapshot()) == request
                        && EcmaJson.Stringify(invalid) == raw,
                        $"Saved {location}/{member}/{field} must reject atomically even when the member is future, unbound or fallen.");
                }
            }
    }

    private void ContentAdmissionKey(Key key)
    {
        foreach (var pressed in new[] { true, false })
        {
            using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed };
            GetViewport().PushInput(input, true);
        }
    }

    private static JsonObject InvalidRosterContent(JsonObject source, string member, string field)
    {
        var invalid = source.DeepClone().AsObject();
        invalid["campaign"]!["state"]!["roster"]![member]![field] = field switch
        {
            "unitType" => JsonValue.Create("unregistered-content-admission-unit"),
            "career" => JsonValue.Create("unregistered-content-admission-career"),
            "unlockedCareers" => new JsonArray("unregistered-content-admission-career"),
            "learnedAbilities" => new JsonArray("unregistered-content-admission-ability"),
            "careerMastery" => new JsonObject { ["unregistered-content-admission-career"] = 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };
        return invalid;
    }

    private static string ContentAdmissionFacts(GrayFlagCampaign owner) => StableContentDocument(owner.Save());

    private static string StableContentDocument(JsonObject document)
    {
        var facts = document.DeepClone().AsObject();
        facts["campaign"]!.AsObject().Remove("savedAt");
        (facts["battle"] as JsonObject)?.Remove("savedAt");
        return EcmaJson.Stringify(facts);
    }
}
