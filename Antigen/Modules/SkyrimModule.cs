using Antigen.Services.Game;
using Autofac;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Analyzers.Skyrim;

namespace Antigen.Modules;

public sealed class SkyrimModule : GameCategoryModule
{
    public override GameCategory Category => GameCategory.Skyrim;

    protected override void RegisterFormattedTopicFormatter(ContainerBuilder builder) =>
        builder.RegisterType<SkyrimFormattedTopicFormatter>().As<IFormattedTopicFormatter>();

    protected override void RegisterAnalyzerResultInfoFactory(ContainerBuilder builder) =>
        builder.RegisterType<SkyrimAnalyzerResultInfoFactory>().As<IAnalyzerResultInfoFactory>();

    protected override void RegisterAnalyzerFilter(ContainerBuilder builder) =>
        builder.RegisterType<SkyrimAnalyzerFilter>().As<IAnalyzerFilter>();

    protected override void RegisterAnalyzers(ContainerBuilder builder) =>
        builder.RegisterModule<SkyrimAnalyzerModule>();
}
