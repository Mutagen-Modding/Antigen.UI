using Antigen.Services;
using Noggog;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels.Profiles;

public sealed partial class WelcomeVM : ResizablePanelVM, ISingleton
{
    private readonly Lazy<ProfilesVM> _profiles;
    private readonly NavigationController _navigation;
    private readonly ActiveProfileController _activeProfile;

    public override double MinResizeHeight => 220.0;

    public GameReleasePickerVM Target { get; } = new("Select Your Game");

    public WelcomeVM(NavigationController navigation, ActiveProfileController activeProfile, Lazy<ProfilesVM> profiles)
    {
        _navigation = navigation;
        _activeProfile = activeProfile;
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
        _activeProfile.Activate(profiles.AddNewProfile(release));
        _navigation.GoTo(profiles);
    }
}
