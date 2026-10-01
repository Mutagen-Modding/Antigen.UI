using System.Reactive.Linq;
using Antigen.Models.Settings;
using Antigen.Services;
using DynamicData.Binding;
using Mutagen.Bethesda;
using Noggog;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Antigen.ViewModels.Profiles;

public sealed partial class ProfilesVM : ResizablePanelVM, ISingleton
{
    private readonly NavigationController _navigation;
    private readonly ActiveProfileController _activeProfile;
    private readonly WelcomeVM _welcome;
    private readonly ProfileFactory _profileFactory;

    public override double MinResizeHeight => 300.0;

    public ObservableCollectionExtended<ProfileVM> Profiles { get; } = [];

    [Reactive] public partial ProfileVM? Selected { get; set; }

    public GameReleasePickerVM NewTarget { get; } = new("Add New Profile");

    public ProfilesVM(
        NavigationController navigation,
        ActiveProfileController activeProfile,
        ProfilesSettingsService profilesSettings,
        WelcomeVM welcome,
        ProfileFactory profileFactory)
    {
        _navigation = navigation;
        _activeProfile = activeProfile;
        _welcome = welcome;
        _profileFactory = profileFactory;
        IsExpanded = true;
        NewTarget.DisposeWith(this);

        Profiles.AddRange(profilesSettings.Load().Select(profileFactory.Create));

        this.WhenAnyValue(x => x.Selected)
            .Subscribe(selected =>
            {
                foreach (var vm in Profiles)
                {
                    vm.IsSelected = ReferenceEquals(vm, selected);
                }
            })
            .DisposeWith(this);
    }

    public IEnumerable<Profile> ToProfiles() => Profiles.Select(vm => vm.ToProfile());

    public void Select(string? profileId)
    {
        Selected = Profiles.FirstOrDefault(vm => vm.Id == profileId) ?? Profiles.FirstOrDefault();
    }

    public void Activate(string? profileId)
    {
        Select(profileId);
        ActivateProfile(Profiles.FirstOrDefault(vm => vm.Id == profileId) ?? Profiles.FirstOrDefault());
    }

    [ReactiveCommand]
    private void ActivateProfile(ProfileVM? profile)
    {
        _activeProfile.Activate(profile);
    }

    [ReactiveCommand]
    private void AddProfile()
    {
        if (NewTarget.Release is not { } release) return;

        AddNewProfile(release);
    }

    public ProfileVM AddNewProfile(GameRelease release)
    {
        var profile = new Profile
        {
            Id = Guid.NewGuid().ToString(),
            Name = string.Empty,
            Release = release
        };

        var vm = _profileFactory.Create(profile);
        Profiles.Add(vm);
        Selected = vm;
        return vm;
    }

    [ReactiveCommand]
    private void DeleteProfile()
    {
        if (Selected is not { } removing) return;

        var index = Profiles.IndexOf(removing);
        Profiles.Remove(removing);

        var next = Profiles.Count == 0 ? null : Profiles[Math.Clamp(index, 0, Profiles.Count - 1)];
        Selected = next;

        if (removing.IsActive)
        {
            ActivateProfile(next);
        }

        if (Profiles.Count == 0)
        {
            _navigation.GoTo(_welcome);
        }

        removing.Dispose();
    }

    [ReactiveCommand]
    private void Back()
    {
        _navigation.Back();
    }
}
