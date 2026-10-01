using System.IO.Abstractions;
using Antigen.Models.Settings;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments.DI;
using Noggog;

namespace Antigen.Services;

public sealed class DataFolderResolver(
    IDataDirectoryLookup lookup,
    IFileSystem fileSystem,
    ILogger<DataFolderResolver> logger) : ISingleton
{
    public GetResponse<DirectoryPath> Resolve(Profile profile) => Resolve(profile.Release, profile.DataFolderOverride);

    public GetResponse<DirectoryPath> Resolve(GameRelease release, string? overridePath)
    {
        try
        {
            if (!overridePath.IsNullOrWhitespace())
            {
                return Exists(new DirectoryPath(overridePath));
            }

            return lookup.TryGet(release, out var located)
                ? Exists(located)
                : GetResponse<DirectoryPath>.Fail(
                    "Could not automatically locate the Data folder. Run the game once, or set the folder manually.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not locate the data folder for {Release}", release);
            return GetResponse<DirectoryPath>.Fail(ex.Message);
        }
    }

    private GetResponse<DirectoryPath> Exists(DirectoryPath folder) =>
        fileSystem.Directory.Exists(folder)
            ? GetResponse<DirectoryPath>.Succeed(folder)
            : GetResponse<DirectoryPath>.Fail(folder, $"Data folder did not exist: {folder}");
}
