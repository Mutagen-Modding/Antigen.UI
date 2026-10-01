using Antigen.Services.Game;
using Autofac;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Analyzers.Skyrim;

namespace Antigen.Modules;

public sealed class SkyrimModule : GameCategoryModule
{
    public override GameCategory Category => GameCategory.Skyrim;

    protected override IReg<IFormattedTopicFormatter> FormattedTopicFormatter => Register<SkyrimFormattedTopicFormatter>();
    protected override IReg<IAnalyzerResultInfoFactory> AnalyzerResultInfoFactory => Register<SkyrimAnalyzerResultInfoFactory>();
    protected override IReg<IAnalyzerFilter> AnalyzerFilter => Register<SkyrimAnalyzerFilter>();

    protected override void RegisterAnalyzers(ContainerBuilder builder) =>
        builder.RegisterModule<SkyrimAnalyzerModule>();
}
