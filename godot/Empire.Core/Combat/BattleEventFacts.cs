using System.Text.Json.Nodes;

namespace Empire.Core.Combat;

/// <summary>The source combat event contract: a strike carries both an attacker and damage.</summary>
public static class BattleEventFacts
{
    public static bool IsStrike(JsonObject value) => value.ContainsKey("attacker") && value.ContainsKey("damage");
    public static int StrikeCount(IEnumerable<JsonObject> events) => events.Count(IsStrike);
}
