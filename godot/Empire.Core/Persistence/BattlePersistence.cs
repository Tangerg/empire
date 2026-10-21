using System.Globalization;
using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Persistence;

public sealed class BattlePersistence
{
    public const int Schema = 1;
    private readonly BattleSaveEnvironment environment;
    private readonly IBattleSaveReader reader;

    public BattlePersistence(BattleSaveEnvironment environment, IBattleSaveReader reader)
    {
        this.environment = environment with { RulesetManifest = (JsonObject)environment.RulesetManifest.DeepClone() };
        this.reader = reader;
    }

    public JsonObject Header(JsonNode? raw) => reader.Header(raw);

    public JsonObject Save(JsonObject state, string? savedAt = null)
    {
        RulesetManifest.RequirePersistent(environment.RulesetManifest);
        savedAt ??= DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        if (!StoredSchema.ValidTimestamp(savedAt))
            throw new DomainInvariantException("cannot save battle with an invalid timestamp");
        var document = new JsonObject
        {
            ["schema"] = Schema,
            ["battle"] = new JsonObject
            {
                ["levelId"] = state["levelId"]?.DeepClone(), ["levelName"] = state["levelName"]?.DeepClone(),
                ["turn"] = state["turn"]?.DeepClone(), ["phase"] = state["phase"]?.DeepClone(),
            },
            ["ruleset"] = environment.RulesetManifest.DeepClone(), ["savedAt"] = savedAt, ["state"] = state.DeepClone(),
        };
        try { return reader.Load(document, environment); }
        catch (StoredDocumentException error)
        { throw new DomainInvariantException($"cannot save invalid live battle: {error.Message}", error); }
    }

    public JsonObject Load(JsonNode? raw)
    {
        RulesetManifest.RequirePersistent(environment.RulesetManifest);
        return (JsonObject)reader.Load(raw, environment)["state"]!.DeepClone();
    }

}
