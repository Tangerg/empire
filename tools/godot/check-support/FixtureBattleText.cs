using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Feedback;

namespace Empire.Checks;

/// <summary>Composes current shipped battle presentation, never adapting historical source fixtures.</summary>
internal static class FixtureBattleText
{
    internal static BattleText Load(string repositoryRoot, GrayFlagEngine engine, GameText text)
    {
        var data = Path.Combine(repositoryRoot, "godot", "data");
        var levels = EcmaJson.Parse(File.ReadAllText(Path.Combine(data, "grayflag-campaign-levels.json")))!.AsArray();
        var document = BattleTextDocument.Parse(new BattleTextDefinitions(levels, engine.Objectives, engine.ScenarioEffects),
            File.ReadAllText(Path.Combine(data, "grayflag-battle-text.json")));
        var objectives = ObjectiveText.CreateDefault(text);
        objectives.ValidateCoverage(engine.Objectives.Keys());
        return new BattleText(text, document, objectives.Seal());
    }

    internal static SessionPresentationText Session(string repositoryRoot, GrayFlagEngine engine, GameText text)
    {
        var data = Path.Combine(repositoryRoot, "godot", "data");
        return SessionPresentationText.Compose(new SessionTextRules(engine, text),
            EcmaJson.Parse(File.ReadAllText(Path.Combine(data, "grayflag-campaign-levels.json")))!.AsArray(),
            File.ReadAllText(Path.Combine(data, "grayflag-battle-text.json")),
            File.ReadAllText(Path.Combine(data, "grayflag-content-text.json")));
    }
}
