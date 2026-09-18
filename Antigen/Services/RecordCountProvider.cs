using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Headers;
using Mutagen.Bethesda.Plugins.Records.Internals;

namespace Antigen.Services;

/// <summary>
///     Record counts straight out of each plugin's HEDR subrecord.  Cached by path and last-write time.
/// </summary>
public sealed class RecordCountProvider(IFileSystem fileSystem, ILogger<RecordCountProvider> logger) : ISingleton
{
    private readonly ConcurrentDictionary<(string Path, DateTime Written), uint?> _cache = new();

    public uint? Get(GameRelease release, ModKey modKey, string path)
    {
        DateTime written;
        try
        {
            written = fileSystem.File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return _cache.GetOrAdd((path, written), _ => Read(release, modKey, path));
    }

    private uint? Read(GameRelease release, ModKey modKey, string path)
    {
        try
        {
            var header = ModHeaderFrame.FromPath(new ModPath(modKey, path), release, fileSystem);
            foreach (var subrecord in header)
            {
                if (subrecord.RecordType != RecordTypes.HEDR) continue;
                if (subrecord.Content.Length < 8) break;

                return BinaryPrimitives.ReadUInt32LittleEndian(subrecord.Content.Slice(4, 4));
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read record count for {Path}", path);
        }

        return null;
    }
}
