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
    IObservable<AnalysisEvent> Analyze(IReadOnlySet<ModKey> targets, Severity minimumSeverity);
}

public sealed class AnalyzerService(
    IFileSystem fileSystem,
    IReadOnlyList<IAnalyzer> analyzers,
    ILoadOrderListingsProvider loadOrderListingsProvider,
    IGameReleaseContext gameReleaseContext,
    IAnalyzerFilter analyzerFilter,
    IAnalyzerResultInfoFactory infoFactory,
    ILogger<AnalyzerService> logger) : IAnalyzerService, IActiveScoped
{
    public IObservable<AnalysisEvent> Analyze(IReadOnlySet<ModKey> targets, Severity minimumSeverity) =>
        Observable.Create<AnalysisEvent>((observer, cancel) => Run(targets, minimumSeverity, observer, cancel))
            .SubscribeOn(RxSchedulers.TaskpoolScheduler);

    private async Task Run(IReadOnlySet<ModKey> targets, Severity minimumSeverity, IObserver<AnalysisEvent> observer, CancellationToken cancel)
    {
        void ReportStatus(AnalyzerStatus status, string message) =>
            observer.OnNext(new AnalysisEvent.Status(new StatusUpdate(status, message)));

        IAsyncEnumerable<AnalyzerResult>? results = null;

        logger.LogInformation("Starting analysis of {TargetCount} mods with minimum severity {MinimumSeverity}", targets.Count, minimumSeverity);
        var stopwatch = Stopwatch.StartNew();

        var loadOrder = loadOrderListingsProvider.Get().ToArray();

        IGameEnvironment? env = null;
        try
        {
            // Try to create the environment multiple times in case a mod file is being written to and causes an error
            var retryCount = 0;
            while (env is null && retryCount < 3)
            {
                try
                {
                    env = GameEnvironmentBuilder.Create(gameReleaseContext.Release)
                        .WithLoadOrder(loadOrder)
                        .Build();
                }
                catch (Exception ex)
                {
                    // We might get errors to create the environment if a mod file is currently being written to - Retry
                    retryCount++;
                    logger.LogWarning(ex, "Failed to build game environment (attempt {Attempt})", retryCount);
                }
            }

            if (env is null)
            {
                ReportStatus(AnalyzerStatus.Error, "Failed to create game environment");
                logger.LogError("Giving up building the game environment after {Attempts} attempts", retryCount);
                return;
            }

            try
            {
                ReportStatus(AnalyzerStatus.Preparing, "Preparing analysis...");

                var notSelectedMods = loadOrder
                    .Select(l => l.ModKey)
                    .Where(key => !targets.Contains(key))
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
                logger.LogWarning("Analysis produced no runner - aborting");
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
            logger.LogInformation("Analysis completed - {IssueCount} issues found in {ElapsedMs}ms", count, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            if (cancel.IsCancellationRequested)
            {
                logger.LogInformation("Analysis was cancelled after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }

            env?.Dispose();
        }
    }
}
