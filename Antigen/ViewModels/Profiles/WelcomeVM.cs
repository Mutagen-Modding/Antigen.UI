using Noggog;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels.Profiles;

public sealed partial class WelcomeVM : ResizablePanelVM, ISingleton
{
    private readonly Lazy<ProfilesVM> _profiles;
    private readonly NavigationController _navigation;

    public override double MinResizeHeight => 220.0;

    public GameReleasePickerVM Target { get; } = new("Select Your Game");

    public WelcomeVM(NavigationController navigation, Lazy<ProfilesVM> profiles)
    {
        _navigation = navigation;
        _profiles = profiles;
        IsExpanded = true;
        ExpandedHeight = 320.0;
        Target.DisposeWith(this);
    }

    [ReactiveCommand]
    private void CreateFirstProfile()
    {
        if (Target.Release is not { } release) return;

        var profiles = _profiles.Value;
        profiles.AddNewProfile(release);
        _navigation.GoTo(profiles);
    }
}
