using System.IO.Abstractions;
using System.Reflection;
using Antigen.Services;
using Autofac;
using Avalonia.Controls;
using Mutagen.Bethesda.Autofac;
using Mutagen.Bethesda.Environments.DI;
using Mutagen.Bethesda.Installs.DI;
using Mutagen.Bethesda.Plugins.Meta;
using Noggog.Reactive;
using Noggog.UI;
using Module = Autofac.Module;

namespace Antigen.Modules;

public class MainModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterModule<LoggingModule>();

        // Register base services
        builder.RegisterType<FileSystem>()
            .As<IFileSystem>()
            .SingleInstance();

        builder.RegisterType<SchedulerProvider>()
            .As<ISchedulerProvider>()
            .SingleInstance();

        builder.Register(context =>
            {
                var window = context.Resolve<Window>();
                return new AvaloniaPathPickerDialogProvider(() => window);
            })
            .As<IPathPickerDialogProvider>()
            .SingleInstance();

        builder.RegisterModule<MutagenModule>();

        // Register as singletons here, so each profile doesnt redo the work
        builder.RegisterType<GameLocatorLookupCache>()
            .As<IGameDirectoryLookup>()
            .As<IDataDirectoryLookup>()
            .SingleInstance();

        builder.RegisterType<CachedProtonPrefixProvider>()
            .As<IProtonPrefixProvider>()
            .SingleInstance();

        builder.Register(context =>
        {
            var gameReleaseContext = context.Resolve<IGameReleaseContext>();
            return GameConstants.Get(gameReleaseContext.Release);
        });

        RegisterMarkedTypes(builder, typeof(App).Assembly);
    }

    private void RegisterMarkedTypes(ContainerBuilder builder, Assembly assembly)
    {
        builder.RegisterAssemblyTypes(assembly)
            .AssignableTo<ISingleton>()
            .AsSelf()
            .AsImplementedInterfaces()
            .SingleInstance();

        builder.RegisterAssemblyTypes(assembly)
            .AssignableTo<ITransient>()
            .AsSelf()
            .AsImplementedInterfaces();

        builder.RegisterAssemblyTypes(assembly)
            .AssignableTo<IProfileScoped>()
            .AsSelf()
            .AsImplementedInterfaces()
            .InstancePerMatchingLifetimeScope(LifetimeScopes.Profile);

        builder.RegisterAssemblyTypes(assembly)
            .AssignableTo<IActiveScoped>()
            .AsSelf()
            .AsImplementedInterfaces()
            .InstancePerMatchingLifetimeScope(LifetimeScopes.Active);
    }
}
