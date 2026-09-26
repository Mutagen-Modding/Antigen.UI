using Mutagen.Bethesda.Plugins;
using Noggog;

namespace Antigen.ViewModels;

public sealed class SessionVM : ViewModel, ITransient
{
    public ModWatcherVM Watcher { get; }
    public ResultTallyVM Tally { get; }
    public AnalyzerVM Analyzer { get; }

    public SessionVM(
        ModKey modKey,
        Func<ModKey, ModWatcherVM> watcherFactory,
        Func<ModWatcherVM, ResultTallyVM> tallyFactory,
        Func<ModWatcherVM, AnalyzerVM> analyzerFactory)
    {
        Watcher = watcherFactory(modKey).DisposeWith(this);
        Tally = tallyFactory(Watcher).DisposeWith(this);
        Analyzer = analyzerFactory(Watcher).DisposeWith(this);
    }
}
