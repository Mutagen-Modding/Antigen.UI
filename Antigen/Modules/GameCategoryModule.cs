using Antigen.Services.Game;
using Autofac;
using Module = Autofac.Module;

namespace Antigen.Modules;

public abstract class GameCategoryModule : Module
{
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
