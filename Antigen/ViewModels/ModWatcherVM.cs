using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Antigen.Models.Analyzer;
using Antigen.Models.Settings;
using Antigen.Services;
using DynamicData;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Analyzers.SDK.Topics;
using Mutagen.Bethesda.Plugins;
using Noggog;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels;

public sealed partial class ModWatcherVM : ViewModel, ITransient
{
    private readonly SourceCache<AnalyzerResultInfo, string> _allResults = new(x => x.GetIdentifier());
    private readonly Subject<Unit> _runStarted = new();
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ModWatcherVM> _logger;

    [Reactive] public partial bool IsAnalyzing { get; set; }
    [Reactive] public partial string Status { get; set; }
    [Reactive] public partial AnalyzerStatus AnalyzerStatus { get; set; }
    [Reactive] public partial Severity MinimumSeverity { get; set; } = Severity.None;

    public IObservableCache<AnalyzerResultInfo, string> AllResults => _allResults;
    public IObservableCache<AnalyzerResultInfo, string> ApplicableResults { get; }
    public IObservable<Unit> RunStarted => _runStarted;

    public ModKey ModKey { get; }

    public ModWatcherVM(
        ModKey modKey,
        IModWatcher modWatcher,
        ISettingsService settingsService,
        ILogger<ModWatcherVM> logger)
    {
        ModKey = modKey;
        _settingsService = settingsService;
        _logger = logger;
        _allResults.DisposeWith(this);
        _runStarted.DisposeWith(this);

        ApplicableResults = _allResults.Connect()
            .Filter(settingsService.RulesChanged
                .Unit()
                .StartWith(Unit.Default)
                .Select(_ => new Func<AnalyzerResultInfo, bool>(x => !settingsService.IsIgnored(modKey, x))))
            .AsObservableCache()
            .DisposeWith(this);

        Status = "Initializing...";
        AnalyzerStatus = AnalyzerStatus.Idle;

        modWatcher.Watch(modKey, this.WhenAnyValue(x => x.MinimumSeverity))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(ConsumeRun)
            .Switch()
            .Subscribe()
            .DisposeWith(this);

        Status = $"Watching {modKey.FileName}...";
    }

    private IObservable<Unit> ConsumeRun(IObservable<AnalysisEvent> run)
    {
        _runStarted.OnNext(Unit.Default);
        var seen = new HashSet<string>();
        var failed = false;

        return run
            .Publish(events => Observable.Merge(
                events.OfType<AnalysisEvent.Status>()
                    .Select(x => x.Update)
                    .Do(x => failed |= x.Status == AnalyzerStatus.Error)
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Do(UpdateStatus)
                    .Unit(),
                events.OfType<AnalysisEvent.Result>()
                    .Select(x => x.Info)
                    .Buffer(TimeSpan.FromMilliseconds(1000), RxSchedulers.TaskpoolScheduler)
                    .Where(x => x.Count > 0)
                    .Do(batch =>
                    {
                        seen.UnionWith(batch.Select(x => x.GetIdentifier()));
                        _allResults.AddOrUpdate(batch);
                    })
                    .Unit()))
            .Concat(Observable.Defer(() =>
            {
                if (!failed) RemoveUnseen(seen);
                return Observable.Empty<Unit>();
            }))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Catch<Unit, Exception>(ex =>
            {
                OnAnalysisFailed(ex);
                return Observable.Empty<Unit>();
            });
    }

    private void RemoveUnseen(IReadOnlySet<string> seen)
    {
        _allResults.RemoveKeys(_allResults.Keys.Where(key => !seen.Contains(key)).ToArray());
    }

    private void OnAnalysisFailed(Exception exception)
    {
        _logger.LogError(exception, "Analysis of {ModKey} failed", ModKey);

        AnalyzerStatus = AnalyzerStatus.Error;
        Status = $"Analysis failed: {exception.Message}";
        IsAnalyzing = false;
    }

    private void UpdateStatus(StatusUpdate update)
    {
        AnalyzerStatus = update.Status;
        Status = update.Message ?? Status;
        IsAnalyzing = update.Status is AnalyzerStatus.Analyzing or AnalyzerStatus.Preparing;
    }

    [ReactiveCommand]
    public void IgnoreResult(AnalyzerResultInfo resultInfo, IgnoreType ignoreType)
    {
        _settingsService.AddRule(ModKey, resultInfo, ignoreType);
    }
}
