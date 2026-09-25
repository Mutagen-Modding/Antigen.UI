using System.Reactive.Linq;
using Antigen.Services;
using Antigen.ViewModels.Profiles;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels;

public sealed partial class NavigationController : ReactiveObject, ISingleton
{
    private readonly ActiveProfileController _activeProfile;
    private readonly Lazy<WelcomeVM> _welcome;

    [Reactive] public partial ResizablePanelVM? Active { get; private set; }

    private ResizablePanelVM? _last;

    public NavigationController(ActiveProfileController activeProfile, Lazy<WelcomeVM> welcome)
    {
        _activeProfile = activeProfile;
        _welcome = welcome;

        _activeProfile.WhenAnyValue(x => x.Active)
            .Skip(1)
            .Subscribe(_ => ProfileSwitched());
    }

    public void GoHome()
    {
        GoTo(Fallback);
    }

    public void GoTo(ResizablePanelVM panel)
    {
        _last = null;
        Active = panel;
    }

    public void Open(ResizablePanelVM panel)
    {
        if (Active == panel) return;

        _last = Active;
        Active = panel;
    }

    public void Back()
    {
        Active = _last ?? Fallback;
        _last = null;
    }

    private void ProfileSwitched()
    {
        if (BelongsToProfile(_last))
        {
            _last = null;
        }

        if (Active is LoadingVM || BelongsToProfile(Active))
        {
            GoHome();
        }
    }

    private static bool BelongsToProfile(ResizablePanelVM? panel) => panel is not (null or ISingleton);

    private ResizablePanelVM Fallback => _activeProfile.Active?.Home ?? (ResizablePanelVM)_welcome.Value;
}
