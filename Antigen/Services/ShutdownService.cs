using Antigen.Models.Settings;
using Antigen.ViewModels;
using Antigen.ViewModels.Profiles;
using Microsoft.Extensions.Logging;

namespace Antigen.Services;

public sealed class ShutdownService(
    GuiSettingsService guiSettings,
    ProfilesSettingsService profilesSettings,
    ProfilesVM profiles,
    GlobalSettingsVM globalSettings,
    MainVM mainVM,
    ILogger<ShutdownService> logger) : ISingleton
{
    public void Save()
    {
        logger.LogInformation("Exiting");

        profilesSettings.Save(profiles.ToProfiles());

        guiSettings.Save(guiSettings.Current with
        {
            WindowX = mainVM.WindowX,
            WindowY = mainVM.WindowY,
            ExpandedHeight = mainVM.ExpandedHeight,
            ExpandedWidth = mainVM.ExpandedWidth,
            WorkerThreadPercentage = globalSettings.CorePercentage,
            ColorScheme = globalSettings.ColorScheme,
            ActiveProfileId = profiles.Profiles.FirstOrDefault(p => p.IsActive)?.Id
        });
    }
}
