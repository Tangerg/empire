namespace Empire.Core.Campaign;

public sealed class CampaignActionException : Exception
{
    public CampaignActionException(string message) : base(message) { }
}

public sealed class CampaignInvariantException : Exception
{
    public CampaignInvariantException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
