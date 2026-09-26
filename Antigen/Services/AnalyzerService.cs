using System.Diagnostics;
using System.IO.Abstractions;
using System.Reactive.Linq;
using Antigen.Models.Analyzer;
using Antigen.Services.Game;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Analyzers;
using Mutagen.Bethesda.Analyzers.Reporting.Handlers;
using Mutagen.Bethesda.Analyzers.SDK.Analyzers;
using Mutagen.Bethesda.Analyzers.SDK.Topics;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Environments.DI;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order.DI;
using Noggog;
using ReactiveUI;

namespace Antigen.Services;

public abstract record AnalysisEvent
{
    public sealed record Status(StatusUpdate Update) : AnalysisEvent;
    public sealed record Result(AnalyzerResultInfo Info) : AnalysisEvent;
}

public interface IAnalyzerService
{
    IObservable<AnalysisEvent> Analyze(ModKey modKey, Severity minimumSeverity);
}

public sealed class AnalyzerService(
    IFileSystem fileSystem,
    IDataDirectoryProvider dataDirectoryProvider,
    ModInfoProvider modInfoProvider,
    IReadOnlyList<IAnalyzer> analyzers,
    ILoadOrderListingsProvider loadOrderListingsProvider,
    IGameReleaseContext gameReleaseContext,
    IAnalyzerFilter analyzerFilter,
    IAnalyzerResultInfoFactory infoFactory,
    ILogger<AnalyzerService> logger) : IAnalyzerService, IActiveScoped
{
    public IObservable<AnalysisEvent> Analyze(ModKey modKey, Severity minimumSeverity) =>
        Observable.Create<AnalysisEvent>((observer, cancel) => Run(modKey, minimumSeverity, observer, cancel))
            .SubscribeOn(RxSchedulers.TaskpoolScheduler);

    private async Task Run(ModKey modKey, Severity minimumSeverity, IObserver<AnalysisEvent> observer, CancellationToken cancel)
    {
        void ReportStatus(AnalyzerStatus status, string message) =>
            observer.OnNext(new AnalysisEvent.Status(new StatusUpdate(status, message)));

        IAsyncEnumerable<AnalyzerResult>? results = null;

        logger.LogInformation("Starting analysis of {ModKey} with minimum severity {MinimumSeverity}", modKey, minimumSeverity);
        var stopwatch = Stopwatch.StartNew();

        var modInfos = loadOrderListingsProvider.Get()
            .Select(l => modInfoProvider.GetModInfo(fileSystem.Path.Combine(dataDirectoryProvider.Path, l.FileName), fileSystem, gameReleaseContext.Release))
            .WhereNotNull()
            .ToArray();

        var masterInfos = modInfoProvider.GetMasterInfos(modInfos);
        var loadOrder = loadOrderListingsProvider.Get()
            .Where(l => l.ModKey == modKey || masterInfos.TryGetValue(modKey, out var masterInfo) && masterInfo.Masters.Contains(l.ModKey))
            .ToArray();

        logger.LogInformation("Resolved {LoadOrderCount} of {ModCount} mods", loadOrder.Length, modInfos.Length);

        IGameEnvironment? env = null;
        try
        {
            // Try to create the environment multiple times in case a mod file is being written to and causes an error
            var retryCount = 0;
            while (env is null && retryCount < 3)
            {
                try
                {
                    // Create environment for only the mod and its transitive dependencies
                    env = GameEnvironmentBuilder.Create(gameReleaseContext.Release)
                        .WithLoadOrder(loadOrder)
                        .Build();
                }
                catch (Exception ex)
                {
                    // We might get errors to create the environment if a mod file is currently being written to - Retry
                    retryCount++;
                    logger.LogWarning(ex, "Failed to build game environment for {ModKey} (attempt {Attempt})", modKey, retryCount);
                }
            }

            if (env is null)
            {
                ReportStatus(AnalyzerStatus.Error, "Failed to create game environment");
                logger.LogError("Giving up building the game environment for {ModKey} after {Attempts} attempts", modKey, retryCount);
                return;
            }

            try
            {
                ReportStatus(AnalyzerStatus.Preparing, "Preparing analysis...");

                // Get all mods except the one we're analyzing (treat as blacklisted)
                var allMods = loadOrderListingsProvider.Get().ToList();
                var notSelectedMods = allMods
                    .Where(l => l.FileName != modKey.FileName)
                    .Select(l => l.ModKey)
                    .ToArray();

                ReportStatus(AnalyzerStatus.Preparing, "Building analyzer...");

                // Create analyzer with all built-in analyzers
                var enabledAnalyzers = analyzers.Where(analyzerFilter.ShouldAnalyze).ToArray();
                var analyzer = AnalyzerRunnerBuilder.Create(gameReleaseContext.Release)
                    .WithLinkCache(env.LinkCache)
                    .WithAnalyzers(enabledAnalyzers)
                    .WithBlacklistedMods(notSelectedMods)
                    .WithMinimumSeverity(minimumSeverity)
                    .WithFileSystem(fileSystem)
                    .Build();

                logger.LogInformation("Built analyzer with {AnalyzerCount} of {TotalAnalyzerCount} analyzers enabled", enabledAnalyzers.Length, analyzers.Count);

                ReportStatus(AnalyzerStatus.Analyzing, "Running analyzers...");

                results = analyzer.Analyze();

                ReportStatus(AnalyzerStatus.Analyzing, "Processing results...");
            }
            catch (Exception ex)
            {
                ReportStatus(AnalyzerStatus.Error, $"Analysis failed: {ex.Message}");
                logger.LogError(ex, "Analysis error");
            }

            if (results is null)
            {
                logger.LogWarning("Analysis of {ModKey} produced no runner - aborting", modKey);
                return;
            }

            var count = 0;
            await foreach (var result in results.WithCancellation(cancel))
            {
                if (cancel.IsCancellationRequested) return;

                count++;
                if (count % 10 == 0)
                {
                    ReportStatus(AnalyzerStatus.Analyzing, $"Found {count} issues...");
                }

                observer.OnNext(new AnalysisEvent.Result(infoFactory.Create(result, env.LinkCache)));
            }

            ReportStatus(AnalyzerStatus.Completed, $"Analysis complete - {count} issues found");
            logger.LogInformation("Analysis of {ModKey} completed - {IssueCount} issues found in {ElapsedMs}ms", modKey, count, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            if (cancel.IsCancellationRequested)
            {
                logger.LogInformation("Analysis of {ModKey} was cancelled after {ElapsedMs}ms", modKey, stopwatch.ElapsedMilliseconds);
            }

            env?.Dispose();
        }
    }
}
