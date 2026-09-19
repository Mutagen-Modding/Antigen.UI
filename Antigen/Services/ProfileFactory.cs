using Antigen.Models.Settings;
using Antigen.ViewModels.Profiles;
using Autofac;
using Noggog;

namespace Antigen.Services;

public sealed class ProfileFactory(ILifetimeScope root) : ISingleton
{
    public ProfileVM Create(Profile profile)
    {
        if (!GameSupport.IsSupported(profile.Release))
        {
            throw new NotSupportedException($"No module for {profile.Release}. Profiles only offer what {nameof(GameSupport)} lists.");
        }

        var scope = root.BeginLifetimeScope(LifetimeScopes.Profile);
        var vm = scope.Resolve<ProfileVM>(TypedParameter.From(profile));
        scope.DisposeWith(vm);
        return vm;
    }
}
