using System.Reactive.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments.DI;
using Mutagen.Bethesda.Plugins;
using Noggog;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels.Profiles;

public sealed partial class ActiveProfileVM : ViewModel, IActiveScoped
{
    private readonly NavigationController _navigation;
    private readonly Func<ModKey, SessionVM> _sessionFactory;

    public ProfileVM Profile { get; }

    public GameRelease Release { get; }

    public HomeVM Home { get; }

    [Reactive] public partial SessionVM? Session { get; private set; }

    public ActiveProfileVM(
        ProfileVM profile,
        IGameReleaseContext release,
        HomeVM home,
        NavigationController navigation,
        Func<ModKey, SessionVM> sessionFactory)
    {
        Profile = profile;
        Release = release.Release;
        Home = home;
        _navigation = navigation;
        _sessionFactory = sessionFactory;

        Home.StartRequested
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(StartWatching)
            .DisposeWith(this);
    }

    private void StartWatching(ModKey modKey)
    {
        var previous = Session;

        Session = _sessionFactory(modKey);
        _navigation.GoTo(Session.Analyzer);

        previous?.Dispose();
    }
}
