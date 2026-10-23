using Empire.Core.Registries;

namespace Empire.Core.Abilities;

public sealed class AbilityRegistry : KeyedRegistry<AbilityDefinition, AbilityRegistry>
{
    public AbilityRegistry() : base("ability") { }
    protected override string KeyOf(AbilityDefinition entry) => entry.Id;
    protected override void ValidateEntry(AbilityDefinition entry)
    {
        if (!Enum.IsDefined(entry.TargetKind))
            throw new DomainInvariantException($"Ability {entry.Id} has an unknown target kind.");
    }
    public AbilityRegistry Clone() => CopyInto(new AbilityRegistry());
}
