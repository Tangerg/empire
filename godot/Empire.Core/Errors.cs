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
    public IllegalActionException(string message)
        : base(message)
    {
    }
}

public class StoredDocumentException : Exception
{
    public StoredDocumentException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
