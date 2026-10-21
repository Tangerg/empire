using System.Text.Json.Nodes;
using Empire.Core.Scenario;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Lifecycle;

/// <summary>Owns trigger timing, ledger and stabilization; handlers own payload behavior.</summary>
public sealed class ScenarioSystem(ScenarioRules rules)
{
    public ScenarioRules Rules { get; } = rules;
    public bool ConditionMet(JsonObject state, JsonObject condition) => Rules.ScenarioConditions.Evaluate(state, condition, Rules.Content);

    public void Run(JsonObject state, string timing, Action<JsonObject> emit)
    {
        var scenario = Object(state["scenario"], "scenario");
        var triggers = Objects(scenario["triggers"], "triggers").ToArray();
        var fired = new HashSet<string>(StringComparer.Ordinal);
        var occurrence = $"{Integer(state, "turn")}:{Integer(state, "currentPlayer")}:{timing}";
        for (var pass = 0; pass < triggers.Length + 1; pass++)
        {
            var changed = false;
            foreach (var trigger in triggers)
            {
                var id = Text(trigger, "id");
                if (fired.Contains(id) || !Due(state, trigger, timing, occurrence) || !ConditionMet(state, Object(trigger["condition"], "condition")))
                    continue;
                fired.Add(id);
                if (trigger["repeat"] is null)
                    Array(scenario["firedTriggerIds"], "firedTriggerIds").Add(id);
                else
                {
                    var runtime = Object(scenario["triggerRuntime"], "triggerRuntime");
                    var previous = runtime[id] as JsonObject;
                    runtime[id] = new JsonObject { ["count"] = previous is null ? 1 : Number(previous, "count", 0) + 1, ["lastOccurrence"] = occurrence };
                }
                foreach (var effect in Objects(trigger["effects"], "effects"))
                {
                    Rules.ScenarioEffects.Apply(Rules, state, effect, emit);
                }
                changed = true;
            }
            if (!changed)
                return;
        }
        throw new DomainInvariantException($"scenario trigger loop did not stabilise at {timing}");
    }

    private static bool Due(JsonObject state, JsonObject trigger, string timing, string occurrence)
    {
        if (Text(trigger, "timing") != timing)
            return false;
        var scenario = Object(state["scenario"], "scenario");
        var id = Text(trigger, "id");
        if (trigger["repeat"] is not JsonObject repeat)
            return !Array(scenario["firedTriggerIds"], "firedTriggerIds").Any(entry => Text(entry, "fired trigger id") == id);
        var cadence = Number(repeat["everyRounds"], "everyRounds");
        var turn = Number(state["turn"], "turn");
        var start = Number(repeat, "startTurn", 1);
        if (cadence < 1 || cadence != Math.Truncate(cadence) || turn < start ||
            repeat["endTurn"] is { } end && turn > Number(end, "endTurn") || (turn - start) % cadence != 0)
            return false;
        var runtime = Object(scenario["triggerRuntime"], "triggerRuntime")[id] as JsonObject;
        var count = runtime is null ? 0 : Number(runtime, "count", 0);
        return (repeat["maxFirings"] is null || count < Number(repeat["maxFirings"], "maxFirings")) &&
            runtime?["lastOccurrence"]?.GetValue<string>() != occurrence;
    }

}
