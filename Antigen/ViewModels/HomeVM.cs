using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Antigen.Services;
using Antigen.ViewModels.Profiles;
using DynamicData;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Plugins;
using Noggog;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels;

public sealed partial class HomeVM : ResizablePanelVM, IActiveScoped
{
    private readonly Subject<ModKey> _startRequested = new();

    public override double MinResizeHeight => 100.0;

    public IObservable<ModKey> StartRequested => _startRequested;

    public ReadOnlyObservableCollection<ModKey> FilteredModKeys { get; }

    [Reactive] public partial string SearchText { get; set; } = string.Empty;

    [Reactive] public partial ErrorResponse State { get; private set; } = ErrorResponse.Success;

    public HomeVM(ProfileVM profile, ILogger<HomeVM> logger)
    {
        IsExpanded = true;
        ExpandedHeight = 696.0;

        profile.SelectedLoadOrder
            .Transform(listing => listing.ModKey)
            .Filter(this.WhenAnyValue(x => x.SearchText)
                .Select(search => new Func<ModKey, bool>(key =>
                    string.IsNullOrWhiteSpace(search)
                 || key.FileName.String.Contains(search, StringComparison.OrdinalIgnoreCase))))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Bind(out var filtered)
            .Subscribe()
            .DisposeWith(this);
        FilteredModKeys = filtered;

        profile.LoadOrderState
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(state =>
            {
                State = state;
                if (state.Failed)
                {
                    logger.LogWarning("Load order unavailable for {Profile}: {Reason}", profile.Name, state.Reason);
                }
            })
            .DisposeWith(this);
    }

    [ReactiveCommand]
    private void StartWatching(ModKey modKey)
    {
        if (modKey.IsNull) return;

        _startRequested.OnNext(modKey);
    }
}
