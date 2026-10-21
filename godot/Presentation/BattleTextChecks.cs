#nullable enable
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Feedback;

namespace Empire.Presentation;

internal static class BattleTextChecks
{
    internal static JsonObject OpeningLevel() => (JsonObject)PresentationJson
        .Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
        .Single(level => level["id"]!.GetValue<string>() == "c01-01").DeepClone();

    internal static BattleText Load(BattleEngine engine, GameText text)
    {
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
        var objectives = ObjectiveText.CreateDefault(text);
        objectives.ValidateCoverage(engine.Objectives.Keys());
        var document = BattleTextDocument.Parse(new BattleTextDefinitions(levels, engine.Objectives, engine.ScenarioEffects),
            Godot.FileAccess.GetFileAsString("res://data/grayflag-battle-text.json"));
        return new BattleText(new(text, objectives.Seal()), document);
    }

    internal static SessionPresentationText Session(BattleEngine engine, GameText text) =>
        SessionPresentationText.Compose(new SessionTextRules(engine, text),
            PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json"),
            Godot.FileAccess.GetFileAsString("res://data/grayflag-battle-text.json"),
            Godot.FileAccess.GetFileAsString("res://data/grayflag-content-text.json"));
}
