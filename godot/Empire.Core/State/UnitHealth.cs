using System.Text.Json.Nodes;

namespace Empire.Core.State;

/// <summary>Health transitions over the existing unit state and its canonical catalog.</summary>
public sealed class UnitHealth(ReadOnlyContent catalog, JsonObject unit)
{
    public double Current => Read().Current;
    public double Missing
    {
        get
        {
            var health = Read();
            return health.Maximum - health.Current;
        }
    }

    public double Damage(double requested, bool nonlethal = false)
    {
        RequireAmount(requested, "damage");
        var health = Read();
        var limit = nonlethal ? Math.Max(0, health.Current - 1) : health.Current;
        var amount = Math.Min(limit, StateJson.Round(requested));
        if (amount > 0)
            unit["hp"] = health.Current - amount;
        return amount;
    }

    public double Heal(double requested)
    {
        RequireAmount(requested, "healing");
        var health = Read();
        var amount = Math.Min(StateJson.Round(requested), health.Maximum - health.Current);
        unit["hp"] = health.Current + amount;
        return amount;
    }

    /// <summary>An omitted strength preserves the snapshot; an explicit strength uses the game's rounding.</summary>
    public void RestoreHealth(double? requested = null)
    {
        if (requested is { } amount)
            RequireAmount(amount, "restored health");
        var health = Read();
        var restored = requested ?? health.Current;
        if (restored <= 0 || restored > health.Maximum)
            throw new DomainInvariantException("restored health must be positive and no greater than maximum health");
        if (requested is not null)
            unit["hp"] = Math.Min(health.Maximum, Math.Max(1, StateJson.Round(restored)));
    }

    public void ChangeType(string nextType)
    {
        var hp = HealthAfterTypeChange(nextType);
        unit["type"] = nextType;
        unit["hp"] = hp;
    }

    // Career costs emit before the type changes. Preflight the same invariant before those effects.
    internal void ValidateTypeChange(string nextType) => HealthAfterTypeChange(nextType);

    private double HealthAfterTypeChange(string nextType)
    {
        var health = Read();
        if (health.Current <= 0)
            throw new DomainInvariantException("a health-preserving type change requires a living unit");
        var maximum = Maximum(nextType);
        return Math.Min(maximum, Math.Max(1, StateJson.Round(maximum * (health.Current / health.Maximum))));
    }

    private (double Current, double Maximum) Read()
    {
        var raw = unit["hp"];
        var current = raw is JsonValue scalar && scalar.TryGetValue<double>(out var value)
            ? value : StateJson.Number(raw, "unit health");
        RequireAmount(current, "unit health");
        var maximum = Maximum(StateJson.Text(unit, "type"));
        if (current > maximum)
            throw new DomainInvariantException("unit health exceeds its canonical maximum");
        return (current, maximum);
    }

    private double Maximum(string type)
    {
        var definition = catalog.Definition("units", type)
            ?? throw new DomainInvariantException($"unknown unit type \"{type}\"");
        var maximum = StateJson.Number(definition.GetProperty("maxHp"), "maximum health");
        if (maximum < 1)
            throw new DomainInvariantException("maximum health must be at least one");
        return maximum;
    }

    private static void RequireAmount(double amount, string subject)
    {
        if (!double.IsFinite(amount) || amount < 0)
            throw new DomainInvariantException($"{subject} must be a finite non-negative number, got {amount}");
    }
}
