using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Objectives;
using Empire.Core.Scenario;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

public sealed record BattleTextDefinitions(JsonArray Levels, ObjectiveHandlerRegistry Objectives,
    ScenarioEffectHandlerRegistry ScenarioEffects);

/// <summary>Validated presentation metadata addressed only by canonical battle identities.</summary>
public sealed class BattleTextDocument
{
    private sealed record TextEntry(string? Key, string? Literal, bool Generated = false)
    {
        public string? Resolve(GameText text) => Key is { } key ? text.Format(key) : Literal;
    }

    private sealed record PlayerEntry(TextEntry Name, IReadOnlyDictionary<string, TextEntry> Objectives);

    private sealed record LevelEntry(TextEntry Title, TextEntry Description,
        IReadOnlyDictionary<string, TextEntry> Signals, IReadOnlyDictionary<int, PlayerEntry> Players);

    private readonly IReadOnlyDictionary<string, LevelEntry> levels;
    private readonly ObjectiveHandlerRegistry handlers;

    private BattleTextDocument(IReadOnlyDictionary<string, LevelEntry> levels,
        ObjectiveHandlerRegistry handlers)
    {
        this.levels = levels;
        this.handlers = handlers.Clone().Seal();
    }

    public static BattleTextDocument Parse(BattleTextDefinitions definitions, string json)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(json);
        var levels = definitions.Levels;
        var handlers = definitions.Objectives;
        var effects = definitions.ScenarioEffects;
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(effects);
        var root = Fields(EcmaJson.Parse(json, rejectDuplicateProperties: true), "battle text document", "schema", "levels");
        if (Integer(root["schema"], "battle text metadata schema") != 2)
            throw new LocalizationException("Unsupported battle text metadata schema.");
        var declaredLevels = Properties(root["levels"], "battle text levels");
        var canonicalLevels = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var raw in levels)
        {
            var level = Object(raw, "level");
            var id = RequiredString(level["id"], "level id");
            if (!canonicalLevels.TryAdd(id, level))
                throw new LocalizationException($"Duplicate canonical level identity '{id}'.");
        }
        RequireSameKeys(canonicalLevels.Keys, declaredLevels.Keys, "levels");

        var result = new Dictionary<string, LevelEntry>(StringComparer.Ordinal);
        foreach (var (levelId, level) in canonicalLevels)
        {
            var declaredLevel = Fields(declaredLevels[levelId], $"level '{levelId}'", "title", "description", "signals", "players");
            var title = Entry(declaredLevel["title"], $"level title '{levelId}'", allowGenerated: false);
            var description = Entry(declaredLevel["description"], $"level description '{levelId}'", allowGenerated: false);
            var declaredSignals = Properties(declaredLevel["signals"], $"signals for '{levelId}'");
            var signalIds = new HashSet<string>(StringComparer.Ordinal);
            if (level["scenario"]?["triggers"] is { } triggers)
                foreach (var trigger in Array(triggers, $"triggers for '{levelId}'"))
                    foreach (var effect in Array(Object(trigger, "trigger")["effects"], "trigger effects"))
                        signalIds.UnionWith(effects.Signals(Object(effect, "scenario effect")));
            RequireSameKeys(signalIds, declaredSignals.Keys, $"signals for '{levelId}'");
            var signals = declaredSignals.ToDictionary(pair => pair.Key,
                pair => Entry(pair.Value, $"signal '{levelId}/{pair.Key}'", allowGenerated: false), StringComparer.Ordinal);
            var declaredPlayers = Properties(declaredLevel["players"], $"players for '{levelId}'");
            var canonicalPlayers = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            foreach (var raw in Array(level["players"], $"players for '{levelId}'"))
            {
                var player = Object(raw, "player");
                var playerId = Integer(player["id"], "player id").ToString(CultureInfo.InvariantCulture);
                if (!canonicalPlayers.TryAdd(playerId, player))
                    throw new LocalizationException($"Duplicate player identity '{levelId}/{playerId}'.");
            }
            RequireSameKeys(canonicalPlayers.Keys, declaredPlayers.Keys, $"players for '{levelId}'");
            var playerEntries = new Dictionary<int, PlayerEntry>();
            foreach (var (playerId, player) in canonicalPlayers)
            {
                var path = $"{levelId}/{playerId}";
                var declaredPlayer = Fields(declaredPlayers[playerId], path, "name", "objectives");
                var name = Entry(declaredPlayer["name"], $"player name '{path}'", allowGenerated: false);
                var declaredObjectives = Properties(declaredPlayer["objectives"], $"objectives for '{path}'");
                var objectiveIds = new HashSet<string>(StringComparer.Ordinal);
                var authored = player["objectives"] is { } rawObjectives ? Array(rawObjectives, $"objectives for '{path}'") : null;
                var effective = authored is { Count: > 0 } ? authored : Array(level["victory"], $"victory for '{levelId}'");
                foreach (var raw in effective)
                    Visit(Object(raw, "objective"));
                RequireSameKeys(objectiveIds, declaredObjectives.Keys, $"objectives for '{path}'");
                var descriptions = declaredObjectives.ToDictionary(pair => pair.Key,
                    pair => Entry(pair.Value, $"objective '{path}/{pair.Key}'", allowGenerated: true), StringComparer.Ordinal);
                playerEntries.Add(int.Parse(playerId, CultureInfo.InvariantCulture), new(name, descriptions));

                void Visit(JsonObject objective)
                {
                    var id = RequiredString(objective["id"], $"objective id for '{path}'");
                    if (!objectiveIds.Add(id))
                        throw new LocalizationException($"Duplicate objective identity '{path}/{id}'.");
                    var kind = RequiredString(objective["type"], $"objective kind for '{path}/{id}'");
                    if (!handlers.Has(kind))
                        throw new LocalizationException($"Unknown objective kind '{kind}' for '{path}/{id}'.");
                    foreach (var child in handlers.Children(objective))
                        Visit(child);
                }
            }
            result.Add(levelId, new(title, description, signals, playerEntries));
        }
        return new BattleTextDocument(result, handlers);
    }

    internal IReadOnlyList<string> ObjectiveKinds => handlers.Keys();

    internal void ValidateKeys(GameText text)
    {
        var entries = levels.Values.SelectMany(level => level.Players.Values
            .SelectMany(player => player.Objectives.Values.Prepend(player.Name))
            .Concat(level.Signals.Values).Append(level.Title).Append(level.Description)).ToArray();
        text.ValidateKeys(entries.Where(entry => entry.Key is not null).Select(entry => entry.Key!));
        foreach (var entry in entries.Where(entry => !entry.Generated))
            _ = entry.Resolve(text);
    }

    internal string LevelTitle(GameText text, string levelId) => Level(levelId).Title.Resolve(text)!;
    internal string LevelDescription(GameText text, string levelId) => Level(levelId).Description.Resolve(text)!;
    internal string SignalName(GameText text, string levelId, string signalId) =>
        Level(levelId).Signals.TryGetValue(signalId, out var entry) ? entry.Resolve(text)!
            : throw new LocalizationException($"Missing signal presentation '{levelId}/{signalId}'.");

    internal string PlayerName(GameText text, string levelId, int playerId) => Player(levelId, playerId).Name.Resolve(text)!;

    internal string? ObjectiveDescription(GameText text, string levelId, int playerId, string objectiveId)
    {
        var player = Player(levelId, playerId);
        return player.Objectives.TryGetValue(objectiveId, out var entry) ? entry.Resolve(text)
            : throw new LocalizationException($"Missing objective presentation '{levelId}/{playerId}/{objectiveId}'.");
    }

    internal IReadOnlyList<JsonElement> Children(JsonElement definition) => handlers.Children(Object(EcmaJson.Parse(definition.GetRawText()), "objective"))
        .Select(EcmaJson.ToElement).ToArray();

    private LevelEntry Level(string levelId) => levels.TryGetValue(levelId, out var level) ? level
        : throw new LocalizationException($"Missing level presentation '{levelId}'.");

    private PlayerEntry Player(string levelId, int playerId)
    {
        return Level(levelId).Players.TryGetValue(playerId, out var player) ? player
            : throw new LocalizationException($"Missing player presentation '{levelId}/{playerId}'.");
    }

    private static TextEntry Entry(JsonNode? value, string path, bool allowGenerated)
    {
        var fields = Properties(value, path);
        if (allowGenerated && fields.Count == 1 && fields.TryGetValue("generated", out var generated) &&
            generated is JsonValue scalar && scalar.TryGetValue<bool>(out var flag) && flag)
            return new(null, null, Generated: true);
        if (fields.Count != 1 || (!fields.ContainsKey("key") && !fields.ContainsKey("literal")))
            throw new LocalizationException(allowGenerated
                ? $"{path} must contain exactly one 'key', 'literal', or 'generated: true' field."
                : $"{path} must contain exactly one 'key' or 'literal' field.");
        var (field, raw) = fields.Single();
        var text = RequiredString(raw, path);
        return field == "key" ? new(text, null) : new(null, text);
    }

    private static Dictionary<string, JsonNode?> Fields(JsonNode? value, string path, params string[] expected)
    {
        var fields = Properties(value, path);
        RequireSameKeys(expected, fields.Keys, path);
        return fields;
    }

    private static Dictionary<string, JsonNode?> Properties(JsonNode? value, string path)
    {
        var result = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, property) in Object(value, path))
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new LocalizationException($"{path} contains an empty identity.");
            if (!result.TryAdd(key, property))
                throw new LocalizationException($"Duplicate field or identity '{key}' in {path}.");
        }
        return result;
    }

    private static void RequireSameKeys(IEnumerable<string> expected, IEnumerable<string> actual, string path)
    {
        var required = expected.ToHashSet(StringComparer.Ordinal);
        var declared = actual.ToHashSet(StringComparer.Ordinal);
        var missing = required.Except(declared).ToArray();
        var unknown = declared.Except(required).ToArray();
        if (missing.Length != 0 || unknown.Length != 0)
            throw new LocalizationException($"Invalid {path}: missing [{string.Join(", ", missing)}]; unknown [{string.Join(", ", unknown)}].");
    }

    private static string RequiredString(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw new LocalizationException($"{path} must be a nonempty string.");
        var text = EcmaJson.ReadString(EcmaJson.Parse(value.GetRawText()));
        if (string.IsNullOrWhiteSpace(text))
            throw new LocalizationException($"{path} must be a nonempty string.");
        return text;
    }

    private static string RequiredString(JsonNode? value, string path) => value is null
        ? throw new LocalizationException($"Missing {path}.") : RequiredString(EcmaJson.ToElement(value), path);

    private static int Integer(JsonNode? value, string path)
    {
        if (value is null)
            throw new LocalizationException($"Missing {path}.");
        var element = EcmaJson.ToElement(value);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var result))
            throw new LocalizationException($"{path} must be an integer.");
        return result;
    }

    private static JsonObject Object(JsonNode? value, string path) => value as JsonObject
        ?? throw new LocalizationException($"{path} must be an object.");

    private static JsonArray Array(JsonNode? value, string path) => value as JsonArray
        ?? throw new LocalizationException($"{path} must be an array.");
}
