using System.IO.Abstractions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Antigen.Models.Analyzer;
using Antigen.ViewModels.Profiles;
using DynamicData;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Analyzers.SDK.Topics;
using Mutagen.Bethesda.Plugins;
using Noggog;
using ReactiveMarbles.ObservableEvents;
using ReactiveUI;

namespace Antigen.Services;

public interface IModWatcher
{
    IObservable<IObservable<AnalysisEvent>> Watch(IObservable<Severity> minimumSeverity);
}

public sealed class ModWatcher(
    IFileSystem fileSystem,
    ProfileVM profile,
    IAnalyzerService analyzerService,
    ILogger<ModWatcher> logger)
    : IModWatcher, IActiveScoped
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(500);

    public IObservable<IObservable<AnalysisEvent>> Watch(IObservable<Severity> minimumSeverity)
    {
        var targets = profile.SelectedLoadOrder
            .ToCollection()
            .Select(listings => (IReadOnlySet<ModKey>)listings.Select(listing => listing.ModKey).ToHashSet());

        var runs = profile.WhenAnyValue(x => x.DataFolderResult)
            .DistinctUntilChanged()
            .Select(result => result.Succeeded
                ? WatchFolder(result.Value, targets, minimumSeverity)
                : Observable.Return(Observable.Return<AnalysisEvent>(
                    new AnalysisEvent.Status(new StatusUpdate(AnalyzerStatus.Idle, $"Data folder unavailable: {result.Reason}")))))
            .Switch();

        var loadOrderFailures = profile.LoadOrderState
            .Where(state => state.Failed)
            .Do(state => logger.LogWarning("Load order unavailable for {Profile}: {Reason}", profile.Name, state.Reason))
            .Select(state => Observable.Return<AnalysisEvent>(
                new AnalysisEvent.Status(new StatusUpdate(AnalyzerStatus.Error, $"Load order unavailable: {state.Reason}"))));

        return runs.Merge(loadOrderFailures);
    }

    private IObservable<IObservable<AnalysisEvent>> WatchFolder(
        DirectoryPath dataFolder,
        IObservable<IReadOnlySet<ModKey>> targets,
        IObservable<Severity> minimumSeverity) =>
        targets
            .Throttle(Settle, RxSchedulers.TaskpoolScheduler)
            .Select(mods => Changes(dataFolder, mods)
                .Throttle(Settle, RxSchedulers.TaskpoolScheduler)
                .Do(_ => logger.LogInformation("Change detected.  Restarting analysis of {ModCount} mods", mods.Count))
                .StartWith(Unit.Default)
                .Select(_ => mods))
            .Switch()
            .CombineLatest(minimumSeverity.DistinctUntilChanged(), analyzerService.Analyze);

    private IObservable<Unit> Changes(DirectoryPath dataFolder, IReadOnlySet<ModKey> targets)
    {
        var fileNames = targets
            .Select(key => key.FileName.String)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Observable.Defer(() =>
            {
                logger.LogInformation("Started watching {ModCount} mods in {DataFolder}", fileNames.Count, dataFolder);
                return Observable.Merge(WatcherChanges(dataFolder, fileNames), PolledChanges(dataFolder, fileNames));
            })
            .Finally(() => logger.LogInformation("Stopped watching {ModCount} mods in {DataFolder}", fileNames.Count, dataFolder));
    }

    private IObservable<Unit> WatcherChanges(DirectoryPath dataFolder, IReadOnlySet<string> fileNames) =>
        Observable.Create<Unit>(observer =>
        {
            var watcher = fileSystem.FileSystemWatcher.New(dataFolder.Path);
            var subscription = watcher.Events().Changed
                .Where(e => e.Name is { } name && fileNames.Contains(name))
                .Unit()
                .Subscribe(observer);
            watcher.EnableRaisingEvents = true;
            return new CompositeDisposable(subscription, watcher);
        });

    // Polling fallback for setups like MO2 where watcher events don't fire
    private IObservable<Unit> PolledChanges(DirectoryPath dataFolder, IReadOnlySet<string> fileNames) =>
        Observable.Interval(TimeSpan.FromSeconds(2), RxSchedulers.TaskpoolScheduler)
            .Select(_ => LatestWrite(dataFolder, fileNames))
            .DistinctUntilChanged()
            .Skip(1)
            .Unit();

    private DateTime LatestWrite(DirectoryPath dataFolder, IEnumerable<string> fileNames) =>
        fileNames
            .Select(fileName => fileSystem.File.GetLastWriteTimeUtc(fileSystem.Path.Combine(dataFolder.Path, fileName)))
            .DefaultIfEmpty()
            .Max();
}
