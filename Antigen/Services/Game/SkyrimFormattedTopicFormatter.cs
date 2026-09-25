using Mutagen.Bethesda.Skyrim;

namespace Antigen.Services.Game;

public sealed class SkyrimFormattedTopicFormatter : FormattedTopicFormatter
{
    protected override object? Describe(object? item) => item switch
    {
        IConditionFloatGetter condition => $"Condition: {condition.Data.RunOnType}.{condition.Data.Function} {condition.CompareOperator} {condition.ComparisonValue}",
        IConditionGlobalGetter condition => $"Condition: {condition.Data.RunOnType}.{condition.Data.Function} {condition.CompareOperator} Global={condition.ComparisonValue.FormKey}",
        _ => base.Describe(item)
    };
}
