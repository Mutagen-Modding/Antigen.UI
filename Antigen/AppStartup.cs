using System.Runtime.InteropServices;
using Antigen.Models.Settings;
using Antigen.Services;
using Antigen.ViewModels;
using Antigen.ViewModels.Profiles;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace Antigen;

/// <summary>Brings the app up once the container exists, and puts it away again on exit.</summary>
public sealed class AppStartup(
    Window window,
    MainVM main,
    NavigationController navigation,
    ProfilesVM profiles,
    WelcomeVM welcome,
    LoadingVM loading,
    GuiSettingsService guiSettings,
    ShutdownService shutdown,
    ILogger<AppStartup> logger) : ISingleton
{
    public void Start()
    {
        logger.LogInformation(
            "Antigen starting - {Runtime} on {OS} with {ProcessorCount} processors",
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            Environment.ProcessorCount);

        var saved = guiSettings.Current;

        if (profiles.Profiles.Count == 0)
        {
            navigation.GoTo(welcome);
        }
        else
        {
            navigation.GoTo(loading);
            profiles.Activate(saved.ActiveProfileId);
        }

        window.DataContext = main;

        RestorePosition(saved);
    }

    public void Shutdown()
    {
        shutdown.Save();
    }

    private void RestorePosition(GuiSettings saved)
    {
        if (saved.WindowX is { } x && saved.WindowY is { } y
                                   && window.Screens.All.Any(s => s.Bounds.Contains(new PixelPoint(x, y))))
        {
            window.Position = new PixelPoint(x, y);
            return;
        }

        if (window.Screens.Primary is { } screen)
        {
            window.Position = new PixelPoint(
                screen.WorkingArea.X + (screen.WorkingArea.Width - (int)window.Width) / 2,
                screen.WorkingArea.Y + (screen.WorkingArea.Height - (int)window.Height) / 2
            );
        }
    }
}
