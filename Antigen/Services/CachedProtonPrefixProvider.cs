using System.Collections.Concurrent;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Installs.DI;

namespace Antigen.Services;

/// <summary>
/// Cache the results for the purposes of this app
/// </summary>
public sealed class CachedProtonPrefixProvider : IProtonPrefixProvider
{
    private readonly ProtonPrefixProvider _inner = new();
    private readonly ConcurrentDictionary<GameRelease, string?> _localAppData = new();
    private readonly ConcurrentDictionary<GameRelease, string?> _myDocuments = new();

    public string? TryGetProtonLocalAppData(GameRelease release) =>
        _localAppData.GetOrAdd(release, _inner.TryGetProtonLocalAppData);

    public string? TryGetProtonMyDocuments(GameRelease release) =>
        _myDocuments.GetOrAdd(release, _inner.TryGetProtonMyDocuments);
}
