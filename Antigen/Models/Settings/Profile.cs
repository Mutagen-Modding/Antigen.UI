using Mutagen.Bethesda;
using Noggog;

namespace Antigen.Models.Settings;

public sealed record ProfilesSettingsFile(IReadOnlyList<Profile> Profiles);

public sealed record Profile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required GameRelease Release { get; init; }
    public string? DataFolderOverride { get; init; }
    public bool FollowLoadOrder { get; init; } = true;
    public IReadOnlyList<PluginSelection> Plugins { get; init; } = [];
    public string DisplayName => Name.IsNullOrWhitespace() ? Release.ToDescriptionString() : Name;
}

public sealed record PluginSelection(string FileName, bool Enabled);
