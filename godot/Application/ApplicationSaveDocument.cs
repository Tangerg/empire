using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Campaign;
using Empire.Core.Determinism;

namespace Empire.Application;

internal enum CampaignLocation { Campaign, Battle, Camp }

internal sealed record ApplicationSaveDocument(JsonObject CampaignState, CampaignLocation Location,
    JsonObject? Battle, JsonArray Events)
{
    internal const int Schema = 2;

    internal static ApplicationSaveDocument Read(JsonNode? input, JsonObject definition)
    {
        if (input is not JsonObject document || document["schema"] is not JsonValue schema ||
            !schema.TryGetValue<int>(out var version) || version != Schema)
            throw new StoredDocumentException("Unsupported application save schema; only the current native format is accepted.");
        var fields = new[] { "schema", "campaign", "mode", "battle", "events" };
        if (document.Count != fields.Length || fields.Any(field => !document.ContainsKey(field)))
            throw new StoredDocumentException("Application save fields do not match the current native format.");
        if (document["mode"] is not JsonValue mode || !mode.TryGetValue<string>(out var name))
            throw new StoredDocumentException("Application save mode is missing or invalid.");
        var location = name switch
        {
            "campaign" => CampaignLocation.Campaign,
            "battle" => CampaignLocation.Battle,
            "camp" => CampaignLocation.Camp,
            _ => throw new StoredDocumentException("Application save mode is unknown."),
        };
        var state = CampaignSave.Load(document["campaign"], definition)["state"]!.AsObject();
        var pending = state["pendingBattle"] is not null;
        if (pending != (location != CampaignLocation.Campaign) ||
            pending && state["status"]!.GetValue<string>() != "active")
            throw new StoredDocumentException("Application location and pending campaign battle disagree.");
        if (document["events"] is not JsonArray events)
            throw new StoredDocumentException("Saved event history must be an array.");
        if (location == CampaignLocation.Battle)
        {
            if (document["battle"] is not JsonObject)
                throw new StoredDocumentException("An active battle save requires its battle document.");
            foreach (var entry in events)
            {
                if (entry is not JsonObject value || value["type"] is not JsonValue type ||
                    !type.TryGetValue<string>(out var eventType) || EcmaText.IsBlank(eventType))
                    throw new StoredDocumentException("Saved event history contains an invalid event type.");
                if (eventType == "scenarioSignal" && (value["signal"] is not JsonValue signal ||
                    !signal.TryGetValue<string>(out var signalName) || EcmaText.IsBlank(signalName)))
                    throw new StoredDocumentException("Saved scenario signal is missing or invalid.");
            }
        }
        else if (document["battle"] is not null || events.Count != 0)
            throw new StoredDocumentException("Campaign and camp saves cannot retain played battle state or events.");
        return new((JsonObject)state.DeepClone(), location, document["battle"]?.DeepClone() as JsonObject,
            (JsonArray)events.DeepClone());
    }
}
