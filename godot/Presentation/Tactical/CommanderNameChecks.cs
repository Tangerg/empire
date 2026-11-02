#nullable enable
using System.Globalization;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation.Tactical;

/// <summary>Detached C01 projection fixtures: no battle commands, turns or AI.</summary>
public partial class CommanderNameChecks : Node
{
    private int assertions;

    private void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        assertions++;
    }

    public override void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
                .Single(value => value["id"]!.GetValue<string>() == "c01-01");
            var translation = ResourceLoader.Load<Translation>(GodotTextCatalog.ResourcePath);
            var messages = translation.GetMessageList().ToDictionary(key => key, key => translation.GetMessage(key).ToString(), StringComparer.Ordinal);
            var identityKeys = StoryDocument.Load(GodotTextCatalog.LoadSimplifiedChinese()).IdentityNameKeys;
            foreach (var alternate in new[] { false, true })
            {
                var catalog = new Dictionary<string, string>(messages, StringComparer.Ordinal);
                if (alternate)
                    foreach (var (identity, key) in identityKeys)
                        catalog[key] = "Alternate identity " + identity;
                var text = new GameText(key => catalog.GetValueOrDefault(key), CultureInfo.GetCultureInfo("zh-CN"));
                var presentation = BattleTextChecks.Session(engine, text);
                foreach (var scenario in new[] { "active", "out-of-range", "absent", "generic", "unassigned" })
                {
                    var state = engine.CreateState(level);
                    var units = state["units"]!.AsArray();
                    var commander = state["commanders"]!.AsArray().OfType<JsonObject>().First();
                    var leader = units.OfType<JsonObject>().Single(unit => unit["id"]!.GetValue<int>() == commander["unitId"]!.GetValue<int>());
                    var inspect = scenario is "active" or "generic" ? leader : units.OfType<JsonObject>().First(unit =>
                        unit != leader && unit["owner"]!.GetValue<int>() == leader["owner"]!.GetValue<int>());
                    inspect["commanderId"] = commander["id"]!.DeepClone();
                    if (scenario == "out-of-range")
                        commander["radius"] = 0;
                    if (scenario == "generic")
                        leader["key"] = "unnamed-fixture-leader";
                    if (scenario == "absent")
                        units.Remove(leader);
                    if (scenario == "unassigned")
                        inspect.Remove("commanderId");
                    var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
                    var before = EcmaJson.Stringify(session.Snapshot());
                    var hud = session.ProjectHud(new(0, 0), new(), null, inspect["id"]!.GetValue<int>(), false);
                    if (scenario == "unassigned")
                    {
                        Require(hud.Sections.Single(section => section.Key == "unit-details").Rows.All(row => row.Key != "commander"),
                            "Unassigned units must not gain a fabricated commander row.");
                        Require(session.Revision == 0 && session.Events.Count == 0 && EcmaJson.Stringify(session.Snapshot()) == before,
                            "Unassigned inspection is read-only.");
                        continue;
                    }
                    var row = hud.Sections.Single(section => section.Key == "unit-details").Rows.Single(value => value.Key == "commander");
                    var expected = scenario == "absent" ? text.Format("ui.hud.commander_absent")
                        : text.Format("ui.hud.commander", session.UnitName(EcmaJson.ToElement(leader)),
                            text.Format(scenario == "out-of-range" ? "ui.hud.commander_out_of_range" : "ui.hud.commander_active"));
                    Require(row.Label == expected, scenario + ": HUD must use the canonical localized leader name or explicit absence, never commander ID.");
                    Require(!row.Label.Contains(commander["id"]!.GetValue<string>(), StringComparison.Ordinal), scenario + ": internal commander identity must not leak.");
                    Require(row.Intent is null && session.Events.Count == 0 && session.Revision == 0, scenario + ": inspection cannot issue a command or event.");
                    Require(EcmaJson.Stringify(session.Snapshot()) == before, scenario + ": projection cannot mutate the detached fixture.");
                }
            }
            GD.Print($"Commander name checks passed: {assertions} assertions; C01 detached projections only.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
