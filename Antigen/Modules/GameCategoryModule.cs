using Antigen.Services.Game;
using Autofac;
using Mutagen.Bethesda;
using Module = Autofac.Module;

namespace Antigen.Modules;

public abstract class GameCategoryModule : Module
{
    public abstract GameCategory Category { get; }

    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        RegisterFormattedTopicFormatter(builder);
        RegisterAnalyzerResultInfoFactory(builder);
        RegisterAnalyzerFilter(builder);
        RegisterAnalyzers(builder);
    }

    protected virtual void RegisterFormattedTopicFormatter(ContainerBuilder builder) =>
        builder.RegisterType<FormattedTopicFormatter>().As<IFormattedTopicFormatter>();

    protected virtual void RegisterAnalyzerResultInfoFactory(ContainerBuilder builder) =>
        builder.RegisterType<AnalyzerResultInfoFactory>().As<IAnalyzerResultInfoFactory>();

    protected virtual void RegisterAnalyzerFilter(ContainerBuilder builder) =>
        builder.RegisterType<AnalyzerFilter>().As<IAnalyzerFilter>();

    protected virtual void RegisterAnalyzers(ContainerBuilder builder)
    {
    }
}
