using System.Text.Json.Nodes;

namespace Empire.Core.State;

internal static class StateDefaults
{
    internal static JsonObject Rules(JsonObject level)
    {
        var result = new JsonObject
        {
            ["captureMode"] = "instant",
            ["captureThreshold"] = 100,
            ["healOnOwnedBuilding"] = true,
            ["counterAttack"] = true,
            ["fog"] = false,
            ["turnLimit"] = null,
            ["friendlyPassThrough"] = true,
            ["enemiesBlockMovement"] = true,
            ["zoneOfControl"] = false,
            ["maxUnitsPerPlayer"] = null,
            ["recruitsActImmediately"] = false,
            ["turnOrder"] = "side",
            ["grid"] = "square4",
            ["highGroundThreshold"] = 1,
            ["highGroundDamageMultiplier"] = 1.1,
            ["sideAttackMultiplier"] = 1.1,
            ["backAttackMultiplier"] = 1.25,
            ["flankAttackMultiplier"] = 1.15,
            ["halfCoverDefense"] = 0.15,
            ["fullCoverDefense"] = 0.3,
            ["moraleEnabled"] = false,
            ["moraleDamageFactor"] = 0.6,
            ["moraleAllyDefeatLoss"] = 12,
            ["moraleDefeatShockRadius"] = 3,
            ["moraleCommanderDefeatLoss"] = 25,
            ["baseResourceGrants"] = new JsonArray(),
            ["siteResourceOverrides"] = new JsonObject(),
        };
        if (level["rules"] is not { } authoredRules) return result;
        var overrides = StateJson.Object(authoredRules, "rules");
        foreach (var pair in overrides) result[pair.Key] = pair.Value?.DeepClone();
        result["baseResourceGrants"] = StateJson.CopyArray(overrides["baseResourceGrants"], "baseResourceGrants");
        result["siteResourceOverrides"] = StateJson.CopyObject(overrides["siteResourceOverrides"], "siteResourceOverrides");
        return result;
    }

    internal static JsonArray Victory() => new()
    {
        new JsonObject { ["type"] = "routEnemies" },
        new JsonObject { ["type"] = "captureHQ" },
    };

    internal static uint LevelSeed(string id)
    {
        var hash = 0x811c9dc5u;
        unchecked
        {
            for (var index = 0; index < id.Length; index++)
            {
                hash = (hash ^ id[index]) * 0x01000193u;
                // JS for-of yields code points, while charCodeAt(0) reads only their first code unit.
                if (char.IsHighSurrogate(id[index]) && index + 1 < id.Length && char.IsLowSurrogate(id[index + 1]))
                    index++;
            }
        }
        return hash;
    }
}
