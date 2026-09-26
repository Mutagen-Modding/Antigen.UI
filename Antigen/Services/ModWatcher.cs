using System.IO.Abstractions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Antigen.Models.Analyzer;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Analyzers.SDK.Topics;
using Mutagen.Bethesda.Environments.DI;
using Mutagen.Bethesda.Plugins;
using Noggog;
using ReactiveMarbles.ObservableEvents;
using ReactiveUI;

namespace Antigen.Services;

public interface IModWatcher
{
    IObservable<IObservable<AnalysisEvent>> Watch(ModKey modKey, IObservable<Severity> minimumSeverity);
}

public sealed class ModWatcher(
    IFileSystem fileSystem,
    IDataDirectoryProvider dataDirectoryProvider,
    IAnalyzerService analyzerService,
    ILogger<ModWatcher> logger)
    : IModWatcher, IActiveScoped
{
    public IObservable<IObservable<AnalysisEvent>> Watch(ModKey modKey, IObservable<Severity> minimumSeverity)
    {
        var filePath = fileSystem.Path.Combine(dataDirectoryProvider.Path, modKey.FileName);

        return Observable.Defer(() =>
            {
                logger.LogInformation("Started watching {ModKey} at {FilePath}", modKey, filePath);
                return Observable.Merge(WatcherChanges(modKey), PolledChanges(filePath));
            })
            .Throttle(TimeSpan.FromMilliseconds(500), RxSchedulers.TaskpoolScheduler)
            .Do(_ => logger.LogInformation("Change detected for {ModKey}.  Restarting analysis", modKey))
            .StartWith(Unit.Default)
            .CombineLatest(minimumSeverity.DistinctUntilChanged(), (_, severity) => severity)
            .Select(severity => analyzerService.Analyze(modKey, severity))
            .Finally(() => logger.LogInformation("Stopped watching {ModKey}", modKey));
    }

    private IObservable<Unit> WatcherChanges(ModKey modKey) =>
        Observable.Create<Unit>(observer =>
        {
            var watcher = fileSystem.FileSystemWatcher.New(dataDirectoryProvider.Path, modKey.FileName);
            var subscription = watcher.Events().Changed.Unit().Subscribe(observer);
            watcher.EnableRaisingEvents = true;
            return new CompositeDisposable(subscription, watcher);
        });

    // Polling fallback for setups like MO2 where watcher events don't fire
    private IObservable<Unit> PolledChanges(string filePath) =>
        Observable.Interval(TimeSpan.FromSeconds(2), RxSchedulers.TaskpoolScheduler)
            .Select(_ => fileSystem.File.GetLastWriteTime(filePath))
            .DistinctUntilChanged()
            .Skip(1)
            .Unit();
}
