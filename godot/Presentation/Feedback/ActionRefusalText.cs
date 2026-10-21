using System.Text.Json;
using Empire.Core;
using Empire.Core.Abilities;
using Empire.Core.Determinism;
using Empire.Core.Registries;
using Empire.Core.State;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

public sealed record ActionRefusalMessage(string Code, string MessageKey,
    Func<ActionRefusalText, JsonElement, object?[]> Arguments);

/// <summary>Formats domain refusals; it never decides whether an action is legal.</summary>
public sealed class ActionRefusalText : KeyedRegistry<ActionRefusalMessage, ActionRefusalText>
{
    private readonly GameText text;
    public ActionRefusalText(GameText text) : base("action refusal presentation")
    {
        this.text = text;
        text.ValidateKeys(new[] { "error.action.reason_separator", "error.action.computer_refused" });
    }
    protected override string KeyOf(ActionRefusalMessage entry) => entry.Code;
    protected override void ValidateEntry(ActionRefusalMessage entry)
    {
        ArgumentNullException.ThrowIfNull(entry.Arguments);
        text.ValidateKeys(new[] { entry.MessageKey });
    }
    public ActionRefusalText Clone() => CopyInto(new ActionRefusalText(text));
    public string Describe(ActionRefusal refusal) => DescribeFacts(refusal.Code, refusal.Details);
    public string DescribeMany(IEnumerable<ActionRefusal> refusals) =>
        string.Join(text.Format("error.action.reason_separator"), refusals.Select(Describe));
    public string DescribeComputer(ActionRefusal refusal) => text.Format("error.action.computer_refused", Describe(refusal));
    private string DescribeFacts(string code, JsonElement details)
    {
        var message = TryGet(code) ?? throw new LocalizationException($"No presentation is registered for refusal '{code}'.");
        return text.Format(message.MessageKey, message.Arguments(this, details));
    }
    private string DescribeNested(JsonElement reasons) => string.Join(text.Format("error.action.reason_separator"),
        reasons.EnumerateArray().Select(reason => DescribeFacts(S(reason, "code"), reason.GetProperty("details"))));
    private static string S(JsonElement details, string key)
    {
        var value = details.GetProperty(key);
        if (value.ValueKind != JsonValueKind.String)
            throw new LocalizationException($"Refusal detail '{key}' must be a string.");
        return FeedbackJson.Text(value);
    }
    private static double N(JsonElement details, string key) => details.GetProperty(key).GetDouble();

    public static ActionRefusalText CreateDefault(GameText text, ReadOnlyContent content, AbilityRegistry abilities)
    {
        string Name(string family, string id) => FeedbackJson.Text((content.Definition(family, id)
            ?? throw new LocalizationException($"Refusal refers to unknown {family} definition '{id}'.")).GetProperty("name"));
        return new ActionRefusalText(text)
            .Register(new("action.over", "error.action.over", (_, _) => []))
            .Register(new("action.deployment_required", "error.action.deployment_required", (_, _) => []))
            .Register(new("action.deployment_finished", "error.action.deployment_finished", (_, _) => []))
            .Register(new("action.unsupported", "error.action.unsupported", (_, d) => [S(d, "kind")]))
            .Register(new("tactics.unit_missing", "error.tactics.unit_missing", (_, d) => [N(d, "unitId")]))
            .Register(new("tactics.not_owner", "error.tactics.not_owner", (_, _) => []))
            .Register(new("tactics.already_acted", "error.tactics.already_acted", (_, _) => []))
            .Register(new("tactics.not_actor", "error.tactics.not_actor", (_, _) => []))
            .Register(new("tactics.path_empty", "error.tactics.path_empty", (_, _) => []))
            .Register(new("tactics.path_origin_mismatch", "error.tactics.path_origin_mismatch", (_, _) => []))
            .Register(new("tactics.target_outside", "error.tactics.target_outside", (_, d) => [N(d, "x"), N(d, "y")]))
            .Register(new("tactics.target_unreachable", "error.tactics.target_unreachable", (_, d) => [N(d, "x"), N(d, "y")]))
            .Register(new("tactics.target_occupied", "error.tactics.target_occupied", (_, _) => []))
            .Register(new("tactics.reaction_unknown", "error.tactics.reaction_unknown", (_, d) => [S(d, "stance")]))
            .Register(new("tactics.facing_invalid", "error.tactics.facing_invalid", (_, d) => [S(d, "facing")]))
            .Register(new("deployment.wrong_phase", "error.deployment.wrong_phase", (_, _) => []))
            .Register(new("deployment.unassigned", "error.deployment.unassigned", (_, _) => []))
            .Register(new("deployment.outside_zone", "error.deployment.outside_zone", (_, _) => []))
            .Register(new("deployment.occupied", "error.deployment.occupied", (_, _) => []))
            .Register(new("deployment.impassable", "error.deployment.impassable", (_, _) => []))
            .Register(new("deployment.swap_outside_zone", "error.deployment.swap_outside_zone", (_, _) => []))
            .Register(new("deployment.swap_impassable", "error.deployment.swap_impassable", (_, _) => []))
            .Register(new("recruitment.terrain_incompatible", "error.recruitment.terrain_incompatible", (_, d) => [Name("terrains", S(d, "terrainId"))]))
            .Register(new("recruitment.not_owner", "error.recruitment.not_owner", (_, _) => []))
            .Register(new("recruitment.occupied", "error.recruitment.occupied", (_, _) => []))
            .Register(new("recruitment.insufficient_resources", "error.recruitment.insufficient_resources", (_, _) => []))
            .Register(new("recruitment.unit_limit", "error.recruitment.unit_limit", (_, d) => [N(d, "limit")]))
            .Register(new("transport.self", "error.transport.self", (_, _) => []))
            .Register(new("transport.not_carrier", "error.transport.not_carrier", (_, d) => [Name("units", S(d, "unitType"))]))
            .Register(new("transport.not_allies", "error.transport.not_allies", (_, _) => []))
            .Register(new("transport.not_adjacent", "error.transport.not_adjacent", (_, _) => []))
            .Register(new("transport.full", "error.transport.full", (_, _) => []))
            .Register(new("transport.carrier_passenger", "error.transport.carrier_passenger", (_, _) => []))
            .Register(new("transport.incompatible_tags", "error.transport.incompatible_tags", (_, _) => []))
            .Register(new("transport.forbidden_tags", "error.transport.forbidden_tags", (_, _) => []))
            .Register(new("transport.carrier_missing", "error.transport.carrier_missing", (_, d) => [N(d, "carrierId")]))
            .Register(new("transport.not_aboard", "error.transport.not_aboard", (_, _) => []))
            .Register(new("transport.disembark_not_adjacent", "error.transport.disembark_not_adjacent", (_, _) => []))
            .Register(new("transport.disembark_occupied", "error.transport.disembark_occupied", (_, _) => []))
            .Register(new("transport.disembark_impassable", "error.transport.disembark_impassable", (_, _) => []))
            .Register(new("career.root_mismatch", "error.career.root_mismatch", (_, _) => []))
            .Register(new("career.path_mismatch", "error.career.path_mismatch", (_, _) => []))
            .Register(new("career.minimum_rank", "error.career.minimum_rank", (_, d) => [N(d, "required")]))
            .Register(new("career.minimum_mastery", "error.career.minimum_mastery", (_, d) => [N(d, "required")]))
            .Register(new("career.insufficient_resources", "error.career.insufficient_resources", (_, _) => []))
            .Register(new("career.unavailable", "error.career.unavailable", (_, d) => [S(d, "careerId")]))
            .Register(new("career.requirements", "error.career.requirements", (presenter, d) => [presenter.DescribeNested(d.GetProperty("reasons"))]))
            .Register(new("formation.unsupported", "error.formation.unsupported", (_, d) => [S(d, "formationId")]))
            .Register(new("formation.adjacent_allies", "error.formation.adjacent_allies", (_, d) => [N(d, "required")]))
            .Register(new("support.commander_unknown", "error.support.commander_unknown", (_, d) => [S(d, "commanderId")]))
            .Register(new("support.commander_not_owner", "error.support.commander_not_owner", (_, _) => []))
            .Register(new("support.tactic_unavailable", "error.support.tactic_unavailable", (_, d) => [S(d, "tacticId")]))
            .Register(new("support.target_invalid", "error.support.target_invalid", (_, _) => []))
            .Register(new("ability.not_owned", "error.ability.not_owned", (_, d) => [Name("units", S(d, "unitType")), abilities.Get(S(d, "abilityId")).Name]))
            .Register(new("ability.weapon_unavailable", "error.ability.weapon_unavailable", (_, d) => [abilities.Get(S(d, "abilityId")).Name, S(d, "weaponId")]))
            .Register(new("ability.unusable", "error.ability.unusable", (_, d) => [abilities.Get(S(d, "abilityId")).Name]))
            .Register(new("ability.target_required", "error.ability.target_required", (_, d) => [abilities.Get(S(d, "abilityId")).Name]))
            .Register(new("ability.target_invalid", "error.ability.target_invalid", (_, _) => []))
            .Register(new("combat.already_casting", "error.combat.already_casting", (_, _) => []));
    }
}
