using Antigen.Models.Settings;
using Antigen.ViewModels.Profiles;
using Autofac;
using Noggog;

namespace Antigen.Services;

public sealed class ProfileFactory(GameScopeFactory games) : ISingleton
{
    public ProfileVM Create(Profile profile)
    {
        var scope = games.Get(profile.Release).BeginLifetimeScope(LifetimeScopes.Profile);
        var vm = scope.Resolve<ProfileVM>(TypedParameter.From(profile));
        scope.DisposeWith(vm);
        return vm;
    }
}
