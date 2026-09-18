using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Antigen.Models.Settings;
using Antigen.Services;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Noggog;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels.Profiles;

public sealed partial class ProfilePluginsVM : ViewModel, IProfileScoped
{
    private readonly IFileSystem _fileSystem;
    private readonly RecordCountProvider _recordCounts;
    private readonly GlobalSettingsVM _globalSettings;
    private readonly ILogger<ProfilePluginsVM> _logger;

    private readonly SourceCache<PluginSelection, ModKey> _manualSelections = new(selection => ModKey.FromFileName(selection.FileName));
    private readonly SerialDisposable _counting = new();

    private ILoadOrderListingGetter[] _listings = [];
    private GameRelease _release;
    private string _dataFolder = string.Empty;
    private bool _loading;
    private bool _applying;

    public ObservableCollectionExtended<PluginEntryVM> Entries { get; } = [];
    public ReadOnlyObservableCollection<PluginEntryVM> FilteredEntries { get; }

    private readonly IObservable<Func<ILoadOrderListingGetter, bool>> _selection;

    [Reactive] public partial bool FollowLoadOrder { get; set; } = true;
    [Reactive] public partial string FilterText { get; set; } = string.Empty;

    [ObservableAsProperty(PropertyName = "SelectionSummary", InitialValue = "string.Empty")]
    private IObservable<string> SelectionSummaryObservable()
    {
        var entries = Entries.ToObservableChangeSet();
        return Observable.CombineLatest(
            entries.AutoRefresh(x => x.Enabled).Filter(x => x.Enabled).Count().StartWith(0),
            entries.Count().StartWith(0),
            (selected, total) => $"{selected} of {total} selected");
    }

    public ProfilePluginsVM(
        IFileSystem fileSystem,
        RecordCountProvider recordCounts,
        GlobalSettingsVM globalSettings,
        ILogger<ProfilePluginsVM> logger)
    {
        _fileSystem = fileSystem;
        _recordCounts = recordCounts;
        _globalSettings = globalSettings;
        _logger = logger;

        InitializeOAPH();

        Entries.ToObservableChangeSet()
            .Filter(this.WhenAnyValue(x => x.FilterText)
                .Unit()
                .StartWith(Unit.Default)
                .Select(_ => new Func<PluginEntryVM, bool>(entry =>
                    string.IsNullOrWhiteSpace(FilterText)
                 || entry.FileName.Contains(FilterText, StringComparison.OrdinalIgnoreCase))))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Bind(out var filtered)
            .Subscribe()
            .DisposeWith(this);

        FilteredEntries = filtered;

        _manualSelections.DisposeWith(this);
        _counting.DisposeWith(this);

        Entries.ToObservableChangeSet()
            .MergeMany(entry => entry.WhenAnyValue(x => x.Enabled)
                .Skip(1)
                .Select(enabled => (Entry: entry, Enabled: enabled)))
            .Subscribe(x => OnEntryToggled(x.Entry, x.Enabled))
            .DisposeWith(this);

        this.WhenAnyValue(x => x.FollowLoadOrder)
            .Skip(1)
            .Subscribe(OnFollowLoadOrderChanged)
            .DisposeWith(this);

        _selection = Observable.Merge(
                this.WhenAnyValue(x => x.FollowLoadOrder).Unit(),
                _manualSelections.Connect().Unit())
            .Select(_ => new Func<ILoadOrderListingGetter, bool>(IsEnabled));
    }

    public IObservable<IChangeSet<ILoadOrderListingGetter>> FilterToSelected(IObservable<IChangeSet<ILoadOrderListingGetter>> loadOrder) =>
        loadOrder.Filter(_selection);

    public IReadOnlyList<PluginSelection> ToSelections() =>
        _manualSelections.Items.ToArray();

    public void Seed(Profile profile)
    {
        _loading = true;
        try
        {
            _manualSelections.Edit(update => update.Load(profile.Plugins
                .Where(selection => ModKey.TryFromNameAndExtension(selection.FileName, out _))));

            FollowLoadOrder = profile.FollowLoadOrder;
        }
        finally
        {
            _loading = false;
        }
    }

    public void Load(Profile profile, GameRelease release, string dataFolder, IEnumerable<ILoadOrderListingGetter> listings)
    {
        var incoming = listings.ToArray();

        // Reselecting a profile restarts its watch, which replays a load order the list already shows.
        if (release == _release
            && dataFolder == _dataFolder
            && incoming.SequenceEqual(_listings, ListingComparer.Instance))
        {
            return;
        }

        _loading = true;
        try
        {
            _release = release;
            _dataFolder = dataFolder;

            _listings = incoming;

            FollowLoadOrder = profile.FollowLoadOrder;
        }
        finally
        {
            _loading = false;
        }

        var built = _listings
            .Select((listing, index) => new PluginEntryVM(index, listing.ModKey, listing.FileName, SizeOf(dataFolder, listing.FileName))
            {
                Enabled = IsEnabled(listing),
                CanToggle = !FollowLoadOrder
            })
            .ToArray();

        RxSchedulers.MainThreadScheduler.Schedule(() => Entries.Load(built));

        StartCounting(built);
    }

    private bool IsEnabled(ILoadOrderListingGetter listing) =>
        FollowLoadOrder ? listing.Enabled : IsManuallyEnabled(listing.ModKey);

    private bool IsManuallyEnabled(ModKey modKey)
    {
        var selection = _manualSelections.Lookup(modKey);
        return selection.HasValue && selection.Value.Enabled;
    }

    private void OnEntryToggled(PluginEntryVM entry, bool enabled)
    {
        if (_applying || FollowLoadOrder) return;

        _manualSelections.AddOrUpdate(new PluginSelection(entry.FileName, enabled));
    }

    private void OnFollowLoadOrderChanged(bool follow)
    {
        if (_loading) return;

        if (!follow)
        {
            _manualSelections.AddOrUpdate(_listings.Select(listing => new PluginSelection(listing.FileName, listing.Enabled)));
        }

        var live = _listings.ToDictionary(l => l.ModKey, l => l.Enabled);

        _applying = true;
        try
        {
            foreach (var entry in Entries)
            {
                entry.CanToggle = !follow;
                entry.Enabled = follow
                    ? live.GetValueOrDefault(entry.ModKey, false)
                    : IsManuallyEnabled(entry.ModKey);
            }
        }
        finally
        {
            _applying = false;
        }
    }

    private long SizeOf(string dataFolder, string fileName)
    {
        try
        {
            return _fileSystem.FileInfo.New(_fileSystem.Path.Combine(dataFolder, fileName)).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return -1;
        }
    }

    private void StartCounting(IReadOnlyList<PluginEntryVM> entries)
    {
        var release = _release;
        var dataFolder = _dataFolder;

        _counting.Disposable = entries.ToObservable()
            .Select(entry => Observable.Start(() =>
            {
                var count = _recordCounts.Get(release, entry.ModKey, _fileSystem.Path.Combine(dataFolder, entry.FileName));
                return (Entry: entry, Text: count is { } value ? value.ToString("N0") : "?");
            }, RxSchedulers.TaskpoolScheduler))
            .Merge(Math.Max(1, _globalSettings.WorkerThreadsProperty))
            .Buffer(TimeSpan.FromMilliseconds(100), RxSchedulers.TaskpoolScheduler)
            .Where(batch => batch.Count > 0)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(
                batch =>
                {
                    foreach (var (entry, text) in batch)
                    {
                        entry.RecordCountText = text;
                    }
                },
                ex => _logger.LogError(ex, "Error counting plugin records"));
    }

    private sealed class ListingComparer : IEqualityComparer<ILoadOrderListingGetter>
    {
        public static readonly ListingComparer Instance = new();

        public bool Equals(ILoadOrderListingGetter? x, ILoadOrderListingGetter? y) =>
            ReferenceEquals(x, y)
         || x is not null && y is not null && x.FileName == y.FileName && x.Enabled == y.Enabled;

        public int GetHashCode(ILoadOrderListingGetter obj) => HashCode.Combine(obj.FileName, obj.Enabled);
    }
}
