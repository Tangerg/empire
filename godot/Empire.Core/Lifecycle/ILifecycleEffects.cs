using System.Text.Json.Nodes;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Lifecycle;

/// <summary>Damage and delayed combat remain owned by the combat pipeline, not the clock.</summary>
public interface ILifecycleEffects
{
    void ResolveDueCasts(JsonObject state, Action<JsonObject> emit);
    void DamageStatus(JsonObject state, JsonObject unit, JsonObject status, double requested, bool nonlethal, Action<JsonObject> emit);
}

/// <summary>Refuses combat features that have not been composed instead of silently dropping them.</summary>
public sealed class StrictLifecycleEffects : ILifecycleEffects
{
    public void ResolveDueCasts(JsonObject state, Action<JsonObject> emit)
    {
        if (Objects(state["pendingCasts"], "pendingCasts").Any(cast => Number(cast["resolveAt"], "resolveAt") <= Number(state["actorTurns"], "actorTurns")))
            throw new DomainInvariantException("Due spell casts require an ILifecycleEffects combat resolver");
    }

    public void DamageStatus(JsonObject state, JsonObject unit, JsonObject status, double requested, bool nonlethal, Action<JsonObject> emit) =>
        throw new DomainInvariantException($"Periodic status \"{Text(status, "id")}\" requires an ILifecycleEffects damage resolver");
}
