using System.Text.Json;
using Empire.Core.Determinism;
using Empire.Core.Objectives;
using Empire.Core.Registries;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

public sealed record ObjectiveTextFormatter(string Kind, IReadOnlyList<string> MessageKeys,
    Func<GameText, JsonElement, Func<int, string>, string> Description,
    Func<GameText, ObjectiveProgress, Func<ObjectiveProgress, string>, string> Progress);

/// <summary>Formats immutable objective definitions and handler-owned facts, never battlefield state.</summary>
public sealed class ObjectiveText : KeyedRegistry<ObjectiveTextFormatter, ObjectiveText>
{
    private readonly GameText text;

    public ObjectiveText(GameText text) : base("objective presentation")
    {
        ArgumentNullException.ThrowIfNull(text);
        this.text = text;
        text.ValidateKeys(new[]
        {
            "objective.status.completed", "objective.status.failed",
            "objective.status.inactive", "objective.status.cancelled",
        });
    }

    protected override string KeyOf(ObjectiveTextFormatter entry) => entry.Kind;

    protected override void ValidateEntry(ObjectiveTextFormatter entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Kind))
            throw new LocalizationException("An objective formatter kind must not be empty.");
        ArgumentNullException.ThrowIfNull(entry.MessageKeys);
        ArgumentNullException.ThrowIfNull(entry.Description);
        ArgumentNullException.ThrowIfNull(entry.Progress);
        text.ValidateKeys(entry.MessageKeys);
    }

    public ObjectiveText Clone() => CopyInto(new ObjectiveText(text));

    public void ValidateCoverage(IEnumerable<string> kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        foreach (var kind in kinds)
            if (!Has(kind))
                throw new LocalizationException($"No presentation is registered for objective kind '{kind}'.");
    }

    public string Describe(ObjectiveProgress objective, Func<ObjectiveProgress, string>? describeChild = null)
    {
        ArgumentNullException.ThrowIfNull(objective);
        return Require(objective.Kind).Description(text, objective.Definition,
            index => describeChild is null ? Describe(objective.Children[index]) : describeChild(objective.Children[index]));
    }

    public string Describe(JsonElement definition, Func<int, string> describeChild)
    {
        ArgumentNullException.ThrowIfNull(describeChild);
        return Require(String(definition, "type")).Description(text, definition, describeChild);
    }

    public string Progress(ObjectiveProgress objective)
    {
        ArgumentNullException.ThrowIfNull(objective);
        var formatter = Require(objective.Kind);
        return objective.Status switch
        {
            "completed" => text.Format("objective.status.completed"),
            "failed" => text.Format("objective.status.failed"),
            "inactive" => text.Format("objective.status.inactive"),
            "cancelled" => text.Format("objective.status.cancelled"),
            "active" => formatter.Progress(text, objective, Progress),
            _ => throw new LocalizationException($"Unknown objective status '{objective.Status}'."),
        };
    }

    private ObjectiveTextFormatter Require(string kind) => TryGet(kind)
        ?? throw new LocalizationException($"No presentation is registered for objective kind '{kind}'.");

    private static double Number(JsonElement value, string key)
    {
        var property = value.GetProperty(key);
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetDouble(out var number) || !double.IsFinite(number))
            throw new LocalizationException($"Objective field '{key}' must be a finite number.");
        return number;
    }

    private static string String(JsonElement value, string key)
    {
        var property = value.GetProperty(key);
        if (property.ValueKind != JsonValueKind.String)
            throw new LocalizationException($"Objective field '{key}' must be a string.");
        return EcmaJson.ReadString(EcmaJson.Parse(property.GetRawText()));
    }

    private static object ScoreNumber(GameText text, JsonElement value) => String(value, "kind") switch
    {
        "finite" => Number(value, "value"),
        "nan" => text.Format("objective.number.nan"),
        "positiveInfinity" => text.Format("objective.number.positive_infinity"),
        "negativeInfinity" => text.Format("objective.number.negative_infinity"),
        var kind => throw new LocalizationException($"Unknown objective number kind '{kind}'."),
    };

    private static ObjectiveTextFormatter Simple(string kind, string descriptionKey, string progressKey,
        Func<ObjectiveProgress, object?[]> progressArguments) => new(kind, new[] { descriptionKey, progressKey },
            (text, _, _) => text.Format(descriptionKey),
            (text, objective, _) => text.Format(progressKey, progressArguments(objective)));

    public static ObjectiveText CreateDefault(GameText text) => new ObjectiveText(text)
        .Register(Simple("routEnemies", "objective.description.rout_enemies", "objective.progress.rout_enemies",
            objective => [Number(objective.Facts, "remaining")]))
        .Register(Simple("captureHQ", "objective.description.capture_hq", "objective.progress.capture_hq",
            objective => [Number(objective.Facts, "remaining")]))
        .Register(Simple("holdAllVillages", "objective.description.hold_all_villages", "objective.progress.hold_all_villages",
            objective => [Number(objective.Facts, "owned"), Number(objective.Facts, "total")]))
        .Register(new("surviveTurns", new[] { "objective.description.survive_turns", "objective.progress.turns" },
            (catalog, definition, _) => catalog.Format("objective.description.survive_turns", Number(definition, "turns")),
            (catalog, objective, _) => catalog.Format("objective.progress.turns", Number(objective.Facts, "current"), Number(objective.Facts, "target"))))
        .Register(Simple("eliminate", "objective.description.eliminate", "objective.progress.eliminate",
            objective => [Number(objective.Facts, "remaining")]))
        .Register(Simple("destroy", "objective.description.destroy", "objective.progress.destroy",
            objective => [Number(objective.Facts, "remaining")]))
        .Register(Simple("neutralizeComposite", "objective.description.neutralize_composite", "objective.progress.neutralize_composite",
            objective => [Number(objective.Facts, "neutralized"), Number(objective.Facts, "target")]))
        .Register(new("protect", new[] { "objective.description.protect", "objective.progress.protect" },
            (catalog, definition, _) => catalog.Format("objective.description.protect", Number(definition, "untilTurn")),
            (catalog, objective, _) => catalog.Format("objective.progress.protect", Number(objective.Facts, "alive"), Number(objective.Facts, "minimum"))))
        .Register(Simple("escort", "objective.description.escort", "objective.progress.escort",
            objective => [Number(objective.Facts, "arrived"), Number(objective.Facts, "target")]))
        .Register(Simple("control", "objective.description.control", "objective.progress.control",
            objective => [Number(objective.Facts, "owned"), Number(objective.Facts, "total")]))
        .Register(new("score", new[]
        {
            "objective.description.score", "objective.progress.score", "objective.number.nan",
            "objective.number.positive_infinity", "objective.number.negative_infinity",
        },
            (catalog, definition, _) => catalog.Format("objective.description.score", Number(definition, "atLeast")),
            (catalog, objective, _) => catalog.Format("objective.progress.score",
                ScoreNumber(catalog, objective.Facts.GetProperty("current")), Number(objective.Facts, "target"))))
        .Register(Simple("interact", "objective.description.interact", "objective.progress.interact", _ => []))
        .Register(Simple("all", "objective.description.all", "objective.progress.stages",
            objective => [Number(objective.Facts, "completed"), Number(objective.Facts, "total")]))
        .Register(Simple("any", "objective.description.any", "objective.progress.stages",
            objective => [Number(objective.Facts, "completed"), Number(objective.Facts, "total")]))
        .Register(Simple("sequence", "objective.description.sequence", "objective.progress.stages",
            objective => [Number(objective.Facts, "completed"), Number(objective.Facts, "total")]))
        .Register(new("optional", new[] { "objective.description.optional" },
            (catalog, _, describeChild) => catalog.Format("objective.description.optional", describeChild(0)),
            (_, objective, progress) => progress(objective.Children[0])))
        .Register(new("failOn", Array.Empty<string>(),
            (_, _, describeChild) => describeChild(0),
            (_, objective, progress) => progress(objective.Children[0])));
}
