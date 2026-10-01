using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Antigen.Models.Settings;
using Microsoft.Extensions.Logging;

namespace Antigen.Services;

public sealed class ProfilesSettingsService : ISingleton
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ProfilesSettingsService> _logger;
    private readonly string _filePath;

    private IReadOnlyList<Profile>? _cached;

    public ProfilesSettingsService(IFileSystem fileSystem, ILogger<ProfilesSettingsService> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _filePath = _fileSystem.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Profiles.json"
        );
    }

    public IReadOnlyList<Profile> Load()
    {
        return _cached ??= Read();
    }

    public void Save(IEnumerable<Profile> profiles)
    {
        var snapshot = profiles.ToArray();
        _cached = snapshot;

        try
        {
            var json = JsonSerializer.Serialize(new ProfilesSettingsFile(snapshot), Options);

            // Write to a temp file then swap, so a crash mid-write can't corrupt the profiles.
            var tempPath = _filePath + ".tmp";
            _fileSystem.File.WriteAllText(tempPath, json);
            _fileSystem.File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to save profiles");
        }
    }

    private IReadOnlyList<Profile> Read()
    {
        if (!_fileSystem.File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            var json = _fileSystem.File.ReadAllText(_filePath);
            var profiles = JsonSerializer.Deserialize<ProfilesSettingsFile>(json, Options)?.Profiles ?? [];

            foreach (var unsupported in profiles.Where(p => !GameSupport.IsSupported(p.Release)))
            {
                _logger.LogWarning("Dropping profile {Profile}: {Release} is not supported", unsupported.Name, unsupported.Release);
            }

            return profiles.Where(p => GameSupport.IsSupported(p.Release)).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogError(ex, "Failed to load profiles");
            return [];
        }
    }
}
