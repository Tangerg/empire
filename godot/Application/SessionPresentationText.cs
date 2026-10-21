using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Feedback;

namespace Empire.Application;

public sealed record SessionTextRules(BattleEngine Engine, GameText Messages);

/// <summary>The presentation collaborators consumed by a session; none owns gameplay state.</summary>
public sealed record SessionPresentationText(GameText Messages, ActionRefusalText Refusals,
    BattleText Battle, ContentText Content, CombatModifierText Modifiers, BattleEventText Events)
{
    public static SessionPresentationText Compose(SessionTextRules rules, JsonArray levels,
        string battleTextJson, string contentTextJson)
    {
        var engine = rules.Engine;
        var text = rules.Messages;
        var objectives = ObjectiveText.CreateDefault(text);
        objectives.ValidateCoverage(engine.Objectives.Keys());
        var battle = new BattleText(new(text, objectives.Seal()), BattleTextDocument.Parse(
            new BattleTextDefinitions(levels, engine.Objectives, engine.ScenarioEffects), battleTextJson));
        var content = new ContentText(text, ContentTextDocument.Parse(
            new ContentTextDefinitions(engine.Catalog, engine.Abilities, engine.Reactions,
                engine.Resources.Adapters, engine.Grids), contentTextJson));
        var modifiers = CombatModifierText.CreateDefault(new(text, content));
        modifiers.ValidateCoverage(engine.Combat.ModifierKinds);
        return new(text, ActionRefusalText.CreateDefault(new(text, content)).Seal(), battle, content, modifiers.Seal(),
            BattleEventText.CreateDefault(new(text, battle)));
    }
}
