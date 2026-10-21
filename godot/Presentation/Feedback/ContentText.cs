using Empire.Core.Abilities;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

/// <summary>Resolves content identities and composes command facts through the active text catalog.</summary>
public sealed class ContentText
{
    private readonly GameText text;
    private readonly ContentTextDocument document;

    public ContentText(GameText text, ContentTextDocument document)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(document);
        this.text = text;
        this.document = document;
        document.ValidateKeys(text);
        text.ValidateKeys(new[] { "command.name.weapon", "command.hint.range" });
        _ = text.Format("command.name.weapon", "", "");
        _ = text.Format("command.hint.range", "", 0, 0);
    }

    public string Name(ContentTextKind kind, string id) => text.Format(document.NameKey(kind, id));
    public string UnitName(string id) => Name(ContentTextKind.Unit, id);
    public string CareerName(string id) => Name(ContentTextKind.Career, id);
    public string ResourceName(string id) => text.Format(document.ResourceNameKey(id));
    public string UnitBlurb(string id) => text.Format(document.UnitBlurbKey(id));
    public string AbilityName(string id) => text.Format(document.AbilityNameKey(id));
    public string AbilityHint(string id) => text.Format(document.AbilityHintKey(id));
    public string ReactionName(string id) => text.Format(document.ReactionNameKey(id));
    public string ReactionHint(string id) => text.Format(document.ReactionHintKey(id));
    public string DirectionName(string gridId, string facing) => text.Format(document.DirectionNameKey(gridId, facing));
    public string WeaponBonus(string weaponId, string bonusId) => text.Format(document.WeaponBonusKey(weaponId, bonusId));

    public string CommandName(CommandOption command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var ability = AbilityName(command.Ability);
        return command.Weapon is { } weapon
            ? text.Format("command.name.weapon", ability, Name(ContentTextKind.Weapon, weapon)) : ability;
    }

    public string CommandHint(CommandOption command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var hint = AbilityHint(command.Ability);
        if (command.Weapon is { } weapon)
            _ = document.NameKey(ContentTextKind.Weapon, weapon);
        return command.Range is { } range ? text.Format("command.hint.range", hint, range.Min, range.Max) : hint;
    }
}
