using System;

namespace Empire.Core;

public class DomainInvariantException : Exception
{
    public DomainInvariantException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class IllegalActionException : Exception
{
    public ActionRefusal Refusal
    {
        get;
    }

    public IllegalActionException(ActionRefusal refusal)
        : base($"Action refused: {refusal?.Code ?? throw new ArgumentNullException(nameof(refusal))}")
    {
        Refusal = refusal;
    }
}

public class StoredDocumentException : Exception
{
    public StoredDocumentException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
