using Antigen.Models.Analyzer;
using Mutagen.Bethesda.Analyzers.SDK.Topics;

namespace Antigen.Services.Game;

public interface IFormattedTopicFormatter
{
    IReadOnlyList<FormattedTopicSegment> Format(IFormattedTopicDefinition? formattedTopicDefinition);
}
