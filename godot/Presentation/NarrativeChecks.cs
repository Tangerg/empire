using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Empire.Localization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Campaign;
using Godot;

namespace Empire.Presentation;

/// <summary>Executes native widgets against the frozen rendered expectations and native localization contracts. No desktop acceptance claim.</summary>
public partial class NarrativeChecks : Node
{
    private int assertions;
    private GameText messages = null!;
    private StoryDocument documents = null!;
    private NarrativePresentationDocument metadata = null!;
    private StoryScreen screen = null!;
    private JsonObject oracle = null!;
    private readonly JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public override async void _Ready()
    {
        try
        {
            messages = GodotTextCatalog.LoadSimplifiedChinese();
            documents = StoryDocument.Load(messages); metadata = NarrativePresentationDocument.Load(messages);
            oracle = PresentationJson.Load<JsonObject>(OS.GetEnvironment("EMPIRE_NARRATIVE_ORACLE"));
            screen = new StoryScreen(); AddChild(screen); await Layout();
            CheckSourceProjections();
            CheckLocalizationBoundary();
            await CheckSourceWidgets();
            CheckNormalTransitions();
            CheckEndingTransitions();
            CheckLifecycle();
            CheckShellLifecycle();
            await CheckLayout();
            CheckMissingArt();
            GD.Print($"Native narrative checks passed: {assertions} assertions, {documents.Stories.Count} documents / {documents.Stories.Sum(story => story.Beats.Count)} beats, {documents.Choices.Count} choices.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private NarrativeContext Context(JsonObject state) => new(messages, metadata, state["battleHistory"]!.AsArray().Count, Text(state, "status") == "active");
    private StoryEntry Story(string id) => documents.Stories.Single(story => story.Id == id);
    private StoryChoice Choice(string id) => documents.Choices.Single(choice => choice.Id == id);
    private static string Text(JsonNode value, string key) => value[key]!.GetValue<string>();
    private static int Number(JsonNode value, string key) => value[key]!.GetValue<int>();
    private static bool Flag(JsonNode value, string key) => value[key]!.GetValue<bool>();
    private async Task Layout() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private T Find<T>(string name) where T : Node => (T)(screen.FindChild(name, true, false) ?? throw new InvalidOperationException($"Missing widget {name}."));
    private void Press(string name) => Find<Button>(name).EmitSignal(Button.SignalName.Pressed);

    private JsonNode StoryProjection(StoryView view) =>
        JsonSerializer.SerializeToNode(new
        {
            campaignTitle = view.Shell.Title, chapterHeading = view.Shell.ChapterHeading, progress = view.Shell.Progress,
            progressFraction = view.Shell.ProgressFraction, progressSegments = view.Shell.ProgressSegments,
            backdropSource = view.Shell.Art.Source, view.Kicker, view.Date, view.Location, view.Title, view.Summary,
            mission = view.Mission is { } mission ? new[] { new { label = view.ObjectiveLabel, text = mission.Objective },
                new { label = view.DangerLabel, text = mission.Danger }, new { label = view.LessonLabel, text = mission.Lesson } } : null,
            view.Speaker, view.Text, portraitSource = view.Portrait?.Source, view.Narration, beatDots = view.ActiveDots,
            view.PrimaryLabel, primaryAction = view.Ending ? "finish" : "nextBeat",
        }, jsonOptions)!;

    private void CheckSourceProjections()
    {
        Equal(documents.Stories.Count, 35, "Export contains every source story");
        Equal(documents.Stories.Sum(story => story.Beats.Count), 91, "Export contains every source beat");
        Equal(documents.Choices.Count, 11, "Export contains every source choice");
        foreach (var item in oracle["storyCases"]!.AsArray().Select(value => value!.AsObject()))
        {
            var view = NarrativeProjection.Story(Story(Text(item, "presentationId")), documents.SpeakerNameKeys,
                Context(item["state"]!.AsObject()), Number(item, "beatIndex"), Flag(item, "ending"));
            var projection = StoryProjection(view);
            foreach (var expected in item["expected"]!.AsObject())
                JsonEqual(projection[expected.Key], expected.Value, Text(item, "name") + ": " + expected.Key);
        }
        foreach (var item in oracle["choiceCases"]!.AsArray().Select(value => value!.AsObject()))
        {
            var choice = Choice(Text(item, "presentationId"));
            var view = NarrativeProjection.Choice(choice, choice.Options.Select(option => option.Id).ToArray(), Context(item["state"]!.AsObject()));
            var projection = JsonSerializer.SerializeToNode(new
            {
                campaignTitle = view.Shell.Title, chapterHeading = view.Shell.ChapterHeading, progress = view.Shell.Progress,
                progressFraction = view.Shell.ProgressFraction, progressSegments = view.Shell.ProgressSegments,
                backdropSource = view.Shell.Art.Source, eyebrow = view.Eyebrow, view.Prompt, view.Context, view.Options, note = view.Note,
            }, jsonOptions)!;
            foreach (var expected in item["expected"]!.AsObject()) JsonEqual(projection[expected.Key], expected.Value, Text(item, "name") + ": " + expected.Key);
        }
        Equal(oracle["projectionCases"]!.AsArray().Count, 4, "Source edge projection fixtures are present");
        Equal(oracle["lifecycleCases"]!.AsArray().Count, 11, "Source DOM lifetime fixtures are present");
        foreach (var item in oracle["projectionCases"]!.AsArray().Select(value => value!.AsObject()))
        {
            var presentation = JsonSerializer.Deserialize<NarrativePresentationDocument>(item["presentation"]!.ToJsonString(), jsonOptions)!;
            var story = JsonSerializer.Deserialize<StoryEntry>(item["story"]!.ToJsonString(), jsonOptions)!;
            var speakers = JsonSerializer.Deserialize<Dictionary<string, string>>(item["speakerNameKeys"]!.ToJsonString(), jsonOptions)!;
            var state = item["state"]!.AsObject();
            var context = new NarrativeContext(messages, presentation, state["battleHistory"]!.AsArray().Count, Text(state, "status") == "active");
            if (item["expectedError"] is not null)
            {
                Throws(() => NarrativeProjection.Story(story, speakers, context, Number(item, "beatIndex"), Flag(item, "ending")), "Unknown speaker is invalid authoring, not display text");
                continue;
            }
            var view = NarrativeProjection.Story(story, speakers, context, Number(item, "beatIndex"), Flag(item, "ending"));
            var projection = StoryProjection(view);
            foreach (var expected in item["expected"]!.AsObject())
                JsonEqual(projection[expected.Key], expected.Value, Text(item, "name") + ": " + expected.Key);
        }
        foreach (var chapter in metadata.Chapters.Keys)
        {
            var zero = CampaignShellView.Create(new NarrativeContext(messages, new NarrativePresentationDocument
                { Schema = 2, TitleKey = metadata.TitleKey, Chapters = metadata.Chapters, BattleTotal = 0 }, 0, true), chapter);
            Equal(zero.ProgressFraction, 0d, "Zero battle count does not divide by zero"); Equal(zero.ProgressSegments, 1, "Zero battle count retains a segment");
        }
        var unknown = new StoryEntry { Id = "speaker-fixture", Chapter = documents.Stories[0].Chapter,
            Beats = new() { new() { Id = "unmapped-speaker", Speaker = "unmapped", TextKey = documents.Stories[0].Beats[0].TextKey } } };
        Throws(() => NarrativeProjection.Story(unknown, documents.SpeakerNameKeys, new(messages, metadata, 0, true), 0, false), "Unknown speaker is rejected explicitly");
        Throws(() => NarrativeProjection.Story(new StoryEntry { Id = "empty" }, documents.SpeakerNameKeys, new(messages, metadata, 0, true), 0, false), "Empty beat document is rejected explicitly");
    }

    private void CheckLocalizationBoundary()
    {
        var catalog = ResourceLoader.Load<Translation>(GodotTextCatalog.ResourcePath);
        var known = catalog.GetMessageList().ToHashSet(StringComparer.Ordinal);
        var alternate = new GameText(key => known.Contains(key) ? catalog.GetMessage(key).ToString() + " [fixture]" : null, CultureInfo.InvariantCulture);
        var entry = documents.Stories[0];
        var before = JsonSerializer.Serialize(documents, jsonOptions);
        var first = NarrativeProjection.Story(entry, documents.SpeakerNameKeys, new(messages, metadata, 0, true), 0, false);
        var second = NarrativeProjection.Story(entry, documents.SpeakerNameKeys, new(alternate, metadata, 0, true), 0, false);
        Equal(second.Title, first.Title + " [fixture]", "Injected catalog owns story wording");
        Equal(second.Text, first.Text + " [fixture]", "Injected catalog owns beat wording");
        Equal(second.Shell.ExitLabel, first.Shell.ExitLabel + " [fixture]", "Injected catalog owns shell labels");
        screen.ShowStory(entry, documents.SpeakerNameKeys, new(alternate, metadata, 0, true), false, () => { }, () => { }, () => { });
        Equal(Find<Label>("StoryTitle").Text, second.Title, "Actual widget renders injected catalog, not hardcoded prose");
        Equal(Find<Button>("StoryPrimary").Text, second.PrimaryLabel, "Actual widget renders injected navigation");
        Equal(JsonSerializer.Serialize(documents, jsonOptions), before, "Localization does not mutate authored identities or keys");
        var reorderedJson = JsonSerializer.SerializeToNode(entry, jsonOptions)!.AsObject();
        var original = reorderedJson["beats"]!.AsArray().Select(value => value!.DeepClone()).Reverse().ToArray();
        reorderedJson["beats"] = new JsonArray(original);
        var reordered = JsonSerializer.Deserialize<StoryEntry>(reorderedJson.ToJsonString(), jsonOptions)!;
        var moved = reordered.Beats.FindIndex(beat => beat.Id == entry.Beats[0].Id);
        Equal(NarrativeProjection.Story(reordered, documents.SpeakerNameKeys, new(messages, metadata, 0, true), moved, false).Text,
            first.Text, "Reordering does not change semantic beat key ownership");
        var noSceneJson = JsonSerializer.SerializeToNode(entry, jsonOptions)!.AsObject(); noSceneJson["scene"] = null;
        var noScene = JsonSerializer.Deserialize<StoryEntry>(noSceneJson.ToJsonString(), jsonOptions)!;
        var chapterView = NarrativeProjection.Story(noScene, documents.SpeakerNameKeys, new(messages, metadata, 0, true), 0, false);
        Equal(chapterView.Shell.Art.Source, metadata.Chapters[entry.Chapter].Art.Source, "Null scene uses the explicitly owned chapter backdrop");
        Equal(chapterView.Speaker, first.Speaker, "Known narrator remains localized without a scene");
        Require(chapterView.Narration, "Known narrator without a portrait is valid authoring");
        var missing = new GameText(key => key == entry.TitleKey ? null : known.Contains(key) ? catalog.GetMessage(key).ToString() : null, CultureInfo.InvariantCulture);
        Throws(() => documents.Validate(missing), "A missing content message fails at load validation, before navigation");
        var duplicate = JsonSerializer.SerializeToNode(documents, jsonOptions)!.AsObject();
        duplicate["stories"]![0]!["beats"]![1]!["id"] = entry.Beats[0].Id;
        Throws(() => JsonSerializer.Deserialize<StoryDocument>(duplicate.ToJsonString(), jsonOptions)!.Validate(messages), "Duplicate beat IDs are rejected");
        var oldShape = JsonSerializer.SerializeToNode(entry, jsonOptions)!.AsObject(); oldShape["title"] = "obsolete literal";
        try { JsonSerializer.Deserialize<StoryEntry>(oldShape.ToJsonString(), jsonOptions); throw new InvalidOperationException("Obsolete literal field accepted."); }
        catch (JsonException) { assertions++; }
    }

    private async Task CheckSourceWidgets()
    {
        foreach (var item in oracle["storyCases"]!.AsArray().Select(value => value!.AsObject()))
        {
            var ending = Flag(item, "ending"); var index = Number(item, "beatIndex");
            // Source-only projections at nonzero ending indices characterize its quirk; ordinary navigation never reaches them.
            if (ending && index != 0) continue;
            var entry = Story(Text(item, "presentationId")); var expected = item["expected"]!;
            var advanced = 0; var finished = 0;
            screen.ShowStory(entry, documents.SpeakerNameKeys, Context(item["state"]!.AsObject()), ending, () => advanced++, () => finished++, () => { });
            for (var beat = 0; beat < index; beat++) Press("StoryPrimary");
            await Layout();
            Equal(screen.BeatIndex, index, "Native cursor matches source visible beat"); Equal(advanced + finished, 0, "Paging does not invoke canonical callbacks");
            foreach (var (name, property) in new[] { ("StoryKicker", "kicker"), ("StoryDate", "date"), ("StoryLocation", "location"),
                ("StoryTitle", "title"), ("StorySummary", "summary"), ("StorySpeaker", "speaker"), ("StoryBeat", "text"),
                ("CampaignTitle", "campaignTitle"), ("CampaignChapter", "chapterHeading"), ("ProgressCount", "progress") })
                Equal(Find<Label>(name).Text, Text(expected, property), $"Native {name} equals source DOM");
            Equal(Find<Button>("StoryPrimary").Text, Text(expected, "primaryLabel"), "Native primary equals source DOM");
            Equal(screen.FindChildren("StoryBeat", "Label", true, false).Count, 1, "Exactly one local dialogue beat is mounted");
            Equal(Find<Control>("BeatDots").GetChildCount(), entry.Beats.Count, "Native dots retain complete beat count");
            for (var dot = 0; dot < entry.Beats.Count; dot++)
                Equal(((ColorRect)Find<Control>("BeatDots").GetChild(dot)).Color, dot <= index ? new Color("#d7aa50") : new Color(1, 1, 1, .15f), "Native active dot matches source beat cursor");
            Require(screen.RenderingDiagnostics.Count == 0, "Every source narrative image loads without a substitute");
            Equal(screen.FindChild("StoryPortrait", true, false) is not null, expected["portraitSource"] is not null, "Narrator has no invented portrait");
            if (screen.PresentedStory!.Portrait is { } portrait)
                Equal(Find<TextureRect>("StoryPortrait").Texture.ResourcePath, portrait.ResourcePath!, "Original portrait texture is mounted");
            Equal(Find<TextureRect>("CampaignBackdrop").Texture.ResourcePath, screen.PresentedStory!.Shell.Art.ResourcePath!, "Original scene/chapter texture is mounted");
            Equal(screen.FindChild("MissionBrief", true, false) is not null, entry.Mission is not null, "Mission panel presence follows source");
        }
        foreach (var item in oracle["choiceCases"]!.AsArray().Select(value => value!.AsObject()))
        {
            var entry = Choice(Text(item, "presentationId")); var expected = item["expected"]!;
            string? selected = null;
            screen.ShowChoice(entry, entry.Options.Select(option => option.Id).ToArray(), Context(item["state"]!.AsObject()), id => selected = id, () => { });
            await Layout();
            Equal(Find<Label>("ChoiceEyebrow").Text, Text(expected, "eyebrow"), "Native choice eyebrow");
            Equal(Find<Label>("ChoicePrompt").Text, Text(expected, "prompt"), "Native choice prompt");
            Equal(Find<Label>("ChoiceContext").Text, Text(expected, "context"), "Native choice context");
            Equal(Find<Label>("ChoiceNote").Text, Text(expected, "note"), "Native choice note");
            foreach (var option in expected["options"]!.AsArray())
            {
                var button = Find<Button>("Choice_" + Text(option!, "id"));
                foreach (var (name, property) in new[] { ("ChoiceNumber", "number"), ("ChoiceLabel", "label"), ("ChoiceDetail", "detail"), ("ChoiceConsequence", "consequence") })
                    Equal(((Label)button.FindChild(name, true, false)).Text, Text(option!, property), "Native option " + property);
            }
            Require(screen.RenderingDiagnostics.Count == 0, "Choice chapter backdrop uses actual exported art");
            Press("Choice_" + entry.Options[0].Id); Equal(selected, entry.Options[0].Id, "Choice dispatch preserves original ID");
        }
    }

    private void CheckNormalTransitions()
    {
        foreach (var item in oracle["transitionCases"]!.AsArray().Select(value => value!.AsObject()).Where(value => Number(value, "initialBeat") == 0))
        {
            var definition = oracle["definition"]!.AsObject();
            var runtime = new CampaignRuntime(definition, item["initialState"]!.AsObject());
            if (Text(runtime.Node(), "type") != "story") continue;
            JsonObject? saved = null;
            void Bind()
            {
                var node = runtime.Node();
                if (Text(node, "type") != "story") return;
                screen.ShowStory(Story(Text(node, "presentation")), documents.SpeakerNameKeys, Context(runtime.Snapshot()), false,
                    () => { runtime.Advance(); saved = CampaignSave.Load(CampaignSave.Create(definition, runtime.Snapshot()), definition)["state"]!.AsObject(); Bind(); },
                    () => throw new InvalidOperationException("Ordinary story invoked finish."), () => { });
            }
            Bind();
            foreach (var expected in item["expected"]!.AsArray())
            {
                Press("StoryPrimary");
                JsonEqual(runtime.Snapshot(), expected!["state"], "Normal callback owns canonical advancement exactly once");
                JsonEqual(saved, expected["savedState"], "Only normal final beat callback persists");
                if (Text(runtime.Node(), "type") == "story") Equal(screen.BeatIndex, Number(expected, "beatIndex"), "Normal local cursor matches source after each click");
            }
        }
    }

    private void CheckEndingTransitions()
    {
        foreach (var item in oracle["transitionCases"]!.AsArray().Select(value => value!.AsObject()).Where(value => Number(value, "initialBeat") == 0))
        {
            var definition = oracle["definition"]!.AsObject();
            var runtime = new CampaignRuntime(definition, item["initialState"]!.AsObject());
            if (Text(runtime.Node(), "type") != "ending") continue;
            JsonObject? saved = null; var exits = 0; var normal = 0;
            void Bind() => screen.ShowStory(Story(Text(runtime.Node(), "presentation")), documents.SpeakerNameKeys, Context(runtime.Snapshot()), true,
                () => normal++, () =>
                {
                    if (Text(runtime.Snapshot(), "status") == "active")
                    {
                        runtime.Advance(); saved = CampaignSave.Load(CampaignSave.Create(definition, runtime.Snapshot()), definition)["state"]!.AsObject(); Bind();
                    }
                    else exits++;
                }, () => exits++);
            Bind();
            var operations = item["operations"]!.AsArray();
            for (var index = 0; index < operations.Count; index++)
            {
                var expected = item["expected"]![index]!;
                // There is deliberately no ending next-beat button; only finish can be dispatched.
                if (operations[index]!.GetValue<string>() == "finish") Press("StoryPrimary");
                Equal(screen.BeatIndex, 0, "Ending never pages normal local beats"); Equal(normal, 0, "Ending never calls normal advancement");
                Equal(exits, Number(expected, "exitCount"), "Ending finish/exit contract matches source");
                JsonEqual(runtime.Snapshot(), expected["state"], "Ending canonical outcome matches source, including failed");
                JsonEqual(saved, expected["savedState"], "Ending persistence occurs only in active finish callback");
                Equal(Find<Button>("StoryPrimary").Text, Text(expected, "primaryLabel"), "Ending finish label follows caller status");
            }
        }
    }

    private void CheckLifecycle()
    {
        var entry = documents.Stories.First(value => value.Beats.Count > 1);
        var context = new NarrativeContext(messages, metadata, 0, true); var continuations = 0; var exits = 0;
        void Bind() => screen.ShowStory(entry, documents.SpeakerNameKeys, context, false, () => continuations++, () => throw new Exception("Unexpected finish"), () => exits++);
        Bind(); var stalePage = Find<Button>("StoryPrimary"); stalePage.EmitSignal(Button.SignalName.Pressed);
        stalePage.EmitSignal(Button.SignalName.Pressed); Equal(screen.BeatIndex, 1, "Double press on old local button cannot skip another beat");
        var staleBinding = Find<Button>("StoryPrimary"); var staleExit = Find<Button>("CampaignExit");
        Bind(); staleBinding.EmitSignal(Button.SignalName.Pressed); staleExit.EmitSignal(Button.SignalName.Pressed);
        Equal(screen.BeatIndex, 0, "Rebound callback is stale"); Equal(exits, 0, "Rebound shell exit is stale");
        var detached = Find<Button>("StoryPrimary"); RemoveChild(screen); detached.EmitSignal(Button.SignalName.Pressed);
        Equal(screen.BeatIndex, 0, "Unmounted callback cannot page"); AddChild(screen); detached.EmitSignal(Button.SignalName.Pressed);
        Equal(screen.BeatIndex, 0, "Remounted old binding remains invalid until explicit bind");
        Bind(); for (var index = 1; index < entry.Beats.Count; index++) Press("StoryPrimary");
        var final = Find<Button>("StoryPrimary"); final.EmitSignal(Button.SignalName.Pressed); final.EmitSignal(Button.SignalName.Pressed); Press("CampaignExit");
        Equal(continuations, 1, "Double final-beat press invokes caller once"); Equal(exits, 0, "Submitted story blocks competing exit");
        Bind(); var back = Find<Button>("CampaignExit"); back.EmitSignal(Button.SignalName.Pressed); back.EmitSignal(Button.SignalName.Pressed); Press("StoryPrimary");
        Equal(exits, 1, "Double back invokes caller once"); Equal(screen.BeatIndex, 0, "Exited binding cannot page");
        var choice = documents.Choices[0]; var selected = 0;
        void BindChoice() => screen.ShowChoice(choice, choice.Options.Select(value => value.Id).ToArray(), context, _ => selected++, () => exits++);
        BindChoice(); var oldChoice = Find<Button>("Choice_" + choice.Options[0].Id); BindChoice(); oldChoice.EmitSignal(Button.SignalName.Pressed);
        Equal(selected, 0, "Rebound choice callback cannot submit");
        var first = Find<Button>("Choice_" + choice.Options[0].Id); first.EmitSignal(Button.SignalName.Pressed); first.EmitSignal(Button.SignalName.Pressed);
        foreach (var option in choice.Options) Press("Choice_" + option.Id);
        Equal(selected, 1, "Exactly one choice can submit per binding");
        BindChoice(); var detachedChoice = Find<Button>("Choice_" + choice.Options[0].Id); RemoveChild(screen); detachedChoice.EmitSignal(Button.SignalName.Pressed); AddChild(screen);
        Equal(selected, 1, "Unmounted choice callback cannot submit");
        Throws(() => NarrativeProjection.Choice(choice, new[] { "absent-choice" }, context), "Unknown canonical option is rejected");
        Throws(() => NarrativeProjection.Choice(choice, new[] { choice.Options[0].Id, choice.Options[0].Id }, context), "Duplicate canonical option is rejected");
        var subset = NarrativeProjection.Choice(choice, new[] { choice.Options[^1].Id }, context);
        Equal(subset.Options.Count, 1, "Only canonical available options are presented");
        Equal(subset.Options[0].Number, $"0{choice.Options.Count}", "Filtering preserves source option number");
    }

    private void CheckShellLifecycle()
    {
        var shell = new CampaignShell(); AddChild(shell); var exits = 0;
        var view = CampaignShellView.Create(new NarrativeContext(messages, metadata, 0, true), documents.Stories[0].Chapter);
        shell.Show(view, () => exits++);
        var old = (Button)shell.FindChild("CampaignExit", true, false);
        shell.Show(view, () => exits++); old.EmitSignal(Button.SignalName.Pressed);
        Equal(exits, 0, "Reused shell rejects old exit callback");
        var current = (Button)shell.FindChild("CampaignExit", true, false);
        current.EmitSignal(Button.SignalName.Pressed); current.EmitSignal(Button.SignalName.Pressed);
        Equal(exits, 1, "Shell exit is a single submission even without immediate caller navigation");
        shell.Show(view, () => exits++); current = (Button)shell.FindChild("CampaignExit", true, false);
        RemoveChild(shell); current.EmitSignal(Button.SignalName.Pressed); AddChild(shell); current.EmitSignal(Button.SignalName.Pressed);
        Equal(exits, 1, "Unmounted and remounted shell cannot revive previous callbacks");
        shell.Show(view, () => exits++); ((Button)shell.FindChild("CampaignExit", true, false)).EmitSignal(Button.SignalName.Pressed);
        Equal(exits, 2, "Explicit shell rebind allows fresh navigation");
        RemoveChild(shell); shell.QueueFree();
    }

    private async Task CheckLayout()
    {
        var entry = documents.Stories.First(value => value.Mission is not null && value.Beats.Count > 1);
        var before = GetViewport().GetVisibleRect().Size;
        var window = GetWindow(); window.Size = new Vector2I(640, 360); await Layout();
        screen.ShowStory(entry, documents.SpeakerNameKeys, new(messages, metadata, 5, true), false, () => { }, () => { }, () => { }); await Layout();
        Equal(Find<GridContainer>("MissionBrief").Columns, 1, "Narrow source mission layout uses one column");
        Equal(Find<GridContainer>("StoryActions").Columns, 1, "Narrow source actions use one column");
        Require(!Find<CampaignProgress>("CampaignProgress").Visible, "Narrow source breakpoint hides progress");
        var scroll = Find<ScrollContainer>("NarrativeScroll"); scroll.ScrollVertical = 50; await Layout();
        Require(scroll.ScrollVertical > 0, "Long narrative is scrollable at narrow height"); var offset = scroll.ScrollVertical;
        Press("StoryPrimary"); await Layout();
        Equal(screen.BeatIndex, 1, "Scrolling does not alter local progression");
        Equal(scroll.ScrollVertical, offset, "Local beat rendering preserves existing scroll container offset");
        window.Size = new Vector2I(1000, 500); await Layout();
        Equal(screen.BeatIndex, 1, "Resize preserves current local beat");
        Equal(Find<GridContainer>("MissionBrief").Columns, 3, "Wide mission layout returns to three columns");
        Require(Find<CampaignProgress>("CampaignProgress").Visible, "Wide layout shows campaign progress");
        var card = Find<Control>("NarrativeCard"); Require(card.Size.X <= screen.Size.X, "Wrapping does not create horizontal overflow");
        screen.ShowStory(entry, documents.SpeakerNameKeys, new(messages, metadata, 5, true), false, () => { }, () => { }, () => { }); await Layout();
        Equal(Find<ScrollContainer>("NarrativeScroll").ScrollVertical, 0, "New caller binding begins at the top");
        var choice = documents.Choices[0]; screen.ShowChoice(choice, choice.Options.Select(value => value.Id).ToArray(), new(messages, metadata, 0, true), _ => { }, () => { });
        window.Size = new Vector2I(640, 360); await Layout(); Equal(Find<GridContainer>("ChoiceOptions").Columns, 1, "Narrow choice cards stack");
        window.Size = new Vector2I(1000, 500); await Layout(); Equal(Find<GridContainer>("ChoiceOptions").Columns, 2, "Wide choice cards use source two columns");
        window.Size = new Vector2I((int)before.X, (int)before.Y); await Layout();
    }

    private void CheckMissingArt()
    {
        var entry = documents.Stories.First(story => story.Beats.Any(beat => metadata.Portraits.ContainsKey(beat.Speaker)));
        var missing = new NarrativePresentationDocument { Schema = metadata.Schema, TitleKey = metadata.TitleKey, CompletionLabelKey = metadata.CompletionLabelKey,
            BattleTotal = metadata.BattleTotal, Chapters = metadata.Chapters,
            StoryScenes = metadata.StoryScenes.ToDictionary(pair => pair.Key, pair => new NarrativeArt { Source = pair.Value.Source, ResourcePath = "res://missing-narrative-art.png" }),
            Portraits = metadata.Portraits.ToDictionary(pair => pair.Key, pair => new NarrativeArt { Source = pair.Value.Source }) };
        screen.ShowStory(entry, documents.SpeakerNameKeys, new(messages, missing, 0, true), false, () => { }, () => { }, () => { });
        Require(screen.RenderingDiagnostics.Any(message => message.Contains("missing-narrative-art.png")), "Absent exported scene gets explicit diagnostic");
        for (var index = 0; index < entry.Beats.Count && screen.PresentedStory!.Portrait is null; index++) Press("StoryPrimary");
        Require(screen.RenderingDiagnostics.Any(message => message.Contains("portrait")), "Missing portrait export gets explicit diagnostic");
        Require(screen.FindChild("StoryPortrait", true, false) is null, "Missing portrait does not fabricate substitute art");
        Require(screen.FindChild("CampaignBackdrop", true, false) is null, "Missing scene does not silently use unrelated chapter art");
    }

    private void Equal<T>(T actual, T expected, string message) => Require(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}, got {actual}.");
    private void JsonEqual(JsonNode? actual, JsonNode? expected, string message) => Require(JsonNode.DeepEquals(actual, expected), $"{message}\nExpected: {expected}\nActual: {actual}");
    private void Require(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }
    private void Throws(Action action, string message) { try { action(); } catch (InvalidOperationException) { assertions++; return; } throw new InvalidOperationException(message); }
}
