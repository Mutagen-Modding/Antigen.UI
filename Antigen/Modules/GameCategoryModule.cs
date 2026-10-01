using Antigen.Services.Game;
using Autofac;
using Mutagen.Bethesda;
using Module = Autofac.Module;

namespace Antigen.Modules;

public abstract class GameCategoryModule : Module
{
    public abstract GameCategory Category { get; }

    protected abstract IReg<IFormattedTopicFormatter> FormattedTopicFormatter { get; }
    protected abstract IReg<IAnalyzerResultInfoFactory> AnalyzerResultInfoFactory { get; }
    protected abstract IReg<IAnalyzerFilter> AnalyzerFilter { get; }
    protected abstract void RegisterAnalyzers(ContainerBuilder builder);

    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType(FormattedTopicFormatter.Type).As<IFormattedTopicFormatter>();
        builder.RegisterType(AnalyzerResultInfoFactory.Type).As<IAnalyzerResultInfoFactory>();
        builder.RegisterType(AnalyzerFilter.Type).As<IAnalyzerFilter>();
        RegisterAnalyzers(builder);
    }

    protected static IReg<T> Register<T>() => new Reg<T>();

    protected interface IReg<out T>
    {
        Type Type { get; }
    }

    private sealed class Reg<T> : IReg<T>
    {
        public Type Type => typeof(T);
    }
}
