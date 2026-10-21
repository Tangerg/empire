namespace Empire.Core.Engine;

public sealed class BattleEngineConfigurationException : DomainInvariantException
{
    public BattleEngineConfigurationException(IReadOnlyList<string> issues)
        : base("invalid battle engine configuration:\n" + string.Join("\n", issues.Select(issue => "- " + issue))) =>
        Issues = Array.AsReadOnly(issues.ToArray());
    public IReadOnlyList<string> Issues
    {
        get;
    }
}

public sealed class BattleLevelException : StoredDocumentException
{
    public BattleLevelException(string levelId, IReadOnlyList<string> issues)
        : base($"invalid battle level \"{levelId}\":\n" + string.Join("\n", issues.Select(issue => "- " + issue)))
    {
        LevelId = levelId;
        Issues = Array.AsReadOnly(issues.ToArray());
    }
    public string LevelId
    {
        get;
    }
    public IReadOnlyList<string> Issues
    {
        get;
    }
}
