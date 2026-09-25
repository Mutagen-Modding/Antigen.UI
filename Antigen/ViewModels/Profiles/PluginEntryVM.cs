using Mutagen.Bethesda.Plugins;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels.Profiles;

public sealed partial class PluginEntryVM : ReactiveObject
{
    public const string PendingRecordCount = "—";

    public ModKey ModKey { get; }
    public string FileName { get; }
    public string Index { get; }
    public string SizeText { get; }

    [Reactive] public partial bool Enabled { get; set; }
    [Reactive] public partial bool CanToggle { get; set; }
    [Reactive] public partial string RecordCountText { get; set; } = PendingRecordCount;

    public PluginEntryVM(int index, ModKey modKey, string fileName, long sizeBytes)
    {
        ModKey = modKey;
        FileName = fileName;
        Index = index.ToString("X2");
        SizeText = FormatSize(sizeBytes);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 0 => string.Empty,
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} GB"
    };
}
