using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Campaign;
using Empire.Core.Determinism;

namespace Empire.Application;

public sealed record LoadedCampaign(JsonObject? State, string? Rejected);

/// <summary>The campaign-only autosave boundary. The separate manual application slot is never read or written here.</summary>
public static class CampaignSlotStorage
{
    public const string PendingBattleRejection = "战役存档包含一场尚未接入恢复通道的进行中战斗";

    public static LoadedCampaign Load(JsonObject definition, string path)
    {
        SaveFile.RequireAbsolutePath(path);
        string raw;
        try
        {
            raw = File.ReadAllText(path, new UTF8Encoding(false, true));
        }
        catch (FileNotFoundException) { return new(null, null); }
        catch (DirectoryNotFoundException) { return new(null, null); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException or DecoderFallbackException)
        {
            return new(null, error.Message);
        }
        if (raw.Length == 0)
            return new(null, null);

        JsonNode? document;
        try
        {
            document = EcmaJson.Parse(raw);
        }
        catch (JsonException error) { return new(null, error.Message); }
        try
        {
            var save = CampaignSave.Load(document, definition);
            var state = save["state"]!.AsObject();
            return state["pendingBattle"] is not null
                ? new(null, PendingBattleRejection)
                : new((JsonObject)state.DeepClone(), null);
        }
        catch (StoredDocumentException error) { return new(null, error.Message); }
    }

    /// <summary>Only the caller's explicit New Campaign action may clear this slot.</summary>
    public static void ClearForNewCampaign(string path)
    {
        SaveFile.Clear(path);
    }

    public static void Save(JsonObject definition, JsonObject state, string path)
    {
        SaveFile.RequireAbsolutePath(path);
        if (state["pendingBattle"] is not null)
            throw new CampaignActionException("cannot save campaign state while a battle is pending");
        SaveFile.WriteDocument(path, CampaignSave.Create(definition, state));
    }
}
