using System;
using System.Collections.Generic;
using Empire.Localization;

namespace Empire.Presentation;

public static class IdentityNames
{
    public static string Resolve(GameText text, IReadOnlyDictionary<string, string> nameKeys, string identity)
    {
        if (!nameKeys.TryGetValue(identity, out var key))
            throw new InvalidOperationException($"Presentation name is not authored for identity: {identity}.");
        return text.Format(key);
    }
}
