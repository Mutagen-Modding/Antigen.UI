using System.Reactive.Linq;
using Antigen.ViewModels.Profiles;
using Autofac;
using Noggog;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Antigen.Services;

public sealed partial class ActiveProfileController : ReactiveObject, ISingleton
{
    [Reactive] private partial ProfileVM? Target { get; set; }

    [ObservableAsProperty(PropertyName = "Active")]
    private IObservable<ActiveProfileVM?> ActiveObservable() =>
        this.WhenAnyValue(x => x.Target)
            .ObserveOn(RxSchedulers.TaskpoolScheduler)
            .Select(profile => profile?.BeginActive())
            .DisposePrevious()
            .Select(scope => scope?.Resolve<ActiveProfileVM>())
            .ObserveOn(RxSchedulers.MainThreadScheduler);

    public ActiveProfileController()
    {
        InitializeOAPH();
    }

    public void Activate(ProfileVM? profile)
    {
        Target = profile;
    }
}
