using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Antigen.Models.Settings;
using Antigen.Services;
using Autofac;
using DynamicData;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments.DI;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Plugins.Order.DI;
using Noggog;
using Noggog.Reactive;
using Noggog.UI;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels.Profiles;

public sealed partial class ProfileVM : ViewModel, IProfileScoped, IDataDirectoryProvider
{
    private readonly ILifetimeScope _scope;
    private readonly ActiveProfileController _activeProfile;
    private readonly DataFolderResolver _dataFolders;

    public IObservable<IChangeSet<ILoadOrderListingGetter>> LoadOrder { get; }
    public IObservable<IChangeSet<ILoadOrderListingGetter>> SelectedLoadOrder { get; }
    public IObservable<ErrorResponse> LoadOrderState { get; }
    public string Id { get; }
    public GameRelease Release { get; }
    public string ReleaseName => Release.ToDescriptionString();
    public ProfilePluginsVM Plugins { get; }
    public PathPickerVM DataFolder { get; }
    [Reactive] public partial string Name { get; set; }
    [Reactive] public partial bool IsSelected { get; internal set; }

    DirectoryPath IDataDirectoryProvider.Path => DataFolderResult.Value;

    public string? DataFolderOverride =>
        DataFolder.TargetPath.IsNullOrWhitespace() ? null : DataFolder.TargetPath;

    [ObservableAsProperty(PropertyName = "DataFolderResult")]
    private IObservable<GetResponse<DirectoryPath>> DataFolderResultObservable()
    {
        return DataFolder.WhenAnyValue(x => x.TargetPath)
            .Debounce(TimeSpan.FromMilliseconds(400), RxSchedulers.TaskpoolScheduler)
            .Select(path => _dataFolders.Resolve(Release, path))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .StartWith(GetResponse<DirectoryPath>.Fail("Not resolved yet"));
    }

    [ObservableAsProperty(PropertyName = "IsActive")]
    private IObservable<bool> IsActiveObservable() =>
        _activeProfile.WhenAnyValue(x => x.Active)
            .Select(active => ReferenceEquals(active?.Profile, this))
            .StartWith(false);

    [ObservableAsProperty(PropertyName = "DisplayName", InitialValue = "string.Empty")]
    private IObservable<string> DisplayNameObservable() =>
        this.WhenAnyValue(x => x.Name)
            .Select(name => name.IsNullOrWhitespace() ? ReleaseName : name);

    [ObservableAsProperty(PropertyName = "DataFolderNote", InitialValue = "string.Empty")]
    private IObservable<string> DataFolderNoteObservable() =>
        this.WhenAnyValue(x => x.DataFolderResult)
            .Select(result => result.Failed ? result.Reason : result.Value.Path);

    public ProfileVM(
        Profile profile,
        ProfilePluginsVM plugins,
        ILifetimeScope scope,
        Lazy<ILiveLoadOrderProvider> liveLoadOrder,
        ActiveProfileController activeProfile,
        DataFolderResolver dataFolders,
        ISchedulerProvider schedulerProvider,
        IPathPickerDialogProvider pathPickerDialogProvider,
        ILogger<ProfileVM> logger)
    {
        _scope = scope;
        _activeProfile = activeProfile;
        _dataFolders = dataFolders;

        Id = profile.Id;
        Name = profile.Name;
        Release = profile.Release;
        Plugins = plugins;
        Plugins.Seed(profile);

        DataFolder = new PathPickerVM(schedulerProvider, pathPickerDialogProvider)
        {
            PathType = PathPickerVM.PathTypeOptions.Folder,
            ExistCheckOption = PathPickerVM.CheckOptions.IfPathNotEmpty,
            PromptTitle = "Select the game's Data folder",
            TargetPath = profile.DataFolderOverride ?? string.Empty
        }.DisposeWith(this);

        InitializeOAPH();

        var loadOrderSource = this.WhenAnyValue(x => x.DataFolderResult)
            .DistinctUntilChanged()
            .ObserveOn(RxSchedulers.TaskpoolScheduler)
            .Select(folder => Watch(liveLoadOrder.Value, logger, Release, folder))
            .Replay(1)
            .RefCount();

        LoadOrder = loadOrderSource.Select(x => x.Listings).Switch();
        SelectedLoadOrder = Plugins.FilterToSelected(LoadOrder);
        LoadOrderState = loadOrderSource.Select(x => x.State).Switch();

        // Only the profile being looked at or run is worth a file watcher.
        this.WhenAnyValue(x => x.IsActive, x => x.IsSelected, (active, selected) => active || selected)
            .DistinctUntilChanged()
            .Select(watching => watching
                ? loadOrderSource.Select(x => x.Listings.ToCollection().Select(listings => (x.Folder, Listings: listings))).Switch()
                : Observable.Empty<(DirectoryPath Folder, IReadOnlyCollection<ILoadOrderListingGetter> Listings)>())
            .Switch()
            .Subscribe(x => Plugins.Load(ToProfile(), Release, x.Folder, x.Listings))
            .DisposeWith(this);
    }

    public ILifetimeScope BeginActive() => _scope.BeginLifetimeScope(LifetimeScopes.Active);

    private readonly record struct LoadOrderSource(
        DirectoryPath Folder,
        IObservable<IChangeSet<ILoadOrderListingGetter>> Listings,
        IObservable<ErrorResponse> State);

    private static LoadOrderSource Watch(
        ILiveLoadOrderProvider liveLoadOrder,
        ILogger<ProfileVM> logger,
        GameRelease release,
        GetResponse<DirectoryPath> folder)
    {
        if (folder.Failed)
        {
            return new LoadOrderSource(
                default,
                Observable.Empty<IChangeSet<ILoadOrderListingGetter>>(),
                Observable.Return(ErrorResponse.Fail(folder.Reason)));
        }

        logger.LogInformation("Watching load order for {Release} in {DataFolder}", release, folder.Value);

        var listings = liveLoadOrder.Get(out var errors);

        return new LoadOrderSource(
            folder.Value,
            listings.SubscribeOn(RxSchedulers.TaskpoolScheduler),
            errors);
    }

    public Profile ToProfile() => new()
    {
        Id = Id,
        Name = Name,
        Release = Release,
        DataFolderOverride = DataFolderOverride,
        FollowLoadOrder = Plugins.FollowLoadOrder,
        Plugins = Plugins.ToSelections()
    };
}
