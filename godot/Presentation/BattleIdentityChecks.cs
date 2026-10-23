#nullable enable
using System.Globalization;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Localization;
using Empire.Presentation.Feedback;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>Instance identity is presentation-only, including campaign reconstruction and detached casualty records.</summary>
internal static class BattleIdentityChecks
{
    internal static void Check(Node owner)
    {
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
        var definition = PresentationJson.Load<JsonObject>("res://data/grayflag-campaign.json");
        var translation = ResourceLoader.Load<Translation>(GodotTextCatalog.ResourcePath);
        var messages = translation.GetMessageList().ToDictionary(key => key, key => translation.GetMessage(key).ToString(), StringComparer.Ordinal);
        var identityKeys = StoryDocument.Load(GodotTextCatalog.LoadSimplifiedChinese()).IdentityNameKeys;
        string? baselineState = null, baselineEvents = null, baselineDigest = null, baselineSave = null;
        foreach (var alternate in new[] { false, true })
        {
            var catalog = new Dictionary<string, string>(messages, StringComparer.Ordinal);
            if (alternate)
                foreach (var (identity, key) in identityKeys)
                    catalog[key] = "Alternate identity " + identity;
            var text = new GameText(key => catalog.GetValueOrDefault(key), CultureInfo.GetCultureInfo("zh-CN"));
            var presentation = BattleTextChecks.Session(engine, text);
            GrayFlagCampaign Campaign() => new(new(engine, presentation), definition, levels);
            var campaign = Campaign();
            campaign.Advance();
            campaign.Advance();
            campaign.Choose("steady-advance");
            campaign.BeginBattle();
            var session = campaign.Battle!;
            var opening = session.Snapshot();
            var bindings = campaign.RequestSnapshot()!["rosterBindings"]!.AsArray();
            string Personal(string identity) => text.Format(identityKeys[identity]);
            var subjects = new[]
            {
                (Key: "campaign-torren", Name: Personal("torren")),
                (Key: "campaign-elin", Name: Personal("elin")),
                (Key: "vanguard-2", Name: presentation.Content.UnitName("c01.swordsman")),
                (Key: "ridge-scout", Name: presentation.Content.UnitName("c01.archer")),
            };
            Require(subjects[0].Name != subjects[2].Name && subjects[1].Name != subjects[3].Name,
                "Named companions must be distinguishable from generic units with the same content type.");
            var screen = new BattleScreen();
            owner.AddChild(screen);
            screen.Board.AnimationsEnabled = false;
            var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
            var registeredLevel = levels.OfType<JsonObject>().Single(level => level["id"]!.GetValue<string>() == "c01-01");
            var rendering = BoardSceneRendering.Load(scene, registeredLevel, engine.CreateState(registeredLevel)["map"]!.AsObject());
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(),
                engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), campaign.BattleLevel(), scene);
            screen.ConfigureGround(rendering.Ground);
            foreach (var (key, expected) in subjects)
            {
                using var cancel = new InputEventKey { Keycode = Key.Escape, Pressed = true };
                screen._UnhandledKeyInput(cancel);
                var unit = Unit(session.Snapshot(), key);
                var id = unit["id"]!.GetValue<int>();
                screen.SelectCell(session.GetTacticalQueries(0).Unit(id)!.At);
                var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
                Require(hud.Projection!.Sections.Single(section => section.Key == "unit").Title == expected
                    && session.UnitName(EcmaJson.ToElement(unit)) == expected,
                    $"The mounted campaign HUD lost the instance name for '{key}'.");
                Require(FacingText(presentation, session, opening, opening, id) == text.Format("battle.event.facing", expected),
                    $"Event text must distinguish the actual campaign record '{key}' from its type.");
                if (unit["owner"]!.GetValue<int>() == 1)
                {
                    Descendants(screen).OfType<Button>().Single(button =>
                        button.GetMeta("hud_key", "").AsString() == "stances/face:north").EmitSignal(Button.SignalName.Pressed);
                    var transition = session.GetSnapshot().Transition!;
                    Require(transition.Events.Any(value => value.GetProperty("type").GetString() == "facingChanged")
                        && transition.EventDescriptions.Contains(text.Format("battle.event.facing", expected))
                        && screen.RecentMessages.Contains(text.Format("battle.event.facing", expected)),
                        $"Committed native HUD actions must display '{key}' by instance identity in the battle log.");
                }
            }
            owner.RemoveChild(screen);
            screen.Free();

            var state = EcmaJson.Stringify(session.Snapshot());
            var events = EcmaJson.Stringify(new JsonArray(session.Events.Select(value => (JsonNode)value).ToArray()));
            var digest = StateDigest.Hash(session.Snapshot());
            var save = EcmaJson.Stringify(engine.SaveBattle(session.Snapshot(), "2026-10-22T00:00:00.000Z"));
            if (baselineState is null)
                (baselineState, baselineEvents, baselineDigest, baselineSave) = (state, events, digest, save);
            else
                Require(state == baselineState && events == baselineEvents && digest == baselineDigest && save == baselineSave,
                    "Changing only canonical identity wording must not alter state, events, digest or the saved battle.");

            void CheckNames(GrayFlagSession restored)
            {
                foreach (var (key, expected) in subjects)
                    Require(restored.UnitName(EcmaJson.ToElement(Unit(restored.Snapshot(), key))) == expected,
                        $"Campaign reconstruction lost the identity of '{key}'.");
            }
            var loaded = Campaign();
            loaded.Load(campaign.Save());
            CheckNames(loaded.Battle!);
            Require(EcmaJson.Stringify(loaded.Battle!.Snapshot()) == state
                && EcmaJson.Stringify(new JsonArray(loaded.Battle.Events.Select(value => (JsonNode)value).ToArray())) == events,
                "In-memory application save/load must retain the named battle's exact state and events.");
            loaded.RestartBattle();
            CheckNames(loaded.Battle!);
            Require(EcmaJson.DeepEquals(loaded.Battle!.Snapshot(), opening), "Restart must reconstruct the same canonical opening.");
            loaded.ExitBattle();
            var camp = Campaign();
            camp.Load(loaded.Save());
            Require(camp.AtCamp, "Camp save must remain at camp until explicitly re-entered.");
            camp.BeginBattle();
            CheckNames(camp.Battle!);
            CheckCampaignSummary(engine, camp, subjects[0].Name);
            CheckDetachedRecords(presentation, session, opening, subjects[0].Name, subjects[2].Name);
            CheckActors(presentation);
            if (!alternate)
                CheckInvalidBindings(engine, levels, presentation, bindings);
        }
    }

    private static void CheckCampaignSummary(BattleEngine engine, GrayFlagCampaign campaign, string expectedName)
    {
        // The existing result-flow terminal-save fixture, not a claim of a victorious gameplay route.
        var document = campaign.Save();
        var terminal = campaign.Battle!.Snapshot();
        engine.Combat.Damage.Withdraw(terminal, Unit(terminal, "campaign-torren"), leaveCorpse: true, _ => { });
        var conclusion = BattleConclusion.LastTeamRemaining(1);
        foreach (var player in terminal["players"]!.AsArray().OfType<JsonObject>())
            player["alive"] = player["team"]!.GetValue<int>() == conclusion.Team;
        terminal["phase"] = "over";
        terminal["conclusion"] = conclusion.ToJson();
        terminal["scenario"]!["eventCounts"]!["gameOver"] = 1;
        document["battle"] = engine.SaveBattle(terminal);
        document["events"] = new JsonArray(new JsonObject { ["type"] = "gameOver", ["conclusion"] = conclusion.ToJson() });
        campaign.Load(document);
        var result = campaign.BattleSummary();
        Require(result.Outcome == "victory" && result.Summary.Fallen.SequenceEqual(new[] { expectedName }),
            "The actual campaign summary boundary must forward instance naming for a fallen roster companion.");
    }

    private static void CheckActors(SessionPresentationText presentation)
    {
        foreach (var (level, key, identity) in new[]
        {
            ("c01-01", "roderick-reserve", "roderick"), ("c01-01", "vanguard-cain", "cain"),
            ("c01-02", "cain", "cain"), ("c01-03", "roderick", "roderick"),
            ("c01-09", "caged-ivra", "ivra"), ("c01-10", "roderick", "roderick"),
            ("c01-12", "imperial-cain", "cain"),
        })
        {
            var names = presentation.UnitNames(level, new JsonArray());
            // Deliberately unrelated type: equal hero-type wording cannot hide a missing actor binding.
            var unit = new JsonObject { ["key"] = key, ["type"] = "c01.archer" };
            Require(names.Identity(key) == identity && names.Name(EcmaJson.ToElement(unit)) ==
                IdentityNames.Resolve(presentation.Messages, presentation.IdentityNameKeys, identity),
                $"Authored actor '{level}/{key}' must select canonical identity '{identity}', independently of type.");
            unit["key"] = "unbound-generic";
            Require(names.Identity("unbound-generic") is null
                && names.Name(EcmaJson.ToElement(unit)) == presentation.Content.UnitName("c01.archer"),
                "An unbound unit must remain a generic content-type name.");
        }
    }

    private static void CheckDetachedRecords(SessionPresentationText presentation, GrayFlagSession session,
        JsonObject opening, string named, string generic)
    {
        // Projection fixtures only: no claim that the campaign reached these casualty or embarkation states.
        var torren = Unit(opening, "campaign-torren");
        var id = torren["id"]!.GetValue<int>();
        foreach (var location in new[] { "before", "embarked", "fallen" })
        {
            var after = opening.DeepClone().AsObject();
            after["units"]!.AsArray().Remove(Unit(after, "campaign-torren"));
            if (location == "embarked")
                after["embarkedUnits"]!.AsArray().Add(new JsonObject { ["unit"] = torren.DeepClone() });
            if (location == "fallen")
                after["markers"]!.AsArray().Add(new JsonObject { ["fallenUnit"] = torren.DeepClone() });
            var before = location == "before" ? opening : after;
            Require(FacingText(presentation, session, before, after, id) == presentation.Messages.Format("battle.event.facing", named),
                $"Named event projection must find the {location} unit record without requiring a live unit.");
            if (location != "fallen")
                continue;
            var genericFallen = Unit(after, "vanguard-2").DeepClone().AsObject();
            after["units"]!.AsArray().Remove(Unit(after, "vanguard-2"));
            genericFallen["owner"] = 1;
            after["markers"]!.AsArray().Add(new JsonObject { ["fallenUnit"] = genericFallen });
            var summary = CampaignBattleSummary.Create(new(presentation.Messages, presentation.Battle, session.UnitName),
                1, 1, EcmaJson.ToElement(after), Array.Empty<string>(), 0);
            Require(summary.Fallen.SequenceEqual(new[] { named, generic }),
                "Fallen summaries must preserve the named companion while retaining generic casualty type labels.");
        }
    }

    private static void CheckInvalidBindings(BattleEngine engine, JsonArray levels, SessionPresentationText presentation,
        JsonArray rosterBindings)
    {
        JsonArray Binding(string key, string identity) => new(new JsonObject
        {
            ["levelUnitKey"] = key,
            ["campaignUnit"] = identity,
        });
        Reject(() => presentation.UnitNames("c01-01", Binding("campaign-torren", "unknown-identity")),
            "not authored for identity", "Unknown canonical identity must fail, not fall back to the type.");
        var missing = presentation.IdentityNameKeys.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        missing.Remove("torren");
        Reject(() => (presentation with { IdentityNameKeys = missing }).UnitNames("c01-01", rosterBindings),
            "not authored for identity", "A missing canonical name binding must fail before opening battle.");
        missing["torren"] = "missing.identity.name";
        Reject(() => (presentation with { IdentityNameKeys = missing }).UnitNames("c01-01", rosterBindings),
            "Missing localization key", "A missing translated identity name must not fall back to the type.");
        Reject(() => presentation.UnitNames("c01-01", Binding("unknown-unit-key", "torren")),
            "unknown unit key", "A roster binding must reference an authored unit key.");
        Reject(() => presentation.UnitNames("c01-01", Binding("roderick-reserve", "roderick")),
            "Competing identity bindings", "Roster and actor ownership must be disjoint even when identities agree.");
        var metadata = PresentationJson.Load<JsonObject>("res://data/grayflag-battle-text.json");
        metadata["levels"]!["c01-01"]!["actors"]!["unknown-unit-key"] = "roderick";
        Reject(() => BattleTextDocument.Parse(new(levels, engine.Objectives, engine.ScenarioEffects), EcmaJson.Stringify(metadata)),
            "unknown unit key", "Authored actor metadata must reject unknown unit keys.");
    }

    private static string FacingText(SessionPresentationText presentation, GrayFlagSession session,
        JsonObject before, JsonObject after, int id)
    {
        var context = new BattleLogContext(new(session.UnitName, presentation.Content.UnitName,
            presentation.Content.CareerName, presentation.Content.ResourceName,
            player => presentation.Battle.PlayerName("c01-01", player)), EcmaJson.ToElement(before), EcmaJson.ToElement(after));
        return presentation.Events.Describe(context, EcmaJson.ToElement(new JsonObject { ["type"] = "facingChanged", ["unit"] = id }));
    }

    private static JsonObject Unit(JsonObject state, string key) => state["units"]!.AsArray().OfType<JsonObject>()
        .Single(unit => unit["key"]?.GetValue<string>() == key);

    private static IEnumerable<Node> Descendants(Node root) => root.GetChildren()
        .SelectMany(child => new[] { child }.Concat(Descendants(child)));

    private static void Reject(Action action, string diagnostic, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException error) when (error.Message.Contains(diagnostic, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
