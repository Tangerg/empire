using System.Text.Json.Nodes;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Campaign;

/// <summary>The authored Candidate 01 policy; generic campaign rules contain no story identifiers.</summary>
public static class GrayFlagCampaignPolicy
{
    public static IReadOnlyDictionary<string, string> MarkerDispositions
    {
        get;
    } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["corpse"] = "fallen",
            ["transport-loss"] = "fallen",
            ["routed"] = "routed",
            ["surrendered"] = "surrendered",
            ["withdrawn"] = "missing",
        });

    public static JsonObject ApplyBattleContext(JsonObject request, JsonObject state)
    {
        var result = (JsonObject)request.DeepClone();
        var level = Object(result["level"], "level");
        var player = Objects(level["players"], "players").FirstOrDefault(candidate => Integer(candidate, "id") == 1);
        if (player is not null)
        {
            var resources = Object(state["resources"], "resources");
            var supplies = Math.Max(0, Number(resources, "supplies", 0));
            var treasury = Math.Max(0, Number(resources, "treasury", 0));
            if (player["resources"]?["funds"] is JsonObject account)
                account["current"] = 180 + supplies * 30 + treasury * 20;
        }
        bool Flag(string id) => CampaignConditionRegistry.Contains(state["flags"], id);
        JsonObject? Unit(string key) => Objects(level["units"], "units").FirstOrDefault(unit => unit["key"]?.GetValue<string>() == key);
        switch (Text(result, "levelId"))
        {
            case "c01-01" when Flag("twin_hills_rushed"):
                if (Unit("campaign-torren") is { } torren)
                {
                    torren["x"] = 9;
                    torren["y"] = 4;
                    torren["hp"] = Math.Min(Number(torren, "hp", 98), 72);
                }
                break;
            case "c01-02" when Flag("bridge_no_promise") && level["scenario"] is JsonObject scenario:
                scenario["engagementRules"] = new JsonArray();
                var enemy = Objects(level["players"], "players").FirstOrDefault(candidate => Integer(candidate, "id") == 2);
                if (enemy?["ai"] is JsonObject ai)
                    ai["aggression"] = 0.68;
                break;
            case "c01-09" when Flag("ivra_freed_by_ritual"):
                foreach (var structure in Objects(level["structures"], "structures"))
                    structure["hp"] = 72;
                break;
            case "c01-12" when Flag("bell_tower_joint_control"):
                if (Unit("imperial-cain") is { } cain)
                    cain["owner"] = 1;
                break;
            case "c01-15" when Flag("silverwood_compromise"):
                if (Unit("controlled-druid") is { } druid)
                    druid["owner"] = 3;
                break;
        }
        var extra = level["extra"] as JsonObject ?? new JsonObject();
        extra["campaignContextApplied"] = true;
        if (extra.Parent is null)
            level["extra"] = extra;
        return result;
    }
    public static JsonObject ApplyBattleResult(JsonObject result, JsonObject state)
    {
        var projected = (JsonObject)result.DeepClone();
        foreach (var unit in Objects(projected["units"], "units"))
        {
            var id = Text(unit, "campaignUnit");
            var campaign = state["roster"]?[id] as JsonObject;
            if (campaign?["tags"] is not JsonArray tags || !CampaignConditionRegistry.Contains(tags, "hero") || Text(unit, "disposition") == "available")
                continue;
            unit["disposition"] = "available";
            unit["hpRatio"] = Math.Max(0.3, Number(unit["hpRatio"], "hpRatio"));
            unit["moraleRatio"] = Math.Max(0.4, Number(unit["moraleRatio"], "moraleRatio"));
            Array(projected["signals"], "signals").Add($"hero_wounded:{id}");
        }
        return projected;
    }
}
