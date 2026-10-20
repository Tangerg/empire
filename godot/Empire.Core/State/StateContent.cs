using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;

namespace Empire.Core.State;

/// <summary>
/// The read-only subset of a composed content catalog needed to construct a state.
/// It does not install combat rules or replace the engine composition root.
/// </summary>
internal sealed class StateContent
{
    private static readonly Dictionary<string, string> Families = new(StringComparer.Ordinal)
    {
        ["movementProfiles"] = "movement profile", ["damageTypes"] = "damage type",
        ["armorClasses"] = "armor class", ["terrains"] = "terrain", ["weapons"] = "weapon",
        ["units"] = "unit", ["statuses"] = "status", ["structures"] = "structure",
        ["terrainOverlays"] = "terrain overlay", ["tactics"] = "tactic",
        ["careers"] = "career", ["formations"] = "formation",
    };

    private readonly Dictionary<string, Dictionary<string, JsonObject>> definitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> terrainCharacters = new(StringComparer.Ordinal);
    private readonly HashSet<string> encodedTerrains = new(StringComparer.Ordinal);
    private string? defaultTerrain;

    internal StateContent(JsonArray packs)
    {
        foreach (var family in Families.Keys)
            definitions[family] = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var ordered = OrderPacks(packs);
        foreach (var pack in ordered)
        {
            foreach (var family in Families.Keys)
            {
                foreach (var entry in StateJson.Objects(pack[family], family))
                {
                    var id = StateJson.Text(entry, "id");
                    if (!definitions[family].TryAdd(id, entry))
                        throw new DomainInvariantException($"content {family} id already registered: \"{id}\"");
                }
            }
        }
        foreach (var pack in ordered)
        {
            if (pack["terrainCharacters"] is { } rawCharacters)
            {
                foreach (var pair in StateJson.Object(rawCharacters, "terrainCharacters"))
                {
                    var character = pair.Key;
                    var terrain = StateJson.Text(pair.Value, "terrain character target");
                    if (character.Length != 1 && !(character.Length == 2 &&
                        char.IsHighSurrogate(character[0]) && char.IsLowSurrogate(character[1])))
                        throw new DomainInvariantException($"terrain character must contain one code point: \"{character}\"");
                    if (!terrainCharacters.TryAdd(character, terrain))
                        throw new DomainInvariantException($"terrain character already registered: \"{character}\"");
                    if (!encodedTerrains.Add(terrain))
                        throw new DomainInvariantException($"terrain already has a character: \"{terrain}\"");
                    Get("terrains", terrain);
                }
            }
            if (pack["defaultTerrain"] is { } rawDefault)
            {
                var requested = StateJson.Text(rawDefault, "defaultTerrain");
                if (defaultTerrain is not null && defaultTerrain != requested)
                    throw new DomainInvariantException("multiple default terrains requested in one installation");
                defaultTerrain = requested;
                Get("terrains", requested);
            }
        }
        if (defaultTerrain is not null && !encodedTerrains.Contains(defaultTerrain))
            throw new DomainInvariantException($"default terrain \"{defaultTerrain}\" has no character encoding");
    }

    internal IEnumerable<string> FamilyIds => Families.Keys;

    internal JsonObject Get(string family, string id) =>
        definitions[family].TryGetValue(id, out var entry) ? entry :
        throw new DomainInvariantException($"unknown {Families[family]} \"{id}\"");

    internal JsonObject? TryGet(string family, string id) => definitions[family].GetValueOrDefault(id);
    internal IEnumerable<JsonObject> All(string family) => definitions[family].Values;
    internal string DefaultTerrain => defaultTerrain ??
        throw new DomainInvariantException("no default terrain installed; install a content pack first");
    internal string? Terrain(string character) => terrainCharacters.GetValueOrDefault(character);

    private static IReadOnlyList<JsonObject> OrderPacks(JsonArray packs)
    {
        var byId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var node in packs)
        {
            var pack = StateJson.Object(node, "content pack");
            var id = StateJson.Text(pack, "id");
            if (EcmaText.IsBlank(id)) throw new DomainInvariantException("content pack id cannot be empty");
            var version = StateJson.Number(pack["version"], "content pack version");
            if (version < 1 || version != Math.Truncate(version))
                throw new DomainInvariantException($"content pack \"{id}\" version must be a positive integer");
            if (!byId.TryAdd(id, pack))
                throw new DomainInvariantException($"duplicate content pack request: \"{id}\"");
        }
        var result = new List<JsonObject>();
        var path = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(JsonObject pack)
        {
            var id = StateJson.Text(pack, "id");
            if (visited.Contains(id)) return;
            var cycleStart = path.IndexOf(id);
            if (cycleStart >= 0)
                throw new DomainInvariantException($"cyclic content pack dependency: {string.Join(" -> ", path.Skip(cycleStart).Append(id))}");
            path.Add(id);
            if (pack["dependencies"] is { } rawDependencies)
            {
                foreach (var node in StateJson.Array(rawDependencies, "dependencies"))
                {
                    var dependency = StateJson.Text(node, "dependency");
                    if (!byId.TryGetValue(dependency, out var required))
                        throw new DomainInvariantException($"content pack \"{id}\" requires \"{dependency}\"");
                    Visit(required);
                }
            }
            path.RemoveAt(path.Count - 1);
            visited.Add(id);
            result.Add(pack);
        }
        foreach (var pack in byId.Values) Visit(pack);
        return result;
    }
}
