namespace Antigen.Models.Analyzer;

/// <summary>
///     A piece of a topic's message: either literal text, or a value that filled one of its placeholders.
/// </summary>
public sealed record FormattedTopicSegment(string Text, bool IsItem);
