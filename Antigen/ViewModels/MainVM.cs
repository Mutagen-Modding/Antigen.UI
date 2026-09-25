using System.Reactive.Linq;
using Antigen.Services;
using Antigen.ViewModels.Profiles;
using Antigen.Views;
using Avalonia.Controls;
using Noggog;
using Noggog.UI;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels;

public sealed partial class MainVM : ViewModel, ISingleton
{
    private readonly NavigationController _navigation;
    private readonly ActiveProfileController _activeProfile;
    private readonly GlobalSettingsVM _globalSettings;
    private readonly ProfilesVM _profiles;
    private readonly IMainWindow _mainWindow;

    private ResizablePanelVM? _sizedPanel;
    private double _expandedHeight;
    private double _expandedWidth;

    [Reactive] public partial int WindowX { get; set; }
    [Reactive] public partial int WindowY { get; set; }
    [Reactive] public partial bool AnchoredToBottom { get; set; }

    public string Version { get; }

    public double ExpandedHeight => ActivePanel?.ExpandedHeight ?? _expandedHeight;
    public double ExpandedWidth => ActivePanel?.ExpandedWidth ?? _expandedWidth;

    [ObservableAsProperty(PropertyName = "ProfileName", InitialValue = "\"Profiles\"")]
    private IObservable<string> ProfileNameObservable() =>
        _activeProfile.WhenAnyValue(x => x.Active)
            .Select(active => active?.Profile.DisplayName ?? "Profiles");

    [ObservableAsProperty(PropertyName = "ActivePanel")]
    private IObservable<ResizablePanelVM?> ActivePanelObservable() =>
        _navigation.WhenAnyValue(x => x.Active);

    [ObservableAsProperty(PropertyName = "IsExpanded")]
    private IObservable<bool> IsExpandedObservable() =>
        _navigation.WhenAnyValue(x => x.Active)
            .Select(panel => panel?.WhenAnyValue(x => x.IsExpanded) ?? Observable.Return(false))
            .Switch()
            .StartWith(true);

    [ObservableAsProperty(PropertyName = "ShowPeek")]
    private IObservable<bool> ShowPeekObservable() =>
        _navigation.WhenAnyValue(x => x.Active)
            .Select(panel => panel?.WhenAnyValue(x => x.IsExpanded, x => x.IsPeeking, (expanded, peeking) => !expanded && peeking)
                ?? Observable.Return(false))
            .Switch()
            .StartWith(false);

    [ObservableAsProperty(PropertyName = "Session")]
    private IObservable<SessionVM?> SessionObservable() =>
        _activeProfile.WhenAnyFallback(x => x.Active!.Session);

    [ObservableAsProperty(PropertyName = "ShowStatusBar")]
    private IObservable<bool> ShowStatusBarObservable() =>
        this.WhenAnyValue(x => x.Session)
            .Select(session => session is not null)
            .StartWith(false);

    [ObservableAsProperty(PropertyName = "StatusBarDock")]
    private IObservable<Dock> StatusBarDockObservable() =>
        this.WhenAnyValue(x => x.ShowPeek, x => x.AnchoredToBottom,
            (peeking, bottom) => peeking && !bottom ? Dock.Top : Dock.Bottom)
            .StartWith(Dock.Bottom);

    [ObservableAsProperty(PropertyName = "PeekArrowDown")]
    private IObservable<bool> PeekArrowDownObservable() =>
        this.WhenAnyValue(x => x.ShowPeek, x => x.AnchoredToBottom, (peeking, bottom) => peeking == bottom)
            .StartWith(true);

    [ObservableAsProperty(PropertyName = "ShowStatusDivider")]
    private IObservable<bool> ShowStatusDividerObservable() =>
        this.WhenAnyValue(x => x.IsExpanded, x => x.ShowPeek, x => x.ShowStatusBar,
            (expanded, peeking, status) => (expanded || peeking) && status)
            .StartWith(false);

    public MainVM(
        GuiSettingsService guiSettings,
        GlobalSettingsVM globalSettings,
        ProfilesVM profiles,
        NavigationController navigation,
        ActiveProfileController activeProfile,
        VersionProvider versionProvider,
        IMainWindow mainWindow)
    {
        _navigation = navigation;
        _activeProfile = activeProfile;
        _globalSettings = globalSettings;
        _profiles = profiles;
        _mainWindow = mainWindow;

        Version = $"v{versionProvider.Current}";

        var saved = guiSettings.Current;
        WindowX = saved.WindowX ?? 0;
        WindowY = saved.WindowY ?? 0;
        _expandedHeight = saved.ExpandedHeight;
        _expandedWidth = saved.ExpandedWidth;

        InitializeOAPH();

        _navigation.WhenAnyValue(x => x.Active)
            .Subscribe(CarrySize)
            .DisposeWith(this);
    }

    [ReactiveCommand]
    private void OpenSettings()
    {
        _navigation.Open(_globalSettings);
    }

    [ReactiveCommand]
    private void OpenProfile()
    {
        _navigation.Open(_profiles);
    }

    [ReactiveCommand]
    private void ToggleCollapsed()
    {
        if (ActivePanel is not { } panel) return;

        panel.IsExpanded = !panel.IsExpanded;
        panel.IsPeeking = false;
    }

    [ReactiveCommand]
    private void TogglePeek()
    {
        if (ActivePanel is not { } panel) return;

        panel.IsPeeking = !panel.IsPeeking;
    }

    [ReactiveCommand]
    private void Minimize()
    {
        _mainWindow.Minimize();
    }

    [ReactiveCommand]
    private void ToggleMaximize()
    {
        _mainWindow.ToggleMaximize();
    }

    [ReactiveCommand]
    private void Close()
    {
        _mainWindow.Close();
    }

    // Carry the resized height across panel switches so the window keeps its size.
    private void CarrySize(ResizablePanelVM? panel)
    {
        if (_sizedPanel is { } leaving)
        {
            _expandedHeight = leaving.ExpandedHeight;
            _expandedWidth = leaving.ExpandedWidth;
        }

        _sizedPanel = panel;
        if (panel is null) return;

        panel.ExpandedHeight = Math.Clamp(_expandedHeight, panel.MinResizeHeight, panel.MaxResizeHeight);
        panel.ExpandedWidth = Math.Clamp(_expandedWidth, panel.MinResizeWidth, panel.MaxResizeWidth);
    }
}
