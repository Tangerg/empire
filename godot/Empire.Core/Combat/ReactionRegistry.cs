using Empire.Core.Registries;

namespace Empire.Core.Combat;

public sealed record ReactionBehavior(string Id, bool Intercepts, double IncomingMultiplier, bool Retaliates, bool ConservesResources)
{
    public double? SupportAttackMultiplier
    {
        get; init;
    }
    public double TriggeredRankProgress
    {
        get; init;
    }
}

public sealed class ReactionRegistry() : KeyedRegistry<ReactionBehavior, ReactionRegistry>("reaction stance")
{
    protected override string KeyOf(ReactionBehavior entry) => entry.Id;
    protected override void ValidateEntry(ReactionBehavior entry)
    {
        if (!double.IsFinite(entry.TriggeredRankProgress) || entry.TriggeredRankProgress < 0 ||
            entry.SupportAttackMultiplier is { } multiplier && (!double.IsFinite(multiplier) || multiplier < 0))
            throw new DomainInvariantException($"reaction stance \"{entry.Id}\" has invalid support modifiers");
    }
    public ReactionRegistry Clone() => CopyInto(new ReactionRegistry());
    public static ReactionRegistry Core() => new ReactionRegistry()
        .Register(new("counter", false, 1, true, false))
        .Register(new("guard", false, 0.7, false, false))
        .Register(new("support", true, 1, true, false)
        {
            SupportAttackMultiplier = 0.6,
            TriggeredRankProgress = 20,
        })
        .Register(new("conserve", false, 1, true, true));
}
