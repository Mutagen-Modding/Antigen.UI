using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments.DI;

namespace Antigen.ViewModels.Profiles;

public sealed class ActiveProfileVM(
    ProfileVM profile,
    IGameReleaseContext release,
    ModWatcherVM watcher,
    ResultTallyVM tally,
    AnalyzerVM analyzer)
    : ViewModel, IActiveScoped
{
    public ProfileVM Profile { get; } = profile;

    public GameRelease Release { get; } = release.Release;

    public ModWatcherVM Watcher { get; } = watcher;

    public ResultTallyVM Tally { get; } = tally;

    public AnalyzerVM Analyzer { get; } = analyzer;
}
