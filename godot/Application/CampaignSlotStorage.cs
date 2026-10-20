using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Campaign;
using Empire.Core.Determinism;

namespace Empire.Application;

public sealed record LoadedCampaign(JsonObject? State, string? Rejected, bool LegacyApplicationEnvelope = false);

/// <summary>The campaign-only autosave boundary. The separate manual application slot is never read or written here.</summary>
public static class CampaignSlotStorage
{
    public const string PendingBattleRejection = "战役存档包含一场尚未接入恢复通道的进行中战斗";

    public static LoadedCampaign Load(JsonObject definition, string path)
    {
        RequireAbsolutePath(path);
        string raw;
        try { raw = File.ReadAllText(path, new UTF8Encoding(false, true)); }
        catch (FileNotFoundException) { return new(null, null); }
        catch (DirectoryNotFoundException) { return new(null, null); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException or DecoderFallbackException)
        { return new(null, error.Message); }
        if (raw.Length == 0) return new(null, null);

        JsonNode? document;
        try { document = EcmaJson.Parse(raw); }
        catch (JsonException error) { return new(null, error.Message); }
        try
        {
            var legacy = document is JsonObject wrapper && !wrapper.ContainsKey("state") &&
                wrapper["campaign"] is JsonObject inner && inner.ContainsKey("state");
            if (legacy) document = ReadCleanLegacyEnvelope(document!.AsObject());
            var save = CampaignSave.Load(document, definition);
            var state = save["state"]!.AsObject();
            return state["pendingBattle"] is not null
                ? new(null, PendingBattleRejection)
                : new((JsonObject)state.DeepClone(), null, legacy);
        }
        catch (StoredDocumentException error) { return new(null, error.Message); }
    }

    /// <summary>Only the caller's explicit New Campaign action may clear this slot.</summary>
    public static void ClearForNewCampaign(string path)
    {
        RequireAbsolutePath(path);
        File.Delete(path);
    }

    public static void Save(JsonObject definition, JsonObject state, string path)
    {
        RequireAbsolutePath(path);
        if (state["pendingBattle"] is not null)
            throw new CampaignActionException("cannot save campaign state while a battle is pending");
        var text = EcmaJson.Stringify(CampaignSave.Create(definition, state));
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var created = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.WriteThrough))
            {
                created = true;
                using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
                writer.Write(text);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            // Same-directory rename replaces the old slot only after the new document is complete.
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception writeError)
        {
            try { if (created) File.Delete(temporary); }
            catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException or SecurityException)
            { throw new AggregateException("Campaign autosave failed and its temporary file could not be removed.", writeError, cleanupError); }
            throw;
        }
    }

    private static JsonNode ReadCleanLegacyEnvelope(JsonObject wrapper)
    {
        if (!EcmaJson.DeepEquals(wrapper["schema"], JsonValue.Create(1)) ||
            !wrapper.ContainsKey("request") || !wrapper.ContainsKey("battle") ||
            wrapper["request"] is not null || wrapper["battle"] is not null ||
            wrapper["atCamp"]?.GetValueKind() != JsonValueKind.False ||
            wrapper["events"] is not JsonArray { Count: 0 })
            throw new StoredDocumentException("应用存档包含战斗进度或结构无效，不能作为战役自动存档读取");
        return wrapper["campaign"]!;
    }

    private static void RequireAbsolutePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException("Campaign storage needs an explicit absolute filesystem path.", nameof(path));
    }
}
