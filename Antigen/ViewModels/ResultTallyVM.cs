using System.Reactive.Linq;
using Antigen.Models.Analyzer;
using DynamicData;
using DynamicData.Aggregation;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels;

public sealed partial class ResultTallyVM : ViewModel, ITransient
{
    private readonly ModWatcherVM _watcher;

    [ObservableAsProperty(PropertyName = "TotalResults")]
    private IObservable<int> TotalResultsObservable() =>
        _watcher.ApplicableResults.Connect()
            .Count()
            .ObserveOn(RxSchedulers.MainThreadScheduler);

    [ObservableAsProperty(PropertyName = "NewResultsCount")]
    private IObservable<int> NewResultsCountObservable() =>
        _watcher.ApplicableResults.Connect()
            .Filter(_watcher.RunStarted
                .Select(_ => _watcher.AllResults.Keys.ToHashSet())
                .StartWith(new HashSet<string>())
                .Select(baseline => new Func<AnalyzerResultInfo, bool>(x => !baseline.Contains(x.GetIdentifier()))))
            .Count()
            .ObserveOn(RxSchedulers.MainThreadScheduler);

    [ObservableAsProperty(PropertyName = "ResolvedResults")]
    private IObservable<int> ResolvedResultsObservable() =>
        _watcher.AllResults.Connect()
            .Scan(0, (total, changes) => total + changes.Removes)
            .ObserveOn(RxSchedulers.MainThreadScheduler);

    public ResultTallyVM(ModWatcherVM watcher)
    {
        _watcher = watcher;
        InitializeOAPH();
    }
}
