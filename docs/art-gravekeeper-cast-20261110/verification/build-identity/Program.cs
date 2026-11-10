using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

string Hash(byte[] x) => Convert.ToHexString(SHA256.HashData(x)).ToLowerInvariant();
var output = args[2];
Directory.CreateDirectory(output);
var reports = new List<object>();
bool passed = true;
foreach (var name in new[] { "Empire.dll", "Empire.Core.dll" })
{
    var a = Inspect(Path.Combine(args[0], name));
    var b = Inspect(Path.Combine(args[1], name));
    var differences = Enumerable.Range(0, Math.Min(a.Raw.Length, b.Raw.Length)).Where(i => a.Raw[i] != b.Raw[i]).Select(i => new { offset = i, oldByte = a.Raw[i], newByte = b.Raw[i] }).ToArray();
    var remaining = Enumerable.Range(0, Math.Min(a.Normal.Length, b.Normal.Length)).Where(i => a.Normal[i] != b.Normal[i]).ToArray();
    var methodsEqual = JsonSerializer.Serialize(a.Methods) == JsonSerializer.Serialize(b.Methods);
    passed &= methodsEqual && a.Normal.SequenceEqual(b.Normal);
    var result = new
    {
        name,
        oldFirstFieldMetadataOffset = a.FirstFieldMetadataOffset,
        newFirstFieldMetadataOffset = b.FirstFieldMetadataOffset,
        oldPath = a.Path,
        newPath = b.Path,
        oldSha256 = Hash(a.Raw),
        newSha256 = Hash(b.Raw),
        oldLength = a.Raw.Length,
        newLength = b.Raw.Length,
        oldVersion = a.Version,
        newVersion = b.Version,
        oldMvid = a.Mvid,
        newMvid = b.Mvid,
        methodCount = a.Methods.Count,
        methodsEqual,
        oldTables = a.Tables,
        newTables = b.Tables,
        oldNormalization = a.Ranges,
        newNormalization = b.Ranges,
        differentBytes = differences.Length,
        differences,
        remainingUnexplainedOffsets = remaining,
        normalizedWholeFileEqual = a.Normal.SequenceEqual(b.Normal),
        normalizedSha256Old = Hash(a.Normal),
        normalizedSha256New = Hash(b.Normal),
        scope = "Read-only PEReader/MetadataReader. Whole-file normalization restricted to COFF timestamp, module MVID, assembly informational-version commit, matching native version-resource commit, CodeView GUID and debug entry stamp, PDB checksum bytes. No method/type/field/signature/IL/local/EH or managed-resource bytes excluded. Reflection can observe the excluded metadata: not universal behavioral identity."
    };
    File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    File.WriteAllText(Path.Combine(output, name + ".methods.old.json"), JsonSerializer.Serialize(a.Methods));
    File.WriteAllText(Path.Combine(output, name + ".methods.new.json"), JsonSerializer.Serialize(b.Methods));
    reports.Add(result);
    Console.WriteLine($"{name}: methods={a.Methods.Count}, methodsEqual={methodsEqual}, raw differences={differences.Length}, unexplained={remaining.Length}, normalizedEqual={a.Normal.SequenceEqual(b.Normal)}");
}
File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));

Environment.ExitCode = passed ? 0 : 1;

Audit Inspect(string path)
{
    var raw = File.ReadAllBytes(path);
    var norm = (byte[])raw.Clone();
    using var pe = new PEReader(new MemoryStream(raw));
    var md = pe.GetMetadataReader();
    var ranges = new List<object>();
    void Mask(int offset, int length, string reason)
    {
        ranges.Add(new
        {
            offset,
            length,
            reason,
            originalHex = Convert.ToHexString(raw.AsSpan(offset, length))
        });
        Array.Clear(norm, offset, length);
    }
    int Rva(int rva)
    {
        var s = pe.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva < s.VirtualAddress + Math.Max(s.VirtualSize, s.SizeOfRawData));
        return s.PointerToRawData + rva - s.VirtualAddress;
    }
    int Find(byte[] needle, int start, int length)
    {
        var hits = new List<int>();
        for (int i = start; i <= start + length - needle.Length; i++)
            if (raw.AsSpan(i, needle.Length).SequenceEqual(needle))
                hits.Add(i);
        if (hits.Count != 1)
            throw new Exception($"Expected unique bytes: {path} hits={hits.Count}");
        return hits[0];
    }
    var mvid = md.GetGuid(md.GetModuleDefinition().Mvid);
    int metadata = pe.PEHeaders.MetadataStartOffset;
    Mask(pe.PEHeaders.CoffHeaderStartOffset + 4, 4, "COFF deterministic-build timestamp");
    Mask(Find(mvid.ToByteArray(), metadata, pe.PEHeaders.MetadataSize), 16, "Module MVID");
    string version = "";
    foreach (var h in md.GetAssemblyDefinition().GetCustomAttributes())
    {
        var ca = md.GetCustomAttribute(h);
        if (ca.Constructor.Kind != HandleKind.MemberReference)
            continue;
        var member = md.GetMemberReference((MemberReferenceHandle)ca.Constructor);
        if (member.Parent.Kind != HandleKind.TypeReference)
            continue;
        var type = md.GetTypeReference((TypeReferenceHandle)member.Parent);
        if (md.GetString(type.Name) != "AssemblyInformationalVersionAttribute")
            continue;
        var reader = md.GetBlobReader(ca.Value);
        if (reader.ReadUInt16() != 1)
            throw new Exception("Bad custom attribute prolog");
        version = reader.ReadSerializedString()!;
        var commit = version.Split('+').Last();
        if (commit.Length != 40 || !commit.All(Uri.IsHexDigit))
            throw new Exception("Unexpected version " + version);
        var bytes = md.GetBlobBytes(ca.Value);
        int blob = Find(bytes, metadata, pe.PEHeaders.MetadataSize);
        int at = Encoding.UTF8.GetString(bytes).IndexOf(commit, StringComparison.Ordinal);
        Mask(blob + at, 40, "AssemblyInformationalVersionAttribute git commit only");
        var dir = pe.PEHeaders.PEHeader!.ResourceTableDirectory;
        if (dir.Size > 0)
        {
            var needle = Encoding.Unicode.GetBytes(commit);
            Mask(Find(needle, Rva(dir.RelativeVirtualAddress), dir.Size), needle.Length, "Native version resource informational git commit only");
        }
    }
    if (version == "")
        throw new Exception("No informational version");
    var debug = pe.ReadDebugDirectory();
    int debugOffset = Rva(pe.PEHeaders.PEHeader!.DebugTableDirectory.RelativeVirtualAddress);
    for (int i = 0; i < debug.Length; i++)
    {
        var d = debug[i];
        if (d.Type == DebugDirectoryEntryType.CodeView)
        {
            var cv = pe.ReadCodeViewDebugDirectoryData(d);
            if (!raw.AsSpan(d.DataPointer, 4).SequenceEqual("RSDS"u8))
                throw new Exception("Not RSDS");
            Mask(debugOffset + i * 28 + 4, 4, "CodeView debug entry stamp");
            Mask(d.DataPointer + 4, 16, "CodeView PDB GUID only (path and age retained)");
        }
        else if (d.Type == DebugDirectoryEntryType.PdbChecksum)
        {
            var ck = pe.ReadPdbChecksumDebugDirectoryData(d);
            int prefix = Encoding.UTF8.GetByteCount(ck.AlgorithmName) + 1;
            Mask(d.DataPointer + prefix, ck.Checksum.Length, "PDB checksum only (algorithm retained)");
        }
    }
    var methods = new List<object>();
    foreach (var h in md.MethodDefinitions)
    {
        var m = md.GetMethodDefinition(h);
        object? body = null;
        if (m.RelativeVirtualAddress != 0)
        {
            var x = pe.GetMethodBody(m.RelativeVirtualAddress);
            body = new
            {
                il = Convert.ToHexString(x.GetILBytes()!),
                x.MaxStack,
                x.LocalVariablesInitialized,
                localSignatureToken = MetadataTokens.GetToken(x.LocalSignature),
                localSignature = x.LocalSignature.IsNil ? "" : Convert.ToHexString(md.GetBlobBytes(md.GetStandaloneSignature(x.LocalSignature).Signature)),
                exceptions = x.ExceptionRegions.Select(e => new { kind = e.Kind.ToString(), e.TryOffset, e.TryLength, e.HandlerOffset, e.HandlerLength, e.FilterOffset, catchTypeToken = MetadataTokens.GetToken(e.CatchType) }).ToArray()
            };
        }
        methods.Add(new
        {
            token = MetadataTokens.GetToken(h),
            name = md.GetString(m.Name),
            signature = Convert.ToHexString(md.GetBlobBytes(m.Signature)),
            attributes = (int)m.Attributes,
            implAttributes = (int)m.ImplAttributes,
            m.RelativeVirtualAddress,
            body
        });
    }
    var tables = Enum.GetValues<TableIndex>().Select(t => new { table = t.ToString(), rows = md.GetTableRowCount(t) }).Where(x => x.rows > 0).Cast<object>().ToList();
    return new(path, raw, norm, version, mvid.ToString(), ranges, methods, tables, metadata + md.GetTableMetadataOffset(TableIndex.Field));
}
record Audit(string Path, byte[] Raw, byte[] Normal, string Version, string Mvid, List<object> Ranges, List<object> Methods, List<object> Tables, int FirstFieldMetadataOffset);
