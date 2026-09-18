using Autofac;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments.DI;

namespace Antigen.Services;

public sealed class GameScopeFactory(ILifetimeScope root) : ISingleton, IDisposable
{
    private readonly Dictionary<GameRelease, ILifetimeScope> _scopes = new();
    private readonly Lock _lock = new();

    public ILifetimeScope Get(GameRelease release)
    {
        lock (_lock)
        {
            if (_scopes.TryGetValue(release, out var existing)) return existing;

            var scope = root.BeginLifetimeScope(builder => Configure(builder, release));
            _scopes[release] = scope;
            return scope;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var scope in _scopes.Values)
            {
                scope.Dispose();
            }
            _scopes.Clear();
        }
    }

    private static void Configure(ContainerBuilder builder, GameRelease release)
    {
        builder.RegisterInstance(new GameReleaseInjection(release))
            .AsImplementedInterfaces();

        if (!GameSupport.IsSupported(release))
        {
            throw new NotSupportedException($"No module for {release}. Profiles only offer what {nameof(GameSupport)} lists.");
        }

        builder.RegisterModule(GameSupport.ModuleFor(release));
    }
}
