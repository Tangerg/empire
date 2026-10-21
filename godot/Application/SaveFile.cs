using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Determinism;

namespace Empire.Application;

public enum SaveFileOperation
{
    Read, Write, Clear
}

public sealed class SaveStorageException(SaveFileOperation operation, string filePath, Exception cause)
    : IOException($"Save file {operation.ToString().ToLowerInvariant()} failed: {cause.Message}", cause)
{
    public SaveFileOperation Operation { get; } = operation;
    public string FilePath { get; } = filePath;
}

/// <summary>Explicit filesystem ownership shared by the two independent save slots.</summary>
public static class SaveFile
{
    public static JsonNode? ReadDocument(string path)
    {
        RequireAbsolutePath(path);
        string text;
        try
        {
            text = File.ReadAllText(path, new UTF8Encoding(false, true));
        }
        catch (DecoderFallbackException error) { throw new StoredDocumentException("Save file is not valid UTF-8.", error); }
        catch (Exception error) when (IsAccessFailure(error)) { throw new SaveStorageException(SaveFileOperation.Read, path, error); }
        try
        {
            return EcmaJson.Parse(text);
        }
        catch (JsonException error) { throw new StoredDocumentException("Save file is not valid JSON.", error); }
    }

    public static void WriteDocument(string path, JsonNode document)
    {
        RequireAbsolutePath(path);
        var text = EcmaJson.Stringify(document);
        var directory = Path.GetDirectoryName(path)!;
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var created = false;
        try
        {
            Directory.CreateDirectory(directory);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.WriteThrough))
            {
                created = true;
                using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
                writer.Write(text);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception error)
        {
            try
            {
                if (created)
                    File.Delete(temporary);
            }
            catch (Exception cleanupError) when (IsAccessFailure(cleanupError))
            {
                var failures = new AggregateException("Save write and temporary-file cleanup both failed.", error, cleanupError);
                if (IsAccessFailure(error))
                    throw new SaveStorageException(SaveFileOperation.Write, path, failures);
                throw failures;
            }
            if (IsAccessFailure(error))
                throw new SaveStorageException(SaveFileOperation.Write, path, error);
            throw;
        }
    }

    public static void Clear(string path)
    {
        RequireAbsolutePath(path);
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (IsAccessFailure(error)) { throw new SaveStorageException(SaveFileOperation.Clear, path, error); }
    }

    internal static bool IsAccessFailure(Exception error) => error is IOException or UnauthorizedAccessException or SecurityException;

    internal static void RequireAbsolutePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException("Save storage needs an explicit absolute filesystem path.", nameof(path));
    }
}
