#nullable enable
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Feedback;

namespace Empire.Presentation;

internal static class BattleTextChecks
{
    internal static JsonObject OpeningLevel() => (JsonObject)PresentationJson
        .Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
        .Single(level => level["id"]!.GetValue<string>() == "c01-01").DeepClone();

    internal static BattleText Load(GrayFlagEngine engine, GameText text)
    {
        var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");
        var objectives = ObjectiveText.CreateDefault(text);
        objectives.ValidateCoverage(engine.Objectives.Keys());
        var document = BattleTextDocument.Parse(
            Godot.FileAccess.GetFileAsString("res://data/grayflag-battle-text.json"), levels, engine.Objectives);
        return new BattleText(text, document, objectives.Seal());
    }
}
