using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Empire.Core.Determinism;

namespace Empire.Core;

/// <summary>Detached domain facts explaining a rejected action; wording belongs to presentation.</summary>
public sealed record ActionRefusal
{
    [JsonPropertyName("code")]
    public string Code
    {
        get;
    }
    [JsonPropertyName("details")]
    public JsonElement Details
    {
        get;
    }

    public ActionRefusal(string code, JsonObject? details = null)
    {
        if (string.IsNullOrWhiteSpace(code) || !code.Contains('.') || code.Any(char.IsWhiteSpace))
            throw new ArgumentException("An action refusal needs a scoped semantic code.", nameof(code));
        Code = code;
        Details = EcmaJson.ToElement(details ?? new JsonObject());
    }
}
