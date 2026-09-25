using Mutagen.Bethesda.Plugins;
using Noggog;

namespace Antigen.ViewModels;

public sealed class SessionVM : ViewModel, ITransient
{
    public ModWatcherVM Watcher { get; }
    public AnalyzerVM Analyzer { get; }

    public SessionVM(
        ModKey modKey,
        Func<ModKey, ModWatcherVM> watcherFactory,
        Func<ModWatcherVM, AnalyzerVM> analyzerFactory)
    {
        Watcher = watcherFactory(modKey).DisposeWith(this);
        Analyzer = analyzerFactory(Watcher).DisposeWith(this);
    }
}
