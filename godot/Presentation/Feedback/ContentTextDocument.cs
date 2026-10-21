using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Abilities;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

public enum ContentTextKind
{
    MovementProfile, DamageType, ArmorClass, Terrain, Weapon, Unit,
    Status, Structure, TerrainOverlay, Tactic, Career, Formation,
}

public sealed record ContentTextDefinitions(ReadOnlyContent Content, AbilityRegistry Abilities,
    ReactionRegistry Reactions, ResourceAdapterRegistry Resources, GridRegistry Grids);

/// <summary>Immutable message-key bindings validated against the consumed engine composition.</summary>
public sealed class ContentTextDocument
{
    private sealed record Entry(string Name, string? Blurb, FrozenDictionary<string, string> Bonuses);
    private sealed record DescribedEntry(string Name, string Hint);

    private static readonly FrozenDictionary<ContentTextKind, string> Families =
        new Dictionary<ContentTextKind, string>
        {
            [ContentTextKind.MovementProfile] = "movementProfiles", [ContentTextKind.DamageType] = "damageTypes",
            [ContentTextKind.ArmorClass] = "armorClasses", [ContentTextKind.Terrain] = "terrains",
            [ContentTextKind.Weapon] = "weapons", [ContentTextKind.Unit] = "units",
            [ContentTextKind.Status] = "statuses", [ContentTextKind.Structure] = "structures",
            [ContentTextKind.TerrainOverlay] = "terrainOverlays", [ContentTextKind.Tactic] = "tactics",
            [ContentTextKind.Career] = "careers", [ContentTextKind.Formation] = "formations",
        }.ToFrozenDictionary();

    private readonly FrozenDictionary<ContentTextKind, FrozenDictionary<string, Entry>> content;
    private readonly FrozenDictionary<string, DescribedEntry> abilities;
    private readonly FrozenDictionary<string, DescribedEntry> reactions;
    private readonly FrozenDictionary<string, string> resources;
    private readonly FrozenDictionary<string, FrozenDictionary<string, string>> directions;

    private ContentTextDocument(FrozenDictionary<ContentTextKind, FrozenDictionary<string, Entry>> content,
        FrozenDictionary<string, DescribedEntry> abilities, FrozenDictionary<string, DescribedEntry> reactions,
        FrozenDictionary<string, string> resources, FrozenDictionary<string, FrozenDictionary<string, string>> directions)
    {
        this.content = content;
        this.abilities = abilities;
        this.reactions = reactions;
        this.resources = resources;
        this.directions = directions;
    }

    public static ContentTextDocument Parse(ContentTextDefinitions definitions, string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(definitions.Content);
        ArgumentNullException.ThrowIfNull(definitions.Abilities);
        ArgumentNullException.ThrowIfNull(definitions.Reactions);
        ArgumentNullException.ThrowIfNull(definitions.Resources);
        ArgumentNullException.ThrowIfNull(definitions.Grids);
        try
        {
            var root = Fields(EcmaJson.Parse(json, rejectDuplicateProperties: true), "content text document",
                "schema", "content", "abilities", "reactions", "resources", "directions");
            var schema = EcmaJson.ToElement(root["schema"]);
            if (schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1)
                throw new LocalizationException("Unsupported content text metadata schema.");
            RequireSameKeys(Families.Values, definitions.Content.Families, "canonical content families");
            var declared = Fields(root["content"], "content families", Families.Values.ToArray());
            var content = new Dictionary<ContentTextKind, FrozenDictionary<string, Entry>>();
            foreach (var (kind, family) in Families)
            {
                var canonical = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var definition in definitions.Content.Definitions(family))
                {
                    var id = Identity(definition, $"{family} definition");
                    if (!canonical.TryAdd(id, definition))
                        throw new LocalizationException($"Duplicate canonical {family} identity '{id}'.");
                }
                var entries = Fields(declared[family], family, canonical.Keys.ToArray());
                content.Add(kind, entries.ToFrozenDictionary(pair => pair.Key,
                    pair => ParseEntry(pair.Value, kind, canonical[pair.Key], $"{family}/{pair.Key}"), StringComparer.Ordinal));
            }
            var declaredDirections = Fields(root["directions"], "grids", definitions.Grids.Keys().ToArray());
            var directions = declaredDirections.ToFrozenDictionary(pair => pair.Key, pair =>
                Keys(pair.Value, definitions.Grids.Get(pair.Key).Directions.Select(direction => direction.Id),
                    $"directions for '{pair.Key}'"), StringComparer.Ordinal);
            return new(content.ToFrozenDictionary(),
                DescribedEntries(root["abilities"], definitions.Abilities.Keys(), "abilities"),
                DescribedEntries(root["reactions"], definitions.Reactions.Keys(), "reactions"),
                Keys(root["resources"], definitions.Resources.Keys(), "resources"), directions);
        }
        catch (JsonException error)
        {
            throw new LocalizationException("Invalid content text metadata JSON.", error);
        }
    }

    internal void ValidateKeys(GameText text)
    {
        var keys = content.Values.SelectMany(entries => entries.Values).SelectMany(entry =>
                entry.Bonuses.Values.Prepend(entry.Name).Concat(entry.Blurb is { } blurb ? new[] { blurb } : []))
            .Concat(abilities.Values.Concat(reactions.Values).SelectMany(entry => new[] { entry.Name, entry.Hint }))
            .Concat(resources.Values).Concat(directions.Values.SelectMany(entries => entries.Values)).ToArray();
        text.ValidateKeys(keys);
        foreach (var key in keys) _ = text.Format(key);
    }

    internal string NameKey(ContentTextKind kind, string id) => ContentEntry(kind, id).Name;
    internal string UnitBlurbKey(string id) => ContentEntry(ContentTextKind.Unit, id).Blurb
        ?? throw new LocalizationException($"Missing unit blurb presentation '{id}'.");
    internal string AbilityNameKey(string id) => Lookup(abilities, id, "ability").Name;
    internal string AbilityHintKey(string id) => Lookup(abilities, id, "ability").Hint;
    internal string ReactionNameKey(string id) => Lookup(reactions, id, "reaction").Name;
    internal string ReactionHintKey(string id) => Lookup(reactions, id, "reaction").Hint;
    internal string ResourceNameKey(string id) => Lookup(resources, id, "resource");
    internal string DirectionNameKey(string gridId, string directionId) =>
        Lookup(Lookup(directions, gridId, "grid"), directionId, $"direction for '{gridId}'");
    internal string WeaponBonusKey(string weaponId, string bonusId) =>
        Lookup(ContentEntry(ContentTextKind.Weapon, weaponId).Bonuses, bonusId, $"bonus for weapon '{weaponId}'");

    private Entry ContentEntry(ContentTextKind kind, string id) => content.TryGetValue(kind, out var entries)
        ? Lookup(entries, id, Families[kind])
        : throw new LocalizationException($"Unknown content presentation kind '{kind}'.");

    private static Entry ParseEntry(JsonNode? value, ContentTextKind kind, JsonElement definition, string path)
    {
        var fields = Properties(value, path);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "name" };
        if (kind == ContentTextKind.Unit) allowed.Add("blurb");
        if (kind == ContentTextKind.Weapon) allowed.Add("bonuses");
        var unknown = fields.Keys.Except(allowed).ToArray();
        if (!fields.ContainsKey("name") || unknown.Length != 0)
            throw new LocalizationException($"Invalid {path}: a name key is required; unknown fields [{string.Join(", ", unknown)}].");
        var bonusIds = new HashSet<string>(StringComparer.Ordinal);
        if (kind == ContentTextKind.Weapon && definition.TryGetProperty("bonuses", out var bonuses))
        {
            if (bonuses.ValueKind != JsonValueKind.Array)
                throw new LocalizationException($"Weapon bonuses for '{path}' must be an array.");
            foreach (var bonus in bonuses.EnumerateArray())
            {
                var id = Identity(bonus, $"bonus for '{path}'");
                if (!bonusIds.Add(id)) throw new LocalizationException($"Duplicate canonical bonus identity '{path}/{id}'.");
            }
        }
        var bonusKeys = fields.TryGetValue("bonuses", out var rawBonuses)
            ? Keys(rawBonuses, bonusIds, $"bonuses for '{path}'")
            : FrozenDictionary<string, string>.Empty;
        RequireSameKeys(bonusIds, bonusKeys.Keys, $"bonuses for '{path}'");
        return new(RequiredString(fields["name"], $"name for '{path}'"),
            fields.TryGetValue("blurb", out var blurb) ? RequiredString(blurb, $"blurb for '{path}'") : null, bonusKeys);
    }

    private static FrozenDictionary<string, DescribedEntry> DescribedEntries(JsonNode? value,
        IEnumerable<string> ids, string path) => Fields(value, path, ids.ToArray()).ToFrozenDictionary(pair => pair.Key,
        pair =>
        {
            var fields = Fields(pair.Value, $"{path}/{pair.Key}", "name", "hint");
            return new DescribedEntry(RequiredString(fields["name"], $"name for '{path}/{pair.Key}'"),
                RequiredString(fields["hint"], $"hint for '{path}/{pair.Key}'"));
        }, StringComparer.Ordinal);

    private static FrozenDictionary<string, string> Keys(JsonNode? value, IEnumerable<string> ids, string path) =>
        Fields(value, path, ids.ToArray()).ToFrozenDictionary(pair => pair.Key,
            pair => RequiredString(pair.Value, $"key for '{path}/{pair.Key}'"), StringComparer.Ordinal);

    private static T Lookup<T>(FrozenDictionary<string, T> entries, string id, string path) =>
        !string.IsNullOrWhiteSpace(id) && entries.TryGetValue(id, out var entry) ? entry
            : throw new LocalizationException($"Missing {path} presentation '{id}'.");

    private static Dictionary<string, JsonNode?> Fields(JsonNode? value, string path, params string[] expected)
    {
        var fields = Properties(value, path);
        RequireSameKeys(expected, fields.Keys, path);
        return fields;
    }

    private static Dictionary<string, JsonNode?> Properties(JsonNode? value, string path)
    {
        if (value is not JsonObject obj) throw new LocalizationException($"{path} must be an object.");
        var fields = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, property) in obj)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new LocalizationException($"{path} contains an empty identity.");
            if (!fields.TryAdd(key, property)) throw new LocalizationException($"Duplicate identity '{key}' in {path}.");
        }
        return fields;
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

    private static string Identity(JsonElement definition, string path) =>
        definition.ValueKind == JsonValueKind.Object && definition.TryGetProperty("id", out var id)
            ? RequiredString(EcmaJson.Parse(id.GetRawText()), $"identity for '{path}'")
            : throw new LocalizationException($"Missing canonical identity for '{path}'.");

    private static string RequiredString(JsonNode? value, string path)
    {
        if (value is not JsonValue || EcmaJson.ToElement(value).ValueKind != JsonValueKind.String)
            throw new LocalizationException($"{path} must be a nonempty string.");
        var text = EcmaJson.ReadString(value);
        if (string.IsNullOrWhiteSpace(text)) throw new LocalizationException($"{path} must be a nonempty string.");
        return text;
    }
}
